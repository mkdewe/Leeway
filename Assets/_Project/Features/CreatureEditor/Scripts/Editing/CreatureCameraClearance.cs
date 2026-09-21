using Unity.Cinemachine;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// How far a camera has to stand from a creature so it does not cut through it.
    /// </summary>
    /// <remarks>
    /// Shared by both shots, because the question is the same one in the sculpting view and in the
    /// playground, and the answer has to agree — a creature that fits in one and not the other would
    /// mean the same leg slices the shot in one mode only.
    /// </remarks>
    public static class CameraClearance
    {
        /// <summary>
        /// The smallest distance from <paramref name="target"/> at which the whole creature stays in
        /// front of the lens.
        /// </summary>
        /// <remarks>
        /// <para>The silhouette's radius, plus the near clip plane — everything closer than that is cut
        /// away, so a camera seated exactly on the silhouette would already be showing the inside of
        /// the model — plus a margin, so a leg swinging through its step does not touch the lens.</para>
        ///
        /// <para>Returns <c>0</c> when the target carries no <see cref="CreatureFocusPoint"/>: nothing
        /// is known about the silhouette, and the authored framing is a better guess than one invented
        /// here.</para>
        /// </remarks>
        public static float RequiredDistance(Transform target, float nearClipPlane, float margin)
        {
            if (target == null) return 0f;

            CreatureFocusPoint focus = target.GetComponentInParent<CreatureFocusPoint>();
            if (focus == null) return 0f;

            return focus.SilhouetteRadius + nearClipPlane + margin;
        }
    }

    /// <summary>
    /// Keeps the orbiting sculpt camera outside the creature's silhouette.
    /// </summary>
    /// <remarks>
    /// <para>The orbit radius is authored for the starter creature, while what the player builds
    /// grows: legs reach down, an arm sticks out sideways, the spine gets longer. Once the silhouette
    /// reaches past the authored radius, turning the shot drives the camera <b>through</b> the
    /// geometry — a leg passes across the lens and the player sees the model from the inside, which
    /// with single-sided faces means seeing nothing at all.</para>
    ///
    /// <para>So the authored radius becomes a <b>floor</b>, never a ceiling: a small creature keeps
    /// the framing exactly as it was set in the scene, and only a creature that outgrows it pushes the
    /// camera out. Pulling the camera in is deliberately not done here — that is occlusion, a
    /// different problem, and Cinemachine's Deoccluder already solves it.</para>
    ///
    /// <para>The measurement comes from <see cref="CreatureFocusPoint"/>, so it is taken once per
    /// rebuild rather than every frame: chasing the bounds continuously would tie the framing to the
    /// step cycle and the camera would breathe along with the legs.</para>
    /// </remarks>
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class CreatureCameraClearance : MonoBehaviour
    {
        [SerializeField] private CinemachineOrbitalFollow _orbit;

        [Tooltip("Extra room between the silhouette and the lens, so a leg in mid-step does not brush it.")]
        [SerializeField, Min(0f)] private float _margin = 0.4f;

        /// <summary>The radius as authored in the scene — the framing we never go below.</summary>
        private float _authoredRadius;
        private Cinemachine3OrbitRig.Orbit[] _authoredOrbits;
        private CinemachineCamera _camera;

        private void Reset() => _orbit = GetComponent<CinemachineOrbitalFollow>();

        private void Awake()
        {
            if (_orbit == null) _orbit = GetComponent<CinemachineOrbitalFollow>();
            TryGetComponent(out _camera);

            if (_orbit == null) return;

            _authoredRadius = _orbit.Radius;
            _authoredOrbits = new[] { _orbit.Orbits.Top, _orbit.Orbits.Center, _orbit.Orbits.Bottom };
        }

        private void LateUpdate()
        {
            if (_orbit == null) return;

            float near = _camera != null ? _camera.Lens.NearClipPlane : 0.1f;
            float required = CameraClearance.RequiredDistance(_orbit.FollowTarget, near, _margin);
            if (required <= 0f) return;

            if (_orbit.OrbitStyle == CinemachineOrbitalFollow.OrbitStyles.Sphere)
            {
                _orbit.Radius = Mathf.Max(_authoredRadius, required);
                return;
            }

            ApplyToRings(required);
        }

        /// <summary>
        /// The three-ring orbit is a shape, not a distance, so it is scaled rather than overwritten.
        /// </summary>
        /// <remarks>
        /// The ring that decides is the <b>nearest</b> one: clearing only the widest would still let
        /// the camera dive into the creature at the top of its arc. Every ring is scaled by the same
        /// factor, so the authored silhouette of the orbit — a wide belt, a tight top — survives.
        /// </remarks>
        private void ApplyToRings(float required)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < _authoredOrbits.Length; i++)
                nearest = Mathf.Min(nearest, Distance(_authoredOrbits[i]));

            if (nearest <= 0.0001f) return;

            float factor = Mathf.Max(1f, required / nearest);

            _orbit.Orbits.Top = Scale(_authoredOrbits[0], factor);
            _orbit.Orbits.Center = Scale(_authoredOrbits[1], factor);
            _orbit.Orbits.Bottom = Scale(_authoredOrbits[2], factor);
        }

        /// <summary>A ring sits <c>Height</c> above the target and <c>Radius</c> out from it — the hypotenuse is the distance.</summary>
        private static float Distance(Cinemachine3OrbitRig.Orbit orbit)
            => Mathf.Sqrt(orbit.Radius * orbit.Radius + orbit.Height * orbit.Height);

        private static Cinemachine3OrbitRig.Orbit Scale(Cinemachine3OrbitRig.Orbit orbit, float factor)
            => new() { Radius = orbit.Radius * factor, Height = orbit.Height * factor };
    }
}
