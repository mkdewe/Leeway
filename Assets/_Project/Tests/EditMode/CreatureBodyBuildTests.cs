using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Tests for the body build pipeline. They aim at skinning — the most fragile part of the project,
    /// where a defect does not break the compile, it quietly turns the creature into spaghetti.
    /// </summary>
    /// <remarks>
    /// They deliberately run on <b>real assets</b> (the part catalog, the build settings) rather than on
    /// fixtures: the point is that breaking the project's data should be as loud as breaking the code.
    /// The purely logical tests live in the other files and touch nothing of Unity beyond the maths.
    /// </remarks>
    public class CreatureBodyBuildTests
    {
        private const string CatalogPath = "Assets/_Project/Features/CreatureEditor/Data/PartCatalog.asset";
        private const string SettingsPath = "Assets/_Project/Features/CreatureEditor/Data/BodyBuildSettings.asset";

        private CreaturePartCatalog _catalog;
        private CreatureBodyBuildSettings _settings;
        private PartRuleSet _rules;
        private GameObject _root;
        private BuiltCreatureBody _built;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<CreaturePartCatalog>(CatalogPath);
            _settings = AssetDatabase.LoadAssetAtPath<CreatureBodyBuildSettings>(SettingsPath);

            Assert.IsNotNull(_catalog, $"No part catalog at {CatalogPath}.");
            Assert.IsNotNull(_settings, $"No build settings at {SettingsPath}.");

            _rules = _catalog.BuildRuleSet();
            _root = new GameObject("TestCreatureRoot");
        }

        [TearDown]
        public void TearDown()
        {
            _built?.Dispose();
            _built = null;

            if (_root != null) Object.DestroyImmediate(_root);
        }

        private BuiltCreatureBody BuildStarter(int seed = 1)
        {
            CreatureGenome genome = StarterGenomeFactory.Create(seed, _rules);
            _built = CreatureBodyBuilder.Build(genome, _catalog, _settings, _root.transform, CreatureStatTuning.Default);
            Assert.IsNotNull(_built, "The builder returned null for a valid starter genome.");
            return _built;
        }

        [Test]
        public void Build_RigCountsAgree()
        {
            CreatureGenome genome = StarterGenomeFactory.Create(1, _rules);
            _built = CreatureBodyBuilder.Build(genome, _catalog, _settings, _root.transform, CreatureStatTuning.Default);

            Assert.AreEqual(genome.VertebraCount, _built.Bones.Length, "The bone count does not match the vertebra count.");
            Assert.AreEqual(_built.Bones.Length, _built.Renderer.bones.Length, "The SkinnedMeshRenderer got a different bone count than the rig.");
            Assert.AreEqual(_built.Bones.Length, _built.GeneratedMesh.bindposes.Length, "The bindpose count does not match the bone count.");
        }

        /// <summary>
        /// The heart of correct skinning: in the rest pose every bone's skinning matrix has to be the
        /// identity. When it is not, the creature is bent from the moment it is born — and that is
        /// exactly the kind of defect that raises no error at all.
        /// </summary>
        [Test]
        public void Build_BindPoses_AreInverseOfRestPose()
        {
            BuildStarter();

            Matrix4x4 rootToWorld = _root.transform.localToWorldMatrix;
            Matrix4x4[] bindposes = _built.GeneratedMesh.bindposes;

            for (int i = 0; i < _built.Bones.Length; i++)
            {
                Matrix4x4 boneToRoot = rootToWorld.inverse * _built.Bones[i].localToWorldMatrix;
                Matrix4x4 skin = boneToRoot * bindposes[i];

                for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    Assert.AreEqual(Matrix4x4.identity[r, c], skin[r, c], 1e-4f,
                        $"Bone {i}: the skinning matrix in the rest pose is not the identity ([{r},{c}]).");
            }
        }

        /// <summary>
        /// Proof that the weights really were assigned to the right bones: rotating the last bone has
        /// to move the tail and <b>not</b> move the head. Skinning is computed by hand rather than via
        /// <c>BakeMesh</c> — we are testing the data in the mesh, not Unity's implementation.
        /// </summary>
        [Test]
        public void Build_RotatingTailBone_MovesTailButNotHead()
        {
            BuildStarter();

            Vector3[] rest = SkinToRootSpace();
            _built.Bones[_built.Bones.Length - 1].localRotation *= Quaternion.Euler(60f, 0f, 0f);
            Vector3[] bent = SkinToRootSpace();

            Mesh mesh = _built.GeneratedMesh;
            BoneWeight[] weights = mesh.boneWeights;
            int lastBone = _built.Bones.Length - 1;

            float maxHeadShift = 0f;
            float maxTailShift = 0f;

            for (int i = 0; i < rest.Length; i++)
            {
                float shift = Vector3.Distance(rest[i], bent[i]);
                float tailInfluence = InfluenceOf(weights[i], lastBone);

                if (tailInfluence <= 0f) maxHeadShift = Mathf.Max(maxHeadShift, shift);
                else if (tailInfluence >= 0.99f) maxTailShift = Mathf.Max(maxTailShift, shift);
            }

            Assert.Less(maxHeadShift, 1e-5f, "Rotating the tail bone moved vertices that do not belong to it at all.");
            Assert.Greater(maxTailShift, 0.05f, "Rotating the tail bone did not move the vertices fully assigned to it.");
        }

        [Test]
        public void Build_TriangleWinding_AgreesWithNormals()
        {
            BuildStarter();

            Mesh mesh = _built.GeneratedMesh;
            Vector3[] verts = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] tris = mesh.triangles;

            int flipped = 0;
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];

                Vector3 geometric = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (geometric.sqrMagnitude < 1e-12f) continue; // a degenerate triangle — says nothing either way

                Vector3 shaded = normals[a] + normals[b] + normals[c];
                if (Vector3.Dot(geometric.normalized, shaded.normalized) <= 0f) flipped++;
            }

            Assert.AreEqual(0, flipped, $"{flipped} triangles are wound against their own normals — they will vanish under backface culling.");
        }

        /// <summary>
        /// The whole commit model rests on the same genome giving every player the same body. Any
        /// <c>Random</c> or dependence on time inside the builder drifts the clients apart without a
        /// single error message.
        /// </summary>
        [Test]
        public void Build_IsPureFunctionOfItsInputs()
        {
            CreatureGenome genome = StarterGenomeFactory.Create(7, _rules);

            var firstRoot = new GameObject("First");
            var secondRoot = new GameObject("Second");
            BuiltCreatureBody first = null, second = null;

            try
            {
                first = CreatureBodyBuilder.Build(genome, _catalog, _settings, firstRoot.transform, CreatureStatTuning.Default);
                second = CreatureBodyBuilder.Build(genome, _catalog, _settings, secondRoot.transform, CreatureStatTuning.Default);

                Vector3[] a = first.GeneratedMesh.vertices;
                Vector3[] b = second.GeneratedMesh.vertices;
                Assert.AreEqual(a.Length, b.Length, "Two builds of the same genome gave a different vertex count.");

                float maxDiff = 0f;
                for (int i = 0; i < a.Length; i++) maxDiff = Mathf.Max(maxDiff, Vector3.Distance(a[i], b[i]));
                Assert.AreEqual(0f, maxDiff, "Two builds of the same genome gave different vertices.");

                Assert.AreEqual(first.Stats.MaxHp, second.Stats.MaxHp, 1e-6f);
                Assert.AreEqual(first.Stats.Mass, second.Stats.Mass, 1e-6f);
                Assert.AreEqual(first.PartInstances.Length, second.PartInstances.Length);
            }
            finally
            {
                first?.Dispose();
                second?.Dispose();
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void Build_MeshContainsNoInvalidNumbers()
        {
            BuildStarter();

            Mesh mesh = _built.GeneratedMesh;
            foreach (Vector3 v in mesh.vertices)
                Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.magnitude),
                    $"A vertex contains NaN/Inf: {v}.");

            foreach (Vector3 n in mesh.normals)
                Assert.AreEqual(1f, n.magnitude, 1e-2f, $"A normal is not normalised: {n}.");
        }

        [Test]
        public void Build_BoneWeights_SumToOneAndTargetExistingBones()
        {
            BuildStarter();

            int boneCount = _built.Bones.Length;
            foreach (BoneWeight w in _built.GeneratedMesh.boneWeights)
            {
                float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                Assert.AreEqual(1f, sum, 1e-4f, "Bone weights do not sum to 1.");

                if (w.weight0 > 0f) Assert.Less(w.boneIndex0, boneCount, "A weight points at a bone that does not exist.");
                if (w.weight1 > 0f) Assert.Less(w.boneIndex1, boneCount, "A weight points at a bone that does not exist.");
            }
        }

        /// <summary>
        /// A mirrored part yields <b>two</b> instances under the same bone — the genome describes a
        /// pair of legs with one gene. The test guards that ratio and the binding to the bone.
        /// </summary>
        [Test]
        public void Build_PartInstances_MatchGenomeIncludingMirroredPairs()
        {
            CreatureGenome genome = StarterGenomeFactory.Create(3, _rules);
            _built = CreatureBodyBuilder.Build(genome, _catalog, _settings, _root.transform, CreatureStatTuning.Default);

            int expected = 0;
            int cursor = 0;

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene gene = genome.GetPart(i);
                if (!_catalog.TryGetPart(gene.PartId, out CreaturePartDefinition definition) || definition?.Prefab == null)
                    continue; // a missing prefab is logged and skipped, it does not break the build

                int spawned = gene.Mirrored && definition.MirrorCapable ? 2 : 1;
                expected += spawned;

                for (int s = 0; s < spawned; s++)
                {
                    CreaturePartInstance instance = _built.PartInstances[cursor + s];
                    Assert.AreSame(_built.Bones[gene.BoneIndex], instance.Object.transform.parent,
                        $"Part \"{definition.PartKey}\" hangs under a different bone than the genome declares.");
                    Assert.AreEqual(i, instance.GeneIndex,
                        $"Instance \"{definition.PartKey}\" points at the wrong gene — the editor would grab the wrong part.");
                }

                if (spawned == 2)
                {
                    float rightX = _built.PartInstances[cursor].Object.transform.localPosition.x;
                    float leftX = _built.PartInstances[cursor + 1].Object.transform.localPosition.x;
                    Assert.AreEqual(-rightX, leftX, 1e-5f,
                        $"The pair \"{definition.PartKey}\" is not mirrored about the YZ plane.");
                }

                cursor += spawned;
            }

            Assert.AreEqual(expected, _built.PartInstances.Length, "The part instance count does not match the genome.");
        }

        /// <summary>
        /// Mirroring has to go through negating the position and the quaternion, never through negative
        /// scale — that reverses triangle winding, breaks lighting and turns colliders inside out.
        /// </summary>
        [Test]
        public void Build_MirroredParts_NeverUseNegativeScale()
        {
            BuildStarter(3);

            foreach (CreaturePartInstance part in _built.PartInstances)
            {
                GameObject instance = part.Object;
                if (instance == null) continue;

                Vector3 scale = instance.transform.localScale;
                Assert.Greater(scale.x, 0f, $"\"{instance.name}\" has a negative X scale.");
                Assert.Greater(scale.y, 0f, $"\"{instance.name}\" has a negative Y scale.");
                Assert.Greater(scale.z, 0f, $"\"{instance.name}\" has a negative Z scale.");
                Assert.AreEqual(scale.x, scale.y, 1e-5f, $"\"{instance.name}\" has a non-uniform scale.");
                Assert.AreEqual(scale.x, scale.z, 1e-5f, $"\"{instance.name}\" has a non-uniform scale.");
            }
        }

        /// <summary>
        /// The caps have to be hemispheres of the end vertebra's radius. This is not a stylistic
        /// choice: <see cref="PartPlacement"/> measures the body as a capsule, so any other tip means
        /// parts floating in the air in front of the nose or sunk inside it.
        /// </summary>
        [Test]
        public void Build_Caps_AreHemispheresOfTheEndVertebra()
        {
            CreatureGenome genome = StarterGenomeFactory.Create(1, _rules);
            _built = CreatureBodyBuilder.Build(genome, _catalog, _settings, _root.transform, CreatureStatTuning.Default);

            Matrix4x4[] boneToRoot = CreatureRigBuilder.ComputeBoneToRoot(genome);
            int last = genome.VertebraCount - 1;

            Vector3 head = boneToRoot[0].GetColumn(3);
            Vector3 tail = boneToRoot[last].GetColumn(3);

            AssertCapIsSpherical(head, (head - (Vector3)boneToRoot[1].GetColumn(3)).normalized,
                genome.GetVertebra(0).Radius, "head");

            AssertCapIsSpherical(tail, (tail - (Vector3)boneToRoot[last - 1].GetColumn(3)).normalized,
                genome.GetVertebra(last).Radius, "tail");
        }

        /// <summary>
        /// Checks that every vertex sticking out beyond the end vertebra lies exactly on a sphere of
        /// its radius.
        /// </summary>
        private void AssertCapIsSpherical(Vector3 center, Vector3 outward, float radius, string which)
        {
            int checkedVertices = 0;

            foreach (Vector3 vertex in _built.GeneratedMesh.vertices)
            {
                if (Vector3.Dot(vertex - center, outward) <= 1e-4f) continue;

                Assert.AreEqual(radius, Vector3.Distance(vertex, center), 1e-3f,
                    $"A vertex of the {which} cap does not lie on the sphere of the end vertebra.");
                checkedVertices++;
            }

            Assert.Greater(checkedVertices, 0, $"The {which} cap has not a single vertex — the body is cut off.");
        }

        [Test]
        public void Build_Collider_EnclosesSpine()
        {
            var capsule = _root.AddComponent<CapsuleCollider>();
            CreatureGenome genome = StarterGenomeFactory.Create(1, _rules);

            _built = CreatureBodyBuilder.Build(genome, _catalog, _settings, _root.transform, CreatureStatTuning.Default);
            CreatureColliderFitter.Fit(capsule, genome, CreatureRigBuilder.ComputeBoneToRoot(genome), _settings);

            Assert.AreEqual(2, capsule.direction, "The capsule has to lie along Z — the spine runs on that axis.");
            Assert.Greater(capsule.radius, 0f, "A capsule of zero radius will fall through the floor.");

            float maxRadius = 0f;
            for (int i = 0; i < genome.VertebraCount; i++) maxRadius = Mathf.Max(maxRadius, genome.GetVertebra(i).Radius);
            Assert.GreaterOrEqual(capsule.radius, maxRadius - 1e-4f, "The capsule is narrower than the thickest vertebra.");
        }

        [Test]
        public void Build_ThenDispose_ReleasesGeneratedObjects()
        {
            BuildStarter();

            Transform armature = _built.Armature;
            SkinnedMeshRenderer renderer = _built.Renderer;

            _built.Dispose();
            _built = null;

            Assert.IsTrue(armature == null, "The armature survived Dispose — the next commit would add a second skeleton.");
            Assert.IsTrue(renderer == null, "The SkinnedMeshRenderer survived Dispose.");
        }

        /// <summary>Linear blend skinning computed by hand, in root space.</summary>
        private Vector3[] SkinToRootSpace()
        {
            Mesh mesh = _built.GeneratedMesh;
            Vector3[] verts = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindposes = mesh.bindposes;
            Matrix4x4 worldToRoot = _root.transform.worldToLocalMatrix;

            var skinned = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                BoneWeight w = weights[i];
                Vector3 acc = Vector3.zero;

                acc += Skin(verts[i], w.boneIndex0, w.weight0, bindposes, worldToRoot);
                acc += Skin(verts[i], w.boneIndex1, w.weight1, bindposes, worldToRoot);
                acc += Skin(verts[i], w.boneIndex2, w.weight2, bindposes, worldToRoot);
                acc += Skin(verts[i], w.boneIndex3, w.weight3, bindposes, worldToRoot);

                skinned[i] = acc;
            }
            return skinned;
        }

        private Vector3 Skin(Vector3 vertex, int boneIndex, float weight, Matrix4x4[] bindposes, Matrix4x4 worldToRoot)
        {
            if (weight <= 0f) return Vector3.zero;

            Matrix4x4 boneToRoot = worldToRoot * _built.Bones[boneIndex].localToWorldMatrix;
            return (boneToRoot * bindposes[boneIndex]).MultiplyPoint3x4(vertex) * weight;
        }

        private static float InfluenceOf(BoneWeight w, int boneIndex)
        {
            float sum = 0f;
            if (w.boneIndex0 == boneIndex) sum += w.weight0;
            if (w.boneIndex1 == boneIndex) sum += w.weight1;
            if (w.boneIndex2 == boneIndex) sum += w.weight2;
            if (w.boneIndex3 == boneIndex) sum += w.weight3;
            return sum;
        }
    }
}
