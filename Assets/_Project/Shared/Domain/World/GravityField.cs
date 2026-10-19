using System;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>Immutable, scene-independent gravity with constant acceleration magnitude.
    /// Reference radius/plane describes sea level, not terrain height.</summary>
    public sealed class GravityField
    {
        public bool IsRadial { get; }
        public Vector3 Origin { get; }
        public float ReferenceRadius { get; }
        public float Strength { get; }
        public Vector3 FlatUp { get; }

        private GravityField(bool radial, Vector3 origin, float radius, float strength, Vector3 up)
        {
            RequireFinite(origin, nameof(origin));
            if (float.IsNaN(strength) || float.IsInfinity(strength) || strength < 0f)
                throw new ArgumentOutOfRangeException(nameof(strength));
            IsRadial = radial;
            Origin = origin;
            ReferenceRadius = radius;
            Strength = strength;
            FlatUp = up;
        }

        public static GravityField Flat(Vector3 origin, Vector3 up, float strength = 9.81f)
        {
            RequireFinite(up, nameof(up));
            if (up.sqrMagnitude < 1e-12f) throw new ArgumentException("Up must be nonzero.", nameof(up));
            return new GravityField(false, origin, 0f, strength, up.normalized);
        }

        public static GravityField Radial(Vector3 center, float referenceRadius, float strength = 9.81f)
        {
            if (float.IsNaN(referenceRadius) || float.IsInfinity(referenceRadius) || referenceRadius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(referenceRadius));
            return new GravityField(true, center, referenceRadius, strength, Vector3.up);
        }

        /// <summary>At the exact centre the direction is undefined; use a deterministic +Y fallback.
        /// Acceleration there is zero. No fallback band is introduced around the centre.</summary>
        public Vector3 UpAt(Vector3 position)
        {
            if (!IsRadial) return FlatUp;
            Vector3 offset = position - Origin;
            float length = offset.magnitude;
            return length > 0f ? offset / length : Vector3.up;
        }

        public Vector3 AccelerationAt(Vector3 position) =>
            IsRadial && position.Equals(Origin) ? Vector3.zero : -UpAt(position) * Strength;

        public float AltitudeAt(Vector3 position) => IsRadial
            ? (position - Origin).magnitude - ReferenceRadius
            : Vector3.Dot(position - Origin, FlatUp);

        public SurfaceFrame FrameAt(Vector3 position) => new SurfaceFrame(UpAt(position));

        private static void RequireFinite(Vector3 value, string name)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y) ||
                float.IsNaN(value.z) || float.IsInfinity(value.z))
                throw new ArgumentException("Vector must be finite.", name);
        }
    }
}
