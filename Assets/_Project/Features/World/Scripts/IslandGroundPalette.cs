using Leeway.World.Domain;
using UnityEngine;

namespace Leeway.World
{
    /// <summary>A worker-safe snapshot of the island's ground colours, shared by flat previews and spherical LODs.</summary>
    public readonly struct IslandGroundPalette
    {
        public readonly Color Grass, GrassDark, Sand, Rock, SeaBed;
        private readonly float _grassMinNormalY;

        public IslandGroundPalette(IslandBiome biome)
        {
            Grass=biome.Grass; GrassDark=biome.GrassDark; Sand=biome.Sand;
            Rock=biome.Rock; SeaBed=biome.SeaBed;
            _grassMinNormalY=Mathf.Cos(biome.GrassMaxSlope*Mathf.Deg2Rad);
        }

        public Color At(IslandHeightField field, IslandNoise patches, Vector3 centre, Vector3 normal)
        {
            var settings=field.Settings;
            float fresh=field.FreshWaterLevel(centre.x,centre.z);
            Color colour;
            if(centre.y<settings.SeaLevel-0.2f || centre.y<fresh-0.3f) colour=SeaBed;
            else if(normal.y<_grassMinNormalY) colour=Rock;
            else if(centre.y<settings.BeachHeight+0.45f || centre.y<fresh+0.6f) colour=Sand;
            else
            {
                float patch=patches.Fbm(centre.x/22f,centre.z/22f,2);
                colour=Color.Lerp(Grass,GrassDark,Mathf.SmoothStep(0f,1f,(patch-0.35f)*3f));
            }
            float hash=Mathf.Sin(centre.x*12.9898f+centre.z*78.233f+centre.y*37.719f)*43758.5453f;
            float jitter=0.97f+0.06f*(hash-Mathf.Floor(hash));
            return new Color(colour.r*jitter,colour.g*jitter,colour.b*jitter,1);
        }
    }
}
