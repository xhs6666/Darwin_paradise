using System;
using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>FBM 参数（数据驱动）。</summary>
    [Serializable]
    public class FBMConfig
    {
        public int octaves = 5;
        public float frequency = 0.05f;
        public float amplitude = 1f;
        public float lacunarity = 2f;
        public float gain = 0.5f;
        public float warpStrength = 0f;
        public int seed = 0;
    }

    /// <summary>侵蚀参数（数据驱动）。</summary>
    [Serializable]
    public class ErosionConfig
    {
        // ★ 修复（防Bug回归）：侵蚀边界平滑回到全局连续高度场。
        public int seamBlendCells = 6;
        public bool enableHydraulic = true;
        // ★ 修复：旧版每面只有 40 个水滴，对 128 细分（1.6 万格/面）几乎等于没侵蚀。
        // 提高到 300 个水滴，同时保持低强度，才能刻出可见的沟谷而不削平山脊。
        public int hydraulicIterations = 300;
        public float hydraulicStrength = 0.08f;

        public bool enableThermal = true;
        public int thermalIterations = 5;
        public float thermalTalusAngle = 0.03f;
        public float thermalStrength = 0.28f;
    }

    /// <summary>
    /// 行星地形总配置。
    /// ★ 修改：把山脉、盆地、峡谷等地貌雕刻参数全部暴露为配置，
    /// 避免在生成器里写死数值（规则 1：内容与阈值数据化）。
    /// </summary>
    [Serializable]
    public class PlanetTerrainConfig
    {
        public string id;
        public int subdivisions = 24;
        public float radius = 100f;
        public float heightScale = 15f;
        public int seed = 42;
        public FBMConfig baseNoise = new FBMConfig();
        public FBMConfig detailNoise = new FBMConfig();
        public ErosionConfig erosion = new ErosionConfig();

        [Header("陆地基础起伏")]
        [Tooltip("海岸线相对海平面的抬升，控制沙滩高度")]
        public float coastalRise = 0.012f;
        [Tooltip("大陆内部相对海岸的整体抬升，越大内陆高原越明显")]
        public float inlandRise = 0.22f;
        [Tooltip("大陆内部高度曲线指数，>1 时沿海更平坦、内陆抬升更快")]
        public float inlandCurve = 1.35f;

        [Header("山脉雕刻")]
        [Tooltip("山脉相对海平面的最大额外高度（以高度比例计）")]
        public float mountainHeight = 0.72f;
        // ★ 新增：造山带核心/边缘宽度及山脊破碎程度，统一配置避免地貌写死。
        public float mountainCoreWidth = 0.015f;
        public float mountainBeltWidth = 0.16f;
        [Range(0f, 1f)] public float mountainDetailStrength = 0.65f;
        public float mountainFrequency = 1.65f;
        public int mountainOctaves = 2;
        [Tooltip("脊线窗口锐度，越大山脊越窄越尖")]
        public float mountainSharpness = 2.4f;
        [Tooltip("从陆地比例多少开始出现山脉")]
        [Range(0f, 1f)] public float mountainStart = 0.03f;
        [Tooltip("陆地比例达到该值时山脉完全展开")]
        [Range(0.01f, 1f)] public float mountainFull = 0.32f;
        [Tooltip("山脉带噪声的域扭曲，打破规则条带")]
        public float mountainWarp = 0.45f;
        [Tooltip("山脉带遮罩强度，0 表示全大陆均匀造山")]
        [Range(0f, 1f)] public float mountainBeltStrength = 0.75f;

        [Header("盆地与峡谷雕刻")]
        [Tooltip("盆地最大下切深度")]
        public float basinDepth = 0.10f;
        public float basinFrequency = 1.35f;
        [Tooltip("盆地噪声阈值，越低盆地面积越大")]
        [Range(0f, 1f)] public float basinThreshold = 0.28f;
        [Tooltip("盆地边缘过渡宽度")]
        [Range(0.01f, 0.5f)] public float basinWidth = 0.30f;

        [Tooltip("峡谷/河谷最大下切深度")]
        public float valleyDepth = 0.085f;
        public float valleyFrequency = 3.8f;
        [Tooltip("峡谷噪声锐度，越大越像细窄河道")]
        public float valleySharpness = 3.2f;

        [Header("细节")]
        [Tooltip("高频细节噪声相对强度")]
        public float detailStrength = 0.02f;
        [Tooltip("海床相对海平面的最低比例，避免海底被压到球心")]
        [Range(0.05f, 0.9f)] public float oceanFloorRatio = 0.22f;
    }
}
