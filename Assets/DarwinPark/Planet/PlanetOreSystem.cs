using UnityEngine;
using DarwinPark.Core;

namespace DarwinPark.Planet
{
    public class PlanetOreSystem : MonoBehaviour
    {
        public static PlanetOreSystem Instance { get; private set; }

        [Header("矿石分布设置")]
        public int mapResolution = 512;
        public float baseOreChance = 0.4f; // 稍微增加一点覆盖率
        public float noiseScale = 20f; // ★ 核心修复：把频率调高，矿脉就会变碎变自然
        public float maxOreAmount = 100f;
        public float regenRate = 0.5f;

        public Texture2D OreMapTexture { get; private set; }

        private Color[] _oreMapPixels;
        private float _regenTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        // ★ 修复（防Bug回归）：重进场景后不能沿用已经销毁的静态实例与纹理。
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (OreMapTexture != null) Destroy(OreMapTexture);
        }

        private void Start()
        {
            GenerateOreMap();
        }

        private void GenerateOreMap()
        {
            OreMapTexture = new Texture2D(mapResolution, mapResolution, TextureFormat.RGBA32, false, true);
            // ★ 修复（防Bug回归）：矿量属于线性数据；经度环绕，纬度不能从北极跨到南极。
            OreMapTexture.wrapModeU = TextureWrapMode.Repeat;
            OreMapTexture.wrapModeV = TextureWrapMode.Clamp;
            OreMapTexture.filterMode = FilterMode.Bilinear;
            _oreMapPixels = new Color[mapResolution * mapResolution];

            for (int y = 0; y < mapResolution; y++)
            {
                for (int x = 0; x < mapResolution; x++)
                {
                    // ★ 修改：使用分形噪声生成更碎的矿斑
                    float nx = (float)x / mapResolution * noiseScale;
                    float ny = (float)y / mapResolution * noiseScale;
                    float noise1 = Mathf.PerlinNoise(nx + 100f, ny + 100f);
                    float noise2 = Mathf.PerlinNoise(nx * 3f + 50f, ny * 3f + 50f) * 0.4f;

                    float finalNoise = (noise1 + noise2) / 1.4f;

                    // ★ 修改：降低阈值，增加分布面积，并将对比度拉高
                    // ★ 修复（防Bug回归）：采用 Inspector 的分布参数，避免配置字段被硬编码覆盖。
                    float threshold = 1f - Mathf.Clamp01(baseOreChance); // 越低矿越多
                    float amount = 0f;
                    if (finalNoise > threshold)
                    {
                        // 用 Lerp 让从边缘到中心有从 0 到 1 的平滑过渡
                        amount = Mathf.Clamp01((finalNoise - threshold) / (1f - threshold));
                        amount = Mathf.Pow(amount, 1.5f); // 强化中心亮度
                    }

                    // ★ 强制提高 R 通道的存储亮度
                    _oreMapPixels[y * mapResolution + x] = new Color(amount, 0, 0, 1);
                }
            }
            OreMapTexture.SetPixels(_oreMapPixels);
            OreMapTexture.Apply();
            Debug.Log("[OreSystem] 矿石分布图生成完毕。");
        }

        private void Update()
        {
            // ★ 修复（防Bug回归）：新矿脉系统启用后须遵循模拟暂停，且纹理未就绪时不能刷新。
            if (_oreMapPixels == null || OreMapTexture == null) return;
            float dt = Time.deltaTime;
            if (ServiceLocator.TryGet<SimulationClock>(out var clock))
            {
                if (!clock.IsRunning) return;
                dt = clock.GameDeltaTime;
            }
            _regenTimer += dt;
            if (_regenTimer >= 1f)
            {
                _regenTimer = 0;
                for (int i = 0; i < _oreMapPixels.Length; i++)
                {
                    if (_oreMapPixels[i].r < 1f && _oreMapPixels[i].r > 0f)
                    {
                        float r = _oreMapPixels[i].r + (regenRate / maxOreAmount);
                        _oreMapPixels[i] = new Color(Mathf.Min(1f, r), 0, 0, 1);
                    }
                }
                OreMapTexture.SetPixels(_oreMapPixels);
                OreMapTexture.Apply();
            }
        }

        /// <summary>
        /// 世界坐标转 UV
        /// </summary>
        private Vector2 WorldToUV(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos).normalized;
            float u = (Mathf.Atan2(localPos.x, localPos.z) / (2f * Mathf.PI)) + 0.5f;
            float v = (Mathf.Asin(localPos.y) / Mathf.PI) + 0.5f;
            return new Vector2(u, v);
        }

        public float GetOreAmount(Vector3 worldPos)
        {
            if (OreMapTexture == null) return 0f;
            Vector2 uv = WorldToUV(worldPos);
            int x = Mathf.Clamp((int)(uv.x * mapResolution), 0, mapResolution - 1);
            int y = Mathf.Clamp((int)(uv.y * mapResolution), 0, mapResolution - 1);
            return _oreMapPixels[y * mapResolution + x].r * maxOreAmount;
        }

        public float ConsumeOre(Vector3 worldPos, float amount)
        {
            if (OreMapTexture == null) return 0f;
            Vector2 uv = WorldToUV(worldPos);
            int x = Mathf.Clamp((int)(uv.x * mapResolution), 0, mapResolution - 1);
            int y = Mathf.Clamp((int)(uv.y * mapResolution), 0, mapResolution - 1);

            int index = y * mapResolution + x;
            float current = _oreMapPixels[index].r * maxOreAmount;
            float actualConsume = Mathf.Min(amount, current);

            if (actualConsume > 0)
            {
                float newValue = (current - actualConsume) / maxOreAmount;
                _oreMapPixels[index] = new Color(newValue, 0, 0, 1);
                OreMapTexture.SetPixels(_oreMapPixels);
                OreMapTexture.Apply();
            }
            return actualConsume;
        }
    }
}