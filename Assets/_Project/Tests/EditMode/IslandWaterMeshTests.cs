using System.Collections.Generic;
using Leeway.World;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class IslandWaterMeshTests
    {
        [TestCase(1)][TestCase(7)][TestCase(12345)]
        public void Shoreline_IsClippedToTerrain_WithoutDegenerateFaces(int seed)
        {
            var field=IslandHeightField.Build(new IslandSettings(),seed);
            Mesh mesh=IslandMeshBuilder.BuildWaterSurface(field);
            try
            {
                var v=mesh.vertices;var t=mesh.triangles;var c=mesh.colors;int shore=0;
                for(int i=0;i<v.Length;i++)
                {
                    Assert.IsFalse(float.IsNaN(v[i].y)||float.IsInfinity(v[i].y));
                    if(c[i].r<0.00001f)
                    {Assert.AreEqual(field.SampleHeight(v[i].x,v[i].z)+0.008f,v[i].y,0.015f);shore++;}
                }
                Assert.Greater(shore,20);
                for(int i=0;i<t.Length;i+=3)
                {
                    Vector3 a=v[t[i]],b=v[t[i+1]],d=v[t[i+2]],centre=(a+b+d)/3;
                    Assert.Greater(Vector3.Cross(b-a,d-a).sqrMagnitude,1e-11f);
                    Assert.GreaterOrEqual(centre.y+0.003f,field.SampleHeight(centre.x,centre.z));
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void SeaRing_HasWaveGeometry_AndMatchesGridBoundary()
        {
            Mesh mesh=IslandMeshBuilder.BuildSeaRing(6000,520,0.008f,2.5f);
            try
            {
                Assert.Greater(mesh.vertexCount,10000);
                var v=mesh.vertices;var t=mesh.triangles;int perimeter=208*4;
                for(int i=0;i<perimeter;i++)
                {
                    Assert.AreEqual(260f,Mathf.Max(Mathf.Abs(v[i].x),Mathf.Abs(v[i].z)),0.001f);
                    Assert.AreEqual(0.008f,v[i].y,0.001f);
                    Assert.LessOrEqual(Vector3.Distance(v[i],v[(i+1)%perimeter]),2.501f);
                }
                for(int i=0;i<t.Length;i+=3)
                    Assert.Greater(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).y,0);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        // Regression for waterfall overlays floating up to 7.57 m above the river:
        // every rendered vertex, regardless of flow attributes, must use the physical datum.
        [TestCase(1)][TestCase(7)][TestCase(12345)]
        public void StillAndFlowingWater_UseExactlyTheSameSurfaceDatum(int seed)
        {
            var field=IslandHeightField.Build(new IslandSettings(),seed);
            Mesh mesh=IslandMeshBuilder.BuildWaterSurface(field);
            try
            {
                var vertices=mesh.vertices;var flows=mesh.uv2;
                Assert.AreEqual(vertices.Length,flows.Length);
                int moving=0,still=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 p=vertices[i];
                    float expected=field.SampleWaterSurface(p.x,p.z)+0.008f;
                    Assert.AreEqual(expected,p.y,0.002f,$"Competing water height at vertex {i}");
                    if(flows[i].x>0.1f)moving++;else still++;
                }
                Assert.Greater(moving,20);Assert.Greater(still,20);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(1)][TestCase(7)][TestCase(12345)]
        public void SharedWaterEdges_NeverHaveStackedHeights(int seed)
        {
            var field=IslandHeightField.Build(new IslandSettings(),seed);
            Mesh mesh=IslandMeshBuilder.BuildWaterSurface(field);
            try
            {
                var heights=new Dictionary<Vector2Int,float>();int shared=0;
                foreach(Vector3 p in mesh.vertices)
                {
                    var key=new Vector2Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.z*100000));
                    if(heights.TryGetValue(key,out float h))
                    {Assert.AreEqual(h,p.y,0.002f,"Water must not have two levels at the same XZ");shared++;}
                    else heights.Add(key,p.y);
                }
                Assert.Greater(shared,100);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(1)][TestCase(7)][TestCase(12345)]
        public void Lakes_RemainHorizontal_WhenFlowFootprintsCrossThem(int seed)
        {
            var field=IslandHeightField.Build(new IslandSettings(),seed);int wet=0;
            foreach(var lake in field.Lakes)
            for(int z=0;z<field.VertexCount;z++)for(int x=0;x<field.VertexCount;x++)
            {
                Vector3 p=field.VertexPosition(x,z);
                if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(lake.Center.x,lake.Center.z))>lake.Radius*0.85f)continue;
                if(p.y>=lake.Center.y-0.01f)continue;
                Assert.AreEqual(lake.Center.y,field.WaterSurfaceAt(x,z),0.001f,
                    "Flow may not lower one strip of a standing pool");wet++;
            }
            Assert.Greater(wet,0);
        }

        [TestCase(1)][TestCase(7)][TestCase(12345)]
        public void RiverMouth_DoesNotCarryElevatedWaterOverSubmergedSeaBed(int seed)
        {
            var field=IslandHeightField.Build(new IslandSettings(),seed);int checkedVertices=0;
            foreach(var river in field.Rivers)
            {
                Vector3 mouth=river.Points[river.Points.Count-1];
                for(int z=0;z<field.VertexCount;z++)for(int x=0;x<field.VertexCount;x++)
                {
                    Vector3 p=field.VertexPosition(x,z);
                    if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(mouth.x,mouth.z))>river.Width*2)continue;
                    if(p.y>=field.Settings.SeaLevel-0.8f)continue;
                    Assert.LessOrEqual(field.FreshWaterAt(x,z),field.Settings.SeaLevel);checkedVertices++;
                }
            }
            Assert.Greater(checkedVertices,0);
        }

        [TestCase(1)][TestCase(7)]
        public void SeaReceivingReaches_HaveLocalImpactFoam(int seed)
        {
            var field=IslandHeightField.Build(new IslandSettings(),seed);
            var impacts=IslandMeshBuilder.BuildWaterImpacts(field);
            foreach(var river in field.Rivers)
            {
                bool covered=false;
                foreach(var hit in impacts)
                {
                    if(!hit.Ocean)continue;
                    foreach(var point in river.Points)
                        if(field.SampleWaterSurface(point.x,point.z)<=field.Settings.SeaLevel+0.2f&&
                           Vector2.Distance(new Vector2(hit.Position.x,hit.Position.z),
                               new Vector2(point.x,point.z))<river.Width*2)covered=true;
                }
                Assert.IsTrue(covered);
            }
            foreach(var hit in impacts)
            {
                Assert.Greater(hit.Radius,0);Assert.AreEqual(1,hit.Direction.magnitude,0.001f);
                if(hit.Ocean)Assert.AreEqual(field.Settings.SeaLevel+0.008f,hit.Position.y,0.001f);
            }
        }
    }
}
