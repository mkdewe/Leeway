using System;
using System.Collections.Generic;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>Where one kind of prop may grow. Mirrors a layer of IslandBiome without its prefabs.</summary>
    [Serializable]
    public struct ScatterRule
    {
        [field: Tooltip("Instances per 100 m² where every other condition is met.")]
        [field: SerializeField] public float Density { get; private set; }
        [field: SerializeField] public float MinHeight { get; private set; }
        [field: SerializeField] public float MaxHeight { get; private set; }
        [field: Tooltip("Steepest allowed slope, in degrees.")]
        [field: Range(0f, 90f)]
        [field: SerializeField] public float MaxSlope { get; private set; }
        [field: Tooltip("Keeps the safe hub clear. Distance from the hub edge, in metres.")]
        [field: SerializeField] public float HubClearance { get; private set; }
        [field: Tooltip("Groups instances into patches: 0 = even cover, higher = fewer, denser patches.")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float Clustering { get; private set; }
        [field: SerializeField] public float ClusterScale { get; private set; }
        [field: SerializeField] public float MinScale { get; private set; }
        [field: SerializeField] public float MaxScale { get; private set; }

        public ScatterRule(float density, float minHeight, float maxHeight, float maxSlope, float hubClearance,
            float clustering, float clusterScale, float minScale, float maxScale)
        {
            Density = density;
            MinHeight = minHeight;
            MaxHeight = maxHeight;
            MaxSlope = maxSlope;
            HubClearance = hubClearance;
            Clustering = clustering;
            ClusterScale = clusterScale;
            MinScale = minScale;
            MaxScale = maxScale;
        }
    }

    public readonly struct ScatterPoint
    {
        public readonly int Rule;
        public readonly int Variant;
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly float Yaw;
        public readonly float Scale;

        public ScatterPoint(int rule, int variant, Vector3 position, Vector3 normal, float yaw, float scale)
        {
            Rule = rule;
            Variant = variant;
            Position = position;
            Normal = normal;
            Yaw = yaw;
            Scale = scale;
        }
    }

    /// <summary>
    /// Deterministic prop placement on an <see cref="IslandHeightField"/>: a jittered grid per
    /// rule (even spacing without a Poisson pass), filtered by height, slope, hub clearance and
    /// a patch-noise mask.
    /// </summary>
    public static class IslandScatter
    {
        public static List<ScatterPoint> Plan(IslandHeightField field, IReadOnlyList<ScatterRule> rules, IReadOnlyList<int> variantCounts)
        {
            var points = new List<ScatterPoint>();
            IslandSettings s = field.Settings;

            for (int r = 0; r < rules.Count; r++)
            {
                ScatterRule rule = rules[r];
                int variants = variantCounts[r];
                if (rule.Density <= 0f || variants <= 0) continue;

                // Separate stream per rule: adding a layer must not reshuffle the others.
                var rng = new System.Random(unchecked(field.Seed * 486187739 + r * 16777619));
                var patches = new IslandNoise(unchecked(field.Seed + 7919 * (r + 1)));
                float spacing = Mathf.Sqrt(100f / rule.Density);
                int cells = Mathf.CeilToInt(s.Size / spacing);
                float minNormalY = Mathf.Cos(rule.MaxSlope * Mathf.Deg2Rad);
                float hubKeepOut = s.HubRadius + rule.HubClearance;
                float clusterScale = Mathf.Max(rule.ClusterScale, 1f);

                for (int cz = 0; cz < cells; cz++)
                for (int cx = 0; cx < cells; cx++)
                {
                    // Every random draw happens before any rejection, so the stream stays aligned.
                    float jx = (float)rng.NextDouble();
                    float jz = (float)rng.NextDouble();
                    float yaw = (float)rng.NextDouble() * 360f;
                    float scaleT = (float)rng.NextDouble();
                    int variant = rng.Next(variants);

                    float x = -s.HalfSize + (cx + jx) * spacing;
                    float z = -s.HalfSize + (cz + jz) * spacing;

                    if (rule.Clustering > 0f)
                    {
                        float patch = patches.Fbm(x / clusterScale, z / clusterScale, 2);
                        if (patch < rule.Clustering) continue;
                    }

                    float h = field.SampleHeight(x, z);
                    if (h < rule.MinHeight || h > rule.MaxHeight) continue;
                    if (h < field.FreshWaterLevel(x, z) + 0.15f) continue;
                    if (field.IsInsideCave(x, z, 1.5f)) continue;

                    Vector3 normal = field.SampleNormal(x, z);
                    if (normal.y < minNormalY) continue;

                    float hubDx = x - field.HubCenter.x;
                    float hubDz = z - field.HubCenter.z;
                    if (hubDx * hubDx + hubDz * hubDz < hubKeepOut * hubKeepOut) continue;

                    float scale = Mathf.Lerp(rule.MinScale, rule.MaxScale, scaleT);
                    points.Add(new ScatterPoint(r, variant, new Vector3(x, h, z), normal, yaw, scale));
                }
            }

            return points;
        }
    }
}
