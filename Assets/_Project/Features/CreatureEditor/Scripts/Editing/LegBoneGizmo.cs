using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The joint handles of the selected leg. The counterpart of the vertebra handles, only for a
    /// chain that grows out of a part.
    /// </summary>
    /// <remarks>
    /// <para><b>A leg is a bone, not an icon.</b> Until now its build lived entirely in the catalog —
    /// the player could pick stubby or slender legs, and that was the end of their authority. The joint
    /// handle records the reshaping in <see cref="PartGene"/>, so two pairs of the same legs on one
    /// creature can differ, and the catalog becomes a starting point rather than a verdict.</para>
    ///
    /// <para>Dragging a joint changes the <b>link length</b>, scrolling over it the number of bends.
    /// That is the same pair of gestures the player already knows from the spine (drag shapes, scroll
    /// grows), so there is nothing new to learn.</para>
    ///
    /// <para>We draw our own shapes instead of using colliders: a joint is a point, not a surface, so a
    /// hit is measured as the distance from the ray to that point. The same pattern as
    /// <see cref="PartRotationGizmo"/> and <see cref="SpineExtendGizmo"/>.</para>
    /// </remarks>
    public class LegBoneGizmo : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Material _material;

        [Tooltip("The radius of the joint ball.")]
        [SerializeField] private float _jointRadius = 0.045f;

        [Tooltip("Hit padding beyond the ball's radius - a joint should be grabbable without aiming at a single pixel.")]
        [SerializeField] private float _hitPadding = 0.05f;

        [SerializeField] private Color _normalColor = new(0.55f, 1f, 0.6f, 1f);
        [SerializeField] private Color _highlightColor = new(1f, 0.85f, 0.3f, 1f);

        private readonly List<Vector3> _joints = new();
        private readonly List<Transform> _markers = new();
        private MaterialPropertyBlock _block;

        private int _highlighted = -1;

        public bool IsVisible { get; private set; }

        /// <summary>The gene of the part whose leg we are showing. <c>-1</c> when the gizmo is hidden.</summary>
        public int GeneIndex { get; private set; } = -1;

        /// <summary>How many joints the shown leg has, hip and foot included.</summary>
        public int JointCount => _joints.Count;

        /// <summary>
        /// Shows the joints of the first non-mirrored leg grown from the given gene. If there is no such
        /// leg the gizmo hides — not every part has a chain.
        /// </summary>
        public void Show(IReadOnlyList<ProceduralLeg> legs, int geneIndex)
        {
            ProceduralLeg leg = Find(legs, geneIndex);
            if (leg == null)
            {
                Hide();
                return;
            }

            GeneIndex = geneIndex;
            IsVisible = true;

            _joints.Clear();
            for (int i = 0; i < leg.Joints.Count; i++) _joints.Add(leg.Joints[i]);

            SyncMarkers();
        }

        public void Hide()
        {
            IsVisible = false;
            GeneIndex = -1;
            _highlighted = -1;
            _joints.Clear();

            foreach (Transform marker in _markers)
                if (marker != null) marker.gameObject.SetActive(false);
        }

        /// <summary>The joint under the cursor. The one nearest the ray axis wins, not the one nearest the camera.</summary>
        public bool TryPick(Ray ray, out int jointIndex)
        {
            jointIndex = -1;
            if (!IsVisible) return false;

            float tolerance = _jointRadius + _hitPadding;
            float best = tolerance;

            for (int i = 0; i < _joints.Count; i++)
            {
                Vector3 offset = _joints[i] - ray.origin;
                float along = Vector3.Dot(offset, ray.direction);
                if (along <= 0f) continue;

                float distance = Vector3.Distance(_joints[i], ray.origin + ray.direction * along);
                if (distance >= best) continue;

                best = distance;
                jointIndex = i;
            }

            return jointIndex >= 0;
        }

        public void SetHighlighted(int jointIndex)
        {
            _highlighted = jointIndex;
            RefreshColors();
        }

        public Vector3 JointPosition(int index)
            => index >= 0 && index < _joints.Count ? _joints[index] : Vector3.zero;

        private static ProceduralLeg Find(IReadOnlyList<ProceduralLeg> legs, int geneIndex)
        {
            if (legs == null) return null;

            foreach (ProceduralLeg leg in legs)
                if (leg != null && leg.GeneIndex == geneIndex) return leg;

            return null;
        }

        /// <summary>
        /// Adds or hides balls so there are exactly as many of them as there are joints.
        /// </summary>
        /// <remarks>
        /// A pool rather than destroying and recreating: the number of bends changes with the scroll
        /// wheel, i.e. several times a second, and every such change rebuilds the preview.
        /// </remarks>
        private void SyncMarkers()
        {
            while (_markers.Count < _joints.Count) _markers.Add(CreateMarker(_markers.Count));

            for (int i = 0; i < _markers.Count; i++)
            {
                Transform marker = _markers[i];
                if (marker == null) continue;

                bool used = i < _joints.Count;
                marker.gameObject.SetActive(used);
                if (!used) continue;

                marker.position = _joints[i];
                marker.localScale = Vector3.one * (_jointRadius * 2f);
            }

            RefreshColors();
        }

        private Transform CreateMarker(int index)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"LegJoint_{index}";
            go.transform.SetParent(transform, false);

            // We work out hits ourselves from the radius, so the primitive's collider is only an
            // obstacle — it would catch the clicks meant for the body underneath.
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            if (_material != null) renderer.sharedMaterial = _material;

            return go.transform;
        }

        private void RefreshColors()
        {
            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < _markers.Count; i++)
            {
                if (_markers[i] == null || !_markers[i].gameObject.activeSelf) continue;
                if (!_markers[i].TryGetComponent(out Renderer renderer)) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, i == _highlighted ? _highlightColor : _normalColor);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
