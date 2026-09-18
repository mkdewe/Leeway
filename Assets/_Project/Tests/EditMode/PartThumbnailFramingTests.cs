using System.Collections.Generic;
using System.Linq;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Checks that the thumbnail frame holds the <b>whole</b> part at every angle of the spin — and
    /// for every part in the catalog, not just the one currently being looked at.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this is not a tautology.</b> The frame is computed from <c>Renderer.bounds</c>, and
    /// those come from the volume stored in the <b>mesh</b>, not from its vertices. A mesh with
    /// understated bounds — after a manual edit or after an import — gives a frame tighter than the
    /// model, and the part pokes a corner outside the frame exactly when the spin turns it side-on. The
    /// test compares the frame against the <b>real vertices</b>, so it catches precisely that case.</para>
    ///
    /// <para><b>Why one measurement is enough instead of spinning.</b> The preview rotates about the
    /// centre of the bounds, and rotation does not change the distance from the axis. So if the
    /// farthest vertex fits inside the framing radius, it fits at every angle — and vice versa.
    /// Rendering a full spin of every part would check the same thing, only slower and less precisely
    /// than this does.</para>
    /// </remarks>
    public class PartThumbnailFramingTests
    {
        /// <summary>Slack for single-precision rounding error in the matrix multiply.</summary>
        private const float Epsilon = 1e-4f;

        private static CreaturePartCatalog Catalog()
        {
            string[] found = AssetDatabase.FindAssets($"t:{nameof(CreaturePartCatalog)}");
            Assert.IsNotEmpty(found, "There is no part catalog in the project.");

            var catalog = AssetDatabase.LoadAssetAtPath<CreaturePartCatalog>(AssetDatabase.GUIDToAssetPath(found[0]));
            Assert.IsNotNull(catalog, "The part catalog cannot be loaded.");

            return catalog;
        }

        private static IEnumerable<CreaturePartDefinition> Parts()
            => Catalog().Parts.Where(part => part != null && part.Prefab != null);

        [Test]
        public void EveryPart_FitsItsThumbnailAtEveryAngleOfTheSpin()
        {
            var tooBig = new List<string>();

            foreach (CreaturePartDefinition part in Parts())
            {
                GameObject subject = Object.Instantiate(part.Prefab);
                try
                {
                    Assert.IsTrue(PartThumbnailRenderer.TryMeasure(subject, out Bounds bounds),
                        $"Part {part.PartKey} has no visible geometry — its tile will be left without a preview.");

                    float frame = PartThumbnailRenderer.FramingRadius(bounds);
                    float model = FarthestVertexFrom(subject, bounds.center);

                    if (model > frame + Epsilon)
                        tooBig.Add($"{part.PartKey}: the model reaches {model:F4}, the frame ends at {frame:F4}");
                }
                finally
                {
                    Object.DestroyImmediate(subject);
                }
            }

            CollectionAssert.IsEmpty(tooBig,
                "These parts leave the thumbnail frame during the spin:\n" + string.Join("\n", tooBig));
        }

        /// <summary>
        /// The model's farthest <b>vertex</b> from the spin axis — that is what decides whether
        /// anything leaves the frame, not the volume stored in the mesh.
        /// </summary>
        private static float FarthestVertexFrom(GameObject subject, Vector3 pivot)
        {
            float farthest = 0f;

            foreach (MeshFilter filter in subject.GetComponentsInChildren<MeshFilter>(false))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                // We count only what the camera will see — exactly the set the frame is measured over.
                if (!filter.TryGetComponent(out Renderer renderer) || !renderer.enabled) continue;

                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                    farthest = Mathf.Max(farthest, Vector3.Distance(toWorld.MultiplyPoint3x4(vertex), pivot));
            }

            return farthest;
        }
    }
}
