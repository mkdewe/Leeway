using System.Collections.Generic;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>A lake basin. <see cref="Center"/>.y is the water surface.</summary>
    public readonly struct IslandLake
    {
        public readonly Vector3 Center;
        public readonly float Radius;

        public IslandLake(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>
    /// A river as a polyline from its source to the sea. Each point's Y is the water surface there;
    /// it never rises downstream, so a drop between two points is a waterfall.
    /// </summary>
    public sealed class IslandRiver
    {
        public IReadOnlyList<Vector3> Points { get; }
        public float Width { get; }

        public IslandRiver(IReadOnlyList<Vector3> points, float width)
        {
            Points = points;
            Width = width;
        }
    }

    /// <summary>A cave carved into a cliff. Position is the floor centre; the mouth faces <see cref="Facing"/>.</summary>
    public readonly struct IslandCave
    {
        public readonly Vector3 Position;
        public readonly Vector3 Facing;
        public readonly float Radius;

        public IslandCave(Vector3 position, Vector3 facing, float radius)
        {
            Position = position;
            Facing = facing;
            Radius = radius;
        }

        public float Yaw => Mathf.Atan2(Facing.x, Facing.z) * Mathf.Rad2Deg;
    }
}
