using System;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>
    /// What the player chooses about a planet: how big it is, how much of it is sea and how many islands
    /// there are. The seed decides the rest.
    /// </summary>
    [Serializable]
    public sealed class PlanetSettings
    {
        public const float MinRadius = 1000f;
        public const float MaxRadius = 12000f;
        public const float MinWater = 0.2f;
        public const float MaxWater = 0.95f;
        public const int MinIslands = 1;
        public const int MaxIslands = 40;

        [Tooltip("Radius of the planet in metres. The islands grow and shrink with it.")]
        [SerializeField] private float _radius = 3500f;

        [Tooltip("Fraction of the surface covered by sea.")]
        [Range(MinWater, MaxWater)]
        [SerializeField] private float _waterCoverage = 0.7f;

        [Tooltip("Islands on the planet, the home island included.")]
        [Range(MinIslands, MaxIslands)]
        [SerializeField] private int _islandCount = 9;

        [Tooltip("Added to the world seed: another number, another planet.")]
        [SerializeField] private int _seedOffset;

        public float Radius { get => _radius; set => _radius = Mathf.Clamp(value, MinRadius, MaxRadius); }
        public float WaterCoverage { get => _waterCoverage; set => _waterCoverage = Mathf.Clamp(value, MinWater, MaxWater); }
        public int IslandCount { get => _islandCount; set => _islandCount = Mathf.Clamp(value, MinIslands, MaxIslands); }
        public int SeedOffset { get => _seedOffset; set => _seedOffset = value; }

        public PlanetSettings Clone() => new()
        {
            Radius = _radius, WaterCoverage = _waterCoverage, IslandCount = _islandCount, SeedOffset = _seedOffset,
        };

        public bool SameAs(PlanetSettings other) => other != null
            && Mathf.Approximately(_radius, other._radius)
            && Mathf.Approximately(_waterCoverage, other._waterCoverage)
            && _islandCount == other._islandCount
            && _seedOffset == other._seedOffset;
    }
}
