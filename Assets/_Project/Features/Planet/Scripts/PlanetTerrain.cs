using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Leeway.World.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.Planet
{
    /// <summary>
    /// The ground of a planet at the level of detail the camera needs: the sphere is six faces of a cube, each a
    /// quadtree of square tiles, and a tile is split into four whenever its grid would show as coarse as
    /// <see cref="TargetPixels"/> on screen. Seen whole the planet is a few hundred tiles; a few hundred metres up
    /// the ground under the camera is made of cells of a few metres.
    /// </summary>
    /// <remarks>
    /// <para>Tiles are sampled from the <see cref="PlanetModel"/> on worker threads and uploaded a few a frame, so
    /// that coming close never stalls. A tile that is wanted and not yet ready is stood in for by the coarser tile
    /// above it; its children replace it only when all four are ready, so there are never holes or overlaps. Each
    /// tile carries a skirt, a short fringe hanging under its edge, that hides the cracks between neighbours of
    /// different detail.</para>
    ///
    /// <para>Vertices are stored relative to the tile's own centre: the planet is thousands of metres across and the
    /// camera may be a few metres from the ground, which single-precision positions would not survive.</para>
    /// </remarks>
    public sealed class PlanetTerrain
    {
        public const int Grid = 24;
        public const int MinLevel = 2;
        public const int MaxLevel = 10;

        // Samples are taken one ring wider than the tile, so that edge normals agree with the neighbour's.
        private const int Ring = Grid + 3;

        private static readonly Vector3[] FaceNormal = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        private static readonly Vector3[] FaceU = { new(0, 0, -1), new(0, 0, 1), new(1, 0, 0), new(1, 0, 0), new(1, 0, 0), new(-1, 0, 0) };
        private static readonly Vector3[] FaceV = { Vector3.up, Vector3.up, new(0, 0, -1), Vector3.forward, Vector3.up, Vector3.up };

        [StructLayout(LayoutKind.Sequential)]
        public struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Color32 Colour;
        }

        /// <summary>One tile's finished arrays.</summary>
        public sealed class Data
        {
            public Vertex[] Vertices;
            public Vector3 Origin;
            public float Radius;
            public Bounds LocalBounds;
        }

        private enum State { Empty, Building, Ready }

        private sealed class Node
        {
            public int Face, Level, X, Y;
            public Node Parent;
            public Node[] Children;
            public Vector3 Direction;
            public float Size;
            public State State;
            public Task<Data> Job;
            public Data Data;
            public GameObject Object;
            public Mesh Mesh;
            public bool Shown;
            public int ShownFrame;
            public int LastUsed;
            public float Priority;
        }

        private static ushort[] _indices;

        // Building a tile needs a few tens of kilobytes of arrays, and hundreds of tiles are built as the camera climbs:
        // left to the garbage collector they would stall the game every fraction of a second. The arrays are lent instead.
        private static readonly ConcurrentBag<Vector3[]> PointPool = new();
        private static readonly ConcurrentBag<Color32[]> ColourPool = new();
        private static readonly ConcurrentBag<Vertex[]> VertexPool = new();
        private static readonly Comparison<Node> ByKey = (a, b) => Key(a).CompareTo(Key(b));
        private readonly Plane[] _frustum = new Plane[6];
        private readonly Stack<Mesh> _meshPool = new();

        private readonly PlanetModel _model;
        private readonly Dictionary<PlanetTileKey,Node> _nodes=new();
        private readonly HashSet<PlanetTileKey> _fixedTiles=new(), _fixedParents=new(), _anchorKeys=new(), _nextAnchorKeys=new();
        private int _fixedLevel=-1;
        private readonly bool _water;
        private readonly Func<Vector3,PlanetModel.Point,Color32> _colour;
        private readonly Transform _parent;
        private readonly Material _material;
        private readonly Node[] _roots;
        private readonly List<Node> _all = new();
        private readonly List<Node> _wanted = new();
        private readonly List<Node> _building = new();
        private readonly List<Node> _shown = new();
        private readonly List<Node> _previous = new();
        private readonly HashSet<Node> _coverageParents = new();
        private readonly Stack<GameObject> _pool = new();
        private int _frame;
        private int _residentCount,_nextBudgetAdjustment;
        private float _pressurePixels;
        private bool _disposed;
        private const int PoolLimit=32;
        private int _maxResidentTiles=1536;
        public int MaxResidentTiles {get=>_maxResidentTiles;set{_maxResidentTiles=Mathf.Max(128,value);_selectionDirty=true;}}
        public int PooledMeshes=>_meshPool.Count;
        public int PooledObjects=>_pool.Count;
        public int PendingTiles=>_building.Count;
        private int _nextEvict,_evictionCursor;
        private bool _selectionDirty=true;
        private Matrix4x4 _lastCameraMatrix,_lastProjection;
        private int _lastLimit=-1,_lastPixelHeight;
        private bool _lastShow;

        public float TargetPixels { get; set; } = 5f;

        /// <summary>The finest level that may be asked for: the coarsest while nobody is looking, to spare the work.</summary>
        public int LimitLevel { get; set; } = MinLevel;
        public int MaxParallel { get; set; } = 4;
        public int UploadsPerFrame { get; set; } = 3;

        public PlanetModel Model => _model;
        public int ResidentTiles => _residentCount;
        public int ShownTiles => _shown.Count;

        /// <summary>True once the tiles of the coarsest level are all ready: the planet can be shown whole.</summary>
        public bool CoarseReady { get; private set; }
        public bool ViewReady => CoarseReady && _wanted.Count==0 && _building.Count==0 && !_selectionDirty;


        /// <summary>Pin gameplay geometry to the collision grid. Zoom may change distant LOD,
        /// but never the triangles under a player. Anchors are quantised and padded by a tile.</summary>
        public void SetGroundAnchors(IReadOnlyList<Vector3> points,int level)
        {
            _nextAnchorKeys.Clear();
            foreach(var point in points)_nextAnchorKeys.Add(PlanetTileKey.At(point-_parent.position,level));
            if(level==_fixedLevel&&_anchorKeys.SetEquals(_nextAnchorKeys))return;
            _fixedLevel=level;_anchorKeys.Clear();_anchorKeys.UnionWith(_nextAnchorKeys);
            _fixedTiles.Clear();_fixedParents.Clear();_selectionDirty=true;
            float width=Mathf.PI*0.5f*_model.Radius/(1<<level),step=width*0.4f;
            var home=_model.GetPatch(0);
            var homeKey=home!=null?PlanetTileKey.At(home.DirectionAt(home.Field.HubCenter.x,home.Field.HubCenter.z),level):default;
            foreach(var anchor in _anchorKeys)
            {
                // Prepare the home island as one finished landscape, including directions
                // initially behind the camera. Keep a bounded ring around travelling players.
                float radius=48f;
                if(home!=null&&anchor.Equals(homeKey))
                {
                    var hub=home.Field.HubCenter;
                    float reach=(home.Field.Settings.HalfSize+new Vector2(hub.x,hub.z).magnitude)*home.HorizontalScale+24f;
                    radius=Mathf.Min(reach,Mathf.Min(384f,width*10f));
                }
                int count=Mathf.CeilToInt((radius+width*1.75f)/step);
                var frame=new SurfaceFrame(anchor.CenterDirection);
                for(int y=-count;y<=count;y++)
                for(int x=-count;x<=count;x++)
                {
                    var key=PlanetTileKey.At(anchor.CenterDirection+
                        (frame.Right*x+frame.Forward*y)*(step/_model.Radius),level);
                    if(!_fixedTiles.Add(key))continue;
                    for(int l=key.Level-1;l>=MinLevel;l--)
                        _fixedParents.Add(new PlanetTileKey(key.Face,l,key.X>>(key.Level-l),key.Y>>(key.Level-l)));
                }
            }
        }

        public bool StableGroundReadyAt(Vector3 point)
        {
            if(_fixedLevel<0)return true;
            var key=PlanetTileKey.At(point-_parent.position,_fixedLevel);
            if(!_fixedTiles.Contains(key))return true;
            if(!_nodes.TryGetValue(key,out var node)||node.State!=State.Ready)return false;
            for(var parent=node.Parent;parent!=null;parent=parent.Parent)
                foreach(var sibling in ChildrenOf(parent))if(sibling.State!=State.Ready)return false;
            return true;
        }

        public PlanetTerrain(PlanetModel model, Transform parent, Material material, bool water = false,
            Func<Vector3,PlanetModel.Point,Color32> colour = null)
        {
            _model = model;
            _water = water;
            _colour = colour;
            _parent = parent;
            _material = material;

            int perSide = 1 << MinLevel;
            var roots = new List<Node>();
            for (int face = 0; face < 6; face++)
                for (int y = 0; y < perSide; y++)
                    for (int x = 0; x < perSide; x++)
                        roots.Add(Make(face, MinLevel, x, y, null));
            _roots = roots.ToArray();
        }

        // ---------------------------------------------------------------- tiles

        /// <summary>Samples one tile of the planet. Safe to call from any thread.</summary>
        public static Data Build(PlanetModel model, int face, int level, int x, int y, bool water = false,
            Func<Vector3,PlanetModel.Point,Color32> colour = null)
        {
            int cells = 1 << level;
            float u0 = -1f + 2f * x / cells, v0 = -1f + 2f * y / cells;
            float step = 2f / cells / Grid;

            Vector3 origin = DirectionOf(face, u0 + 1f / cells, v0 + 1f / cells) * model.Radius;

            // The samples: one ring beyond the tile on every side.
            if (!PointPool.TryTake(out Vector3[] points)) points = new Vector3[Ring * Ring];
            if (!ColourPool.TryTake(out Color32[] colours)) colours = new Color32[Ring * Ring];
            for (int t = 0; t < Ring; t++)
            {
                for (int s = 0; s < Ring; s++)
                {
                    Vector3 dir = DirectionOf(face, u0 + (s - 1) * step, v0 + (t - 1) * step);
                    PlanetModel.Point sample = model.Sample(dir);
                    points[t * Ring + s] = dir * (model.Radius + (water ? 0f : sample.GroundHeight));
                    colours[t * Ring + s] = water ? new Color32((byte)(Mathf.Clamp01(-sample.GroundHeight / 3f) * 255), 0, 255,
                        (byte)(Mathf.Clamp01(0.5f - sample.GroundHeight / 4f) * 255)) : colour != null ? colour(dir, sample) : PlanetPalette.Of(model, sample);
                }
            }

            int side = Grid + 1;
            float size = Mathf.PI * 0.5f * model.Radius / cells;
            float skirt = size * 0.05f + 0.5f;
            if (!VertexPool.TryTake(out Vertex[] vertices)) vertices = new Vertex[side * side + 4 * side];

            for (int t = 0; t < side; t++)
            {
                for (int s = 0; s < side; s++)
                {
                    int i = (t + 1) * Ring + s + 1;
                    Vector3 du = points[i + 1] - points[i - 1];
                    Vector3 dv = points[i + Ring] - points[i - Ring];
                    Vector3 normal = Vector3.Cross(du, dv).normalized;
                    vertices[t * side + s] = new Vertex { Position = points[i] - origin, Normal = normal, Colour = colours[i] };
                }
            }

            // Skirts: the four edges, copied and let down towards the centre of the planet.
            int next = side * side;
            for (int edge = 0; edge < 4; edge++)
            {
                for (int k = 0; k < side; k++)
                {
                    int s = edge == 0 ? k : edge == 1 ? k : edge == 2 ? 0 : Grid;
                    int t = edge == 0 ? 0 : edge == 1 ? Grid : k;
                    Vertex source = vertices[t * side + s];
                    source.Position -= (source.Position + origin).normalized * skirt;
                    vertices[next++] = source;
                }
            }

            ReturnBuffer(PointPool,points);
            ReturnBuffer(ColourPool,colours);
            var localBounds=new Bounds(vertices[0].Position,Vector3.zero);
            for(int i=1;i<vertices.Length;i++)localBounds.Encapsulate(vertices[i].Position);
            localBounds.Expand(water?4f:0.02f); // Include ocean-wave displacement.
            return new Data { Vertices = vertices, Origin = origin, Radius = size * 0.8f + model.MaxHeight, LocalBounds=localBounds };
        }

        /// <summary>The indices every tile shares: a grid of two triangles to the cell, then the skirts, drawn both ways round.</summary>
        public sealed class CollisionData
        {
            public Vector3[] Vertices;
            public int[] Indices;
            public Vector3 Origin;
        }

        /// <summary>Same surface samples and triangle diagonal as rendering, with one extra cell
        /// outside each edge. The overlap prevents floating-point raycast cracks at tile/cube seams;
        /// render skirts never become physical walls.</summary>
        public static CollisionData BuildCollision(PlanetModel model, PlanetTileKey key)
        {
            int cells=1<<key.Level;
            float u0=-1f+2f*key.X/cells, v0=-1f+2f*key.Y/cells;
            float step=2f/cells/Grid;
            Vector3 origin=key.CenterDirection*model.Radius;
            int side=Grid+3;
            var vertices=new Vector3[side*side];
            for(int y=0;y<side;y++)
            for(int x=0;x<side;x++)
            {
                Vector3 dir=PlanetTileKey.Direction(key.Face,u0+(x-1)*step,v0+(y-1)*step);
                vertices[y*side+x]=dir*(model.Radius+model.Sample(dir).GroundHeight)-origin;
            }
            var indices=new int[(side-1)*(side-1)*6];
            int next=0;
            for(int y=0;y<side-1;y++)
            for(int x=0;x<side-1;x++)
            {
                int a=y*side+x,b=a+1,c=a+side,d=c+1;
                indices[next++]=a;indices[next++]=b;indices[next++]=c;
                indices[next++]=b;indices[next++]=d;indices[next++]=c;
            }
            return new CollisionData {Vertices=vertices,Indices=indices,Origin=origin};
        }

        private static void ReturnBuffer<T>(ConcurrentBag<T[]> pool,T[] buffer)
        {
            if(buffer==null)return;
            lock(pool){if(pool.Count<PoolLimit)pool.Add(buffer);}
        }

        internal static void ReleaseData(Data data)
        {
            if (data?.Vertices == null) return;
            ReturnBuffer(VertexPool,data.Vertices);
            data.Vertices = null;
        }

        public static ushort[] Indices()
        {
            if (_indices != null) return _indices;

            int side = Grid + 1;
            var list = new List<ushort>(Grid * Grid * 6 + 4 * Grid * 12);
            for (int t = 0; t < Grid; t++)
            {
                for (int s = 0; s < Grid; s++)
                {
                    int a = t * side + s, b = a + 1, c = a + side, d = c + 1;
                    list.Add((ushort)a); list.Add((ushort)b); list.Add((ushort)c);
                    list.Add((ushort)b); list.Add((ushort)d); list.Add((ushort)c);
                }
            }

            for (int edge = 0; edge < 4; edge++)
            {
                for (int k = 0; k < Grid; k++)
                {
                    int s0 = edge == 0 ? k : edge == 1 ? k : edge == 2 ? 0 : Grid;
                    int t0 = edge == 0 ? 0 : edge == 1 ? Grid : k;
                    int s1 = edge < 2 ? s0 + 1 : s0;
                    int t1 = edge < 2 ? t0 : t0 + 1;
                    int top0 = t0 * side + s0, top1 = t1 * side + s1;
                    int low0 = side * side + edge * side + k, low1 = low0 + 1;

                    list.Add((ushort)top0); list.Add((ushort)top1); list.Add((ushort)low0);
                    list.Add((ushort)top1); list.Add((ushort)low1); list.Add((ushort)low0);
                    list.Add((ushort)top0); list.Add((ushort)low0); list.Add((ushort)top1);
                    list.Add((ushort)top1); list.Add((ushort)low0); list.Add((ushort)low1);
                }
            }

            _indices = list.ToArray();
            return _indices;
        }

        // The direction a point of a cube face looks out at: the cube is spread over the sphere with a tangent, so that
        // the tiles near the middle of a face and those near its corners are about the same size.
        private static Vector3 DirectionOf(int face, float u, float v)
        {
            return PlanetTileKey.Direction(face, u, v);
        }

        // ---------------------------------------------------------------- the tree

        private Node Make(int face, int level, int x, int y, Node parent)
        {
            int cells = 1 << level;
            var node = new Node
            {
                Face = face, Level = level, X = x, Y = y, Parent = parent,
                Direction = DirectionOf(face, -1f + (2f * x + 1f) / cells, -1f + (2f * y + 1f) / cells),
                Size = Mathf.PI * 0.5f * _model.Radius / cells,
            };
            _all.Add(node);
            _nodes.Add(new PlanetTileKey(face,level,x,y),node);
            return node;
        }

        private Node[] ChildrenOf(Node node)
        {
            if (node.Children != null) return node.Children;

            node.Children = new Node[4];
            for (int i = 0; i < 4; i++)
                node.Children[i] = Make(node.Face, node.Level + 1, node.X * 2 + (i & 1), node.Y * 2 + (i >> 1), node);
            return node.Children;
        }

        // ---------------------------------------------------------------- each frame

        /// <summary>
        /// Chooses the tiles to draw for this camera, asks for the ones that are missing and shows what is ready.
        /// With <paramref name="show"/> false the tiles are only asked for and prepared: a terrain being made ready
        /// behind the one on screen.
        /// </summary>
        public void Update(UnityEngine.Camera camera, bool show)
        {
            if(_disposed)return;
            _frame++;
            Collect();
            if(_frame>=_nextBudgetAdjustment)
            {
                float current=Mathf.Max(TargetPixels,_pressurePixels);
                if(_residentCount+_building.Count>=MaxResidentTiles*0.85f &&
                    (_shown.Count>MaxResidentTiles*0.55f||_wanted.Count>_building.Count))
                {_pressurePixels=Mathf.Min(80f,current*1.25f);_selectionDirty=true;}
                else if(_residentCount<MaxResidentTiles*0.5f&&_pressurePixels>TargetPixels)
                {_pressurePixels=Mathf.Max(TargetPixels,current/1.25f);_selectionDirty=true;}
                _nextBudgetAdjustment=_frame+30;
            }
            var cameraMatrix=camera.transform.localToWorldMatrix;
            var projection=camera.projectionMatrix;
            if(!_selectionDirty&&((Vector3)_lastCameraMatrix.GetColumn(3)-camera.transform.position).sqrMagnitude<0.01f&&
                Quaternion.Angle(_lastCameraMatrix.rotation,camera.transform.rotation)<0.05f&&_lastProjection==projection&&
                _lastLimit==LimitLevel&&_lastPixelHeight==camera.pixelHeight&&_lastShow==show)
            {
                if(_frame>=_nextEvict){_nextEvict=_frame+1;Evict();}
                return;
            }
            _selectionDirty=false;_lastCameraMatrix=cameraMatrix;_lastProjection=projection;
            _lastLimit=LimitLevel;_lastPixelHeight=camera.pixelHeight;_lastShow=show;

            Vector3 eye = camera.transform.position - _parent.position;
            float pixelsPerRadian = camera.pixelHeight / (2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            Plane[] frustum = _frustum;
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);

            _wanted.Clear();
            _previous.Clear();
            _previous.AddRange(_shown);
            _shown.Clear();

            bool coarse = true;
            foreach (Node root in _roots)
            {
                if (root.State != State.Ready) coarse = false;
                // The coarsest tiles are always wanted, seen or not: they are what makes the planet whole.
                if (root.State == State.Empty) Want(root, 0f);
                Visit(root, eye, pixelsPerRadian, frustum);
            }
            CoarseReady = coarse;

            Dispatch();

            // Hidden prewarming needs the same coverage retention as a visible world.
            _coverageParents.Clear();
            foreach(var leaf in _shown)
                for(var ancestor=leaf.Parent;ancestor!=null;ancestor=ancestor.Parent)_coverageParents.Add(ancestor);
            if (show) Present();
            else HideAll();

            if (_frame >= _nextEvict)
            {
                _nextEvict = _frame + 1;
                Evict();
            }
        }

        // Whether something is drawn for this part of the sphere: itself, what is under it, or nothing is needed.
        private bool Visit(Node node, Vector3 eye, float pixelsPerRadian, Plane[] frustum)
        {
            node.LastUsed = _frame;

            var address=new PlanetTileKey(node.Face,node.Level,node.X,node.Y);
            bool fixedLeaf=_fixedTiles.Contains(address),fixedBranch=_fixedParents.Contains(address);
            bool visible=Visible(node,eye,frustum);
            if(!visible&&!fixedLeaf&&!fixedBranch)return true;
            // Build parents first. Descending through missing ancestors requests thousands of
            // invisible leaves, sorts them every frame, and delays the first useful refinements.
            if(node.State!=State.Ready)
            {
                Want(node,(eye-node.Direction*_model.Radius).magnitude);
                return false;
            }

            float distance = Mathf.Max((eye - node.Direction * _model.Radius).magnitude - node.Size * 0.75f, 1f);
            float cellPixels = node.Size / Grid / distance * pixelsPerRadian;
            bool split = node.Level < Mathf.Min(MaxLevel, LimitLevel) && cellPixels > Mathf.Max(TargetPixels,_pressurePixels);

            split=fixedBranch||(!fixedLeaf&&split);

            if (split)
            {
                int mark = _shown.Count;
                bool all = true;
                var children=ChildrenOf(node);
                // Complete this replacement before requesting finer descendants. Otherwise
                // near leaves starve their siblings and the whole patch stays at the root LOD.
                bool childrenReady=true;
                foreach(var child in children)
                {
                    if(child.State==State.Ready)continue;
                    childrenReady=false;
                    Want(child,(eye-child.Direction*_model.Radius).magnitude);
                }
                if(childrenReady)
                    foreach(Node child in children)all &= Visit(child,eye,pixelsPerRadian,frustum);
                else all=false;

                if (all) return true;

                // Not all of them are ready: this one stands in until they are.
                _shown.RemoveRange(mark, _shown.Count - mark);
            }

            if (node.State == State.Ready)
            {
                if(visible)_shown.Add(node);
                return true;
            }

            Want(node, distance);
            return false;
        }

        // A tile behind the horizon or outside the view is neither shown nor worth making.
        private bool Visible(Node node, Vector3 eye, Plane[] frustum)
        {
            float reach = node.Size * 0.8f + _model.MaxHeight;
            float distance = eye.magnitude;
            if (distance > _model.Radius)
            {
                float horizon = Mathf.Acos(Mathf.Clamp01(_model.Radius / distance));
                float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(node.Direction, eye / distance), -1f, 1f));
                if (angle > horizon + reach / _model.Radius) return false;
            }

            Vector3 centre = _parent.position + node.Direction * _model.Radius;
            foreach (Plane plane in frustum)
                if (plane.GetDistanceToPoint(centre) < -reach * 1.2f) return false;
            return true;
        }

        private void Want(Node node, float distance)
        {
            if (node.State != State.Empty) return;
            node.Priority = distance;
            _wanted.Add(node);
        }

        // Starts work on the nearest wanted tiles, a few at a time.
        private void Dispatch()
        {
            if (_wanted.Count == 0 || _building.Count >= MaxParallel) return;

            // Coarse tiles come first: with them in place the planet is whole whatever else is missing.
            _wanted.Sort((a,b)=>{int level=a.Level.CompareTo(b.Level);return level!=0?level:ByKey(a,b);});

            foreach (Node node in _wanted)
            {
                if (_building.Count >= MaxParallel || _residentCount+_building.Count>=MaxResidentTiles) break;
                if (node.State != State.Empty) continue;
                PlanetModel model = _model;
                int face = node.Face, level = node.Level, x = node.X, y = node.Y;
                node.State = State.Building;
                node.Job = Task.Run(() => Build(model, face, level, x, y, _water, _colour));
                _building.Add(node);
            }
        }

        private static float Key(Node node) => node.Level <= MinLevel ? -1f : node.Priority;

        // Takes the tiles the workers have finished and gives each a mesh.
        private void Collect()
        {
            int uploads = 0;
            var uploadBudget=System.Diagnostics.Stopwatch.StartNew();
            for (int i = _building.Count - 1; i >= 0 && uploads < UploadsPerFrame && uploadBudget.Elapsed.TotalMilliseconds<0.75; i--)
            {
                Node node = _building[i];
                if (!node.Job.IsCompleted) continue;

                _building.RemoveAt(i);
                if (node.Job.IsFaulted)
                {
                    Debug.LogException(node.Job.Exception.GetBaseException());
                    node.State = State.Empty;
                    continue;
                }

                node.Data = node.Job.Result;
                node.Job = null;
                Upload(node);
                uploads++;
            }
        }

        private void Upload(Node node)
        {
            _selectionDirty=true;
            Data data = node.Data;
            Mesh mesh = _meshPool.Count > 0 ? _meshPool.Pop() : new Mesh();
            mesh.name = $"Tile {node.Face}/{node.Level}/{node.X},{node.Y}";
            mesh.SetVertexBufferParams(data.Vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4));
            mesh.SetVertexBufferData(data.Vertices, 0, 0, data.Vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);

            ushort[] indices = Indices();
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, MeshUpdateFlags.DontRecalculateBounds);

            // Rendering bounds describe this mesh, including skirts, rather than the entire
            // possible mountain range. Inflated bounds submitted thousands of off-screen tiles.
            var bounds = data.LocalBounds;
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length) { bounds = bounds }, MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            ReturnBuffer(VertexPool,data.Vertices);
            data.Vertices = null;

            GameObject go = _pool.Count > 0 ? _pool.Pop() : NewObject();
            go.name = mesh.name;
            go.transform.localPosition = data.Origin;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.SetActive(false);

            node.Mesh = mesh;
            node.Object = go;
            node.State = State.Ready;
            _residentCount++;
            node.Data = null;
        }

        private GameObject NewObject()
        {
            var go = new GameObject("Tile");
            if (_water) go.layer = Mathf.Max(0, LayerMask.NameToLayer("Water"));
            go.transform.SetParent(_parent, false);
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // What was shown last frame and is not now goes out; what is newly chosen comes in.
        private void Present()
        {
            _coverageParents.Clear();
            foreach(var leaf in _shown)
                for(var ancestor=leaf.Parent;ancestor!=null;ancestor=ancestor.Parent)_coverageParents.Add(ancestor);
            foreach (Node node in _shown)
            {
                node.ShownFrame = _frame;
                node.Shown = true;
                if (!node.Object.activeSelf) node.Object.SetActive(true);
            }

            foreach (Node node in _previous)
            {
                if (node.ShownFrame == _frame || node.Object == null) continue;
                node.Shown = false;
                node.Object.SetActive(false);
            }
        }

        private void HideAll()
        {
            foreach (Node node in _previous)
            {
                if (node.Object == null) continue;
                node.Shown = false;
                node.Object.SetActive(false);
            }
            _shown.Clear();
        }

        // Tiles that have not been wanted for a good while go: the finest ones would otherwise pile up as the camera moves.
        private void Evict()
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            int released=0;
            bool pressure=_residentCount+_building.Count>=MaxResidentTiles-4;
            for(int checkedNodes=0;checkedNodes<128&&_all.Count>0;checkedNodes++)
            {
                if(_evictionCursor>=_all.Count)_evictionCursor=0;
                Node node=_all[_evictionCursor++];
                if(node.Level<=MinLevel||node.State!=State.Ready||node.Shown||_fixedTiles.Contains(new PlanetTileKey(node.Face,node.Level,node.X,node.Y))||
                    _fixedParents.Contains(new PlanetTileKey(node.Face,node.Level,node.X,node.Y))||
                    (node.Parent!=null&&_fixedParents.Contains(new PlanetTileKey(node.Parent.Face,node.Parent.Level,node.Parent.X,node.Parent.Y)))||(_coverageParents.Contains(node)||(node.Parent!=null&&_coverageParents.Contains(node.Parent)))||(!pressure&&_frame-node.LastUsed<300))continue;
                Release(node);node.State=State.Empty;
                if(++released>=4||clock.Elapsed.TotalMilliseconds>=0.5)break;
            }
        }

        private void Release(Node node)
        {
            if (node.Object != null)
            {
                node.Object.GetComponent<MeshFilter>().sharedMesh = null;
                node.Object.SetActive(false);
                if(_pool.Count<PoolLimit)_pool.Push(node.Object);else UnityEngine.Object.Destroy(node.Object);
            }
            if (node.Mesh != null)
            {
                _residentCount--;
                if(_meshPool.Count<PoolLimit){node.Mesh.Clear();_meshPool.Push(node.Mesh);}
                else UnityEngine.Object.Destroy(node.Mesh);
            }
            node.Object = null;
            node.Mesh = null;
        }

        /// <summary>Takes every tile off the screen.</summary>
        public void Hide()
        {
            _selectionDirty=true;
            foreach (Node node in _shown)
            {
                if (node.Object == null) continue;
                node.Shown = false;
                node.Object.SetActive(false);
            }
            _shown.Clear();
        }

        /// <summary>Removes everything this terrain made.</summary>
        public void Dispose()
        {
            if(_disposed)return;
            _disposed=true;
            foreach (Node node in _all)
            {
                if(node.Data!=null)ReleaseData(node.Data);
                if(node.Job!=null)
                {
                    // Generation owns managed arrays only; return the result when it finishes.
                    // Never retain or touch Unity objects from this continuation.
                    node.Job.ContinueWith(task=>
                    {
                        if(task.Status==TaskStatus.RanToCompletion)ReleaseData(task.Result);
                        else if(task.IsFaulted){var observed=task.Exception;}
                    },TaskScheduler.Default);
                    node.Job=null;
                }
                if (node.Mesh != null) UnityEngine.Object.Destroy(node.Mesh);
                if (node.Object != null) UnityEngine.Object.Destroy(node.Object);
            }
            while (_pool.Count > 0) UnityEngine.Object.Destroy(_pool.Pop());
            while (_meshPool.Count > 0) UnityEngine.Object.Destroy(_meshPool.Pop());
            _all.Clear();_nodes.Clear();_fixedTiles.Clear();_fixedParents.Clear();_anchorKeys.Clear();
            _shown.Clear();
            _building.Clear();
            _coverageParents.Clear();_previous.Clear();_wanted.Clear();
            _residentCount=0;
        }
    }
}
