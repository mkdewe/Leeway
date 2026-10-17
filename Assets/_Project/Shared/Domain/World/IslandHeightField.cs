using System.Collections.Generic;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>
    /// Heights of one island on a regular grid, centred on the origin, plus the water and caves
    /// carved into it. Deterministic for a given (settings, seed) pair, so the server and every
    /// client build the same terrain and only the seed travels over the network.
    /// </summary>
    /// <remarks>
    /// Built in stages, each working on the result of the previous one: relief and terraces,
    /// the flat safe hub, lake basins, rivers running from lakes and springs to the sea, and caves
    /// cut into cliff faces. The hub is protected from every later stage.
    /// </remarks>
    public sealed class IslandHeightField
    {
        private readonly float[] _heights;
        private readonly float[] _water;
        private readonly float[] _hubWeight;
        private readonly List<IslandLake> _lakes = new();
        private readonly List<IslandRiver> _rivers = new();
        private readonly List<IslandCave> _caves = new();

        public IslandSettings Settings { get; }
        public int Seed { get; }
        public int VertexCount { get; }
        public Vector3 HubCenter { get; private set; }
        public IReadOnlyList<IslandLake> Lakes => _lakes;
        public IReadOnlyList<IslandRiver> Rivers => _rivers;
        public IReadOnlyList<IslandCave> Caves => _caves;

        private IslandHeightField(IslandSettings settings, int seed)
        {
            Settings = settings;
            Seed = seed;
            VertexCount = settings.Resolution + 1;
            _heights = new float[VertexCount * VertexCount];
            _water = new float[VertexCount * VertexCount];
            _hubWeight = new float[VertexCount * VertexCount];
            for (int i = 0; i < _water.Length; i++) _water[i] = float.NegativeInfinity;
        }

        public float HeightAt(int x, int z) => _heights[z * VertexCount + x];

        public Vector3 VertexPosition(int x, int z)
        {
            float cell = Settings.CellSize;
            return new Vector3(-Settings.HalfSize + x * cell, HeightAt(x, z), -Settings.HalfSize + z * cell);
        }

        /// <summary>Height on the rendered triangle under a world XZ position; sea floor outside the grid.</summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            float gx = (worldX + Settings.HalfSize) / Settings.CellSize;
            float gz = (worldZ + Settings.HalfSize) / Settings.CellSize;
            int max = Settings.Resolution;
            if (gx < 0f || gz < 0f || gx > max || gz > max) return Settings.SeaFloorDepth;

            int x0 = Mathf.Min(Mathf.FloorToInt(gx), max - 1);
            int z0 = Mathf.Min(Mathf.FloorToInt(gz), max - 1);
            float tx = gx - x0;
            float tz = gz - z0;

            // Same diagonal as IslandMeshBuilder, so the sample lies on the rendered triangle.
            float h00 = HeightAt(x0, z0);
            float h11 = HeightAt(x0 + 1, z0 + 1);
            if (tx >= tz)
            {
                float h10 = HeightAt(x0 + 1, z0);
                return h00 + (h10 - h00) * tx + (h11 - h10) * tz;
            }
            float h01 = HeightAt(x0, z0 + 1);
            return h00 + (h11 - h01) * tx + (h01 - h00) * tz;
        }

        /// <summary>Up-facing surface normal from central differences.</summary>
        public Vector3 SampleNormal(float worldX, float worldZ)
        {
            float e = Settings.CellSize * 0.5f;
            float dx = SampleHeight(worldX + e, worldZ) - SampleHeight(worldX - e, worldZ);
            float dz = SampleHeight(worldX, worldZ + e) - SampleHeight(worldX, worldZ - e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        /// <summary>Surface of a lake or river at a grid vertex, or -infinity where there is none.</summary>
        public float FreshWaterAt(int x, int z) => _water[z * VertexCount + x];

        /// <summary>Surface of a lake or river at the nearest grid vertex, or -infinity where there is none.</summary>
        public float FreshWaterLevel(float worldX, float worldZ)
        {
            int x = Mathf.RoundToInt((worldX + Settings.HalfSize) / Settings.CellSize);
            int z = Mathf.RoundToInt((worldZ + Settings.HalfSize) / Settings.CellSize);
            if (x < 0 || z < 0 || x >= VertexCount || z >= VertexCount) return float.NegativeInfinity;
            return _water[z * VertexCount + x];
        }

        /// <summary>True where the ground is under the sea, a lake or a river.</summary>
        public bool IsUnderWater(float worldX, float worldZ, float margin = 0.1f)
        {
            float h = SampleHeight(worldX, worldZ);
            return h < Settings.SeaLevel + margin || h < FreshWaterLevel(worldX, worldZ) + margin;
        }

        public bool IsLand(float worldX, float worldZ) => !IsUnderWater(worldX, worldZ, 0.05f);

        /// <summary>True inside a cave footprint - props must not grow there.</summary>
        public bool IsInsideCave(float worldX, float worldZ, float margin = 0f)
        {
            foreach (IslandCave cave in _caves)
            {
                float dx = worldX - cave.Position.x;
                float dz = worldZ - cave.Position.z;
                float r = cave.Radius + margin;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        public static IslandHeightField Build(IslandSettings s, int seed)
        {
            var field = new IslandHeightField(s, seed);
            var noise = new IslandNoise(seed);
            var rng = new System.Random(seed ^ 0x5bd1e995);

            field.BuildRelief(noise, rng);
            // Each feature draws from its own stream, so tuning lakes doesn't move the caves.
            field.CarveLakes(new System.Random(seed * 31 + 1));
            field.CarveRivers(new System.Random(seed * 31 + 2), noise);
            field.CarveCaves(new System.Random(seed * 31 + 3));
            return field;
        }

        // ---------------------------------------------------------------- relief and hub

        private void BuildRelief(IslandNoise noise, System.Random rng)
        {
            IslandSettings s = Settings;
            // Shifts the sample window so neighbouring seeds don't share the noise origin.
            float ox = (float)rng.NextDouble() * 1000f;
            float oz = (float)rng.NextDouble() * 1000f;

            float hubAngle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float hubCoast = CoastRadius(s, noise, hubAngle) * s.HalfSize;
            Vector2 hub = new Vector2(Mathf.Cos(hubAngle), Mathf.Sin(hubAngle)) * (hubCoast * s.HubDistance);
            float hubHeight = Mathf.Max(s.HubMinHeight, Terrace(s, RawHeight(s, noise, hub.x, hub.y, ox, oz)));
            HubCenter = new Vector3(hub.x, hubHeight, hub.y);

            for (int z = 0; z < VertexCount; z++)
            for (int x = 0; x < VertexCount; x++)
            {
                Vector3 p = VertexPosition(x, z);
                float h = Terrace(s, RawHeight(s, noise, p.x, p.z, ox, oz));

                float hubDist = Vector2.Distance(new Vector2(p.x, p.z), hub);
                float w = SmoothStep01(1f - (hubDist - s.HubRadius) / Mathf.Max(s.HubBlend, 0.01f));
                int i = z * VertexCount + x;
                _hubWeight[i] = w;
                _heights[i] = Mathf.Lerp(h, hubHeight, w);
            }
        }

        private static float RawHeight(IslandSettings s, IslandNoise noise, float wx, float wz, float ox, float oz)
        {
            float r = new Vector2(wx, wz).magnitude / s.HalfSize;
            float coast = CoastRadius(s, noise, Mathf.Atan2(wz, wx));

            float land = SmoothStep01((coast - r) / s.ShoreWidth);

            float nx = wx / s.Size * s.HillFrequency + ox;
            float nz = wz / s.Size * s.HillFrequency + oz;
            float hills = noise.Fbm(nx, nz, s.HillOctaves);
            float centre = Mathf.Clamp01(1f - r / Mathf.Max(coast, 0.01f));

            float landHeight = s.BeachHeight + hills * hills * s.HillHeight + centre * centre * s.CentralRise;
            // Hills fade in from the beach so the coast stays walkable.
            landHeight = Mathf.Lerp(s.BeachHeight, landHeight, Mathf.Clamp01(centre * 4f));
            return Mathf.Lerp(s.SeaFloorDepth, landHeight, land);
        }

        private static float CoastRadius(IslandSettings s, IslandNoise noise, float angle)
        {
            // Sampling on a circle keeps the coastline seamless at +-PI.
            float cx = Mathf.Cos(angle) * s.CoastFrequency + 500f;
            float cz = Mathf.Sin(angle) * s.CoastFrequency + 500f;
            float wobble = noise.Fbm(cx, cz, 3) * 2f - 1f;
            return s.CoastRadius * (1f + wobble * s.CoastIrregularity);
        }

        private static float Terrace(IslandSettings s, float h)
        {
            if (h <= s.BeachHeight || s.TerraceStrength <= 0f || s.TerraceStep <= 0f) return h;

            float t = (h - s.BeachHeight) / s.TerraceStep;
            float step = Mathf.Floor(t);
            float edge = Mathf.Clamp01((t - step - (0.5f - s.TerraceSharpness)) / (2f * s.TerraceSharpness));
            float terraced = s.BeachHeight + (step + edge * edge * (3f - 2f * edge)) * s.TerraceStep;
            return Mathf.Lerp(h, terraced, s.TerraceStrength);
        }

        // ---------------------------------------------------------------- lakes

        private void CarveLakes(System.Random rng)
        {
            IslandSettings s = Settings;
            for (int attempt = 0; attempt < 80 && _lakes.Count < s.LakeCount; attempt++)
            {
                float radius = Mathf.Lerp(s.LakeRadiusMin, s.LakeRadiusMax, (float)rng.NextDouble());
                Vector2 c = RandomInIsland(rng, 0.6f);
                float h = SampleHeight(c.x, c.y);
                if (h < s.BeachHeight + 2.5f) continue;
                if (!FarFromHub(c, radius + s.BankWidth + 8f)) continue;

                bool overlaps = false;
                foreach (IslandLake other in _lakes)
                    if (Vector2.Distance(c, new Vector2(other.Center.x, other.Center.z)) < (radius + other.Radius) * 1.8f + 10f)
                        overlaps = true;
                if (overlaps) continue;

                // Water must sit below every point of the rim, or it would float over a low side.
                float level = h - 0.5f;
                for (int i = 0; i < 20; i++)
                {
                    float a = i * Mathf.PI * 2f / 20f;
                    float rim = SampleHeight(c.x + Mathf.Cos(a) * radius * 1.3f, c.y + Mathf.Sin(a) * radius * 1.3f);
                    level = Mathf.Min(level, rim - 0.4f);
                }
                if (level < s.BeachHeight + 1f) continue;

                var lake = new IslandLake(new Vector3(c.x, level, c.y), radius);
                CarveBasin(lake.Center, radius, level, s.LakeDepth);
                _lakes.Add(lake);
            }
        }

        private void CarveBasin(Vector3 center, float radius, float level, float depth)
        {
            float reach = radius + Settings.BankWidth;
            ForVerticesNear(center.x, center.z, reach, (i, d) =>
            {
                float target = d < radius
                    ? level - depth * (1f - (d / radius) * (d / radius))
                    : Mathf.Lerp(level + 0.3f, _heights[i], SmoothStep01((d - radius) / Settings.BankWidth));
                Lower(i, target);
                if (d < radius * 1.15f) _water[i] = Mathf.Max(_water[i], level);
            });
        }

        // ---------------------------------------------------------------- rivers

        private void CarveRivers(System.Random rng, IslandNoise noise)
        {
            IslandSettings s = Settings;
            foreach (IslandLake lake in _lakes)
            {
                Vector2 c = new Vector2(lake.Center.x, lake.Center.z);
                Vector2 outward = c.sqrMagnitude > 1f ? c.normalized : RandomDirection(rng);
                TraceRiver(c + outward * lake.Radius * 0.8f, outward, lake.Center.y, rng, noise);
            }

            for (int i = 0, attempts = 0; i < s.SpringCount && attempts < 40; attempts++)
            {
                Vector2 spring = RandomInIsland(rng, 0.45f);
                float h = SampleHeight(spring.x, spring.y);
                if (h < s.BeachHeight + 5f || !FarFromHub(spring, 20f)) continue;
                Vector2 outward = spring.sqrMagnitude > 1f ? spring.normalized : RandomDirection(rng);
                TraceRiver(spring, outward, h - 0.6f, rng, noise);
                i++;
            }
        }

        private void TraceRiver(Vector2 start, Vector2 outward, float level, System.Random rng, IslandNoise noise)
        {
            IslandSettings s = Settings;
            const float step = 1.5f;
            var points = new List<Vector3>();
            Vector2 p = start;
            Vector2 dir = outward;
            float phase = (float)rng.NextDouble() * 100f;

            for (int n = 0; n < 600; n++)
            {
                float ground = SampleHeight(p.x, p.y);
                // Inside a lake the surface is already set; its carved bed must not drag the river down.
                if (FreshWaterLevel(p.x, p.y) < level - 0.01f) level = Mathf.Min(level, ground - 0.5f);
                points.Add(new Vector3(p.x, Mathf.Max(level, s.SeaLevel - 0.3f), p.y));
                if (ground < s.SeaLevel - 1f) break;

                // Downhill pull plus a slow wobble; the outward term guarantees it reaches the coast.
                Vector3 normal = SampleNormal(p.x, p.y);
                Vector2 downhill = new Vector2(normal.x, normal.z);
                float wobble = (noise.Sample(phase + n * 0.035f, 7.3f)) * s.RiverMeander;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 wanted = outward * 0.8f + downhill * 2f + side * wobble;
                dir = Vector2.Lerp(dir, wanted.normalized, 0.25f).normalized;

                // Never head into the hub.
                Vector2 toHub = new Vector2(HubCenter.x, HubCenter.z) - p;
                float hubGuard = Settings.HubRadius + Settings.HubBlend + s.RiverWidth * 2f;
                if (toHub.magnitude < hubGuard + 10f && Vector2.Dot(toHub.normalized, dir) > 0f)
                    dir = (dir - toHub.normalized * Vector2.Dot(toHub.normalized, dir) * 1.5f).normalized;

                p += dir * step;
            }

            if (points.Count < 8) return;

            float half = s.RiverWidth * 0.5f;
            foreach (Vector3 point in points)
            {
                float surface = point.y;
                ForVerticesNear(point.x, point.z, half + s.BankWidth, (i, d) =>
                {
                    float target = d < half
                        ? surface - s.RiverDepth * (1f - (d / half) * (d / half))
                        : Mathf.Lerp(surface + 0.35f, _heights[i], SmoothStep01((d - half) / s.BankWidth));
                    Lower(i, target);
                    if (d < half * 1.2f) _water[i] = Mathf.Max(_water[i], surface);
                });
            }

            _rivers.Add(new IslandRiver(points, s.RiverWidth));
        }

        // ---------------------------------------------------------------- caves

        private void CarveCaves(System.Random rng)
        {
            IslandSettings s = Settings;
            for (int attempt = 0; attempt < 600 && _caves.Count < s.CaveCount; attempt++)
            {
                Vector2 site = RandomInIsland(rng, 0.85f);
                float h = SampleHeight(site.x, site.y);
                if (h < s.BeachHeight + 1f || IsUnderWater(site.x, site.y, 1.5f)) continue;
                if (SampleNormal(site.x, site.y).y < 0.93f) continue;
                if (!FarFromHub(site, s.CaveRadius + 12f)) continue;

                // The mouth looks away from the steepest rise within reach - i.e. out of the cliff.
                Vector2 uphill = Vector2.zero;
                float bestRise = 0f;
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float rise = SampleHeight(site.x + d.x * s.CaveRadius * 1.6f, site.y + d.y * s.CaveRadius * 1.6f) - h;
                    if (rise > bestRise)
                    {
                        bestRise = rise;
                        uphill = d;
                    }
                }
                if (bestRise < s.CaveMinCliff) continue;

                Vector2 centre = site + uphill * s.CaveRadius * 0.6f;
                bool crowded = false;
                foreach (IslandCave other in _caves)
                    if (Vector2.Distance(centre, new Vector2(other.Position.x, other.Position.z)) < 45f) crowded = true;
                foreach (IslandLake lake in _lakes)
                    if (Vector2.Distance(centre, new Vector2(lake.Center.x, lake.Center.z)) < lake.Radius + s.CaveRadius + 8f) crowded = true;
                if (crowded || !IsDry(centre, s.CaveRadius + 4f)) continue;

                // Cut the cliff down to the floor inside the footprint; the cave shell roofs it over.
                ForVerticesNear(centre.x, centre.y, s.CaveRadius * 1.05f, (i, d) => Lower(i, h));
                _caves.Add(new IslandCave(new Vector3(centre.x, h, centre.y), new Vector3(-uphill.x, 0f, -uphill.y), s.CaveRadius));
            }
        }

        // ---------------------------------------------------------------- helpers

        private void ForVerticesNear(float cx, float cz, float radius, System.Action<int, float> visit)
        {
            float cell = Settings.CellSize;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((cx - radius + Settings.HalfSize) / cell));
            int x1 = Mathf.Min(VertexCount - 1, Mathf.CeilToInt((cx + radius + Settings.HalfSize) / cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((cz - radius + Settings.HalfSize) / cell));
            int z1 = Mathf.Min(VertexCount - 1, Mathf.CeilToInt((cz + radius + Settings.HalfSize) / cell));

            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = -Settings.HalfSize + x * cell - cx;
                float dz = -Settings.HalfSize + z * cell - cz;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d <= radius) visit(z * VertexCount + x, d);
            }
        }

        /// <summary>Carving only ever lowers terrain, and never inside the safe hub.</summary>
        private void Lower(int i, float target)
        {
            if (target >= _heights[i]) return;
            _heights[i] = Mathf.Lerp(target, _heights[i], _hubWeight[i]);
        }

        private bool FarFromHub(Vector2 p, float margin)
            => Vector2.Distance(p, new Vector2(HubCenter.x, HubCenter.z)) > Settings.HubRadius + Settings.HubBlend + margin;

        private bool IsDry(Vector2 c, float radius)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                if (IsUnderWater(c.x + Mathf.Cos(a) * radius, c.y + Mathf.Sin(a) * radius, 0.5f)) return false;
            }
            return !IsUnderWater(c.x, c.y, 0.5f);
        }

        private Vector2 RandomInIsland(System.Random rng, float fractionOfCoast)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float r = Mathf.Sqrt((float)rng.NextDouble()) * Settings.CoastRadius * Settings.HalfSize * fractionOfCoast;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        private static Vector2 RandomDirection(System.Random rng)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
