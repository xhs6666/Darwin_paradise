using UnityEngine;
using UnityEngine.Rendering;
using DarwinPark.Core;

namespace DarwinPark.Planet
{
    /// <summary>★ 新增：独立海面、体积云和薄大气层；不改变地形碰撞及气候数据。</summary>
    [RequireComponent(typeof(PlanetTerrainMeshBuilder))]
    public sealed class PlanetWeatherVisuals : MonoBehaviour
    {
        public Shader oceanShader, cloudShader, atmosphereShader;
        [Header("海洋")]
        public Color shallowWater = new Color(0.12f, 0.62f, 0.82f);
        public Color deepWater = new Color(0.015f, 0.13f, 0.32f);
        public float shelfDepth = 3f;
        [Min(.1f)] public float underwaterVisibility = 14f;
        [Range(0f,.25f)] public float oceanWaveStrength = .065f;
        [Header("云层（海平面以上高度，山峰可穿出云层）")]
        public bool showClouds = true;
        [Tooltip("相机距海平面低于此高度时隐藏体积云，便于观察地面实体")]
        public float hideCloudsBelowAltitude = 100f;
        public float cloudClearance = 3f;
        public float cloudThickness = 5f;
        [Range(0f, 1f)] public float cloudCoverage = 0.48f;
        public float cloudFrequency = 9f;
        public float cloudDetail = 80f;
        public float cloudSpeed = 0.3f;
        public float cloudDensity = 3.5f;
        [Range(6, 24)] public int cloudSteps = 8;
        [Header("大气")]
        public float atmosphereThickness = 12f;
        public float atmosphereDensity = 0.08f;
        public Color atmosphereColor = new Color(0.2f, 0.52f, 1f);
        public Color twilightColor=new Color(1f,.3f,.09f),nightSkyColor=new Color(.008f,.015f,.045f);
        [Range(.02f,.4f)] public float twilightWidth=.16f;
        [Range(0,1)] public float nightScattering=.035f;
        private Mesh _oceanMesh, _cloudMesh, _atmosphereMesh;
        private Material _oceanMaterial, _cloudMaterial, _atmosphereMaterial;
        private GameObject _ocean, _clouds, _atmosphere;
        private PlanetTerrainMeshBuilder _builder;
        private MaterialPropertyBlock _sourceBlock;
        private AtmospherePalette _palette;
        private float _displayLayer;
        private float _cloudHeading=float.NaN;

        private void ApplyPalette()
        {
            var palette=AtmospherePalette.Current;if(_atmosphereMaterial==null||ReferenceEquals(_palette,palette))return;_palette=palette;
            atmosphereColor=palette.Day;twilightColor=palette.Twilight;nightSkyColor=palette.Night;
            _atmosphereMaterial.SetColor("_ScatterColor",atmosphereColor);_atmosphereMaterial.SetColor("_TwilightColor",twilightColor);_atmosphereMaterial.SetColor("_NightColor",nightSkyColor);
            // ★ 新增：昼夜只上传一个全局向量，不遍历实体、逐顶点改色或重建任何网格。
            Shader.SetGlobalVector("_PlanetDayNight",new Vector4(palette.nightSurfaceBrightness,palette.terminatorWidth,palette.enableDayNight?1:0,_displayLayer));
        }

        public void Rebuild()
        {
            _builder = GetComponent<PlanetTerrainMeshBuilder>();
            var data = _builder.Data;
            if (data == null) return;
            Release();
            float sea = _builder.WaterRadius;
            // ★ 适配：轨道相机必须停在海面以上，不能只靠海底碰撞体限制拉近。
            var orbit=Camera.main != null ? Camera.main.GetComponent<OrbitCamera>() : null;
            if(orbit != null) orbit.minimumSurfaceRadius=sea;
            // ★ 修复（防Bug回归）：云层以海平面为基准，否则一座山会将全球云层撑成远离地表的空心壳。
            float cloudBottom = sea + cloudClearance;
            float cloudTop = cloudBottom + cloudThickness;
            // ★ 适配：气壳覆盖实际山峰，不能只围绕海平面计算厚度。
            float atmosphereTop = AtmosphereViewPolicy.OuterRadius(data,cloudTop+atmosphereThickness,atmosphereThickness);
            _ocean = CreateShell("OceanSurface", data, sea, oceanShader, ref _oceanMesh, out _oceanMaterial);
            if (_oceanMaterial != null)
            {
                var depth = new Color[data.Vertices.Length];
                for (int i = 0; i < depth.Length; i++)
                {
                    // ★ 适配：海面顶点和海床共用索引，保留底部真实起伏和完整深度。
                    depth[i] = new Color(Mathf.Max(0, sea - data.Vertices[i].magnitude), 0, 0, 1);
                }
                _oceanMesh.colors = depth;
                _oceanMaterial.SetColor("_ShallowColor", shallowWater);
                _oceanMaterial.SetColor("_DeepColor", deepWater);
                _oceanMaterial.SetFloat("_OceanOptics", 1);
                _oceanMaterial.SetFloat("_ShelfDepth", Mathf.Max(.1f, shelfDepth));
                _oceanMaterial.SetFloat("_SeaRadius", sea);
                _oceanMaterial.SetFloat("_Visibility", underwaterVisibility);
                _oceanMaterial.SetFloat("_WaveStrength", oceanWaveStrength);
                ApplyOceanLayer(0);
            }
            _clouds = CreateShell("CloudVolume", data, cloudTop, cloudShader, ref _cloudMesh, out _cloudMaterial);
            if (_cloudMaterial != null)
            {
                _cloudMaterial.SetFloat("_BottomRadius", cloudBottom);
                _cloudMaterial.SetFloat("_TopRadius", cloudTop);
                _cloudMaterial.SetFloat("_LightOccluderRadius", sea);
                _cloudMaterial.SetFloat("_Coverage", cloudCoverage);
                _cloudMaterial.SetFloat("_Frequency", cloudFrequency);
                _cloudMaterial.SetFloat("_Detail", cloudDetail);
                _cloudMaterial.SetFloat("_Density", cloudDensity);
                _cloudMaterial.SetFloat("_Steps", cloudSteps);
            }
            _atmosphere = CreateShell("AtmosphericScattering", data, atmosphereTop, atmosphereShader,
                ref _atmosphereMesh, out _atmosphereMaterial);
            if (_atmosphereMaterial != null)
            {
                _atmosphereMaterial.SetFloat("_SurfaceRadius", sea);
                _atmosphereMaterial.SetFloat("_OuterRadius", atmosphereTop);
                _atmosphereMaterial.SetFloat("_Density", atmosphereDensity);
                _atmosphereMaterial.SetColor("_ScatterColor", atmosphereColor);
                // ★ 新增：天空随真实太阳方向连续变化，控制视角能区分日间、晨昏和夜空，而非只看到固定蓝壳。
                _atmosphereMaterial.SetColor("_TwilightColor",twilightColor);_atmosphereMaterial.SetColor("_NightColor",nightSkyColor);
                _atmosphereMaterial.SetFloat("_TwilightWidth",twilightWidth);_atmosphereMaterial.SetFloat("_NightScattering",nightScattering);
            }
            _sourceBlock = new MaterialPropertyBlock();
            _displayLayer = 0;
            _cloudHeading=float.NaN;
            _palette=null;ApplyPalette();
        }

        private GameObject CreateShell(string name, PlanetTerrainData data, float radius, Shader shader,
            ref Mesh mesh, out Material material)
        {
            material = null;
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError($"[PlanetWeather] {name} 缺少可用 Shader，请检查场景引用。", this);
                return null;
            }
            // ★ 适配：海面保留原深度顶点；云和大气改用外接低模代理，解析积分和地形碰撞保持原逻辑。
            mesh=WeatherShellGeometry.Create(data,radius,name);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            material = new Material(shader) { name = name + " (Runtime)" };
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private void LateUpdate()
        {
            if (_builder == null || _sourceBlock == null) return;
            GetComponent<MeshRenderer>().GetPropertyBlock(_sourceBlock);
            float layer = _sourceBlock.GetFloat("_LayerMode");
            // ★ 适配：图层只在切换时上传，河湖共享这一标记，数据视图沿用原有可读颜色。
            if(layer!=_displayLayer){_displayLayer=layer;_palette=null;ApplyOceanLayer(layer);}
            // 数据图层关闭天气遮挡，海面延续同一气候/矿石图，保持图层切换完整。
            // ★ 修复（防Bug回归）：靠近地表自动隐藏云层；拉远恢复，独立保留用户总开关与数据层规则。
            var camera = Camera.main;
            // ★ 适配：每颗星球统一检查一次配色文件，修改后只更新材质，不重建世界网格。
            AtmospherePalette.Poll();ApplyPalette();
            AtmosphereViewPolicy.Apply(_atmosphereMaterial,camera);
            float altitude = camera != null ? transform.InverseTransformPoint(camera.transform.position).magnitude - _builder.WaterRadius : float.MaxValue;
            if (_clouds != null) _clouds.SetActive(showClouds && layer < 0.5f && altitude > hideCloudsBelowAltitude);
            if (_oceanMaterial != null)
            {
                _oceanMaterial.SetFloat("_LayerMode", layer);
                _oceanMaterial.SetFloat("_ClimateReady", _sourceBlock.GetFloat("_ClimateReady"));
                _oceanMaterial.SetTexture("_ClimateMap", _sourceBlock.GetTexture("_ClimateMap"));
                _oceanMaterial.SetTexture("_OreMap", _sourceBlock.GetTexture("_OreMap"));
            }
            if(_clouds!=null&&_clouds.activeSelf&&ServiceLocator.TryGet<SimulationClock>(out var clock))
            {
                float heading=Mathf.Repeat(clock.GameTime*cloudSpeed,360f);
                if(heading!=_cloudHeading)
                {
                    _cloudHeading=heading;
                    _clouds.transform.localRotation=Quaternion.AngleAxis(heading,Vector3.up);
                }
            }
        }

        private void ApplyOceanLayer(float layer)
        {
            GetComponent<PlanetFreshWaterMesh>()?.ApplyLayer(layer);
            if (_oceanMaterial == null) return;
            // ★ 适配：观察海洋需在海床与生物之后混合；数据层仍用不透明水面保持图层可读。
            bool optical = layer < .5f;
            _oceanMaterial.renderQueue = optical ? (int)RenderQueue.Transparent - 20 : (int)RenderQueue.Geometry + 1;
            _oceanMaterial.SetFloat("_WaterDepthWrite", optical ? 0 : 1);
            _oceanMaterial.SetFloat("_WaterDstBlend", optical ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            _oceanMaterial.SetFloat("_Cull", optical ? (int)CullMode.Off : (int)CullMode.Back);
        }

        private void Release()
        {
            // 重建先停用旧壳，避免延迟销毁造成两层共面闪烁。
            foreach (var go in new[] { _ocean, _clouds, _atmosphere })
                if (go != null) { go.SetActive(false); Destroy(go); }
            foreach (var mesh in new[] { _oceanMesh, _cloudMesh, _atmosphereMesh })
                if (mesh != null) Destroy(mesh);
            foreach (var mat in new[] { _oceanMaterial, _cloudMaterial, _atmosphereMaterial })
                if (mat != null) Destroy(mat);
        }
        private void OnDestroy() => Release();
    }
}
