using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The zone in which a creature may be rebuilt. It only counts entries and exits — the decision to
    /// accept a genome is made by the server in <see cref="CreatureBody"/>.
    /// </summary>
    /// <remarks>
    /// <para>The gate exists for physics reasons, not design ones: swapping a <c>CapsuleCollider</c>
    /// under a moving, predicted <c>Rigidbody</c> can let the creature through the floor for a frame.
    /// Forcing it to stand still inside the zone removes that case, and to the player it reads like a
    /// rule of the game — exactly as in Spore.</para>
    ///
    /// <para>The component works on both sides: the server uses the counter for validation, the client
    /// for a HUD hint. We count overlaps rather than keeping a flag, because two touching pads would
    /// otherwise switch each other off while moving between them.</para>
    /// </remarks>
    /// <remarks>
    /// <para>The zone is a <b>circle</b>, not a square. That is not a matter of taste: a pad has no
    /// preferred direction, so every corner of a square is a place where the player stands "on the
    /// pad" by the rule but off it by what they can see. A circle is the only shape whose reach reads
    /// the same from every side.</para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public class EditorPad : MonoBehaviour
    {
        [SerializeField] private Color _gizmoColor = new Color(0.2f, 0.8f, 1f, 0.25f);

        [Tooltip("How many segments the drawn zone circle has. Gizmo only - the reach itself is smooth.")]
        [SerializeField, Range(12, 96)] private int _gizmoSegments = 48;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (TryResolveBody(other, out CreatureBody body)) body.EnterEditorPad();
        }

        private void OnTriggerExit(Collider other)
        {
            if (TryResolveBody(other, out CreatureBody body)) body.ExitEditorPad();
        }

        /// <summary>
        /// Accepts only a collider sitting <b>directly</b> on the creature object. If we searched the
        /// parents, every ragdoll bone entering the zone would bump the counter separately and a
        /// protruding tail would keep the creature "in the zone" after it had left.
        /// </summary>
        internal static bool TryResolveBody(Collider other, out CreatureBody body)
            => other.TryGetComponent(out body);

        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out Collider col)) return;

            Bounds bounds = col.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);

            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 1f);

            Vector3 previous = bounds.center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= _gizmoSegments; i++)
            {
                float angle = i / (float)_gizmoSegments * Mathf.PI * 2f;
                Vector3 point = bounds.center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
