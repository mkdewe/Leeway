using System.Collections.Generic;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The left-hand version of a mesh: the same shape reflected across its own X axis.
    /// </summary>
    /// <remarks>
    /// <para><b>Why not simply scale by −1.</b> A negative scale flips the winding of every triangle,
    /// so the faces the camera should see are culled and the ones behind them are drawn; the normals
    /// point into the model and the lighting inverts; and colliders come out inside out. That is why
    /// the instantiator forbids it. Reflecting the <b>geometry</b> has none of those problems: the
    /// winding is reversed deliberately, so the result is an honest left-hand model.</para>
    ///
    /// <para><b>Why it is needed at all.</b> The part models are right-hand: a right arm, a right hind
    /// leg. Instantiating the same mesh on the left gives a creature with two right legs — noticeable
    /// on a hand at once, and on a leg as soon as it bends the wrong way.</para>
    ///
    /// <para>Cached per source mesh, because a creature with four legs and two arms would otherwise
    /// build the same reflection six times, and again on every rebuild.</para>
    /// </remarks>
    public static class MirroredMeshCache
    {
        private static readonly Dictionary<Mesh, Mesh> Mirrored = new();

        /// <summary>The mirrored twin of a mesh, built on first use.</summary>
        public static Mesh Of(Mesh source)
        {
            if (source == null) return null;

            if (Mirrored.TryGetValue(source, out Mesh cached) && cached != null) return cached;

            Mesh mirror = Build(source);
            Mirrored[source] = mirror;

            return mirror;
        }

        /// <summary>
        /// Turns a part instance into its own reflection: every mesh <b>and</b> everything the part is
        /// built out of.
        /// </summary>
        /// <remarks>
        /// <para><b>Mirroring the meshes alone is not mirroring the part.</b> A reflection has to reach
        /// the whole assembly: reflecting a mesh flips its geometry inside its own object, but the
        /// object itself stays where it was. On a part built from one centred mesh nobody notices; on a
        /// limb — whose model hangs off-centre and whose socket sits at the ankle — the leg came out
        /// reflected while the hand and the foot stayed on the right-hand side of it, which is exactly
        /// what a left leg with a right-hand foot looks like.</para>
        ///
        /// <para>So each node's local transform is conjugated by the reflection: the offset's <c>x</c>
        /// changes sign, and so do the <c>y</c> and <c>z</c> of its rotation. Composed down the
        /// hierarchy with each mesh reflected in its own space, that is a true reflection of everything
        /// the part is made of — including whatever was fitted into it.</para>
        ///
        /// <para>The root is deliberately left alone: where the mirrored part sits on the body is worked
        /// out by <c>PartPlacement</c> from the mirrored attachment point, and reflecting it here would
        /// undo that.</para>
        /// </remarks>
        public static void Apply(GameObject instance)
        {
            if (instance == null) return;

            foreach (Transform child in instance.transform) Reflect(child);

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                filter.sharedMesh = Of(filter.sharedMesh);

            foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.sharedMesh = Of(skinned.sharedMesh);
        }

        /// <summary>Reflects one node's placement across the YZ plane, and everything hanging off it.</summary>
        private static void Reflect(Transform node)
        {
            Vector3 position = node.localPosition;
            node.localPosition = new Vector3(-position.x, position.y, position.z);

            // A rotation seen in a mirror keeps the component along the mirror's normal and reverses
            // the other two.
            Quaternion rotation = node.localRotation;
            node.localRotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);

            foreach (Transform child in node) Reflect(child);
        }

        private static Mesh Build(Mesh source)
        {
            var mirror = new Mesh
            {
                name = source.name + " (mirrored)",
                indexFormat = source.indexFormat,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Vector3[] vertices = source.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].x = -vertices[i].x;
            mirror.vertices = vertices;

            Vector3[] normals = source.normals;
            for (int i = 0; i < normals.Length; i++) normals[i].x = -normals[i].x;
            mirror.normals = normals;

            // The tangent's handedness (w) flips with the reflection — leave it and every normal map on
            // the part lights from the wrong side.
            Vector4[] tangents = source.tangents;
            for (int i = 0; i < tangents.Length; i++)
            {
                tangents[i].x = -tangents[i].x;
                tangents[i].w = -tangents[i].w;
            }
            mirror.tangents = tangents;

            mirror.uv = source.uv;
            mirror.uv2 = source.uv2;
            mirror.colors32 = source.colors32;
            mirror.boneWeights = source.boneWeights;
            mirror.bindposes = source.bindposes;

            mirror.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] triangles = source.GetTriangles(sub);

                // Reversing the winding is what keeps the reflected model facing outwards.
                for (int i = 0; i < triangles.Length; i += 3)
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);

                mirror.SetTriangles(triangles, sub);
            }

            mirror.RecalculateBounds();

            return mirror;
        }
    }
}
