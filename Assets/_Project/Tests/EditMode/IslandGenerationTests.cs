using System.Collections.Generic;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class IslandGenerationTests
    {
        private static readonly IslandSettings Settings = new();

        [Test]
        public void Build_SameSeed_GivesIdenticalHeights()
        {
            var a = IslandHeightField.Build(Settings, 42);
            var b = IslandHeightField.Build(Settings, 42);

            for (int z = 0; z < a.VertexCount; z++)
            for (int x = 0; x < a.VertexCount; x++)
                Assert.AreEqual(a.HeightAt(x, z), b.HeightAt(x, z), $"vertex {x},{z}");
            Assert.AreEqual(a.HubCenter, b.HubCenter);
        }

        [Test]
        public void Build_DifferentSeeds_GiveDifferentIslands()
        {
            var a = IslandHeightField.Build(Settings, 1);
            var b = IslandHeightField.Build(Settings, 2);

            int differing = 0;
            for (int z = 0; z < a.VertexCount; z++)
            for (int x = 0; x < a.VertexCount; x++)
                if (Mathf.Abs(a.HeightAt(x, z) - b.HeightAt(x, z)) > 0.5f) differing++;

            Assert.Greater(differing, a.VertexCount * a.VertexCount / 10);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(12345)]
        public void Build_GridBorder_IsUnderTheSea(int seed)
        {
            var field = IslandHeightField.Build(Settings, seed);
            int last = field.VertexCount - 1;

            for (int i = 0; i <= last; i++)
            {
                Assert.Less(field.HeightAt(i, 0), Settings.SeaLevel);
                Assert.Less(field.HeightAt(i, last), Settings.SeaLevel);
                Assert.Less(field.HeightAt(0, i), Settings.SeaLevel);
                Assert.Less(field.HeightAt(last, i), Settings.SeaLevel);
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(12345)]
        public void Build_SafeHub_IsFlatLandAboveTheSea(int seed)
        {
            var field = IslandHeightField.Build(Settings, seed);
            Vector3 hub = field.HubCenter;

            Assert.GreaterOrEqual(hub.y, Settings.HubMinHeight);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                float r = Settings.HubRadius * 0.9f;
                float h = field.SampleHeight(hub.x + Mathf.Cos(angle) * r, hub.z + Mathf.Sin(angle) * r);
                Assert.AreEqual(hub.y, h, 0.05f, $"hub sample {i}");
            }
        }

        [Test]
        public void SampleHeight_AtGridVertex_MatchesVertexHeight()
        {
            var field = IslandHeightField.Build(Settings, 3);
            for (int z = 1; z < field.VertexCount - 1; z += 7)
            for (int x = 1; x < field.VertexCount - 1; x += 7)
            {
                Vector3 v = field.VertexPosition(x, z);
                Assert.AreEqual(v.y, field.SampleHeight(v.x, v.z), 1e-3f);
            }
        }

        [Test]
        public void Scatter_IsDeterministic_AndRespectsRules()
        {
            var field = IslandHeightField.Build(Settings, 9);
            var rules = new List<ScatterRule>
            {
                new(density: 2f, minHeight: 1f, maxHeight: 30f, maxSlope: 30f, hubClearance: 5f,
                    clustering: 0.4f, clusterScale: 20f, minScale: 0.8f, maxScale: 1.2f),
            };
            var variants = new List<int> { 3 };

            List<ScatterPoint> a = IslandScatter.Plan(field, rules, variants);
            List<ScatterPoint> b = IslandScatter.Plan(field, rules, variants);

            Assert.Greater(a.Count, 0);
            Assert.AreEqual(a.Count, b.Count);
            float keepOut = Settings.HubRadius + 5f;
            float minNormalY = Mathf.Cos(30f * Mathf.Deg2Rad);
            for (int i = 0; i < a.Count; i++)
            {
                ScatterPoint p = a[i];
                Assert.AreEqual(p.Position, b[i].Position);
                Assert.That(p.Position.y, Is.InRange(1f, 30f));
                Assert.That(p.Variant, Is.InRange(0, 2));
                Assert.That(p.Scale, Is.InRange(0.8f, 1.2f));
                Assert.GreaterOrEqual(p.Normal.y, minNormalY - 1e-4f);
                Vector2 toHub = new Vector2(p.Position.x - field.HubCenter.x, p.Position.z - field.HubCenter.z);
                Assert.GreaterOrEqual(toHub.magnitude, keepOut);
            }
        }

        [Test]
        public void Scatter_AddingALayer_DoesNotMoveExistingOnes()
        {
            var field = IslandHeightField.Build(Settings, 5);
            var first = new ScatterRule(1f, 1f, 30f, 40f, 0f, 0f, 1f, 1f, 1f);
            var second = new ScatterRule(3f, 0f, 30f, 60f, 0f, 0f, 1f, 1f, 1f);

            List<ScatterPoint> alone = IslandScatter.Plan(field, new[] { first }, new[] { 1 });
            List<ScatterPoint> withSecond = IslandScatter.Plan(field, new[] { first, second }, new[] { 1, 1 });

            List<ScatterPoint> firstAgain = withSecond.FindAll(p => p.Rule == 0);
            Assert.AreEqual(alone.Count, firstAgain.Count);
            for (int i = 0; i < alone.Count; i++)
                Assert.AreEqual(alone[i].Position, firstAgain[i].Position);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(12345)]
        public void Rivers_NeverFlowUphill_AndEndInTheSea(int seed)
        {
            var field = IslandHeightField.Build(Settings, seed);
            Assert.Greater(field.Rivers.Count, 0);

            foreach (IslandRiver river in field.Rivers)
            {
                for (int i = 1; i < river.Points.Count; i++)
                    Assert.LessOrEqual(river.Points[i].y, river.Points[i - 1].y + 1e-4f, $"river point {i}");

                Vector3 mouth = river.Points[river.Points.Count - 1];
                Assert.Less(field.SampleHeight(mouth.x, mouth.z), Settings.SeaLevel, "river mouth must reach the sea");
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(12345)]
        public void LakesAndCaves_StayOutOfTheHub_AndLakesHoldWater(int seed)
        {
            var field = IslandHeightField.Build(Settings, seed);
            Vector2 hub = new Vector2(field.HubCenter.x, field.HubCenter.z);
            float hubReach = Settings.HubRadius + Settings.HubBlend;

            foreach (IslandLake lake in field.Lakes)
            {
                Assert.Greater(Vector2.Distance(hub, new Vector2(lake.Center.x, lake.Center.z)), hubReach + lake.Radius);
                Assert.Less(field.SampleHeight(lake.Center.x, lake.Center.z), lake.Center.y, "lake bed is below its surface");
            }

            foreach (IslandCave cave in field.Caves)
            {
                Assert.Greater(Vector2.Distance(hub, new Vector2(cave.Position.x, cave.Position.z)), hubReach + cave.Radius);
                Assert.AreEqual(cave.Position.y, field.SampleHeight(cave.Position.x, cave.Position.z), 0.05f, "cave floor is carved flat");
            }
        }
    }
}
