using System;
using System.Collections.Generic;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>
    /// The star system the player's planet belongs to, as data: a star, a number of planets on circular
    /// orbits round it - the player's among them - and the moons of the player's planet.
    /// </summary>
    /// <remarks>
    /// <para>Positions are a pure function of the system clock, so every machine with the same numbers and
    /// the same clock sees the same sky. Orbits are circles in one plane; their periods follow Kepler's
    /// third law, so the inner planets race and the outer ones crawl. Distances are in astronomical units
    /// where the player's planet is 1 AU out, sizes in radii of the player's planet.</para>
    ///
    /// <para>Nothing in it pulls on anything else yet: no gravity, no tides, no eclipses. It says where
    /// each body is, and that is all the sky needs.</para>
    /// </remarks>
    public sealed class StarSystemModel
    {
        public enum Kind { Rocky, Gas }

        public readonly struct Planet
        {
            public readonly float OrbitAu;
            public readonly float PeriodSeconds;
            public readonly float Phase;
            /// <summary>Radius in radii of the player's planet.</summary>
            public readonly float Size;
            public readonly Kind Kind;
            public readonly Color Colour;
            public readonly Color Accent;
            public readonly bool IsHome;

            public Planet(float orbitAu, float periodSeconds, float phase, float size, Kind kind, Color colour, Color accent, bool isHome)
            {
                OrbitAu = orbitAu;
                PeriodSeconds = periodSeconds;
                Phase = phase;
                Size = size;
                Kind = kind;
                Colour = colour;
                Accent = accent;
                IsHome = isHome;
            }
        }

        public readonly struct Moon
        {
            /// <summary>Distance from the planet's centre, in radii of the planet.</summary>
            public readonly float OrbitRadius;
            public readonly float PeriodSeconds;
            public readonly float Phase;
            /// <summary>Tilt of the orbit out of the ecliptic, in radians.</summary>
            public readonly float Inclination;
            /// <summary>Radius in radii of the planet.</summary>
            public readonly float Size;
            public readonly Color Colour;

            public Moon(float orbitRadius, float periodSeconds, float phase, float inclination, float size, Color colour)
            {
                OrbitRadius = orbitRadius;
                PeriodSeconds = periodSeconds;
                Phase = phase;
                Inclination = inclination;
                Size = size;
                Colour = colour;
            }
        }

        /// <summary>Seconds the player's planet takes to go once round its star.</summary>
        public const float HomeYearSeconds = 480f;

        /// <summary>Radius of the star in radii of the player's planet.</summary>
        public const float StarSize = 24f;

        private static readonly Color[] RockyColours =
        {
            new(0.72f, 0.45f, 0.32f), new(0.62f, 0.60f, 0.56f), new(0.80f, 0.68f, 0.45f), new(0.50f, 0.36f, 0.34f),
        };

        private static readonly Color[] GasColours =
        {
            new(0.86f, 0.72f, 0.50f), new(0.52f, 0.68f, 0.82f), new(0.78f, 0.56f, 0.46f), new(0.66f, 0.74f, 0.58f),
        };

        private readonly Planet[] _planets;
        private readonly Moon[] _moons;

        public IReadOnlyList<Planet> Planets => _planets;
        public IReadOnlyList<Moon> Moons => _moons;

        /// <summary>Index of the player's planet among <see cref="Planets"/>.</summary>
        public int HomeIndex { get; }

        public StarSystemModel(int planetCount, int moonCount, int seed)
        {
            planetCount = Mathf.Clamp(planetCount, 1, 12);
            moonCount = Mathf.Clamp(moonCount, 0, 12);
            var random = new System.Random(unchecked(seed * 16807 + 91));

            // The player's planet sits third from the star when there is room for that.
            HomeIndex = Mathf.Min(2, planetCount - 1);

            // Each orbit about 1.6 times the last, then everything scaled so the player's is 1 AU.
            var raw = new float[planetCount];
            float a = 0.45f;
            for (int i = 0; i < planetCount; i++)
            {
                raw[i] = a;
                a *= 1.45f + (float)random.NextDouble() * 0.35f;
            }

            _planets = new Planet[planetCount];
            for (int i = 0; i < planetCount; i++)
            {
                float au = raw[i] / raw[HomeIndex];
                float period = HomeYearSeconds * au * Mathf.Sqrt(au);
                float phase = (float)(random.NextDouble() * Mathf.PI * 2.0);

                if (i == HomeIndex)
                {
                    _planets[i] = new Planet(au, period, phase, 1f, Kind.Rocky, new Color(0.3f, 0.6f, 0.45f), Color.white, true);
                    continue;
                }

                // Close to the star the planets are rock, far from it gas.
                bool gas = au > 1.9f;
                float size = gas ? 2.4f + (float)random.NextDouble() * 3f : 0.35f + (float)random.NextDouble() * 1.0f;
                Color[] palette = gas ? GasColours : RockyColours;
                Color colour = palette[random.Next(palette.Length)];
                Color accent = Color.Lerp(colour, gas ? Color.white : Color.black, 0.28f);
                _planets[i] = new Planet(au, period, phase, size, gas ? Kind.Gas : Kind.Rocky, colour, accent, false);
            }

            _moons = new Moon[moonCount];
            for (int j = 0; j < moonCount; j++)
            {
                float orbit = 5.5f + 2.6f * j + (float)random.NextDouble() * 0.6f;
                float period = 150f * orbit * Mathf.Sqrt(orbit) / 12.9f;
                float size = Mathf.Max(0.05f, 0.1f - 0.008f * j) * (0.8f + (float)random.NextDouble() * 0.4f);
                float tilt = (float)(random.NextDouble() - 0.5) * 0.5f;
                float shade = 0.55f + (float)random.NextDouble() * 0.3f;
                _moons[j] = new Moon(orbit, period, (float)(random.NextDouble() * Mathf.PI * 2.0), tilt, size,
                    new Color(shade, shade * 0.97f, shade * 0.92f));
            }
        }

        /// <summary>A planet's place in the ecliptic, in AU, the star at the origin.</summary>
        public Vector3 PlanetPosition(int index, double seconds)
        {
            Planet p = _planets[index];
            double angle = p.Phase + seconds / p.PeriodSeconds * (Math.PI * 2.0);
            return new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * p.OrbitAu;
        }

        /// <summary>A moon's place relative to its planet's centre, in radii of the planet.</summary>
        public Vector3 MoonOffset(int index, double seconds)
        {
            Moon m = _moons[index];
            double angle = m.Phase + seconds / m.PeriodSeconds * (Math.PI * 2.0);
            var flat = new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * m.OrbitRadius;
            return Quaternion.AngleAxis(m.Inclination * Mathf.Rad2Deg, Vector3.right) * flat;
        }
    }
}
