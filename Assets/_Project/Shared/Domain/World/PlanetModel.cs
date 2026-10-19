using System;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>
    /// A whole planet as data: where its islands lie on the sphere, where the land ends and the sea begins,
    /// and how high the ground is. Purely a function of its parameters and a seed, so every machine that
    /// builds it from the same numbers gets the same planet.
    /// </summary>
    /// <remarks>
    /// <para>Place the islands, give each a weight and let one scale decide how large they all grow:
    /// that scale is found by bisection so the land the planet ends up with matches the requested
    /// share of the surface. The home island - the one that is played on - keeps its real size and
    /// sits at the north pole, so the planet is built around it rather than the other way round.</para>
    ///
    /// <para>Directions are unit vectors; the planet is centred on the origin and the answer for any
    /// direction is in <see cref="Sample"/>.</para>
    /// </remarks>
    public sealed class PlanetModel
    {
        public readonly struct Island
        {
            public readonly Vector3 Direction;
            /// <summary>Angular radius in radians before the coast's wobble.</summary>
            public readonly float AngularRadius;
            public readonly bool IsHome;

            public Island(Vector3 direction, float angularRadius, bool isHome)
            {
                Direction = direction;
                AngularRadius = angularRadius;
                IsHome = isHome;
            }
        }

        /// <summary>What is at one point of the surface.</summary>
        public readonly struct Point
        {
            /// <summary>Above zero on land, below zero in the sea; 1 at an island's centre.</summary>
            public readonly float Land;
            /// <summary>Height above sea level in metres (zero in the sea).</summary>
            public readonly float Height;
            /// <summary>0 at the shore to 1 in the deep sea.</summary>
            public readonly float Depth;
            /// <summary>Signed physical ground elevation in metres, including the ocean floor.</summary>
            public readonly float GroundHeight;
            public readonly float WaterHeight;

            public Point(float land, float height, float depth) : this(land, height, depth, height) { }

            public Point(float land, float height, float depth, float groundHeight, float waterHeight = 0f)
            {
                Land = land;
                Height = height;
                Depth = depth;
                GroundHeight = groundHeight;
                WaterHeight = waterHeight;
            }

            public bool IsLand => Land > 0f;
        }

        private const int CoverageSamples = 2600;
        private const float CoastWobble = 0.38f;
        private const float MinScale = 0.012f;
        private const float MaxScale = 3.0f;

        private readonly Vector3[] _centers;
        private readonly float[] _weights;
        private readonly float _homeRadius;
        private readonly Vector3 _offsetA, _offsetB, _offsetC, _offsetD;
        private readonly int _noiseSeed;
        private float _scale;
        private Func<float, float, float> _home;
        private float _homeReach;

        private PlanetIslandPatch[] _patches;
        public int PatchCount => _patches?.Length ?? 0;
        public PlanetIslandPatch GetPatch(int index) => _patches == null ? null : _patches[index];

        /// <summary>Build once before publishing the model to render/collision workers.
        /// Every island gets the full generator; no second terrain is kept for a distant view.</summary>
        public void BuildArchipelago(IslandHeightField home, int seed)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (_patches != null) throw new InvalidOperationException("The archipelago is already built.");
            var patches = new PlanetIslandPatch[IslandCount];
            float coast = home.Settings.CoastRadius * home.Settings.HalfSize;
            for (int i=0;i<patches.Length;i++)
            {
                var field=i==0?home:IslandHeightField.Build(home.Settings,unchecked(seed+i*3571));
                float scale=i==0?1f:Mathf.Max(0.1f,IslandRadius(i)*Radius/Mathf.Max(1f,coast));
                // A chart never wraps through its antipode.
                scale=Mathf.Min(scale,Mathf.PI*Radius*0.9f/(field.Settings.HalfSize*0.95f));
                patches[i]=new PlanetIslandPatch(field,_centers[i],Radius,scale);
            }
            _patches=patches;
        }

        private Point SampleArchipelago(Vector3 direction)
        {
            PlanetIslandPatch selected=null;
            float weight=0;
            foreach(var patch in _patches)
            {
                float candidate=patch.WeightAt(direction);
                if(candidate>weight){selected=patch;weight=candidate;if(weight>=1f)break;}
            }
            if(selected==null)return SampleOwn(direction);
            Vector2 at=selected.ChartAt(direction);
            var field=selected.Field;
            float ground=field.SampleHeight(at.x,at.y)-field.Settings.SeaLevel;
            float water=field.SampleWaterSurface(at.x,at.y)-field.Settings.SeaLevel;
            float land=field.IsLand(at.x,at.y)?0.05f+0.45f*Mathf.Clamp01(ground/8f):-0.01f-Mathf.Clamp01((water-ground)/30f);
            float depth=Mathf.Clamp01((water-ground)/Mathf.Max(10f,Radius*0.015f));
            if(weight>=1f)return new Point(land,Mathf.Max(0,ground),depth,ground,water);
            Point own=SampleOwn(direction);
            return new Point(Mathf.Lerp(own.Land,land,weight),Mathf.Lerp(own.Height,Mathf.Max(0,ground),weight),
                Mathf.Lerp(own.Depth,depth,weight),Mathf.Lerp(own.GroundHeight,ground,weight),
                Mathf.Lerp(own.WaterHeight,water,weight));
        }

        public float Radius { get; }
        public float MaxHeight => Radius * 0.022f;
        public int IslandCount => _centers.Length;

        /// <summary>The share of the surface that came out as land - close to what was asked for.</summary>
        public float LandFraction { get; private set; }

        public PlanetModel(float radius, float waterCoverage, int islandCount, int seed, float homeRadiusMetres)
        {
            Radius = Mathf.Max(1f, radius);
            islandCount = Mathf.Max(1, islandCount);
            _noiseSeed = seed * 7919 + 13;

            var rng = new System.Random(_noiseSeed);
            _offsetA = RandomOffset(rng);
            _offsetB = RandomOffset(rng);
            _offsetC = RandomOffset(rng);
            _offsetD = RandomOffset(rng);

            _homeRadius = Mathf.Clamp(homeRadiusMetres / Radius, 0.015f, 0.9f);

            _centers = new Vector3[islandCount];
            _weights = new float[islandCount];
            _centers[0] = Vector3.up;
            _weights[0] = 1f;
            for (int i = 1; i < islandCount; i++)
            {
                _centers[i] = FarthestCandidate(rng, i);
                _weights[i] = Mathf.Lerp(0.55f, 1.5f, (float)rng.NextDouble());
            }

            SolveScale(1f - Mathf.Clamp(waterCoverage, 0f, 1f));
        }

        /// <summary>
        /// Lets the real ground of the home island replace the planet's own near the pole, so that the island the creature
        /// stands on is the same island seen from above. <paramref name="height"/> gives the height in metres over sea level
        /// at a point of the island, east and north of its centre, in metres; it must be safe to call from any thread.
        /// </summary>
        public void SetHome(Func<float, float, float> height, float reachMetres)
        {
            _home = height;
            _homeReach = reachMetres;
        }

        public Island GetIsland(int index) => new(_centers[index], IslandRadius(index), index == 0);

        /// <summary>The surface in the given direction.</summary>
        public Point Sample(Vector3 direction)
        {
            direction.Normalize();
            if (_patches != null) return SampleArchipelago(direction);
            Point own = SampleOwn(direction);
            if (_home == null || _homeReach <= 0f) return own;

            float metres = Angle(direction, Vector3.up) * Radius;
            if (metres >= _homeReach) return own;

            // The real island where it is, melting into the planet's own sea over the last part of its reach.
            float weight = 1f - Smooth01((metres - _homeReach * 0.82f) / (_homeReach * 0.18f));
            var flat = new Vector2(direction.x, direction.z);
            Vector2 at = flat.sqrMagnitude > 1e-12f ? flat.normalized * metres : Vector2.zero;
            float h = _home(at.x, at.y);

            float land = h > 0f ? 0.05f + 0.45f * Smooth01(h / 8f) : -Smooth01(-h / 30f) - 1e-4f;
            float depth = h > 0f ? 0f : Smooth01(-h / 25f);
            return new Point(Mathf.Lerp(own.Land, land, weight), Mathf.Lerp(own.Height, Mathf.Max(h, 0f), weight), Mathf.Lerp(own.Depth, depth, weight), Mathf.Lerp(own.GroundHeight, h, weight));
        }

        private Point SampleOwn(Vector3 direction)
        {
            float wobble = 1f + CoastWobble * Wobble(direction);

            float land = float.NegativeInfinity;
            for (int i = 0; i < _centers.Length; i++)
            {
                float angle = Angle(direction, _centers[i]);
                float value = 1f - angle / (IslandRadius(i) * wobble);
                if (value > land) land = value;
            }

            if (land <= 0f)
            {
                float depth = Smooth01(-land / 1.2f);
                return new Point(land, 0f, depth, -Mathf.Max(10f, Radius * 0.015f) * depth);
            }

            // Rises from the shore, then rolls: a plain slope would make every island a cone.
            float rise = Smooth01(land / 0.45f);
            float hills = 0.5f + 0.5f * (0.65f * Noise(direction * 14f + _offsetC) + 0.35f * Noise(direction * 31f + _offsetD));
            // Finer and finer grain on top, for the eye that comes close: ridges of some hundreds of metres down to knolls of tens.
            float grain = 0.5f * Noise(direction * 70f + _offsetA) + 0.3f * Noise(direction * 150f + _offsetB) + 0.2f * Noise(direction * 330f + _offsetC);
            float height = MaxHeight * rise * (0.22f + 0.78f * hills) + MaxHeight * 0.16f * rise * grain;
            return new Point(land, Mathf.Max(0f, height), 0f);
        }

        // ---------------------------------------------------------------- building the layout

        private float IslandRadius(int index) => index == 0 ? _homeRadius : _scale * _weights[index];

        // Mitchell's best candidate: of a few random spots, the one farthest from every island so far.
        private Vector3 FarthestCandidate(System.Random rng, int placed)
        {
            Vector3 best = Vector3.down;
            float bestGap = -1f;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector3 candidate = RandomDirection(rng);
                float nearest = float.MaxValue;
                for (int i = 0; i < placed; i++) nearest = Mathf.Min(nearest, Angle(candidate, _centers[i]));

                if (nearest > bestGap)
                {
                    bestGap = nearest;
                    best = candidate;
                }
            }
            return best;
        }

        private void SolveScale(float landTarget)
        {
            // A fixed, even spread of points on the sphere stands for the surface; the wobble and the angles to
            // the islands are worked out once, and only the scale changes while the bisection runs.
            var wobble = new float[CoverageSamples];
            var angles = new float[CoverageSamples * _centers.Length];
            for (int s = 0; s < CoverageSamples; s++)
            {
                Vector3 dir = FibonacciPoint(s, CoverageSamples);
                wobble[s] = 1f + CoastWobble * Wobble(dir);
                for (int i = 0; i < _centers.Length; i++) angles[s * _centers.Length + i] = Angle(dir, _centers[i]);
            }

            float Fraction(float scale)
            {
                int land = 0;
                for (int s = 0; s < CoverageSamples; s++)
                {
                    for (int i = 0; i < _centers.Length; i++)
                    {
                        float radius = (i == 0 ? _homeRadius : scale * _weights[i]) * wobble[s];
                        if (angles[s * _centers.Length + i] < radius) { land++; break; }
                    }
                }
                return land / (float)CoverageSamples;
            }

            if (_centers.Length == 1)
            {
                _scale = MinScale;
                LandFraction = Fraction(_scale);
                return;
            }

            float low = MinScale, high = MaxScale;
            if (Fraction(low) >= landTarget)
            {
                // The home island alone already covers what was asked for: the rest stay as islets.
                _scale = low;
            }
            else
            {
                for (int iteration = 0; iteration < 26; iteration++)
                {
                    float middle = 0.5f * (low + high);
                    if (Fraction(middle) < landTarget) low = middle; else high = middle;
                }
                _scale = 0.5f * (low + high);
            }

            LandFraction = Fraction(_scale);
        }

        // ---------------------------------------------------------------- noise

        // The wobble of the coast, in [-1, 1]: wide bays and headlands with a finer grain over them.
        private float Wobble(Vector3 dir) =>
            0.55f * Noise(dir * 5f + _offsetA) + 0.35f * Noise(dir * 11f + _offsetB) + 0.1f * Noise(dir * 26f + _offsetC);

        /// <summary>Smooth value noise in [-1, 1] over three dimensions.</summary>
        private float Noise(Vector3 p)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = Fade(p.x - x0), fy = Fade(p.y - y0), fz = Fade(p.z - z0);

            float Corner(int dx, int dy, int dz) => Lattice(x0 + dx, y0 + dy, z0 + dz);

            float c00 = Mathf.Lerp(Corner(0, 0, 0), Corner(1, 0, 0), fx);
            float c10 = Mathf.Lerp(Corner(0, 1, 0), Corner(1, 1, 0), fx);
            float c01 = Mathf.Lerp(Corner(0, 0, 1), Corner(1, 0, 1), fx);
            float c11 = Mathf.Lerp(Corner(0, 1, 1), Corner(1, 1, 1), fx);
            return Mathf.Lerp(Mathf.Lerp(c00, c10, fy), Mathf.Lerp(c01, c11, fy), fz);
        }

        private float Lattice(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647 + _noiseSeed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFu) / 32767.5f - 1f;
            }
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ---------------------------------------------------------------- geometry

        private static float Angle(Vector3 a, Vector3 b) => Mathf.Atan2(Vector3.Cross(a, b).magnitude, Vector3.Dot(a, b));

        private static Vector3 RandomOffset(System.Random rng) =>
            new((float)rng.NextDouble() * 200f, (float)rng.NextDouble() * 200f, (float)rng.NextDouble() * 200f);

        private static Vector3 RandomDirection(System.Random rng)
        {
            float y = (float)(rng.NextDouble() * 2.0 - 1.0);
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float ring = Mathf.Sqrt(1f - y * y);
            return new Vector3(ring * Mathf.Cos(angle), y, ring * Mathf.Sin(angle));
        }

        private static Vector3 FibonacciPoint(int index, int count)
        {
            const float golden = 2.39996323f;
            float y = 1f - 2f * (index + 0.5f) / count;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = index * golden;
            return new Vector3(ring * Mathf.Cos(angle), y, ring * Mathf.Sin(angle));
        }
    }
}
