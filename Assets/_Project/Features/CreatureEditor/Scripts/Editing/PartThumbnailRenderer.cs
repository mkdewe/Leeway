using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Gives the palette tiles their part previews — photographing the prefabs, and spinning them live
    /// on hover.
    /// </summary>
    /// <remarks>
    /// <para><b>Rendered, not drawn.</b> Hand-drawn icons would have to be produced for every new part
    /// and kept from drifting away from the model — while the palette takes its entire contents
    /// straight from the catalog by design, and adding a part needs no UI work. The preview is meant to
    /// work the same way.</para>
    ///
    /// <para><b>One studio, one live preview.</b> A tile at rest shows a photo (<see cref="Get"/>) — a
    /// static texture computed once and kept in a cache. The live, spinning model goes to <b>only the
    /// tile the cursor is on</b> (<see cref="BeginPreview"/>). That makes the cost one extra render per
    /// frame regardless of how many parts the catalog has — rather than a camera per tile.</para>
    ///
    /// <para><b>The thumbnail is frame zero.</b> The photo and the live preview are produced on the same
    /// rig and in the same frame, so hovering does not jump — the model starts moving from exactly the
    /// position it held on the tile. The frame is computed from half the geometry's diagonal
    /// (<see cref="FramingRadius"/>), and the preview spins around its centre, which means no angle of
    /// rotation can leave the frame.</para>
    ///
    /// <para>The studio stands <b>far below the scene</b> rather than on a separate layer. A layer would
    /// need a free slot in the project and mask discipline in several places; moving it a few kilometres
    /// down gives the same effect with no configuration at all, because the camera has tight clip planes
    /// and the point light a small range, so neither sees nor lights the real scene.</para>
    /// </remarks>
    public class PartThumbnailRenderer : MonoBehaviour
    {
        /// <summary>Where the studio stands — far enough not to see the scene, or be seen by it.</summary>
        private static readonly Vector3 StudioOrigin = new(0f, -5000f, 0f);

        [Header("Preview")]
        [SerializeField, Range(32, 512)] private int _resolution = 128;

        [Tooltip("The direction we look at the part from. Three-quarters by default - a flat part does not come out as a line.")]
        [SerializeField] private Vector3 _viewDirection = new(-0.6f, 0.35f, -1f);

        [Tooltip("How much slack to leave around the geometry. 1 = tight against the edges.")]
        [SerializeField, Range(1f, 2f)] private float _padding = 1.25f;

        [SerializeField] private Color _background = new(0f, 0f, 0f, 0f);

        [Header("Spin on hover")]
        [Tooltip("Degrees per second the part under the cursor spins at.")]
        [SerializeField] private float _spinDegreesPerSecond = 90f;

        [Header("Studio light")]
        [SerializeField] private float _lightIntensity = 2.5f;
        [SerializeField] private float _lightRange = 12f;

        private readonly Dictionary<int, Texture2D> _cache = new();

        private GameObject _studio;
        private Camera _camera;
        private Light _light;

        private Transform _livePivot;
        private RenderTexture _liveTarget;
        private Bounds _liveBounds;
        private float _liveYaw;
        private int _liveId = -1;

        /// <summary>A photo of the part at rest. <c>null</c> when the part has no prefab or no geometry to photograph.</summary>
        public Texture2D Get(CreaturePartDefinition definition)
        {
            if (definition == null || definition.Prefab == null) return null;

            if (_cache.TryGetValue(definition.PartId, out Texture2D cached)) return cached;

            Texture2D rendered = Snapshot(definition);
            _cache[definition.PartId] = rendered;

            return rendered;
        }

        /// <summary>
        /// Switches the preview to a live, spinning model of this part.
        /// </summary>
        /// <returns>
        /// The texture the render goes into, or <c>null</c> when the part cannot be shown. The caller
        /// should put it in its <c>RawImage</c> and give it back through <see cref="EndPreview"/> once
        /// the cursor leaves.
        /// </returns>
        /// <remarks>
        /// There is <b>one</b> live preview: the next call takes it away from the previous tile. The
        /// cursor is in one place anyway, and this way the number of parts in the catalog has no effect
        /// on the cost of a frame.
        /// </remarks>
        public RenderTexture BeginPreview(CreaturePartDefinition definition)
        {
            if (definition == null || definition.Prefab == null) return null;
            if (_liveId == definition.PartId && _liveTarget != null) return _liveTarget;

            EndPreview();

            if (!TryStage(definition, out Transform pivot, out Bounds bounds)) return null;

            _livePivot = pivot;
            _liveBounds = bounds;
            _liveYaw = 0f;
            _liveId = definition.PartId;
            _livePivot.rotation = RotationAt(_liveYaw);

            RenderLive();
            return EnsureTarget();
        }

        /// <summary>
        /// The buffer <b>everything</b> renders into — both the photos and the spin.
        /// </summary>
        /// <remarks>
        /// One buffer, rather than a temporary one for photos and a persistent one for the preview. Two
        /// different buffers mean two formats, two filter modes and two colour-space flags to keep in
        /// step by hand — and every such difference shows on screen as a flicker the moment the cursor
        /// arrives. Here there is nothing to keep in step, because there are not two buffers.
        /// </remarks>
        private RenderTexture EnsureTarget()
        {
            if (_liveTarget != null) return _liveTarget;

            _liveTarget = new RenderTexture(_resolution, _resolution, 16, RenderTextureFormat.ARGB32)
            {
                name = "PartPreview",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return _liveTarget;
        }

        /// <summary>Whether the live preview is currently showing this part.</summary>
        public bool IsPreviewing(CreaturePartDefinition definition)
            => definition != null && _livePivot != null && _liveId == definition.PartId;

        /// <summary>
        /// Stages a part in the studio together with the axis it will spin around.
        /// </summary>
        /// <remarks>
        /// <para>One path for the photo and for the live preview. There used to be two, and the fact that
        /// they produced the same image followed purely from both calling <see cref="FrameCamera"/> with
        /// the same numbers — that is, from nothing. The photo now <b>is</b> frame zero of the spin,
        /// because it is produced on the same rig.</para>
        ///
        /// <para>We spin an axis seated at the centre of the geometry rather than the prefab itself: its
        /// own anchor usually sits at the base of the part, so rotating around it would throw the model
        /// out of frame. From the centre of the geometry, every point of the model lies inside a sphere
        /// with the half-diagonal radius — and that is what sets the frame, which is why no rotation can
        /// leave it.</para>
        /// </remarks>
        private bool TryStage(CreaturePartDefinition definition, out Transform pivot, out Bounds bounds)
        {
            pivot = null;

            EnsureStudio();

            GameObject subject = Instantiate(definition.Prefab, StudioOrigin, Quaternion.identity);
            subject.hideFlags = HideFlags.HideAndDontSave;
            Sterilise(subject);

            if (!TryMeasure(subject, out bounds))
            {
                ClearStudio(subject);
                return false;
            }

            var rig = new GameObject("PartPreviewPivot") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.SetParent(_studio.transform, false);
            rig.transform.position = bounds.center;
            subject.transform.SetParent(rig.transform, true);

            pivot = rig.transform;
            return true;
        }

        /// <summary>The preview's rotation at a given frame. The photo takes zero, <see cref="LateUpdate"/> the rest.</summary>
        private static Quaternion RotationAt(float yawDegrees) => Quaternion.Euler(0f, yawDegrees, 0f);

        /// <summary>Shuts the live preview down. Safe to call when there is none.</summary>
        public void EndPreview()
        {
            if (_livePivot != null) ClearStudio(_livePivot.gameObject);

            _livePivot = null;
            _liveId = -1;
        }

        /// <summary>
        /// Takes a model out of the studio so it <b>stops posing in the same frame</b>.
        /// </summary>
        /// <remarks>
        /// <para>This is not tidying up, it is a correctness condition. There is one studio, and the
        /// camera photographs <b>everything</b> standing in it. Plain <c>Destroy</c> defers the deletion
        /// to the end of the frame, while the palette photographs every part of a category in <b>one</b>
        /// frame — so the second part posed together with the first, the third with both, and so on. Only
        /// the first tile came out clean. The same mechanism broke the cursor's trip between tiles:
        /// <see cref="EndPreview"/> released the previous model while <see cref="BeginPreview"/> staged
        /// the new one before that one had gone.</para>
        ///
        /// <para>We deactivate rather than delete immediately, because <c>DestroyImmediate</c> is
        /// forbidden in <c>OnDisable</c> — and that is exactly where a tile gives the preview back when
        /// the whole palette switches off along with sculpting mode. Deactivating takes effect at once
        /// and is enough: the camera does not render inactive objects.</para>
        /// </remarks>
        private static void ClearStudio(GameObject subject)
        {
            if (subject == null) return;

            subject.SetActive(false);
            Destroy(subject);
        }

        private void LateUpdate()
        {
            if (_livePivot == null) return;

            // Unscaled time: the palette should spin the same way when the game is paused.
            _liveYaw += _spinDegreesPerSecond * Time.unscaledDeltaTime;
            _livePivot.rotation = RotationAt(_liveYaw);

            RenderLive();
        }

        private void RenderLive() => RenderInto(_liveBounds);

        /// <summary>
        /// Renders the studio's contents into the tile buffer.
        /// </summary>
        /// <remarks>
        /// <para><b>A render request, not <c>Camera.Render</c>.</b> Under a scriptable pipeline this is
        /// the supported route; <c>Camera.Render</c> is left over from the built-in pipeline. Both give
        /// the same image here (checked — 2984 opaque pixels on the first render either way), so this is
        /// not a workaround for any defect, just using the API that is right in this version of Unity.</para>
        ///
        /// <para>The fallback path stays for a pipeline that does not support requests. Under URP it is
        /// not used.</para>
        /// </remarks>
        private void RenderInto(Bounds bounds)
        {
            FrameCamera(bounds);

            RenderTexture target = EnsureTarget();
            var request = new RenderPipeline.StandardRequest { destination = target };

            if (RenderPipeline.SupportsRenderRequest(_camera, request))
            {
                RenderPipeline.SubmitRenderRequest(_camera, request);
                return;
            }

            RenderTexture previous = RenderTexture.active;
            try
            {
                _camera.targetTexture = target;
                _camera.Render();
            }
            finally
            {
                _camera.targetTexture = null;
                RenderTexture.active = previous;
            }
        }

        /// <summary>
        /// A photo of the part at rest — <b>literally frame zero</b> of the preview's spin.
        /// </summary>
        /// <remarks>
        /// The same staging and the same frame as <see cref="BeginPreview"/>, so a cursor arriving on a
        /// tile has nothing to jump over: the model simply starts moving from where it stood on the
        /// thumbnail.
        /// </remarks>
        private Texture2D Snapshot(CreaturePartDefinition definition)
        {
            if (!TryStage(definition, out Transform pivot, out Bounds bounds)) return null;

            try
            {
                pivot.rotation = RotationAt(0f);

                RenderInto(bounds);
                return Capture();
            }
            finally
            {
                ClearStudio(pivot.gameObject);

                // A photo renders into the same buffer as the spin, so an ongoing preview has to be
                // redrawn — otherwise the tile under the cursor would blink with someone else's part.
                if (_livePivot != null) RenderLive();
            }
        }

        /// <summary>
        /// The framing radius: half the geometry's diagonal, measured from its centre.
        /// </summary>
        /// <remarks>
        /// Independent of rotation — and that is the entire mechanism by which a spinning part never
        /// pokes a corner out of the frame. Every point of the geometry lies inside a sphere of this
        /// radius about the centre, the preview spins <b>about that same centre</b>, and rotation does
        /// not change the distance from the axis. A frame with this half-extent therefore holds the model
        /// at every angle, not just at the one we happen to be looking at.
        /// </remarks>
        public static float FramingRadius(Bounds bounds) => Mathf.Max(0.01f, bounds.extents.magnitude);

        /// <summary>
        /// Positions the camera so the geometry fills the frame.
        /// </summary>
        /// <remarks>
        /// An orthographic projection, because the preview is meant to show a part's <b>shape</b>, not
        /// its perspective: under perspective a long leg and a short spike come out similar, depending
        /// only on how close the camera stands. The frame's half-extent comes from
        /// <see cref="FramingRadius"/> — the reasoning for that number is there.
        /// </remarks>
        private void FrameCamera(Bounds bounds)
        {
            Vector3 direction = _viewDirection.sqrMagnitude > 1e-6f ? _viewDirection.normalized : Vector3.back;
            float radius = FramingRadius(bounds);

            _camera.orthographic = true;
            _camera.orthographicSize = radius * _padding;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = radius * 6f;

            _camera.transform.position = bounds.center - direction * (radius * 3f);
            _camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // The light travels with the camera, so every part is lit from the side it is seen from —
            // without that, half the catalog would come out as a black silhouette.
            _light.transform.position = _camera.transform.position;
            _light.range = Mathf.Max(_lightRange, radius * 8f);
        }

        /// <summary>Lifts whatever just landed in the buffer into the tile's permanent texture.</summary>
        private Texture2D Capture()
        {
            RenderTexture source = EnsureTarget();
            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = source;

                var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;

                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private void EnsureStudio()
        {
            if (_camera != null) return;

            // The studio stands at the scene root, not under this component. This one sits on a canvas,
            // and a Screen Space canvas is given a scale of its own — setting a world position would
            // then be computed through it. A studio at the root has no such problem and its arithmetic
            // is what it looks like.
            var rig = new GameObject("PartThumbnailStudio") { hideFlags = HideFlags.HideAndDontSave };
            _studio = rig;

            // The camera goes on its own object, not on the studio root. The root is the stage: it is
            // what the photographed models are parented to. The camera moves on every framing, so if the
            // models hung under it, it would drag them along and never find them where it is aiming —
            // the measured displacement reached 5000 units against a framing radius of 0.21.
            var cameraObject = new GameObject("StudioCamera") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(rig.transform, false);

            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _background;
            _camera.cullingMask = ~0;

            var lightObject = new GameObject("StudioLight") { hideFlags = HideFlags.HideAndDontSave };
            lightObject.transform.SetParent(rig.transform, false);

            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.intensity = _lightIntensity;
            _light.range = _lightRange;
        }

        /// <summary>
        /// Strips the photographed model of anything that could interact with the scene.
        /// </summary>
        /// <remarks>
        /// A part prefab is not prepared for being instantiated outside a body: its colliders would stir
        /// up physics five kilometres below the arena, and that is the one thing distance does not deal
        /// with on its own.
        /// </remarks>
        private static void Sterilise(GameObject subject)
        {
            foreach (Collider collider in subject.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (Rigidbody body in subject.GetComponentsInChildren<Rigidbody>(true))
                body.isKinematic = true;
        }

        /// <summary>
        /// Measures what the camera will actually see.
        /// </summary>
        /// <remarks>
        /// Disabled renderers are skipped deliberately: counted into the geometry they would push the
        /// frame out around geometry that is not in the photo, and the part would sit in a corner of the
        /// tile instead of in the middle.
        /// </remarks>
        public static bool TryMeasure(GameObject subject, out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            foreach (Renderer renderer in subject.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled) continue;

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return any;
        }

        private void OnDestroy()
        {
            EndPreview();

            // The studio does not hang under this object, so it will not disappear with it.
            if (_studio != null) ClearStudio(_studio);
            _studio = null;
            _camera = null;
            _light = null;

            if (_liveTarget != null)
            {
                _liveTarget.Release();
                Destroy(_liveTarget);
                _liveTarget = null;
            }

            foreach (Texture2D texture in _cache.Values)
            {
                // The texture was created at runtime and is not an asset — nobody will release it for us.
                if (texture != null) Destroy(texture);
            }

            _cache.Clear();
        }
    }
}
