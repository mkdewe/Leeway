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
        private readonly Transform _parent;
        private readonly Material _material;
        private readonly Node[] _roots;
        private readonly List<Node> _all = new();
        private readonly List<Node> _wanted = new();
        private readonly List<Node> _building = new();
        private readonly List<Node> _shown = new();
        private readonly List<Node> _previous = new();
        private readonly Stack<GameObject> _pool = new();
        private int _frame;
        private int _nextEvict;

        public float TargetPixels { get; set; } = 5f;

        /// <summary>The finest level that may be asked for: the coarsest while nobody is looking, to spare the work.</summary>
        public int LimitLevel { get; set; } = MinLevel;
        public int MaxParallel { get; set; } = 4;
        public int UploadsPerFrame { get; set; } = 3;

        public PlanetModel Model => _model;
        public int ResidentTiles
        {
            get
            {
                int count = 0;
                foreach (Node node in _all) if (node.State == State.Ready) count++;
                return count;
            }
        }
        public int ShownTiles => _shown.Count;

        /// <summary>True once the tiles of the coarsest level are all ready: the planet can be shown whole.</summary>
        public bool CoarseReady { get; private set; }

        public PlanetTerrain(PlanetModel model, Transform parent, Material material)
        {
            _model = model;
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
        public static Data Build(PlanetModel model, int face, int level, int x, int y)
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
                    points[t * Ring + s] = dir * (model.Radius + sample.Height);
                    colours[t * Ring + s] = PlanetPalette.Of(model, sample);
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

            PointPool.Add(points);
            ColourPool.Add(colours);
            return new Data { Vertices = vertices, Origin = origin, Radius = size * 0.8f + model.MaxHeight };
        }

        /// <summary>The indices every tile shares: a grid of two triangles to the cell, then the skirts, drawn both ways round.</summary>
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
            float a = Mathf.Tan(u * Mathf.PI * 0.25f), b = Mathf.Tan(v * Mathf.PI * 0.25f);
            return (FaceNormal[face] + FaceU[face] * a + FaceV[face] * b).normalized;
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
            _frame++;
            Collect();

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

            if (show) Present();
            else HideAll();

            if (_frame >= _nextEvict)
            {
                _nextEvict = _frame + 120;
                Evict();
            }
        }

        // Whether something is drawn for this part of the sphere: itself, what is under it, or nothing is needed.
        private bool Visit(Node node, Vector3 eye, float pixelsPerRadian, Plane[] frustum)
        {
            node.LastUsed = _frame;

            if (!Visible(node, eye, frustum)) return true;

            float distance = Mathf.Max((eye - node.Direction * _model.Radius).magnitude - node.Size * 0.75f, 1f);
            float cellPixels = node.Size / Grid / distance * pixelsPerRadian;
            bool split = node.Level < Mathf.Min(MaxLevel, LimitLevel) && cellPixels > TargetPixels;

            if (split)
            {
                int mark = _shown.Count;
                bool all = true;
                foreach (Node child in ChildrenOf(node))
                    all &= Visit(child, eye, pixelsPerRadian, frustum);

                if (all) return true;

                // Not all of them are ready: this one stands in until they are.
                _shown.RemoveRange(mark, _shown.Count - mark);
            }

            if (node.State == State.Ready)
            {
                _shown.Add(node);
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
            _wanted.Sort(ByKey);

            foreach (Node node in _wanted)
            {
                if (_building.Count >= MaxParallel) break;
                if (node.State != State.Empty) continue;
                PlanetModel model = _model;
                int face = node.Face, level = node.Level, x = node.X, y = node.Y;
                node.State = State.Building;
                node.Job = Task.Run(() => Build(model, face, level, x, y));
                _building.Add(node);
            }
        }

        private static float Key(Node node) => node.Level <= MinLevel ? -1f : node.Priority;

        // Takes the tiles the workers have finished and gives each a mesh.
        private void Collect()
        {
            int uploads = 0;
            for (int i = _building.Count - 1; i >= 0 && uploads < UploadsPerFrame; i--)
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

            var bounds = new Bounds(Vector3.zero, Vector3.one * (data.Radius * 2f));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length) { bounds = bounds }, MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            VertexPool.Add(data.Vertices);
            data.Vertices = null;

            GameObject go = _pool.Count > 0 ? _pool.Pop() : NewObject();
            go.name = mesh.name;
            go.transform.localPosition = data.Origin;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.SetActive(false);

            node.Mesh = mesh;
            node.Object = go;
            node.State = State.Ready;
            node.Data = null;
        }

        private GameObject NewObject()
        {
            var go = new GameObject("Tile");
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
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                Node node = _all[i];
                if (node.Level <= MinLevel || node.State != State.Ready || node.Shown) continue;
                if (_frame - node.LastUsed < 300) continue;

                Release(node);
                node.State = State.Empty;
            }
        }

        private void Release(Node node)
        {
            if (node.Object != null)
            {
                node.Object.GetComponent<MeshFilter>().sharedMesh = null;
                node.Object.SetActive(false);
                _pool.Push(node.Object);
            }
            if (node.Mesh != null) { node.Mesh.Clear(); _meshPool.Push(node.Mesh); }
            node.Object = null;
            node.Mesh = null;
        }

        /// <summary>Takes every tile off the screen.</summary>
        public void Hide()
        {
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
            foreach (Node node in _all)
            {
                if (node.Mesh != null) UnityEngine.Object.Destroy(node.Mesh);
                if (node.Object != null) UnityEngine.Object.Destroy(node.Object);
            }
            while (_pool.Count > 0) UnityEngine.Object.Destroy(_pool.Pop());
            while (_meshPool.Count > 0) UnityEngine.Object.Destroy(_meshPool.Pop());
            _all.Clear();
            _shown.Clear();
            _building.Clear();
        }
    }
}
