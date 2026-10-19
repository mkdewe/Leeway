using System;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>Orthonormal tangent frame. Local X = right, Y = up, Z = forward.
    /// Projects world +Z onto the tangent plane; near +/-Z (squared projection &lt; 1e-6)
    /// projects +X instead. This deterministic chart has a seam; it is not a globally
    /// continuous compass or a history-dependent parallel-transport frame.</summary>
    public readonly struct SurfaceFrame
    {
        public Vector3 Up { get; }
        public Vector3 Forward { get; }
        public Vector3 Right { get; }

        public SurfaceFrame(Vector3 up)
        {
            if (float.IsNaN(up.x) || float.IsInfinity(up.x) ||
                float.IsNaN(up.y) || float.IsInfinity(up.y) ||
                float.IsNaN(up.z) || float.IsInfinity(up.z) || up.sqrMagnitude < 1e-12f)
                throw new ArgumentException("Up must be finite and nonzero.", nameof(up));
            Up = up.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, Up);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(Vector3.right, Up);
            Right = Vector3.Cross(Up, forward).normalized;
            Forward = Vector3.Cross(Right, Up).normalized;
        }

        public float Vertical(Vector3 vector) => Vector3.Dot(vector, Up);
        public Vector3 Tangent(Vector3 vector) => vector - Up * Vertical(vector);
        public Vector3 ToWorld(Vector3 local) => Right * local.x + Up * local.y + Forward * local.z;
        public Vector3 ToLocal(Vector3 world) =>
            new Vector3(Vector3.Dot(world, Right), Vertical(world), Vector3.Dot(world, Forward));

        /// <summary>Yaw in degrees, positive from forward towards right.</summary>
        public Vector3 Heading(float yaw)
        {
            float radians = yaw * Mathf.Deg2Rad;
            return Forward * Mathf.Cos(radians) + Right * Mathf.Sin(radians);
        }

        /// <summary>Zero for a heading with no tangent component.</summary>
        public float YawOf(Vector3 direction)
        {
            float x = Vector3.Dot(direction, Right), z = Vector3.Dot(direction, Forward);
            return x * x + z * z < 1e-12f ? 0f : Mathf.Atan2(x, z) * Mathf.Rad2Deg;
        }

        public Quaternion Rotation(float yaw) => Quaternion.LookRotation(Heading(yaw), Up);
    }
}
