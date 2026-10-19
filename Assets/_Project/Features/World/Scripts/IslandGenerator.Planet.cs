using System.Collections.Generic;
using Leeway.World.Domain;
using UnityEngine;

namespace Leeway.World
{
    public sealed partial class IslandGenerator
    {
        public int PlanetIslandIndex { get; private set; }
        public static event System.Action<IslandGenerator,int> PlanetIslandReady;
        public static void NotifyPlanetIslandReady(IslandGenerator generator,int index)
        {
            generator.PlanetIslandIndex=index;
            PlanetIslandReady?.Invoke(generator,index);
        }

        private PlanetIslandPatch _projection;
        private Vector3 _planetCenter;
        private bool _buildPlanetGround;
        private IslandHeightField _providedField;

        public PlanetIslandPatch Projection => _projection;
        public Vector3 LocalUp(Vector3 world) => _projection == null ? transform.up : (world-_planetCenter).normalized;
        public Quaternion SurfaceRotation(Vector3 chartPoint,float yaw=0) => _projection == null
            ? transform.rotation*Quaternion.Euler(0,yaw,0)
            : _projection.RotationAt(chartPoint)*Quaternion.Euler(0,yaw,0);

        /// <summary>Reuses the model's field, retaining water meshes, cave prefabs and scenery.
        /// PlanetTerrain supplies the shared ground; this generator supplies the island's details.</summary>
        public void GenerateOnPlanet(PlanetIslandPatch projection,Vector3 center,IslandBiome biome=null,bool buildGround=false)
        {
            var work=GenerateOnPlanetAsync(projection,center,biome);
            while(work.MoveNext())
            {
                if(work.Current is System.Collections.IEnumerator nested)
                    while(nested.MoveNext()) { }
            }
        }

        public double PeakPlanetWorkMilliseconds { get; private set; }

        private System.Collections.IEnumerator Budgeted(System.Collections.IEnumerator work)
        {
            var frameBudget=System.Diagnostics.Stopwatch.StartNew();
            while(work != null)
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                bool next=work.MoveNext();
                PeakPlanetWorkMilliseconds=System.Math.Max(PeakPlanetWorkMilliseconds,watch.Elapsed.TotalMilliseconds);
                if(!next)yield break;
                if(work.Current!=null||frameBudget.Elapsed.TotalMilliseconds>=2)
                {
                    yield return work.Current;
                    frameBudget.Restart();
                }
            }
        }

        public System.Collections.IEnumerator GenerateOnPlanetAsync(PlanetIslandPatch projection,Vector3 center,IslandBiome biome=null,bool reveal=true)
        {
            bool reuse=_projection==null&&ReferenceEquals(Field,projection.Field)&&_root!=null;
            if(biome!=null)_biome=biome;
            _projection=projection;_planetCenter=center;_buildPlanetGround=false;
            if(_growActive)StopGrow();
            _growActive=false;
            if(!reuse)
            {
                Clear();Field=projection.Field;
                _root=new GameObject(RootName).transform;
                _root.SetParent(transform,false);
                _root.gameObject.SetActive(false);
                var timer=System.Diagnostics.Stopwatch.StartNew();
                BuildWater();
                var batches=new PropBatches();
                PlaceCaves(batches);PlaceHubStones(batches);
                PeakPlanetWorkMilliseconds=System.Math.Max(PeakPlanetWorkMilliseconds,timer.Elapsed.TotalMilliseconds);
                yield return Budgeted(ScatterPropsAsync(batches));
                yield return Budgeted(BuildBatchesAsync(batches));
            }
            _root.gameObject.SetActive(false);
            var terrain=_root.Find("Terrain");if(terrain!=null)terrain.gameObject.SetActive(false);
            var openSea=_root.Find("OpenSea");if(openSea!=null)openSea.gameObject.SetActive(false);
            Transform[] nodes=_root.GetComponentsInChildren<Transform>(true);
            var oldToChart=new Dictionary<Transform,Matrix4x4>(nodes.Length);
            var rotations=new Dictionary<Transform,Quaternion>(nodes.Length);
            Matrix4x4 inverse=_root.worldToLocalMatrix;
            Quaternion inverseRotation=Quaternion.Inverse(_root.rotation);
            foreach(var node in nodes){oldToChart[node]=inverse*node.localToWorldMatrix;rotations[node]=inverseRotation*node.rotation;}
            foreach(var node in nodes)
            {
                Vector3 chart=oldToChart[node].MultiplyPoint3x4(Vector3.zero);
                node.SetPositionAndRotation(projection.ToWorld(chart,center),projection.RotationAt(chart)*rotations[node]);
            }
            var frameBudget=System.Diagnostics.Stopwatch.StartNew();
            foreach(var filter in _root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(ExcludedPlanetGeometry(filter.transform))continue;
                var watch=System.Diagnostics.Stopwatch.StartNew();
                filter.sharedMesh=WarpMesh(filter.sharedMesh,filter.transform,oldToChart[filter.transform]);
                PeakPlanetWorkMilliseconds=System.Math.Max(PeakPlanetWorkMilliseconds,watch.Elapsed.TotalMilliseconds);
                if(frameBudget.Elapsed.TotalMilliseconds>=2){yield return null;frameBudget.Restart();}
            }
            foreach(var collider in _root.GetComponentsInChildren<MeshCollider>(true))
            {
                if(ExcludedPlanetGeometry(collider.transform))continue;
                var watch=System.Diagnostics.Stopwatch.StartNew();
                collider.sharedMesh=WarpMesh(collider.sharedMesh,collider.transform,oldToChart[collider.transform]);
                PeakPlanetWorkMilliseconds=System.Math.Max(PeakPlanetWorkMilliseconds,watch.Elapsed.TotalMilliseconds);
                if(frameBudget.Elapsed.TotalMilliseconds>=2){yield return null;frameBudget.Restart();}
            }
            foreach(var lod in _root.GetComponentsInChildren<LODGroup>(true))lod.RecalculateBounds();
            UpdatePlanetWaterImpacts();
            _root.gameObject.SetActive(reveal);
            Generated?.Invoke(Field);
        }

        public void SetPlanetPresentation(bool visible)
        {
            if(_root==null)return;
            _root.gameObject.SetActive(visible);
            if(visible&&_growIn&&Application.isPlaying){_growActive=true;StartGrow();}
        }

        private bool ExcludedPlanetGeometry(Transform node)
        {
            for(var t=node;t!=null&&t!=_root;t=t.parent)
                if(t.name=="Terrain"||t.name=="OpenSea")return true;
            return false;
        }

        private void UpdatePlanetWaterImpacts()
        {
            if(_waterProperties==null)return;
            var impacts=IslandMeshBuilder.BuildWaterImpacts(Field);
            var positions=new Vector4[IslandMeshBuilder.MaxWaterImpacts];
            var directions=new Vector4[IslandMeshBuilder.MaxWaterImpacts];
            for(int i=0;i<impacts.Count;i++)
            {
                Vector3 p=FieldToWorld(impacts[i].Position);
                Vector3 d=SurfaceRotation(impacts[i].Position)*impacts[i].Direction;
                positions[i]=new Vector4(p.x,p.y,p.z,impacts[i].Radius);
                directions[i]=new Vector4(d.x,d.y,d.z,impacts[i].Ocean?1:0);
            }
            _waterProperties.SetFloat("_LeewayWaterImpactCount",impacts.Count);
            _waterProperties.SetVectorArray("_LeewayWaterImpactPositions",positions);
            _waterProperties.SetVectorArray("_LeewayWaterImpactDirections",directions);
            foreach(var renderer in _root.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.gameObject.layer==WaterLayer)renderer.SetPropertyBlock(_waterProperties);
        }

        public Vector3 GroundPoint(Vector3 world)
        {
            if(Field==null)return world;
            Vector3 local=WorldToField(world);
            local.y=Field.SampleHeight(local.x,local.z);
            return FieldToWorld(local);
        }

        private Mesh WarpMesh(Mesh source,Transform owner,Matrix4x4 oldToChart)
        {
            if(source==null)return null;
            if(!source.isReadable)throw new System.InvalidOperationException("Planet projection needs a readable mesh: "+source.name);
            bool landscape=false,cave=false;
            for(var ancestor=owner;ancestor!=null&&ancestor!=_root;ancestor=ancestor.parent)
            {
                if(ancestor.name=="Caves")cave=true;
                if(ancestor.name=="Terrain"||ancestor.name=="Water")landscape=true;
            }
            bool merged=owner.name.StartsWith("Props_");
            if(!landscape&&!merged&&!cave)return source; // Authored prop size stays in metres.
            var pivots=new List<Vector4>();
            source.GetUVs(2,pivots);
            bool usePivots=merged&&!landscape&&pivots.Count==source.vertexCount;
            var mesh=Own(Instantiate(source));
            mesh.name=source.name+" (planet)";
            Vector3[] vertices=source.vertices;
            Vector3[] normals=source.normals;
            Matrix4x4 toLocal=owner.worldToLocalMatrix;
            Matrix4x4 normalToChart=oldToChart.inverse.transpose;
            Matrix4x4 worldNormalToLocal=owner.localToWorldMatrix.transpose;
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 chart=oldToChart.MultiplyPoint3x4(vertices[i]);
                Vector3 world;
                Vector3 pivotChart=chart;
                if(usePivots)
                {
                    var pivot=pivots[i];
                    pivotChart=oldToChart.MultiplyPoint3x4(new Vector3(pivot.x,pivot.y,pivot.z));
                    Vector3 delta=chart-pivotChart;
                    delta.x/=_projection.HorizontalScale;delta.z/=_projection.HorizontalScale;
                    world=_projection.ToWorld(pivotChart+delta,_planetCenter);
                }
                else if(cave)
                {
                    pivotChart=oldToChart.MultiplyPoint3x4(Vector3.zero);
                    Vector3 delta=chart-pivotChart;
                    delta.x/=_projection.HorizontalScale;delta.z/=_projection.HorizontalScale;
                    world=_projection.ToWorld(pivotChart+delta,_planetCenter);
                }
                else world=_projection.ToWorld(chart,_planetCenter);
                vertices[i]=toLocal.MultiplyPoint3x4(world);
                if(normals.Length==vertices.Length)
                {
                    Vector3 chartNormal=normalToChart.MultiplyVector(normals[i]).normalized;
                    Vector3 worldNormal=usePivots?_projection.RotationAt(pivotChart)*chartNormal:_projection.NormalToWorld(chart,chartNormal);
                    normals[i]=worldNormalToLocal.MultiplyVector(worldNormal).normalized;
                }
            }
            if (landscape && owner.name == "Water")
            {
                // The streamed sphere owns the ocean. Keep only fresh-water triangles here,
                // including their flow attributes, so two sea surfaces never fight for depth.
                var colours = source.colors;
                var triangles = source.triangles;
                var freshTriangles = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                    if (colours[triangles[i]].g > 0.5f || colours[triangles[i+1]].g > 0.5f || colours[triangles[i+2]].g > 0.5f)
                    {
                        freshTriangles.Add(triangles[i]);
                        freshTriangles.Add(triangles[i+1]);
                        freshTriangles.Add(triangles[i+2]);
                    }
                mesh.SetTriangles(freshTriangles, 0);
            }
            mesh.vertices=vertices;
            if(normals.Length==vertices.Length)mesh.normals=normals;
            else mesh.RecalculateNormals();

            // Merged vegetation carries its pivot in UV2. It must move with the warped vertices.
            if(pivots.Count==vertices.Length)
            {
                for(int i=0;i<pivots.Count;i++)
                {
                    var value=pivots[i];
                    Vector3 chart=oldToChart.MultiplyPoint3x4(new Vector3(value.x,value.y,value.z));
                    Vector3 local=toLocal.MultiplyPoint3x4(_projection.ToWorld(chart,_planetCenter));
                    pivots[i]=new Vector4(local.x,local.y,local.z,value.w);
                }
                mesh.SetUVs(2,pivots);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ProjectGenerated()
        {
            if(_projection==null||_root==null)return;
            Transform[] nodes=_root.GetComponentsInChildren<Transform>(true);
            var oldToChart=new Dictionary<Transform,Matrix4x4>(nodes.Length);
            var rotations=new Dictionary<Transform,Quaternion>(nodes.Length);
            Matrix4x4 rootInverse=_root.worldToLocalMatrix;
            Quaternion inverse=Quaternion.Inverse(_root.rotation);
            foreach(var node in nodes)
            {
                oldToChart[node]=rootInverse*node.localToWorldMatrix;
                rotations[node]=inverse*node.rotation;
            }
            foreach(var node in nodes)
            {
                Vector3 chart=oldToChart[node].MultiplyPoint3x4(Vector3.zero);
                node.SetPositionAndRotation(_projection.ToWorld(chart,_planetCenter),_projection.RotationAt(chart)*rotations[node]);
            }
            foreach(var filter in _root.GetComponentsInChildren<MeshFilter>(true))
                filter.sharedMesh=WarpMesh(filter.sharedMesh,filter.transform,oldToChart[filter.transform]);
            foreach(var collider in _root.GetComponentsInChildren<MeshCollider>(true))
                collider.sharedMesh=WarpMesh(collider.sharedMesh,collider.transform,oldToChart[collider.transform]);
            foreach(var lod in _root.GetComponentsInChildren<LODGroup>(true))lod.RecalculateBounds();
        }
    }
}
