using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Turns a genome into a living object: bones, a skinned mesh, a fitted capsule and the attached
    /// parts.
    /// </summary>
    /// <remarks>
    /// <b>A hard requirement: this has to be a pure function of its inputs.</b> No <c>Random</c>, no
    /// <c>Time</c>, no dependence on frame order. The whole commit model rests on the same genome
    /// producing an identical body on the server and on every client — if the build were even slightly
    /// non-deterministic, players would see different creatures. Deliberately a plain C# class, not a
    /// MonoBehaviour.
    /// </remarks>
    public static class CreatureBodyBuilder
    {
        public const string BodyObjectName = "Body";

        public static BuiltCreatureBody Build(CreatureGenome genome, CreaturePartCatalog catalog,
            CreatureBodyBuildSettings settings, Transform root, CreatureStatTuning tuning)
        {
            if (genome == null || root == null) return null;

            PartRuleSet rules = catalog != null ? catalog.BuildRuleSet() : PartRuleSet.Empty;
            CreatureStats stats = GenomeStatRules.Derive(genome, rules, tuning);

            Matrix4x4[] boneToRoot = CreatureRigBuilder.ComputeBoneToRoot(genome);
            Transform[] bones = CreatureRigBuilder.Build(genome, root, out Transform armature);

            Mesh mesh = SpineMeshGenerator.Generate(genome, boneToRoot, settings, out Matrix4x4[] bindposes);
            mesh.bindposes = bindposes;

            SkinnedMeshRenderer renderer = CreateRenderer(root, genome, mesh, bones, settings);

            CreaturePartInstance[] parts = CreaturePartInstantiator.Instantiate(genome, bones, catalog,
                settings != null ? settings.SkinPalette : null);

            return new BuiltCreatureBody(armature, bones, renderer, mesh, parts, stats);
        }

        private static SkinnedMeshRenderer CreateRenderer(Transform root, CreatureGenome genome, Mesh mesh,
            Transform[] bones, CreatureBodyBuildSettings settings)
        {
            var bodyObject = new GameObject(BodyObjectName);
            bodyObject.transform.SetParent(root, false);

            // Identity relative to the root is required: the bindposes are computed as inverses of the
            // bone→root matrices, so the renderer's transform has to coincide with the root.
            bodyObject.transform.localPosition = Vector3.zero;
            bodyObject.transform.localRotation = Quaternion.identity;
            bodyObject.transform.localScale = Vector3.one;

            var renderer = bodyObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = bones.Length > 0 ? bones[0] : root;

            // Explicit, padded bounds — the default ones cull the creature at the worst moments during
            // a ragdoll, when the bones travel far outside the rest pose.
            renderer.updateWhenOffscreen = false;
            renderer.localBounds = mesh.bounds;

            if (settings != null && settings.BodyMaterial != null)
            {
                renderer.sharedMaterial = settings.BodyMaterial;
                ApplyGenomeSkin(renderer, genome, settings.SkinPalette);
            }

            return renderer;
        }

        /// <summary>
        /// The skin the genome asks for: its colour and its coat pattern.
        /// </summary>
        /// <remarks>
        /// Through a <see cref="MaterialPropertyBlock"/> so the shared body material is not multiplied
        /// into one instance per creature — with a dozen creatures on a map that is the difference
        /// between one draw call's worth of material and a dozen.
        /// </remarks>
        private static void ApplyGenomeSkin(Renderer renderer, CreatureGenome genome, CreatureSkinPalette palette)
        {
            SkinPattern pattern = palette != null ? palette.Get(genome.BodyPattern) : null;
            CreatureSkinPainter.Paint(renderer, genome.PrimaryColor, pattern);
        }
    }
}
