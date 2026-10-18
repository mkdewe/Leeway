using System;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>
    /// What the player chooses about a planet: how big it is, how much of it is sea and how many islands
    /// there are - and about the star system around it: how many planets and how many moons. The seed decides the rest.
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
        public const int MinPlanets = 1;
        public const int MaxPlanets = 8;
        public const int MinMoons = 0;
        public const int MaxMoons = 6;

        [Tooltip("Radius of the planet in metres. The islands grow and shrink with it.")]
        [SerializeField] private float _radius = 3500f;

        [Tooltip("Fraction of the surface covered by sea.")]
        [Range(MinWater, MaxWater)]
        [SerializeField] private float _waterCoverage = 0.7f;

        [Tooltip("Islands on the planet, the home island included.")]
        [Range(MinIslands, MaxIslands)]
        [SerializeField] private int _islandCount = 9;

        [Tooltip("Planets in the star system, the player's own included.")]
        [Range(MinPlanets, MaxPlanets)]
        [SerializeField] private int _planetCount = 5;

        [Tooltip("Moons of the player's planet.")]
        [Range(MinMoons, MaxMoons)]
        [SerializeField] private int _moonCount = 1;

        [Tooltip("Added to the world seed: another number, another planet.")]
        [SerializeField] private int _seedOffset;

        public float Radius { get => _radius; set => _radius = Mathf.Clamp(value, MinRadius, MaxRadius); }
        public float WaterCoverage { get => _waterCoverage; set => _waterCoverage = Mathf.Clamp(value, MinWater, MaxWater); }
        public int IslandCount { get => _islandCount; set => _islandCount = Mathf.Clamp(value, MinIslands, MaxIslands); }
        public int SeedOffset { get => _seedOffset; set => _seedOffset = value; }
        public int PlanetCount { get => _planetCount; set => _planetCount = Mathf.Clamp(value, MinPlanets, MaxPlanets); }
        public int MoonCount { get => _moonCount; set => _moonCount = Mathf.Clamp(value, MinMoons, MaxMoons); }

        public PlanetSettings Clone() => new()
        {
            Radius = _radius, WaterCoverage = _waterCoverage, IslandCount = _islandCount, SeedOffset = _seedOffset,
            PlanetCount = _planetCount, MoonCount = _moonCount,
        };

        public bool SameAs(PlanetSettings other) => other != null
            && Mathf.Approximately(_radius, other._radius)
            && Mathf.Approximately(_waterCoverage, other._waterCoverage)
            && _islandCount == other._islandCount
            && _seedOffset == other._seedOffset;

        /// <summary>The same star system: what the sky is made of, apart from the planet itself.</summary>
        public bool SameSystemAs(PlanetSettings other) => other != null
            && _planetCount == other._planetCount
            && _moonCount == other._moonCount
            && _seedOffset == other._seedOffset;
    }
}
