using System;
using System.Collections.Generic;
using Leeway.Rendering;
using Leeway.World.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.World
{
    /// <summary>
    /// Builds an island under this transform from a biome and a seed: terrain chunks, colliders,
    /// sea, lakes and rivers, caves, the ring around the safe hub and the props. It is purely
    /// local - <see cref="IslandSeed"/> decides the seed over the network, and every peer runs
    /// the same generation.
    /// </summary>
    /// <remarks>
    /// Props are not one GameObject each: their meshes are merged per chunk and material, and only
    /// props with colliders or lights get an instance - stripped of its renderers. That keeps a
    /// 500 m island with thousands of plants at a few hundred draw calls.
    /// </remarks>
    public sealed partial class IslandGenerator : MonoBehaviour
    {
        private const string RootName = "GeneratedIsland";
        private const float PropChunkSize = 64f;

        [SerializeField] private IslandBiome _biome;
        [Tooltip("Seed used by the editor preview buttons.")]
        [SerializeField] private int _previewSeed = 1;
        [SerializeField] private float _waterSize = 6000f;
        [Tooltip("Shift the island so its safe hub lands on this object's position (e.g. under the creature editor pad).")]
        [SerializeField] private bool _centerOnHub;
        [Tooltip("Terrain cells per mesh chunk along each edge.")]
        [SerializeField, Range(8, 64)] private int _chunkCells = 26;

        [Header("Grow-in")]
        [Tooltip("Play mode only: after each generation the props grow from nothing in a wave spreading out from the safe hub.")]
        [SerializeField] private bool _growIn;
        [Tooltip("How fast the wave spreads, in metres per second.")]
        [SerializeField, Min(1f)] private float _growWaveSpeed = 45f;
        [Tooltip("Seconds one prop takes to reach full size.")]
        [SerializeField, Min(0.05f)] private float _growDuration = 0.7f;
        [Tooltip("Random extra delay per prop, so neighbours don't pop in lockstep.")]
        [SerializeField, Min(0f)] private float _growJitter = 0.35f;

        private static readonly int GrowActiveId = Shader.PropertyToID("_LeewayGrowActive");
        private static readonly int GrowElapsedId = Shader.PropertyToID("_LeewayGrowElapsed");
        private static readonly int GrowDurationId = Shader.PropertyToID("_LeewayGrowDuration");

        private Transform _root;
        private MaterialPropertyBlock _waterProperties;
        private readonly List<UnityEngine.Object> _owned = new();
        private readonly Dictionary<GameObject, PrefabParts> _partsCache = new();
        private bool _growActive;
        private float _growStartTime;
        private float _growLongestDelay;

        public IslandBiome Biome => _biome;
        public int PreviewSeed => _previewSeed;
        public IslandHeightField Field { get; private set; }
        public bool IsGenerated => Field != null;

        // The flat field is input data while a planet is loading, never a second visible world.
        public bool PlanetSurfaceOwnsGround { get; set; }

        public event Action<IslandHeightField> Generated;

        public void Generate(int seed)
        {
            if (_biome == null)
            {
                Debug.LogError($"[{nameof(IslandGenerator)}] No biome assigned.", this);
                return;
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            Clear();
            Field = _providedField ?? IslandHeightField.Build(_biome.Shape, seed);

            _root = new GameObject(RootName).transform;
            _root.SetParent(transform, false);
            if (_centerOnHub) _root.localPosition = -Field.HubCenter;
            // The edit-mode preview must not end up saved in the scene.
            if (!Application.isPlaying) _root.gameObject.hideFlags = HideFlags.DontSave;

            if (_projection == null || _buildPlanetGround) BuildTerrain();
            BuildWater();

            _growActive = _growIn && Application.isPlaying;
            _growLongestDelay = 0f;
            var batches = new PropBatches();
            PlaceCaves(batches);
            PlaceHubStones(batches);
            var scatterWork=ScatterPropsAsync(batches);while(scatterWork.MoveNext()) { }
            var batchWork=BuildBatchesAsync(batches);while(batchWork.MoveNext()) { }
            ProjectGenerated();
            if (_growActive) StartGrow();
            if (PlanetSurfaceOwnsGround) _root.gameObject.SetActive(false);

            Debug.Log($"[{nameof(IslandGenerator)}] Island seed {seed} built in {timer.ElapsedMilliseconds} ms " +
                      $"({Field.Lakes.Count} lakes, {Field.Rivers.Count} rivers, {Field.Caves.Count} caves).", this);
            Generated?.Invoke(Field);
        }

        public void Clear()
        {
            Field = null;
            foreach (UnityEngine.Object owned in _owned) DestroyGenerated(owned);
            _owned.Clear();
            _partsCache.Clear();

            if (_root == null) _root = transform.Find(RootName);
            if (_root != null) DestroyGenerated(_root.gameObject);
            _root = null;
        }

        /// <summary>World position on dry land near <paramref name="around"/>; the hub when nothing fits.</summary>
        public Vector3 RandomLandPoint(System.Random rng, Vector3 around, float radius, float minHeight = 0.4f)
        {
            if (Field == null) return around;

            Vector3 local = WorldToField(around);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                float distance = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                float x = local.x + Mathf.Cos(angle) * distance;
                float z = local.z + Mathf.Sin(angle) * distance;
                float h = Field.SampleHeight(x, z);
                if (h >= Field.Settings.SeaLevel + minHeight && Field.IsLand(x, z) && !Field.IsInsideCave(x, z))
                    return FieldToWorld(new Vector3(x, h, z));
            }
            return HubPosition;
        }

        public Vector3 FieldToWorld(Vector3 point) => _projection != null ? _projection.ToWorld(point, _planetCenter)
            : _root != null ? _root.TransformPoint(point) : transform.TransformPoint(point);
        public Vector3 WorldToField(Vector3 point) => _projection != null ? _projection.ToField(point, _planetCenter)
            : _root != null ? _root.InverseTransformPoint(point) : transform.InverseTransformPoint(point);

        public Vector3 HubPosition => Field == null ? transform.position : FieldToWorld(Field.HubCenter);

        /// <summary>World position of the island's own origin (its centre at sea level).</summary>
        public Vector3 IslandCenter => Field == null ? transform.position : FieldToWorld(Vector3.zero);

        /// <summary>Ground height under a world position, in world space.</summary>
        public float GroundHeight(Vector3 world)
        {
            if (Field == null) return transform.position.y;
            Vector3 local = _root.InverseTransformPoint(world);
            return _root.TransformPoint(new Vector3(local.x, Field.SampleHeight(local.x, local.z), local.z)).y;
        }

        private void Update()
        {
            if (!_growActive) return;

            float elapsed = Time.time - _growStartTime;
            Shader.SetGlobalFloat(GrowElapsedId, elapsed);
            if (elapsed > _growLongestDelay + _growDuration + 0.1f) StopGrow();
        }

        private void OnDestroy()
        {
            if (_growActive) StopGrow();
            foreach (UnityEngine.Object owned in _owned) DestroyGenerated(owned);
            _owned.Clear();
        }

        // ---------------------------------------------------------------- terrain and water

        private void BuildTerrain()
        {
            var terrain = new GameObject("Terrain").transform;
            terrain.SetParent(_root, false);

            int cells = Field.Settings.Resolution;
            for (int z0 = 0; z0 < cells; z0 += _chunkCells)
            for (int x0 = 0; x0 < cells; x0 += _chunkCells)
            {
                int x1 = Mathf.Min(x0 + _chunkCells, cells);
                int z1 = Mathf.Min(z0 + _chunkCells, cells);
                Mesh render = IslandMeshBuilder.BuildTerrainChunk(Field, _biome, x0, z0, x1, z1);
                if (render == null) continue;

                var go = new GameObject($"Chunk_{x0}_{z0}");
                go.transform.SetParent(terrain, false);
                go.AddComponent<MeshFilter>().sharedMesh = Own(render);
                go.AddComponent<MeshRenderer>().sharedMaterial = _biome.GroundMaterial;
                go.AddComponent<MeshCollider>().sharedMesh = Own(IslandMeshBuilder.BuildColliderChunk(Field, x0, z0, x1, z1));
            }
        }

        private void BuildWater()
        {
            if (_biome.WaterMaterial == null) return;
            _waterProperties = new MaterialPropertyBlock();
            var impacts = IslandMeshBuilder.BuildWaterImpacts(Field);
            var positions = new Vector4[IslandMeshBuilder.MaxWaterImpacts];
            var directions = new Vector4[IslandMeshBuilder.MaxWaterImpacts];
            for (int i=0;i<impacts.Count;i++)
            {
                Vector3 p=_root.TransformPoint(impacts[i].Position);
                Vector3 d=_root.TransformDirection(impacts[i].Direction).normalized;
                positions[i]=new Vector4(p.x,p.y,p.z,impacts[i].Radius);
                directions[i]=new Vector4(d.x,d.z,impacts[i].Ocean?1f:0f,1f);
            }
            _waterProperties.SetFloat("_LeewayWaterImpactCount",impacts.Count);
            _waterProperties.SetVectorArray("_LeewayWaterImpactPositions",positions);
            _waterProperties.SetVectorArray("_LeewayWaterImpactDirections",directions);

            Mesh surface = IslandMeshBuilder.BuildWaterSurface(Field);
            if (surface != null) AddWaterRenderer("Water", surface, _biome.WaterMaterial);
            if (_projection != null) return;
            GameObject openSea = AddWaterRenderer("OpenSea", IslandMeshBuilder.BuildSeaRing(_waterSize, Field.Settings.Size, Field.Settings.SeaLevel + 0.008f, Field.Settings.CellSize),
                _biome.WaterMaterial, meshCollider: false);

            // The open sea's triangles are kilometres long - a mesh collider of them is unstable in PhysX.
            // A thin box at sea level answers the same raycast; under the island it lies below the ground.
            var seaBox = openSea.AddComponent<BoxCollider>();
            seaBox.center = new Vector3(0f, Field.Settings.SeaLevel - 0.05f, 0f);
            seaBox.size = new Vector3(_waterSize, 0.1f, _waterSize);
        }

        private GameObject AddWaterRenderer(string name, Mesh mesh, Material material, bool meshCollider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = Own(mesh);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.SetPropertyBlock(_waterProperties);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            // The surface can be found by a raycast: creatures float on it. The Water layer collides with
            // nothing (see the layer matrix), so it is something to ask, not something to stand on.
            go.layer = WaterLayer;
            if (meshCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        private static int WaterLayer => Mathf.Max(0, LayerMask.NameToLayer("Water"));

        // ---------------------------------------------------------------- landmarks

        private void PlaceCaves(PropBatches batches)
        {
            if (_biome.CavePrefab == null) return;

            Transform parent = Group("Caves");
            foreach (IslandCave cave in Field.Caves)
            {
                // The cave prefab is authored for a 7 m radius; scale it to the carved footprint.
                float scale = cave.Radius / 7f;
                Matrix4x4 m = Matrix4x4.TRS(cave.Position, Quaternion.Euler(0f, cave.Yaw, 0f), new Vector3(scale * (_projection?.HorizontalScale ?? 1f), scale, scale * (_projection?.HorizontalScale ?? 1f)));
                Place(batches, _biome.CavePrefab, m, parent, combine: true);
            }
        }

        private void PlaceHubStones(PropBatches batches)
        {
            IReadOnlyList<GameObject> stones = _biome.HubStones;
            int count = _biome.HubStoneCount;
            if (stones == null || stones.Count == 0 || count == 0) return;

            Transform parent = Group("HubStones");
            var rng = new System.Random(Field.Seed + 977);
            Vector3 hub = Field.HubCenter;
            float radius = Field.Settings.HubRadius + 1.5f;
            // The gap faces the island centre - the way out of the den toward the rest of the island.
            float gapAngle = Mathf.Atan2(-hub.z, -hub.x);

            for (int i = 0; i < count; i++)
            {
                float a = gapAngle + Mathf.PI * 2f * (i + 1) / (count + 2);
                var pos = new Vector3(hub.x + Mathf.Cos(a) * radius, 0f, hub.z + Mathf.Sin(a) * radius);
                pos.y = Field.SampleHeight(pos.x, pos.z) - 0.2f;
                float yaw = -a * Mathf.Rad2Deg + 90f + ((float)rng.NextDouble() - 0.5f) * 20f;
                float scale = 1.1f + (float)rng.NextDouble() * 0.7f;
                GameObject prefab = stones[rng.Next(stones.Count)];
                if (prefab == null) continue;
                Place(batches, prefab, Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale), parent, combine: true);
            }
        }

        // ---------------------------------------------------------------- props

        private System.Collections.IEnumerator ScatterPropsAsync(PropBatches batches)
        {
            IReadOnlyList<IslandBiome.ScatterLayer> layers = _biome.Layers;
            var rules = new List<ScatterRule>(layers.Count);
            var variantCounts = new List<int>(layers.Count);
            foreach (IslandBiome.ScatterLayer layer in layers)
            {
                rules.Add(layer.Rule);
                variantCounts.Add(layer.Prefabs?.Count ?? 0);
            }

            var parents = new Transform[layers.Count];
            for (int i = 0; i < layers.Count; i++)
                parents[i] = Group(string.IsNullOrEmpty(layers[i].Name) ? $"Layer{i}" : layers[i].Name);

            var field=Field;
            var plan=System.Threading.Tasks.Task.Run(()=>IslandScatter.Plan(field,rules,variantCounts));
            while(!plan.IsCompleted)yield return null;
            if(plan.IsFaulted)throw plan.Exception.GetBaseException();
            var workClock=System.Diagnostics.Stopwatch.StartNew();
            foreach (ScatterPoint point in plan.Result)
            {
                if(workClock.Elapsed.TotalMilliseconds>2){yield return null;workClock.Restart();}
                IslandBiome.ScatterLayer layer = layers[point.Rule];
                GameObject prefab = layer.Prefabs[point.Variant];
                if (prefab == null) continue;

                Vector3 up = Vector3.Slerp(Vector3.up, point.Normal, layer.AlignToNormal);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, point.Yaw, 0f);
                Vector3 position = point.Position - up * (layer.Sink * point.Scale);
                Place(batches, prefab, Matrix4x4.TRS(position, rotation, Vector3.one * point.Scale), parents[point.Rule], layer.CombineMeshes, layer.WindStrength);
            }
        }

        /// <summary>Adds a prefab at a root-local transform: merged visuals plus, if needed, a renderer-less instance for colliders and lights.</summary>
        private void Place(PropBatches batches, GameObject prefab, Matrix4x4 rootLocal, Transform parent, bool combine, float windStrength = 0f)
        {
            if (!combine)
            {
                GameObject full = Instantiate(prefab, parent);
                LodConvention.Ensure(full);
                SetLocal(full.transform, rootLocal);
                return;
            }

            PrefabParts parts = PartsOf(prefab);
            Vector3 pivot = rootLocal.GetColumn(3);
            int chunk = ChunkOf(pivot);
            float growDelay = GrowDelay(pivot);
            foreach (PrefabParts.Part part in parts.Parts)
                batches.Add(chunk, part.Lod, part.Material, part.CastShadows, new PropBatches.Instance
                {
                    Mesh = part.Mesh,
                    SubMesh = part.SubMesh,
                    Transform = rootLocal * part.LocalToPrefab,
                    Pivot = pivot,
                    GrowDelay = growDelay,
                    WindStrength = windStrength,
                });

            if (parts.Proxy != null)
            {
                GameObject proxy = Instantiate(parts.Proxy, parent);
                SetLocal(proxy.transform, rootLocal);
                proxy.SetActive(true);
            }
        }

        private System.Collections.IEnumerator BuildBatchesAsync(PropBatches batches)
        {
            Transform parent = Group("PropMeshes");
            var chunkParents = new Dictionary<int, Transform>();
            var chunkRenderers = new Dictionary<int, List<(Renderer Renderer, int Lod)>>();
            var sources = new Dictionary<(Mesh, int), SubMeshSource>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var grow = new List<Vector4>();
            var wind = new List<Vector2>();
            var triangles = new List<int>();

            foreach (KeyValuePair<PropBatches.Key, List<PropBatches.Instance>> batch in batches.All)
            {
                vertices.Clear();
                normals.Clear();
                uvs.Clear();
                grow.Clear();
                wind.Clear();
                triangles.Clear();

                // Own merge instead of Mesh.CombineMeshes: every vertex also carries its prop's pivot
                // and grow delay in UV2, which Leeway/Toon uses to grow the props in one by one.
                foreach (PropBatches.Instance instance in batch.Value)
                {
                    if (!sources.TryGetValue((instance.Mesh, instance.SubMesh), out SubMeshSource source))
                        sources[(instance.Mesh, instance.SubMesh)] = source = new SubMeshSource(instance.Mesh, instance.SubMesh);

                    int start = vertices.Count;
                    var growData = new Vector4(instance.Pivot.x, instance.Pivot.y, instance.Pivot.z,
                        _growActive ? instance.GrowDelay + 2f : 0f);
                    for (int v = 0; v < source.Vertices.Length; v++)
                    {
                        vertices.Add(instance.Transform.MultiplyPoint3x4(source.Vertices[v]));
                        normals.Add(instance.Transform.inverse.transpose.MultiplyVector(source.Normals[v]).normalized);
                        uvs.Add(source.Uvs[v]);
                        grow.Add(growData);
                        wind.Add(new Vector2(instance.WindStrength, 17f));
                    }
                    foreach (int index in source.Triangles) triangles.Add(start + index);
                }

                var mesh = new Mesh { name = $"Props_{batch.Key.Chunk}_{batch.Key.Material.name}", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(2, grow);
                mesh.SetUVs(3, wind);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                var padded = mesh.bounds;
                padded.Expand(4f); // Include the maximum visual sway in culling bounds.
                mesh.bounds = padded;

                // Each chunk is one object, so that its levels of detail can be switched together.
                if (!chunkParents.TryGetValue(batch.Key.Chunk, out Transform chunkParent))
                {
                    chunkParent = new GameObject("Chunk " + batch.Key.Chunk).transform;
                    chunkParent.SetParent(parent, false);
                    chunkParents[batch.Key.Chunk] = chunkParent;
                    chunkRenderers[batch.Key.Chunk] = new List<(Renderer, int)>();
                }

                var go = new GameObject(mesh.name);
                go.transform.SetParent(chunkParent, false);
                go.AddComponent<MeshFilter>().sharedMesh = Own(mesh);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = batch.Key.Material;
                renderer.shadowCastingMode = batch.Key.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                chunkRenderers[batch.Key.Chunk].Add((renderer, batch.Key.Lod));
                yield return null;
            }

            foreach (KeyValuePair<int, List<(Renderer Renderer, int Lod)>> chunk in chunkRenderers)
            { GroupLevels(chunkParents[chunk.Key], chunk.Value); yield return null; }
        }

        // The merged meshes of a chunk are one LODGroup when the models in it come with levels of detail: far from the
        // camera the chunk shows the simpler meshes of all of them at once. Models with no levels are in every level.
        private static void GroupLevels(Transform chunk, List<(Renderer Renderer, int Lod)> renderers)
        {
            var levels = new SortedSet<int>();
            foreach ((Renderer _, int lod) in renderers) if (lod != LodConvention.Shared) levels.Add(lod);
            if (levels.Count < 2) return;

            var order = new List<int>(levels);
            LodProfile profile = LodProfile.Current;
            var lods = new LOD[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var members = new List<Renderer>();
                foreach ((Renderer renderer, int lod) in renderers)
                    if (lod == order[i] || lod == LodConvention.Shared) members.Add(renderer);
                lods[i] = new LOD(i < order.Count - 1 ? profile.TransitionAfter(i) : 0f, members.ToArray());
            }

            var group = chunk.gameObject.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.fadeMode = LODFadeMode.None;
            group.RecalculateBounds();
        }

        private PrefabParts PartsOf(GameObject prefab)
        {
            if (_partsCache.TryGetValue(prefab, out PrefabParts cached)) return cached;

            var parts = new PrefabParts();
            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
            Dictionary<Renderer, int> levels = LodConvention.LevelsOf(prefab);
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer renderer)) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount && sub < materials.Length; sub++)
                {
                    if (materials[sub] == null) continue;
                    parts.Parts.Add(new PrefabParts.Part
                    {
                        Mesh = filter.sharedMesh,
                        SubMesh = sub,
                        Material = materials[sub],
                        CastShadows = renderer.shadowCastingMode != ShadowCastingMode.Off,
                        LocalToPrefab = rootInverse * filter.transform.localToWorldMatrix,
                        Lod = levels.TryGetValue(renderer, out int level) ? level : LodConvention.Shared,
                    });
                }
            }

            bool needsProxy = prefab.GetComponentInChildren<Collider>(true) != null || prefab.GetComponentInChildren<Light>(true) != null;
            if (needsProxy)
            {
                // An inactive, renderer-less template: instancing it costs only colliders and lights.
                GameObject proxy = Instantiate(prefab, _root);
                proxy.name = prefab.name;
                proxy.SetActive(false);
                foreach (MeshRenderer r in proxy.GetComponentsInChildren<MeshRenderer>(true)) DestroyImmediate(r);
                foreach (MeshFilter f in proxy.GetComponentsInChildren<MeshFilter>(true)) DestroyImmediate(f);
                foreach (LODGroup g in proxy.GetComponentsInChildren<LODGroup>(true)) DestroyImmediate(g);
                parts.Proxy = proxy;
            }

            _partsCache[prefab] = parts;
            return parts;
        }

        /// <summary>Seconds after generation at which a prop starts growing: the wave reaches it, plus jitter.</summary>
        private float GrowDelay(Vector3 rootLocalPivot)
        {
            if (!_growActive) return 0f;

            float dx = rootLocalPivot.x - Field.HubCenter.x;
            float dz = rootLocalPivot.z - Field.HubCenter.z;
            float hash = Mathf.Sin(rootLocalPivot.x * 12.9898f + rootLocalPivot.z * 78.233f) * 43758.5453f;
            float delay = Mathf.Sqrt(dx * dx + dz * dz) / _growWaveSpeed + (hash - Mathf.Floor(hash)) * _growJitter;
            _growLongestDelay = Mathf.Max(_growLongestDelay, delay);
            return delay;
        }

        private void StartGrow()
        {
            _growStartTime = Time.time;
            Shader.SetGlobalFloat(GrowDurationId, _growDuration);
            Shader.SetGlobalFloat(GrowElapsedId, 0f);
            Shader.SetGlobalFloat(GrowActiveId, 1f);
        }

        private void StopGrow()
        {
            _growActive = false;
            Shader.SetGlobalFloat(GrowActiveId, 0f);
        }

        private int ChunkOf(Vector3 rootLocalPosition)
        {
            float half = Field.Settings.HalfSize;
            int perRow = Mathf.CeilToInt(Field.Settings.Size / PropChunkSize);
            int cx = Mathf.Clamp(Mathf.FloorToInt((rootLocalPosition.x + half) / PropChunkSize), 0, perRow - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((rootLocalPosition.z + half) / PropChunkSize), 0, perRow - 1);
            return cz * perRow + cx;
        }

        private Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root, false);
            return t;
        }

        private static void SetLocal(Transform t, Matrix4x4 m)
        {
            t.SetLocalPositionAndRotation(m.GetColumn(3), m.rotation);
            t.localScale = m.lossyScale;
        }

        private T Own<T>(T obj) where T : UnityEngine.Object
        {
            if (!Application.isPlaying) obj.hideFlags = HideFlags.DontSave;
            _owned.Add(obj);
            return obj;
        }

        private static void DestroyGenerated(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private sealed class PrefabParts
        {
            public struct Part
            {
                public Mesh Mesh;
                public int SubMesh;
                public Material Material;
                public bool CastShadows;
                public Matrix4x4 LocalToPrefab;
                /// <summary>Level of detail it belongs to (see LodConvention), or LodConvention.Shared.</summary>
                public int Lod;
            }

            public readonly List<Part> Parts = new();
            public GameObject Proxy;
        }

        private sealed class PropBatches
        {
            public readonly struct Key : IEquatable<Key>
            {
                public readonly int Chunk;
                public readonly int Lod;
                public readonly Material Material;
                public readonly bool CastShadows;

                public Key(int chunk, int lod, Material material, bool castShadows)
                {
                    Chunk = chunk;
                    Lod = lod;
                    Material = material;
                    CastShadows = castShadows;
                }

                public bool Equals(Key other) => Chunk == other.Chunk && Lod == other.Lod && Material == other.Material && CastShadows == other.CastShadows;
                public override bool Equals(object obj) => obj is Key other && Equals(other);
                public override int GetHashCode() => HashCode.Combine(Chunk, Lod, Material.GetHashCode(), CastShadows);
            }

            public struct Instance
            {
                public Mesh Mesh;
                public int SubMesh;
                public Matrix4x4 Transform;
                public Vector3 Pivot;
                public float GrowDelay;
                public float WindStrength;
            }

            public readonly Dictionary<Key, List<Instance>> All = new();

            public void Add(int chunk, int lod, Material material, bool castShadows, Instance instance)
            {
                var key = new Key(chunk, lod, material, castShadows);
                if (!All.TryGetValue(key, out List<Instance> list)) All[key] = list = new List<Instance>();
                list.Add(instance);
            }
        }

        /// <summary>One submesh's vertices, compacted to the ones its triangles actually use.</summary>
        private sealed class SubMeshSource
        {
            public readonly Vector3[] Vertices;
            public readonly Vector3[] Normals;
            public readonly Vector2[] Uvs;
            public readonly int[] Triangles;

            public SubMeshSource(Mesh mesh, int subMesh)
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                int[] source = mesh.GetTriangles(subMesh);

                var remap = new Dictionary<int, int>();
                Triangles = new int[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    if (!remap.TryGetValue(source[i], out int mapped)) remap[source[i]] = mapped = remap.Count;
                    Triangles[i] = mapped;
                }

                Vertices = new Vector3[remap.Count];
                Normals = new Vector3[remap.Count];
                Uvs = new Vector2[remap.Count];
                foreach (KeyValuePair<int, int> pair in remap)
                {
                    Vertices[pair.Value] = vertices[pair.Key];
                    Normals[pair.Value] = normals.Length > 0 ? normals[pair.Key] : Vector3.up;
                    Uvs[pair.Value] = uvs.Length > 0 ? uvs[pair.Key] : Vector2.zero;
                }
            }
        }
    }
}
