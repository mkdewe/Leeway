using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Leeway.CreatureEditor;
using Leeway.World.Domain;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Leeway.Planet
{
    /// <summary>Collision residency follows players, independently of the render camera.
    /// Geometry generation and PhysX cooking run on workers. Unity mesh upload and collider
    /// publication stay on the main thread. A fixed tile level avoids LOD cracks and replacement gaps.</summary>
    public sealed class PlanetCollisionTiles : IDisposable
    {
        private sealed class Tile
        {
            public PlanetTileKey Key;
            public Task<PlanetTerrain.CollisionData> Geometry;
            public Task Bake;
            public Mesh Mesh;
            public GameObject Object;
            public int LastWanted;
            public float Priority;
        }

        private readonly PlanetModel _model;
        private readonly Transform _parent;
        private readonly CreaturePhysicsWorld _world;
        private readonly Dictionary<PlanetTileKey,Tile> _tiles=new();
        private readonly HashSet<PlanetTileKey> _wanted=new();
        private readonly HashSet<PlanetTileKey> _anchors=new();
        private readonly Dictionary<PlanetTileKey,PlanetTileKey[]> _neighborhoods=new();
        private float _cachedRequestRadius=-1;
        private readonly List<Tile> _remove=new();
        private readonly List<Tile> _queue=new();
        private readonly int _level;
        private int _frame;
        private bool _disposed;
        private const MeshColliderCookingOptions Cooking=MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.UseFastMidphase;

        public float RequestRadius { get; set; }=64f;
        public int MaxParallel { get; set; }=2;
        public int UploadsPerFrame { get; set; }=2;
        public double MainThreadBudgetMilliseconds { get; set; }=2;
        public int KeepFrames { get; set; }=180;
        public int ResidentTiles { get; private set; }
        public int PendingTiles { get; private set; }
        public double PeakUploadMilliseconds { get; private set; }
        public double PeakBakeMilliseconds { get; private set; }
        public int Level=>_level;

        public PlanetCollisionTiles(PlanetModel model,Transform parent,CreaturePhysicsWorld world=null)
        {
            _model=model??throw new ArgumentNullException(nameof(model));
            _parent=parent!=null?parent:throw new ArgumentNullException(nameof(parent));
            _world=world;
            _level=Mathf.Clamp(Mathf.CeilToInt(Mathf.Log(Mathf.PI*0.5f*model.Radius/48f,2)),2,PlanetTerrain.MaxLevel);
        }

        public bool ReadyAt(Vector3 worldPosition)
        {
            var key=PlanetTileKey.At(worldPosition-_parent.position,_level);
            return _tiles.TryGetValue(key,out var tile)&&tile.Object!=null && tile.Object.activeSelf;
        }

        public void Update(IReadOnlyList<Vector3> players)
        {
            if(_disposed)throw new ObjectDisposedException(nameof(PlanetCollisionTiles));
            _frame++;
            _wanted.Clear();_anchors.Clear();
            if(_cachedRequestRadius!=RequestRadius){_neighborhoods.Clear();_cachedRequestRadius=RequestRadius;}
            int neighborhoodsBuilt=0;
            float width=Mathf.PI*0.5f*_model.Radius/(1<<_level);
            foreach(var player in players)
            {
                Vector3 offset=player-_parent.position;
                if(offset.sqrMagnitude<1e-8f)continue;
                var anchor=PlanetTileKey.At(offset,_level);
                // Always request the body/spawn tile immediately, even when the neighborhood budget is exhausted.
                Want(anchor,0);
                if(!_anchors.Add(anchor))continue;
                if(!_neighborhoods.TryGetValue(anchor,out var neighbors)&&neighborhoodsBuilt<2)
                {
                    if(_neighborhoods.Count>=512)_neighborhoods.Clear();
                    neighbors=BuildNeighborhood(anchor,width);
                    _neighborhoods[anchor]=neighbors;
                    neighborhoodsBuilt++;
                }
                if(neighbors==null)continue;
                foreach(var key in neighbors)Want(key,1);
            }

            var budget=Stopwatch.StartNew();
            int uploads=0,active=0;
            ResidentTiles=0;PendingTiles=0;
            _queue.Clear();_remove.Clear();
            foreach(var tile in _tiles.Values)
            {
                if(tile.Object!=null && tile.Object.activeSelf)ResidentTiles++;
                if(tile.Geometry!=null || tile.Bake!=null)PendingTiles++;
                bool wanted=_wanted.Contains(tile.Key);
                if(!wanted && tile.Geometry==null && tile.Bake==null && _frame-tile.LastWanted>KeepFrames)
                { _remove.Add(tile);continue; }
                if(tile.Geometry!=null)
                {
                    active++;
                    if(tile.Geometry.IsCompleted && uploads<UploadsPerFrame && budget.Elapsed.TotalMilliseconds<MainThreadBudgetMilliseconds)
                    {
                        if(tile.Geometry.IsFaulted)
                        {
                            UnityEngine.Debug.LogException(tile.Geometry.Exception.GetBaseException());
                            tile.Geometry=null;continue;
                        }
                        var timer=Stopwatch.StartNew();
                        var data=tile.Geometry.Result;
                        tile.Geometry=null;
                        tile.Mesh=new Mesh {name="Collision "+tile.Key};
                        tile.Mesh.vertices=data.Vertices;
                        tile.Mesh.triangles=data.Indices;
                        tile.Mesh.RecalculateBounds();
                        tile.Priority=0;
                        // Store the local origin while the mesh is being cooked.
                        var go=new GameObject("Collision "+tile.Key);
                        go.transform.SetParent(_parent,false);
                        go.transform.localPosition=data.Origin;
                        go.SetActive(false);
                        tile.Object=go;
                        var id=tile.Mesh.GetEntityId();
                        tile.Bake=Task.Run(()=>{var clock=Stopwatch.StartNew();Physics.BakeMesh(id,false,Cooking);return clock.Elapsed.TotalMilliseconds;});
                        PeakUploadMilliseconds=Math.Max(PeakUploadMilliseconds,timer.Elapsed.TotalMilliseconds);
                        uploads++;
                    }
                }
                else if(tile.Bake!=null)
                {
                    active++;
                    if(tile.Bake.IsCompleted && uploads<UploadsPerFrame && budget.Elapsed.TotalMilliseconds<MainThreadBudgetMilliseconds)
                    {
                        if(tile.Bake.IsFaulted)
                        {
                            UnityEngine.Debug.LogException(tile.Bake.Exception.GetBaseException());
                            Release(tile);continue;
                        }
                        PeakBakeMilliseconds=Math.Max(PeakBakeMilliseconds,((Task<double>)tile.Bake).Result);
                        tile.Bake=null;
                        var collider=tile.Object.AddComponent<MeshCollider>();
                        collider.cookingOptions=Cooking;
                        collider.sharedMesh=tile.Mesh;
                        tile.Object.SetActive(true);
                        if(_world!=null)_world.RegisterEnvironment(collider);
                        uploads++;
                    }
                }
                else if(tile.Object==null && wanted)_queue.Add(tile);
            }
            foreach(var tile in _remove){Release(tile);_tiles.Remove(tile.Key);}
            _queue.Sort((a,b)=>a.Priority.CompareTo(b.Priority));
            foreach(var tile in _queue)
            {
                if(active>=MaxParallel)break;
                var key=tile.Key;
                tile.Geometry=Task.Run(()=>PlanetTerrain.BuildCollision(_model,key));
                active++;
            }
        }

        private void Want(PlanetTileKey key,float priority)
        {
            if(!_wanted.Add(key))return;
            if(!_tiles.TryGetValue(key,out var tile))
            {
                tile=new Tile{Key=key};
                _tiles.Add(key,tile);
            }
            tile.LastWanted=_frame;
            tile.Priority=priority;
        }

        private PlanetTileKey[] BuildNeighborhood(PlanetTileKey anchor,float width)
        {
            Vector3 direction=anchor.CenterDirection;
            var frame=new SurfaceFrame(direction);
            float step=width*0.4f;
            // Centre the cached neighborhood on the tile, with room for any point in that tile.
            int count=Mathf.CeilToInt((RequestRadius+width*1.75f)/step);
            var keys=new HashSet<PlanetTileKey>();
            for(int y=-count;y<=count;y++)
            for(int x=-count;x<=count;x++)
                keys.Add(PlanetTileKey.At(direction+(frame.Right*x+frame.Forward*y)*(step/_model.Radius),_level));
            var result=new PlanetTileKey[keys.Count];keys.CopyTo(result);
            return result;
        }

        private void Release(Tile tile)
        {
            if(tile.Bake!=null)
            {
                // Native mesh must outlive its bake. Only disposal waits for an in-flight cook.
                try {tile.Bake.GetAwaiter().GetResult();}catch(Exception e){UnityEngine.Debug.LogException(e);}
                tile.Bake=null;
            }
            if(tile.Geometry!=null)
            {
                tile.Geometry=null;
            }
            if(tile.Object!=null)
            {
                if(tile.Object.TryGetComponent<MeshCollider>(out var collider))
                {
                    collider.enabled=false;
                    if(_world!=null)_world.UnregisterEnvironment(collider);
                }
                tile.Object.SetActive(false);
                Destroy(tile.Object);tile.Object=null;
            }
            if(tile.Mesh!=null){Destroy(tile.Mesh);tile.Mesh=null;}
        }

        public void Dispose()
        {
            if(_disposed)return;
            _disposed=true;
            foreach(var tile in _tiles.Values)Release(tile);
            _tiles.Clear();
            ResidentTiles=0;PendingTiles=0;
        }

        private static void Destroy(Object value)
        {
            if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);
        }
    }
}
