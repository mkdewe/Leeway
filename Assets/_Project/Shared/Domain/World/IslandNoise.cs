using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>
    /// Seeded 2D gradient noise. Unity's Mathf.PerlinNoise has no seed, and every client must
    /// build the same island from the seed alone, so the permutation comes from our own RNG.
    /// </summary>
    public sealed class IslandNoise
    {
        private readonly int[] _perm = new int[512];

        public IslandNoise(int seed)
        {
            var rng = new System.Random(seed);
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        /// <summary>Gradient noise in roughly [-1, 1].</summary>
        public float Sample(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;
            xi &= 255;
            yi &= 255;

            float u = Fade(xf);
            float v = Fade(yf);

            int aa = _perm[_perm[xi] + yi];
            int ab = _perm[_perm[xi] + yi + 1];
            int ba = _perm[_perm[xi + 1] + yi];
            int bb = _perm[_perm[xi + 1] + yi + 1];

            float x1 = Mathf.Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float x2 = Mathf.Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return Mathf.Lerp(x1, x2, v) * 1.41421356f;
        }

        /// <summary>Fractal sum normalised to roughly [0, 1].</summary>
        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float persistence = 0.5f)
        {
            float sum = 0f;
            float amplitude = 1f;
            float norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Sample(x, y) * amplitude;
                norm += amplitude;
                amplitude *= persistence;
                x *= lacunarity;
                y *= lacunarity;
            }
            return Mathf.Clamp01(sum / norm * 0.5f + 0.5f);
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }
    }
}
