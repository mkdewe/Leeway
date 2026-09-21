using System.Collections.Generic;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>Where an authored limb bends, measured from its own geometry.</summary>
    public readonly struct LimbKnee
    {
        /// <summary>How far along the limb the knee sits, 0 at the hip and 1 at the foot.</summary>
        public readonly float Fraction;

        /// <summary>Which way the knee points, in the limb's own space. Zero when the model is straight.</summary>
        public readonly Vector3 BendDirection;

        /// <summary>False when the model has no discernible bend — the caller then picks its own.</summary>
        public readonly bool Found;

        public LimbKnee(float fraction, Vector3 bendDirection, bool found)
        {
            Fraction = fraction;
            BendDirection = bendDirection;
            Found = found;
        }

        /// <summary>The fallback: a joint halfway along, bending wherever the caller says.</summary>
        public static LimbKnee Midpoint => new LimbKnee(0.5f, Vector3.zero, false);
    }

    /// <summary>
    /// What skinning a limb left behind: the skinned renderers it made and the rigid ones it switched
    /// off. Both have to be undone when the leg is taken apart, because the model belongs to the part
    /// the player attached, not to the chain.
    /// </summary>
    public readonly struct LimbBinding
    {
        public readonly GameObject[] Created;
        public readonly MeshRenderer[] Hidden;

        public LimbBinding(GameObject[] created, MeshRenderer[] hidden)
        {
            Created = created;
            Hidden = hidden;
        }

        public bool IsBound => Created != null && Created.Length > 0;

        public static LimbBinding None => new LimbBinding(System.Array.Empty<GameObject>(), System.Array.Empty<MeshRenderer>());

        /// <summary>Puts the limb back the way it was found: rigid renderers on, skinned ones gone.</summary>
        public void Undo()
        {
            if (Hidden != null)
            {
                foreach (MeshRenderer renderer in Hidden)
                    if (renderer != null) renderer.enabled = true;
            }

            if (Created == null) return;

            foreach (GameObject holder in Created)
            {
                if (holder == null) continue;

                if (Application.isPlaying) Object.Destroy(holder);
                else Object.DestroyImmediate(holder);
            }
        }
    }

    /// <summary>
    /// Gives a one-piece limb model a working joint: the same mesh, skinned across the chain's links.
    /// </summary>
    /// <remarks>
    /// <para><b>The problem this solves.</b> A leg drawn by an artist arrives as a single model — thigh,
    /// shank and ankle in one mesh. The IK chain, on the other hand, needs a joint to bend at. Repeating
    /// the model once per link gives the leg twice over with a false knee where the copy starts; using a
    /// single rigid link gives one leg that cannot bend at all. Skinning is the way out: <b>one</b>
    /// model, bound to the chain's links, bending where a leg bends.</para>
    ///
    /// <para><b>Where the joint goes is read off the model</b>, not guessed — see
    /// <see cref="FindKnee"/>. A leg model is drawn already bent, so the point furthest from the line
    /// between hip and ankle is its knee. The weights cross over there, and so does the IK link, so the
    /// crease in the mesh and the joint in the chain are the same place.</para>
    ///
    /// <para><b>The weights are a smooth ramp</b> across a band around the knee rather than a hard
    /// switch. A hard switch creases the mesh into a fold at the joint; the band lets the geometry bend
    /// the way skin does.</para>
    ///
    /// <para>The bound meshes are cached per source mesh and knee, because a four-legged creature would
    /// otherwise build the same one four times, and again on every rebuild.</para>
    /// </remarks>
    public static class SkinnedLimb
    {
        /// <summary>The fraction of the limb's length over which the weights cross from one link to the next.</summary>
        public const float DefaultBlendBand = 0.18f;

        /// <summary>The knee is looked for in this band of the limb — never in the hip or in the foot.</summary>
        private const float SearchFrom = 0.2f;
        private const float SearchTo = 0.8f;

        /// <summary>How far the model has to depart from straight before we call it a bend, as a fraction of its length.</summary>
        private const float MinBendFraction = 0.04f;

        private const int Slices = 24;

        private readonly struct BoundMeshKey
        {
            private readonly Mesh _source;
            private readonly int _kneeMillimetres;
            private readonly int _bandMillimetres;

            public BoundMeshKey(Mesh source, float knee, float band)
            {
                _source = source;
                _kneeMillimetres = Mathf.RoundToInt(knee * 1000f);
                _bandMillimetres = Mathf.RoundToInt(band * 1000f);
            }

            public override int GetHashCode()
                => (_source != null ? _source.GetInstanceID() : 0) ^ (_kneeMillimetres * 397) ^ (_bandMillimetres * 7919);

            public override bool Equals(object obj)
                => obj is BoundMeshKey other
                   && other._source == _source
                   && other._kneeMillimetres == _kneeMillimetres
                   && other._bandMillimetres == _bandMillimetres;
        }

        private static readonly Dictionary<BoundMeshKey, Mesh> Bound = new();

        /// <summary>
        /// Finds the knee in a limb's geometry: the point that departs furthest from the straight line
        /// between its two ends.
        /// </summary>
        /// <remarks>
        /// <para>The limb is sliced along its own axis (<c>+Z</c>, the convention for every part) and each
        /// slice is reduced to the centroid of the vertices in it — the model's centreline. A straight
        /// leg has that line straight; a drawn one bows at the knee, and the slice that bows furthest is
        /// the joint.</para>
        ///
        /// <para>The search deliberately ignores the first and last fifth of the limb. The hoof or paw
        /// juts forward at the far end and would otherwise win the vote — the foot is not the knee.</para>
        /// </remarks>
        /// <param name="limbRoot">The limb's root. Vertices are measured in its space.</param>
        /// <param name="limbLength">How far the limb reaches along <c>+Z</c>, in the root's space.</param>
        public static LimbKnee FindKnee(Transform limbRoot, float limbLength)
        {
            if (limbRoot == null || limbLength <= 1e-4f) return LimbKnee.Midpoint;

            var sums = new Vector3[Slices];
            var counts = new int[Slices];

            foreach (MeshFilter filter in limbRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;

                Matrix4x4 toLimb = limbRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 local = toLimb.MultiplyPoint3x4(vertices[i]);

                    int slice = Mathf.Clamp(Mathf.FloorToInt(local.z / limbLength * Slices), 0, Slices - 1);
                    sums[slice] += local;
                    counts[slice]++;
                }
            }

            int firstSlice = -1;
            int lastSlice = -1;
            for (int i = 0; i < Slices; i++)
            {
                if (counts[i] == 0) continue;

                if (firstSlice < 0) firstSlice = i;
                lastSlice = i;
            }

            if (firstSlice < 0 || lastSlice <= firstSlice) return LimbKnee.Midpoint;

            Vector3 hip = sums[firstSlice] / counts[firstSlice];
            Vector3 ankle = sums[lastSlice] / counts[lastSlice];

            var deviations = new Vector3[Slices];
            var fractions = new float[Slices];
            Vector3 total = Vector3.zero;
            float strongest = 0f;

            for (int i = firstSlice + 1; i < lastSlice; i++)
            {
                if (counts[i] == 0) continue;

                Vector3 centre = sums[i] / counts[i];
                float fraction = centre.z / limbLength;
                if (fraction < SearchFrom || fraction > SearchTo) continue;

                // Where the centreline would be if the limb were straight.
                float along = Mathf.Approximately(ankle.z, hip.z) ? 0f : (centre.z - hip.z) / (ankle.z - hip.z);
                Vector3 deviation = centre - Vector3.Lerp(hip, ankle, along);
                deviation.z = 0f;

                deviations[i] = deviation;
                fractions[i] = fraction;

                // The sum, not the largest: bulges that point opposite ways are not one bend, and they
                // cancel here rather than each claiming to be the knee.
                total += deviation;
                strongest = Mathf.Max(strongest, deviation.magnitude);
            }

            if (strongest < limbLength * MinBendFraction || total.sqrMagnitude < 1e-8f) return LimbKnee.Midpoint;

            Vector3 bend = total.normalized;

            // The knee is the centre of mass of the bulge along the dominant direction, not the single
            // slice that bulges most. These models are low-poly: one slice can hold nine vertices, and
            // its centroid wanders far enough to move the joint by a tenth of the leg. Averaging over
            // the whole bend is steady, and it lands where the leg visibly folds.
            float weighted = 0f;
            float weight = 0f;

            for (int i = firstSlice + 1; i < lastSlice; i++)
            {
                float alignment = Vector3.Dot(deviations[i], bend);
                if (alignment <= 0f) continue;

                weighted += fractions[i] * alignment;
                weight += alignment;
            }

            if (weight <= 0f) return LimbKnee.Midpoint;

            return new LimbKnee(weighted / weight, bend, true);
        }

        /// <summary>
        /// Binds every mesh under <paramref name="limbRoot"/> to <paramref name="bones"/> and swaps in
        /// skinned renderers for the rigid ones.
        /// </summary>
        /// <remarks>
        /// <para>The rigid renderer is switched off rather than destroyed, and the skinned one lives on a
        /// child of its own. The part's authored objects are the very ones the player attached in the
        /// editor — the chain borrows them and has to be able to give them back untouched
        /// (<c>ProceduralLeg.Dispose</c>).</para>
        ///
        /// <para>The bones are expected at rest: strung along <c>+Z</c> in the limb's space, unrotated
        /// and unscaled. That pose <b>is</b> the bind pose, and the solver moves them afterwards.</para>
        /// </remarks>
        /// <returns>What was created and what was switched off, so the caller can undo both.</returns>
        public static LimbBinding Skin(Transform limbRoot, Transform[] bones, float kneeDistance, float blendBand)
        {
            if (limbRoot == null || bones == null || bones.Length < 2) return LimbBinding.None;

            var created = new List<GameObject>();
            var hidden = new List<MeshRenderer>();

            foreach (MeshFilter filter in limbRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                if (source == null || !source.isReadable) continue;
                if (!filter.TryGetComponent(out MeshRenderer rigid)) continue;

                Matrix4x4 toLimb = limbRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Mesh bound = BoundMesh(source, toLimb, kneeDistance, blendBand);
                if (bound == null) continue;

                var holder = new GameObject(filter.name + " (skinned)");
                holder.transform.SetParent(filter.transform, false);

                var skinned = holder.AddComponent<SkinnedMeshRenderer>();
                skinned.sharedMesh = bound;
                skinned.bones = bones;
                skinned.rootBone = bones[0];
                skinned.sharedMaterials = rigid.sharedMaterials;

                // The bind pose is written against this object's transform, so the bounds have to be
                // recomputed from the posed bones — a static box around the rest pose would cull the leg
                // the moment it swung away from it.
                skinned.updateWhenOffscreen = true;

                // A part wears the creature's colour through a property block; without carrying it over
                // the leg would walk around in whatever colour the model was authored in.
                if (rigid.HasPropertyBlock())
                {
                    var block = new MaterialPropertyBlock();
                    rigid.GetPropertyBlock(block);
                    skinned.SetPropertyBlock(block);
                }

                rigid.enabled = false;
                hidden.Add(rigid);
                created.Add(holder);
            }

            return new LimbBinding(created.ToArray(), hidden.ToArray());
        }

        /// <summary>
        /// A copy of the mesh with a weight per vertex: the hip link before the knee, the shank link
        /// after it, and a smooth crossing in between.
        /// </summary>
        /// <param name="toLimb">From the mesh's own space to the limb's — the weights are decided along the limb, not the mesh.</param>
        private static Mesh BoundMesh(Mesh source, Matrix4x4 toLimb, float kneeDistance, float blendBand)
        {
            var key = new BoundMeshKey(source, kneeDistance, blendBand);
            if (Bound.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            Vector3[] vertices = source.vertices;
            if (vertices.Length == 0) return null;

            var weights = new BoneWeight[vertices.Length];
            float half = Mathf.Max(1e-4f, blendBand * 0.5f);

            for (int i = 0; i < vertices.Length; i++)
            {
                float along = toLimb.MultiplyPoint3x4(vertices[i]).z;

                // Below the band the vertex belongs wholly to the thigh, above it wholly to the shank.
                float shank = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(kneeDistance - half, kneeDistance + half, along));

                weights[i] = new BoneWeight
                {
                    boneIndex0 = 0,
                    weight0 = 1f - shank,
                    boneIndex1 = 1,
                    weight1 = shank,
                };
            }

            var mesh = Object.Instantiate(source);
            mesh.name = source.name + " (skinned)";
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.boneWeights = weights;

            // The bind pose maps the mesh's own space into each bone's. The bones are at rest — strung
            // along +Z at 0 and at the knee — so their inverse is that offset, and nothing else.
            mesh.bindposes = new[]
            {
                Matrix4x4.Translate(new Vector3(0f, 0f, 0f)).inverse * toLimb,
                Matrix4x4.Translate(new Vector3(0f, 0f, kneeDistance)).inverse * toLimb,
            };

            mesh.RecalculateBounds();

            Bound[key] = mesh;
            return mesh;
        }
    }
}
