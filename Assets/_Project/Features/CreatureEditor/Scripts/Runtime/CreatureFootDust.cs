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
        [Tooltip("A foot counts as stepping when it moves this many times faster than the body...")]
        [SerializeField, Min(1f)] private float _stepSpeedRatio = 1.8f;
        [Tooltip("...and at least this fast (m/s), so idle fidgeting raises no dust.")]
        [SerializeField] private float _minStepSpeed = 1.5f;

        private static ParticleSystem _sharedDust;

        private CreatureLocomotion _locomotion;
        private readonly List<Vector3> _previous = new();
        private readonly List<bool> _swinging = new();
        private Vector3 _previousBody;

        private void Awake() => _locomotion = GetComponent<CreatureLocomotion>();

        private void LateUpdate()
        {
            IReadOnlyList<Vector3> feet = _locomotion.FootPositions;
            if (feet.Count != _previous.Count)
            {
                // The body was rebuilt with a different number of legs.
                _previous.Clear();
                _swinging.Clear();
                foreach (Vector3 foot in feet)
                {
                    _previous.Add(foot);
                    _swinging.Add(false);
                }
                _previousBody = transform.position;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float bodySpeed = (transform.position - _previousBody).magnitude / dt;
            _previousBody = transform.position;
            float stepSpeed = Mathf.Max(_minStepSpeed, bodySpeed * _stepSpeedRatio);

            for (int i = 0; i < feet.Count; i++)
            {
                float speed = (feet[i] - _previous[i]).magnitude / dt;
                if (speed > stepSpeed) _swinging[i] = true;
                else if (_swinging[i])
                {
                    _swinging[i] = false;
                    Emit(feet[i]);
                }
                _previous[i] = feet[i];
            }
        }

        private void Emit(Vector3 position)
        {
            if (_dustPrefab == null) return;
            if (_sharedDust == null) _sharedDust = Instantiate(_dustPrefab);

            var emit = new ParticleSystem.EmitParams { position = position + Vector3.up * 0.05f, applyShapeToPosition = true };
            _sharedDust.Emit(emit, _particlesPerStep);
        }
    }
}
