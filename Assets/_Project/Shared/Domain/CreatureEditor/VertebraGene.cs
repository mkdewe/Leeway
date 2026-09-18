using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// A single vertebra. The transform is <b>relative to the previous vertebra</b>
    /// (vertebra 0 — relative to the armature root), so the list of vertebrae maps
    /// one-to-one onto the bone hierarchy and a bend propagates naturally down the tail.
    /// </summary>
    public readonly struct VertebraGene
    {
        public readonly Vector3 LocalOffset;
        public readonly Quaternion LocalRotation;

        /// <summary>Body thickness at this vertebra. Feeds the mesh geometry, never the bone scale.</summary>
        public readonly float Radius;

        public VertebraGene(Vector3 localOffset, Quaternion localRotation, float radius)
        {
            LocalOffset = localOffset;
            LocalRotation = localRotation;
            Radius = radius;
        }

        public VertebraGene WithOffset(Vector3 localOffset) => new VertebraGene(localOffset, LocalRotation, Radius);
        public VertebraGene WithRotation(Quaternion localRotation) => new VertebraGene(LocalOffset, localRotation, Radius);
        public VertebraGene WithRadius(float radius) => new VertebraGene(LocalOffset, LocalRotation, radius);
    }
}
