using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Fits a single locomotion capsule to the spine.
    /// </summary>
    /// <remarks>
    /// Deliberately coarse. The capsule only has to be a believable body volume — it is precisely
    /// <b>one</b> simple collider that keeps movement prediction manageable. The exact shape is the
    /// job of the ragdoll colliders, and only while knocked down.
    /// </remarks>
    public static class CreatureColliderFitter
    {
        /// <summary>
        /// Sets the dimensions of an existing capsule. It deliberately neither creates nor destroys a
        /// collider — swapping a collider under a moving, predicted Rigidbody can let the body through
        /// the floor for a frame.
        /// </summary>
        public static void Fit(CapsuleCollider collider, CreatureGenome genome, Matrix4x4[] boneToRoot,
            CreatureBodyBuildSettings settings)
        {
            if (collider == null || genome == null || genome.VertebraCount == 0) return;

            float minRadius = settings != null ? settings.MinColliderRadius : 0.15f;

            Vector3 first = boneToRoot[0].GetColumn(3);
            var bounds = new Bounds(first, Vector3.zero);
            float maxRadius = genome.GetVertebra(0).Radius;

            for (int i = 1; i < genome.VertebraCount; i++)
            {
                bounds.Encapsulate((Vector3)boneToRoot[i].GetColumn(3));
                maxRadius = Mathf.Max(maxRadius, genome.GetVertebra(i).Radius);
            }

            float radius = Mathf.Max(maxRadius, minRadius);

            // The spine runs along -Z (head at the front), so the capsule stands on the Z axis.
            collider.direction = 2;

            // The anchor already sits inside boneToRoot, so the centre comes out anchored.
            collider.center = bounds.center;
            collider.radius = radius;

            // Unity clamps the height to 2*radius anyway — we do it explicitly so the value in the
            // inspector matches what the physics actually simulates.
            collider.height = Mathf.Max(bounds.size.z + 2f * radius, 2f * radius);
        }
    }
}
