using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Generates a skinned tube swept along the spine.
    /// </summary>
    /// <remarks>
    /// Three things in this file decide whether the body looks like a creature or like spaghetti:
    /// <list type="number">
    /// <item>the vertices are authored in <b>root</b> space and the bindposes are the inverses of the
    /// bone→root matrices — only with that pairing does skinning in the rest pose reproduce exactly
    /// the geometry that was authored;</item>
    /// <item>the rotation frames are propagated by double reflection (rotation-minimising frames)
    /// rather than by <c>LookRotation(T, up)</c> — otherwise the tube twists violently about its own
    /// axis as the spine approaches vertical;</item>
    /// <item>the radius goes into the geometry alone, never into the bone scale;</item>
    /// <item>the ends of the body are closed by <b>hemispheres</b> with the end vertebra's radius —
    /// the same shape <see cref="PartPlacement"/> measures the body with, so parts sit exactly on the
    /// skin that is on screen.</item>
    /// </list>
    /// </remarks>
    public static class SpineMeshGenerator
    {
        public const string MeshName = "CreatureBody";

        /// <summary>One station along the resampled spine, together with its local frame and bone weights.</summary>
        private struct Station
        {
            public Vector3 Position;
            public float Radius;
            public Vector3 Tangent;
            public Vector3 Normal;
            public Vector3 Binormal;
            public float ArcLength;
            public int Bone0;
            public int Bone1;
            public float Weight1;
        }

        public static Mesh Generate(CreatureGenome genome, Matrix4x4[] boneToRoot, CreatureBodyBuildSettings settings, out Matrix4x4[] bindposes)
        {
            int boneCount = genome.VertebraCount;
            bindposes = new Matrix4x4[boneCount];
            for (int i = 0; i < boneCount; i++)
                bindposes[i] = boneToRoot[i].inverse;

            var mesh = new Mesh { name = MeshName };
            if (boneCount < 2) return mesh;

            int radialSegments = Mathf.Max(4, settings != null ? settings.RadialSegments : 8);
            int ringsPerSegment = Mathf.Max(1, settings != null ? settings.RingsPerSegment : 3);
            int capRings = Mathf.Max(1, settings != null ? settings.CapRings : 3);

            Station[] stations = BuildStations(genome, boneToRoot, ringsPerSegment);
            BuildFrames(stations);

            FillMesh(mesh, stations, radialSegments, capRings, settings != null ? settings.BoundsPadding : 1.5f);
            return mesh;
        }

        // --- Stations ---

        private static Station[] BuildStations(CreatureGenome genome, Matrix4x4[] boneToRoot, int ringsPerSegment)
        {
            int boneCount = genome.VertebraCount;
            var controls = new Vector3[boneCount];
            var radii = new float[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                controls[i] = boneToRoot[i].GetColumn(3);
                radii[i] = genome.GetVertebra(i).Radius;
            }

            int segments = boneCount - 1;
            int stationCount = segments * ringsPerSegment + 1;
            var stations = new Station[stationCount];

            for (int m = 0; m < stationCount; m++)
            {
                int segment = Mathf.Min(m / ringsPerSegment, segments - 1);
                float t = (m - segment * ringsPerSegment) / (float)ringsPerSegment;

                Vector3 p0 = ControlPoint(controls, segment - 1);
                Vector3 p1 = ControlPoint(controls, segment);
                Vector3 p2 = ControlPoint(controls, segment + 1);
                Vector3 p3 = ControlPoint(controls, segment + 2);

                Vector3 tangent = CatmullRomDerivative(p0, p1, p2, p3, t);
                if (tangent.sqrMagnitude < 1e-8f) tangent = p2 - p1;
                if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.forward;

                // The radius is interpolated linearly (with a smoothed parameter) rather than by a
                // spline — Catmull-Rom can overshoot below zero and turn the mesh inside out.
                float blend = Smoothstep(t);

                stations[m] = new Station
                {
                    Position = CatmullRom(p0, p1, p2, p3, t),
                    Radius = Mathf.Lerp(radii[segment], radii[segment + 1], blend),
                    Tangent = tangent.normalized,
                    Bone0 = segment,
                    Bone1 = segment + 1,
                    Weight1 = blend,
                };
            }

            float arc = 0f;
            stations[0].ArcLength = 0f;
            for (int m = 1; m < stationCount; m++)
            {
                arc += Vector3.Distance(stations[m].Position, stations[m - 1].Position);
                stations[m].ArcLength = arc;
            }

            if (arc > 1e-5f)
            {
                for (int m = 0; m < stationCount; m++)
                    stations[m].ArcLength /= arc;
            }

            return stations;
        }

        /// <summary>Extrapolates control points past the ends of the spine so the spline has a full set of four.</summary>
        private static Vector3 ControlPoint(Vector3[] controls, int index)
        {
            int last = controls.Length - 1;
            if (index < 0) return controls[0] + (controls[0] - controls[1]);
            if (index > last) return controls[last] + (controls[last] - controls[last - 1]);
            return controls[index];
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                         + (-p0 + p2) * t
                         + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                         + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Vector3 CatmullRomDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * ((-p0 + p2)
                         + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t
                         + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
        }

        private static float Smoothstep(float t) => t * t * (3f - 2f * t);

        // --- Rotation frames (double reflection) ---

        private static void BuildFrames(Station[] stations)
        {
            Vector3 tangent0 = stations[0].Tangent;

            Vector3 seed = Mathf.Abs(Vector3.Dot(tangent0, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            Vector3 normal = (seed - Vector3.Dot(seed, tangent0) * tangent0).normalized;

            stations[0].Normal = normal;
            stations[0].Binormal = Vector3.Cross(tangent0, normal);

            for (int i = 0; i < stations.Length - 1; i++)
            {
                Vector3 v1 = stations[i + 1].Position - stations[i].Position;
                float c1 = Vector3.Dot(v1, v1);

                Vector3 reflectedNormal = stations[i].Normal;
                Vector3 reflectedTangent = stations[i].Tangent;

                if (c1 > 1e-10f)
                {
                    reflectedNormal -= (2f / c1) * Vector3.Dot(v1, reflectedNormal) * v1;
                    reflectedTangent -= (2f / c1) * Vector3.Dot(v1, reflectedTangent) * v1;
                }

                Vector3 v2 = stations[i + 1].Tangent - reflectedTangent;
                float c2 = Vector3.Dot(v2, v2);

                Vector3 next = reflectedNormal;
                if (c2 > 1e-10f)
                    next -= (2f / c2) * Vector3.Dot(v2, next) * v2;

                // Orthogonalising against the tangent cancels numerical drift on long spines.
                Vector3 tangent = stations[i + 1].Tangent;
                next = (next - Vector3.Dot(next, tangent) * tangent);
                next = next.sqrMagnitude > 1e-10f ? next.normalized : stations[i].Normal;

                stations[i + 1].Normal = next;
                stations[i + 1].Binormal = Vector3.Cross(tangent, next);
            }
        }

        // --- Mesh ---

        /// <summary>
        /// Stitches the tube out of rings and closes it off with domes.
        /// </summary>
        /// <remarks>
        /// <para>The domes are <b>hemispheres</b> with the end vertebra's radius, not cones pulled to a
        /// point. The reason is not cosmetic: <see cref="PartPlacement"/> measures the body as a
        /// capsule, so any other end shape means parts hanging in the air in front of the nose, or
        /// drowned inside it.</para>
        ///
        /// <para>The rings run in one continuous sequence from the tip of the head to the tip of the
        /// tail, so a single loop stitches the whole thing — the domes have no winding rule of their own
        /// that would have to be kept in step with the tube.</para>
        /// </remarks>
        private static void FillMesh(Mesh mesh, Station[] stations, int radialSegments, int capRings, float boundsPadding)
        {
            int stationCount = stations.Length;
            int ringVerts = radialSegments + 1; // column 0 duplicated for u = 1, so there is no UV seam

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();

            Station head = stations[0];
            Station tail = stations[stationCount - 1];

            // The head dome: hoops from the tip towards the body. The tip itself is added separately,
            // because it is a single vertex rather than a ring.
            for (int j = capRings - 1; j >= 1; j--)
            {
                float angle = j * (Mathf.PI * 0.5f) / capRings;
                AppendRing(head, Mathf.Cos(angle), -Mathf.Sin(angle), 0f, radialSegments, vertices, normals, uvs, weights);
            }

            for (int m = 0; m < stationCount; m++)
                AppendRing(stations[m], 1f, 0f, stations[m].ArcLength, radialSegments, vertices, normals, uvs, weights);

            // The tail dome: hoops from the body towards the tip.
            for (int j = 1; j <= capRings - 1; j++)
            {
                float angle = j * (Mathf.PI * 0.5f) / capRings;
                AppendRing(tail, Mathf.Cos(angle), Mathf.Sin(angle), 1f, radialSegments, vertices, normals, uvs, weights);
            }

            int ringCount = vertices.Count / ringVerts;

            int headApex = vertices.Count;
            vertices.Add(head.Position - head.Tangent * head.Radius);
            normals.Add(-head.Tangent);
            uvs.Add(new Vector2(0.5f, 0f));
            weights.Add(MakeWeight(head));

            int tailApex = vertices.Count;
            vertices.Add(tail.Position + tail.Tangent * tail.Radius);
            normals.Add(tail.Tangent);
            uvs.Add(new Vector2(0.5f, 1f));
            weights.Add(MakeWeight(tail));

            var triangles = new List<int>((ringCount - 1) * radialSegments * 6 + radialSegments * 6);

            for (int r = 0; r < ringCount - 1; r++)
            {
                int rowA = r * ringVerts;
                int rowB = (r + 1) * ringVerts;

                for (int k = 0; k < radialSegments; k++)
                {
                    int a0 = rowA + k;
                    int a1 = rowA + k + 1;
                    int b0 = rowB + k;
                    int b1 = rowB + k + 1;

                    triangles.Add(a0); triangles.Add(a1); triangles.Add(b0);
                    triangles.Add(a1); triangles.Add(b1); triangles.Add(b0);
                }
            }

            // The head's tip looks along -T, so its fan has the reverse order compared to the tail's.
            int lastRow = (ringCount - 1) * ringVerts;
            for (int k = 0; k < radialSegments; k++)
            {
                triangles.Add(headApex); triangles.Add(k + 1); triangles.Add(k);
                triangles.Add(tailApex); triangles.Add(lastRow + k); triangles.Add(lastRow + k + 1);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            Bounds bounds = mesh.bounds;
            bounds.Expand(boundsPadding);
            mesh.bounds = bounds;
        }

        /// <summary>
        /// Adds one ring around a station. <paramref name="radial"/> and <paramref name="axial"/> are
        /// the cosine and sine of the angle on the dome: the pair (1, 0) gives an ordinary tube ring,
        /// and (cos, ±sin) the successive hoops of a hemisphere.
        /// </summary>
        private static void AppendRing(in Station station, float radial, float axial, float v, int radialSegments,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<BoneWeight> weights)
        {
            BoneWeight weight = MakeWeight(station);
            int ringVerts = radialSegments + 1;

            for (int k = 0; k < ringVerts; k++)
            {
                float angle = (k % radialSegments) / (float)radialSegments * Mathf.PI * 2f;
                Vector3 outward = Mathf.Cos(angle) * station.Normal + Mathf.Sin(angle) * station.Binormal;

                // The point lies on a sphere of the station's radius, so the direction from its centre
                // is already the normal — and without normalising, because cos² + sin² = 1.
                Vector3 direction = outward * radial + station.Tangent * axial;

                vertices.Add(station.Position + direction * station.Radius);
                normals.Add(direction);
                uvs.Add(new Vector2(k / (float)radialSegments, v));
                weights.Add(weight);
            }
        }

        /// <summary>
        /// Two bones per vertex. For a spine that is exactly as many as are needed, and it lets us use
        /// the simple <c>Mesh.boneWeights</c> instead of the <c>NativeArray</c> API.
        /// </summary>
        private static BoneWeight MakeWeight(Station station) => new BoneWeight
        {
            boneIndex0 = station.Bone0,
            weight0 = 1f - station.Weight1,
            boneIndex1 = station.Bone1,
            weight1 = station.Weight1,
        };
    }
}
