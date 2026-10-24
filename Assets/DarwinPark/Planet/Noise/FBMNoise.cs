using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>FBM 噪声（实例持有 ImprovedNoise）。★ 适配：构造时注入 seed，不再依赖静态 Init。</summary>
    public class FBMNoise
    {
        private readonly ImprovedNoise _noise;
        public int Octaves, Seed;
        public float Frequency, Amplitude, Lacunarity, Gain, WarpStrength;

        public FBMNoise(FBMConfig cfg, int seed)
        {
            _noise = new ImprovedNoise(seed);
            Octaves = cfg.octaves;
            Frequency = cfg.frequency;
            Amplitude = cfg.amplitude;
            Lacunarity = cfg.lacunarity;
            Gain = cfg.gain;
            WarpStrength = cfg.warpStrength;
            Seed = seed;
        }

        public float Sample(Vector3 unitDir)
        {
            float amp = Amplitude, freq = Frequency, sum = 0f, norm = 0f;
            // ★ 修复（防Bug回归）：种子已进入置换表；大种子叠加后逐层倍频会丢失小数精度，造成地形/气候台阶。
            var p = unitDir * freq;
            for (int i = 0; i < Octaves; i++)
            {
                float n = WarpStrength > 0f
                    ? _noise.WarpedNoise(p, WarpStrength, (Seed & 255) + i * 17)
                    : _noise.Noise(p);
                sum += n * amp;
                norm += amp;
                p *= Lacunarity;
                amp *= Gain;
            }
            return norm > 0f ? sum / norm : 0f;
        }
    }
}