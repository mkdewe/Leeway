using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The brush: drags across the creature and leaves paint on its skin.
    /// </summary>
    /// <remarks>
    /// <para><b>The stroke is a line, not a dot.</b> A dab per frame leaves a dotted trail as soon as
    /// the cursor moves faster than the brush is wide, so each frame paints from the previous hit to
    /// this one. Paint in 3D does the interpolation itself — this only has to remember where the last
    /// dab landed.</para>
    ///
    /// <para><b>It raycasts against the carcass's own collider</b>, the one the editor already uses for
    /// "click on the torso", rather than against the skinned mesh. Raycasting a skinned mesh means
    /// baking it every frame; the capsule is the shape the player sees anyway, and the paint lands
    /// where the surface is.</para>
    /// </remarks>
    public class CreatureSkinBrush : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CreatureEditorController _controller;
        [SerializeField] private CreatureBodyPreview _preview;

        [Header("Brush")]
        [SerializeField] private Color _color = new(0.55f, 0.25f, 0.16f, 1f);

        [Tooltip("Brush radius in metres, so the same brush covers the same patch of skin on any creature.")]
        [SerializeField, Range(0.01f, 0.5f)] private float _radius = 0.12f;

        [Tooltip("Edge falloff. Higher is a harder edge.")]
        [SerializeField, Range(1f, 20f)] private float _hardness = 4f;

        [Tooltip("What the brush may hit. Left empty it uses the paint layer, which carries a collider " +
                 "shaped like the visible surface — the locomotion capsule would put the paint beside the cursor.")]
        [SerializeField] private LayerMask _mask;

        private Camera _camera;
        private bool _stroking;
        private Vector3 _lastPoint;

        /// <summary>Whether the brush is in hand. While it is, the editor's own left-click editing stands down.</summary>
        public bool IsActive { get; private set; }

        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        public float Radius
        {
            get => _radius;
            set => _radius = Mathf.Clamp(value, 0.01f, 0.5f);
        }

        /// <summary>Picks the brush up or puts it down.</summary>
        public void SetActive(bool active)
        {
            IsActive = active;
            _stroking = false;

            if (_controller != null) _controller.IsPainting = active;
        }

        private void Update()
        {
            if (!IsActive || _preview == null) return;

            CreatureSkinCanvas canvas = _preview.SkinCanvas;
            if (canvas == null || !canvas.IsReady) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (!mouse.leftButton.isPressed)
            {
                _stroking = false;
                return;
            }

            // A click that started on the palette is a click on the palette. Without this, picking a
            // colour would also splash it onto whatever the cursor happened to be in front of.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (!TryHit(mouse.position.ReadValue(), out Vector3 point, out Transform hit)) return;

            // A click on a horn paints the horn, a click on the flank paints the flank. The canvas works
            // out which picture that is; the brush only has to say what it hit — and a hit on something
            // with no canvas of its own (a leg) paints nothing rather than smearing the body behind it.
            PaintIn3D.P3dPaintableTexture target = canvas.CanvasFor(hit);
            if (target == null)
            {
                _stroking = false;
                return;
            }

            if (_stroking) canvas.PaintLine(_lastPoint, point, _color, _radius, _hardness, target);
            else canvas.Paint(point, _color, _radius, _hardness, target);

            _lastPoint = point;
            _stroking = true;
        }

        /// <summary>What the brush looks for: the layer the paint colliders are on, unless the scene says otherwise.</summary>
        private LayerMask PaintMask
        {
            get
            {
                if (_mask.value != 0) return _mask;

                int layer = LayerMask.NameToLayer(CreatureSkinCanvas.PaintLayer);
                return layer >= 0 ? 1 << layer : ~0;
            }
        }

        private bool TryHit(Vector2 screenPosition, out Vector3 point, out Transform hitTransform)
        {
            point = default;
            hitTransform = null;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;

            // Triggers are ignored on purpose: the creature's own capsule is one, and it is a fat
            // cylinder around the whole body — a hit on it lands the paint next to where it was aimed.
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, PaintMask, QueryTriggerInteraction.Ignore)) return false;

            // Only the creature being sculpted: the arena and the editor pad are not canvases.
            if (!hit.collider.transform.IsChildOf(_preview.transform) && hit.collider.transform != _preview.transform)
                return false;

            point = hit.point;
            hitTransform = hit.collider.transform;
            return true;
        }
    }
}
