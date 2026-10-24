using System;

namespace DarwinPark.Planet
{
    /// <summary>
    /// ★ 新增：跨平台确定性随机数。
    /// 为什么不用 System.Random：Unity 的 Mono 与 .NET/IL2CPP 在不同版本下
    /// 可能采用不同的随机算法，同一个星球种子在 Editor 和打包后可能生成不同地形。
    /// 这里用固定算法保证“种子 → 地形”在编辑器、Windows、WebGL 上完全一致。
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(int seed)
        {
            // SplitMix64 初始化，避免相邻种子产生高度相关的序列。
            _state = unchecked((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0xD1B54A32D192ED03UL);
            for (int i = 0; i < 4; i++) NextUInt64();
        }

        private ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>返回 [0, maxValue) 的整数；maxValue &lt;= 0 时返回 0。</summary>
        public int Next(int maxValue)
        {
            if (maxValue <= 0) return 0;
            return (int)(NextDouble() * maxValue);
        }

        /// <summary>返回 [minValue, maxValue) 的整数。</summary>
        public int Next(int minValue, int maxValue)
        {
            if (maxValue <= minValue) return minValue;
            return minValue + Next(maxValue - minValue);
        }

        /// <summary>返回 [0,1) 的浮点数。</summary>
        public double NextDouble()
        {
            // 取高 53 位，得到 [0,1) 的双精度。
            return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>返回 [0,1) 的浮点数。</summary>
        public float NextFloat() => (float)NextDouble();
    }
}
