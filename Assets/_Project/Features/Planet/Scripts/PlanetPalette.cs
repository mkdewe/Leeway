using Leeway.World.Domain;
using UnityEngine;

namespace Leeway.Planet
{
    /// <summary>What colour the ground of the planet is: deep sea, shallows, sand, grass, rock and snow.</summary>
    public static class PlanetPalette
    {
        private static readonly Color DeepSea = new(0.09f, 0.27f, 0.50f);
        private static readonly Color ShallowSea = new(0.28f, 0.70f, 0.76f);
        private static readonly Color Sand = new(0.88f, 0.81f, 0.56f);
        private static readonly Color LowGrass = new(0.47f, 0.73f, 0.34f);
        private static readonly Color HighGrass = new(0.28f, 0.55f, 0.27f);
        private static readonly Color Rock = new(0.55f, 0.51f, 0.47f);
        private static readonly Color Snow = new(0.95f, 0.96f, 0.98f);

        /// <summary>The colour of one point of the surface. Safe to call from any thread.</summary>
        public static Color32 Of(PlanetModel model, PlanetModel.Point point)
        {
            if (!point.IsLand)
            {
                // Right at a coast the water is shallow; the colour deepens with the distance out.
                return Color.Lerp(ShallowSea, DeepSea, Mathf.Clamp01(point.Depth * 1.35f));
            }

            float high = model.MaxHeight > 0f ? Mathf.Clamp01(point.Height / model.MaxHeight) : 0f;
            // Soft steps from one kind of ground to the next: at the scale of a vertex a hard edge would only be a stair.
            Color ground = high < 0.34f ? Color.Lerp(LowGrass, HighGrass, high / 0.34f)
                : high < 0.66f ? HighGrass
                : high < 0.84f ? Color.Lerp(HighGrass, Rock, Mathf.InverseLerp(0.66f, 0.74f, high))
                : Color.Lerp(Rock, Snow, Mathf.InverseLerp(0.84f, 0.92f, high));
            float shore = Mathf.Clamp01(1f - Mathf.Min(point.Land / 0.07f, high / 0.045f));
            return Color.Lerp(ground, Sand, shore);
        }
    }
}
