using System;
using Leeway.World;
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


        /// <summary>Capture assets once on the main thread. Sampling reads immutable data only;
        /// the colour at a geographic point never depends on camera distance or tile level.</summary>
        public static Func<Vector3,PlanetModel.Point,Color32> ForBiome(PlanetModel model,IslandBiome biome)
        {
            var palette=new IslandGroundPalette(biome);
            var patches=new PlanetIslandPatch[model.PatchCount];
            var noises=new IslandNoise[patches.Length];
            for(int i=0;i<patches.Length;i++)
            {
                patches[i]=model.GetPatch(i);
                noises[i]=new IslandNoise(patches[i].Field.Seed+101);
            }
            return (direction,point)=>
            {
                int selected=-1;
                float weight=0;
                for(int i=0;i<patches.Length;i++)
                {
                    float candidate=patches[i].WeightAt(direction);
                    if(candidate>weight){selected=i;weight=candidate;if(weight>=1)break;}
                }
                Color fallback=point.IsLand?Color.Lerp(palette.Grass,palette.GrassDark,
                    Mathf.Clamp01(point.Height/Mathf.Max(1,model.MaxHeight))):palette.SeaBed;
                if(selected<0)return (Color32)fallback;
                var field=patches[selected].Field;
                Vector2 at=patches[selected].ChartAt(direction);
                Vector3 chart=new Vector3(at.x,field.SampleHeight(at.x,at.y),at.y);
                Color local=palette.At(field,noises[selected],chart,field.SampleNormal(at.x,at.y));
                return (Color32)Color.Lerp(fallback,local,weight);
            };
        }

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
