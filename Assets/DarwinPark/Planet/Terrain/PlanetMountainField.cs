using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>★ 新增：低频连续造山带，细分支只修饰山脊，避免整个大陆长满等高圆丘。</summary>
    public sealed class PlanetMountainField
    {
        private readonly FBMNoise _belt;
        private readonly FBMNoise _detail;
        private readonly PlanetTerrainConfig _config;
        public PlanetMountainField(PlanetTerrainConfig config)
        {
            _config = config;
            _belt = new FBMNoise(new FBMConfig { frequency = config.mountainFrequency,
                octaves = config.mountainOctaves, warpStrength = config.mountainWarp }, config.seed + 500);
            _detail = new FBMNoise(new FBMConfig { frequency = config.mountainFrequency * 18f,
                octaves = 4, warpStrength = config.mountainWarp }, config.seed + 733);
        }
        public float Sample(Vector3 direction)
        {
            // ★ 修复（防Bug回归）：细节既侵蚀山顶也扭曲脊线，避免生成光滑的封闭“火山口”。
            float fine = _detail.Sample(direction);
            float distance = Mathf.Abs(_belt.Sample(direction) + fine * 0.06f);
            float belt = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(_config.mountainCoreWidth, _config.mountainBeltWidth, distance));
            float ridge = Mathf.Pow(belt, _config.mountainSharpness);
            float detail = Mathf.Clamp01(1f - Mathf.Abs(fine) * 3f);
            return ridge * Mathf.Lerp(1f - _config.mountainDetailStrength, 1f, detail);
        }
    }
}
