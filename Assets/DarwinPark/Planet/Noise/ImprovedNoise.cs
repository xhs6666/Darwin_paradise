using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>
    /// 3D 改进 Perlin 噪声（实例化）。
    /// ★ 适配：从静态 Perm 数组改为实例持有，消除多行星并发生成的竞态（规则3.6）。
    /// </summary>
    public class ImprovedNoise
    {
        private readonly int[] _perm = new int[512];

        public ImprovedNoise(int seed)
        {
            // ★ 修复：改用跨平台确定性随机，避免不同运行时的 System.Random 算法差异
            // 导致同一种子在 Editor 与打包版本生成不同地形（山脉位置漂移）。
            var rng = new DeterministicRandom(seed);
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);

        private float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public float Noise(Vector3 p)
        {
            int X = Mathf.FloorToInt(p.x) & 255;
            int Y = Mathf.FloorToInt(p.y) & 255;
            int Z = Mathf.FloorToInt(p.z) & 255;
            float x = p.x - Mathf.Floor(p.x);
            float y = p.y - Mathf.Floor(p.y);
            float z = p.z - Mathf.Floor(p.z);
            float u = Fade(x), v = Fade(y), w = Fade(z);
            int A = _perm[X] + Y, AA = _perm[A] + Z, AB = _perm[A + 1] + Z;
            int B = _perm[X + 1] + Y, BA = _perm[B] + Z, BB = _perm[B + 1] + Z;
            return Lerp(
                Lerp(Lerp(Grad(_perm[AA], x, y, z), Grad(_perm[BA], x - 1, y, z), u),
                     Lerp(Grad(_perm[AB], x, y - 1, z), Grad(_perm[BB], x - 1, y - 1, z), u), v),
                Lerp(Lerp(Grad(_perm[AA + 1], x, y, z - 1), Grad(_perm[BA + 1], x - 1, y, z - 1), u),
                     Lerp(Grad(_perm[AB + 1], x, y - 1, z - 1), Grad(_perm[BB + 1], x - 1, y - 1, z - 1), u), v),
                w);
        }

        public float WarpedNoise(Vector3 p, float warpStrength, int seedOffset)
        {
            var offset = new Vector3(
                Noise(p + Vector3.one * seedOffset),
                Noise(p + Vector3.one * (seedOffset + 31.4f)),
                Noise(p + Vector3.one * (seedOffset + 61.8f))
            ) * warpStrength;
            return Noise(p + offset);
        }
    }
}