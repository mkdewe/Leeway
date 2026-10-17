using System.Collections.Generic;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Kicks up a puff of dust where a foot lands. It watches the procedural feet instead of
    /// hooking into the gait: a step is a foot moving clearly faster than the body, and it has
    /// landed once it falls back to the body's pace. That covers short legs, which snap to the next
    /// foothold in a single frame, as well as long legs swinging over several.
    /// Purely cosmetic and local - each peer sees the dust of the feet it simulates.
    /// </summary>
    [RequireComponent(typeof(CreatureLocomotion))]
    public sealed class CreatureFootDust : MonoBehaviour
    {
        [Tooltip("World-space particle system the puffs are emitted from. One shared instance is created from this prefab.")]
        [SerializeField] private ParticleSystem _dustPrefab;
        [SerializeField, Min(1)] private int _particlesPerStep = 7;
        [Header("Amount")]
        [Tooltip("Body speed (m/s) at which a step raises the full puff. Slower steps raise proportionally less.")]
        [SerializeField, Min(0.5f)] private float _fullPuffSpeed = 4f;

        [Tooltip("The most a single step may raise, as a multiple of the base puff - sprinting kicks up more.")]
        [SerializeField, Min(1f)] private float _maxPuffScale = 2f;

        [Tooltip("Below this body speed (m/s) a step is a shuffle in place and raises nothing.")]
        [SerializeField, Min(0f)] private float _minBodySpeed = 0.35f;

        private static ParticleSystem _sharedDust;

        private CreatureLocomotion _locomotion;
        private readonly List<bool> _wasStepping = new();
        private Vector3 _previousBody;

        private void Awake() => _locomotion = GetComponent<CreatureLocomotion>();

        private void LateUpdate()
        {
            IReadOnlyList<ProceduralLeg> legs = _locomotion.Legs;

            // No dust in the air - and the tracking starts over on landing, so the feet settling back under
            // the body do not read as a step.
            if (_locomotion.IsAirborne || legs.Count != _wasStepping.Count)
            {
                _wasStepping.Clear();
                foreach (ProceduralLeg leg in legs) _wasStepping.Add(leg.IsStepping);
                _previousBody = transform.position;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 travel = transform.position - _previousBody;
            travel.y = 0f;
            float bodySpeed = travel.magnitude / dt;
            _previousBody = transform.position;

            // How much of the body's weight is actually on the ground: a leg in the air or over nothing
            // raises no dust, and neither does a creature balanced on one foot raise as much as one on four.
            int grounded = 0;
            for (int i = 0; i < legs.Count; i++)
                if (legs[i].HasGroundContact && !legs[i].IsStepping) grounded++;

            float contact = legs.Count > 0 ? grounded / (float)legs.Count : 0f;
            float speedScale = Mathf.Clamp(bodySpeed / _fullPuffSpeed, 0.2f, _maxPuffScale);

            for (int i = 0; i < legs.Count; i++)
            {
                bool stepping = legs[i].IsStepping;

                // A foot that was in the air and is now down has landed.
                if (_wasStepping[i] && !stepping && legs[i].HasGroundContact && bodySpeed >= _minBodySpeed)
                    Emit(legs[i].FootPosition, Mathf.RoundToInt(_particlesPerStep * speedScale * Mathf.Lerp(0.4f, 1f, contact)));

                _wasStepping[i] = stepping;
            }
        }

        private void Emit(Vector3 position, int count)
        {
            if (_dustPrefab == null || count <= 0) return;
            if (_sharedDust == null) _sharedDust = Instantiate(_dustPrefab);

            var emit = new ParticleSystem.EmitParams { position = position + Vector3.up * 0.05f, applyShapeToPosition = true };
            _sharedDust.Emit(emit, count);
        }
    }
}
