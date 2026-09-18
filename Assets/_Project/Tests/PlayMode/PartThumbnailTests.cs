using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>
    /// Guards three things that cannot be checked anywhere but on the pixels: that a tile shows
    /// <b>one</b> part — its own — that hovering it jumps to nothing else, and that nothing is clipped
    /// by the edge.
    /// </summary>
    /// <remarks>
    /// <para><b>Models overlapping.</b> There is one studio and the camera photographs everything
    /// standing in it, while a plain <c>Destroy</c> only releases the object at the end of the frame.
    /// The palette photographs a whole category in <b>one</b> frame, so the second part posed together
    /// with the first, the third with the two before it. Only the first tile came out clean — and that
    /// is the one you look at when checking. The measure is the <b>area</b> of the same part
    /// photographed alone and photographed after another: an extra model always adds pixels, never
    /// removes them, so there is no way for it to hide inside an edge tolerance.</para>
    ///
    /// <para><b>The jump on hover.</b> The thumbnail is meant to be literally frame zero of the spin.
    /// The comparison here is pixel for pixel, because that is exactly the point: anything that differs
    /// is seen by the player as a jolt the moment the cursor enters the tile.</para>
    ///
    /// <para><b>Clipping in the frame.</b> That the frame is computed wide enough to hold the part at
    /// every angle of the spin is guarded by <c>PartThumbnailFramingTests</c> — on the geometry,
    /// because measuring vertices is more precise and cheaper than rendering a full spin of every part.
    /// What is checked here is the other half of the same matter: that the render keeps to that frame
    /// and no model reaches the edge of the image.</para>
    /// </remarks>
    public class PartThumbnailTests
    {
        /// <summary>How opaque a pixel has to be to count as model rather than background.</summary>
        private const byte OpaqueThreshold = 8;

        /// <summary>
        /// How far two photographs of the same part may drift apart. The slack is for the edges — the
        /// difference this is about is an entire extra model.
        /// </summary>
        private const float Tolerance = 0.05f;

        /// <summary>The channel-difference sum below which two pixels count as the same image.</summary>
        private const int ChannelTolerance = 24;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Application.runInBackground = true;
            yield return CreatureEditorTestScene.EnsureLoaded();
        }

        [UnityTest]
        public IEnumerator Snapshot_ShowsOnlyItsOwnPart_EvenWhenTakenAfterAnother()
        {
            CreaturePartDefinition[] parts = TwoPartsWithPrefabs();

            int alone = InFreshStudio(studio => Opaque(studio.Get(parts[1])));
            int afterAnother = InFreshStudio(studio =>
            {
                // Without a single yield — exactly how the palette builds a category's tiles.
                studio.Get(parts[0]);
                return Opaque(studio.Get(parts[1]));
            });

            Assert.Greater(alone, 0, "The photograph of the part came out empty — the studio sees nothing.");
            Assert.AreEqual(alone, afterAnother, alone * Tolerance,
                "The second part in the queue has the first one on it — the studio was not emptied between shots.");

            yield break;
        }

        /// <summary>
        /// The same trap from the live preview's side: a cursor running across the tiles must not lay
        /// one model over another.
        /// </summary>
        [UnityTest]
        public IEnumerator LivePreview_ShowsOnlyTheHoveredPart_AfterLeavingAnother()
        {
            CreaturePartDefinition[] parts = TwoPartsWithPrefabs();

            int alone = InFreshStudio(studio => Opaque(studio.Get(parts[1])));
            int afterHoveringAnother = InFreshStudio(studio =>
            {
                studio.BeginPreview(parts[0]);

                RenderTexture live = studio.BeginPreview(parts[1]);
                Assert.IsNotNull(live, "The live preview did not come up.");
                Assert.IsTrue(studio.IsPreviewing(parts[1]), "The preview lost the part we asked for.");

                Texture2D frame = ReadBack(live);
                try
                {
                    return Opaque(frame);
                }
                finally
                {
                    Object.DestroyImmediate(frame);
                }
            });

            Assert.AreEqual(alone, afterHoveringAnother, alone * Tolerance,
                "The hovered model does not match the thumbnail — the previous part was left in the studio.");

            yield break;
        }

        /// <summary>
        /// The thumbnail has to be the very frame the spin starts from — otherwise hovering swaps the
        /// image for a different one and the model visibly jumps.
        /// </summary>
        [UnityTest]
        public IEnumerator Thumbnail_IsExactlyTheFirstFrameOfTheSpin()
        {
            var jumped = new List<string>();

            foreach (CreaturePartDefinition part in PartsWithPrefabs())
            {
                CreaturePartDefinition captured = part;

                int different = InFreshStudio(studio =>
                {
                    Texture2D still = studio.Get(captured);
                    Assert.IsNotNull(still, $"Part {captured.PartKey} has no thumbnail.");

                    RenderTexture live = studio.BeginPreview(captured);
                    Assert.IsNotNull(live, $"Part {captured.PartKey} has no live preview.");

                    Texture2D frame = ReadBack(live);
                    try
                    {
                        return DifferingPixels(still, frame);
                    }
                    finally
                    {
                        Object.DestroyImmediate(frame);
                    }
                });

                if (different > 0) jumped.Add($"{part.PartKey}: {different} pixels differ");
            }

            CollectionAssert.IsEmpty(jumped,
                "These parts jump on hover — the thumbnail shows something other than the first frame of the spin:\n"
                + string.Join("\n", jumped));

            yield break;
        }

        /// <summary>
        /// Nothing may touch the edge of a tile.
        /// </summary>
        /// <remarks>
        /// The frame is computed to hold the part at <b>every</b> angle of the spin — that the numbers
        /// add up is guarded by <c>PartThumbnailFramingTests</c> on the geometry. Here we check the
        /// other half: that the render actually keeps to that frame rather than drifting off it on, say,
        /// the texture's aspect ratio.
        /// </remarks>
        [UnityTest]
        public IEnumerator EveryPart_KeepsClearOfTheFrameEdge()
        {
            var clipped = new List<string>();

            foreach (CreaturePartDefinition part in PartsWithPrefabs())
            {
                CreaturePartDefinition captured = part;

                int onEdge = InFreshStudio(studio =>
                {
                    Texture2D still = studio.Get(captured);
                    Assert.IsNotNull(still, $"Part {captured.PartKey} has no thumbnail.");

                    return OpaqueOnBorder(still);
                });

                if (onEdge > 0) clipped.Add($"{part.PartKey}: {onEdge} pixels on the edge itself");
            }

            CollectionAssert.IsEmpty(clipped,
                "These parts touch the edge of the tile, which means they are clipped:\n" + string.Join("\n", clipped));

            yield break;
        }

        /// <summary>
        /// How many model pixels lie on the outer border of the image. A clipped part ends exactly at
        /// the edge, so checking the outermost row and column is enough — there is no thinner margin.
        /// </summary>
        private static int OpaqueOnBorder(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;

            int onEdge = 0;
            for (int y = 0; y < height; y++)
            {
                bool edgeRow = y == 0 || y == height - 1;

                for (int x = 0; x < width; x++)
                {
                    if (!edgeRow && x != 0 && x != width - 1) continue;
                    if (pixels[y * width + x].a > OpaqueThreshold) onEdge++;
                }
            }

            return onEdge;
        }

        private static IReadOnlyList<CreaturePartDefinition> PartsWithPrefabs()
        {
            CreaturePartCatalog catalog = Object.FindFirstObjectByType<CreatureBodyPreview>()?.Catalog;
            Assert.IsNotNull(catalog, "The scene has no part catalog.");

            CreaturePartDefinition[] parts = catalog.Parts
                .Where(part => part != null && part.Prefab != null)
                .ToArray();

            Assert.IsNotEmpty(parts, "The catalog has not a single part with a prefab.");
            return parts;
        }

        private static CreaturePartDefinition[] TwoPartsWithPrefabs()
        {
            CreaturePartDefinition[] parts = PartsWithPrefabs().Take(2).ToArray();

            Assert.AreEqual(2, parts.Length, "The test needs two parts with prefabs.");
            return parts;
        }

        /// <summary>
        /// How many pixels visibly differ. The threshold absorbs the rounding on edges — the difference
        /// this is about is a different image, not a different shade.
        /// </summary>
        private static int DifferingPixels(Texture2D left, Texture2D right)
        {
            Assert.AreEqual(left.width, right.width, "The photograph and the preview are different sizes.");
            Assert.AreEqual(left.height, right.height, "The photograph and the preview are different sizes.");

            Color32[] a = left.GetPixels32();
            Color32[] b = right.GetPixels32();

            int different = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int delta = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g)
                          + Mathf.Abs(a[i].b - b[i].b) + Mathf.Abs(a[i].a - b[i].a);

                if (delta > ChannelTolerance) different++;
            }

            return different;
        }

        /// <summary>
        /// Runs a measurement in its own, fresh studio. Each has a cache of its own, so two runs over
        /// the same part really do photograph it instead of handing back a remembered shot.
        /// </summary>
        private static int InFreshStudio(System.Func<PartThumbnailRenderer, int> measure)
        {
            var host = new GameObject("ThumbnailTestStudio");
            try
            {
                // The pixels are read inside: the host's OnDestroy releases the texture cache.
                return measure(host.AddComponent<PartThumbnailRenderer>());
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static int Opaque(Texture2D texture)
        {
            Assert.IsNotNull(texture, "The studio did not return a photograph of the part we asked for.");
            return texture.GetPixels32().Count(pixel => pixel.a > OpaqueThreshold);
        }

        private static Texture2D ReadBack(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;

                var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };

                texture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                texture.Apply();

                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }
    }
}
