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
        private Mesh _oceanMesh, _cloudMesh, _atmosphereMesh;
        private Material _oceanMaterial, _cloudMaterial, _atmosphereMaterial;
        private GameObject _ocean, _clouds, _atmosphere;
        private PlanetTerrainMeshBuilder _builder;
        private MaterialPropertyBlock _sourceBlock;

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
            float atmosphereTop = cloudTop + atmosphereThickness;
            _ocean = CreateShell("OceanSurface", data, sea, oceanShader, ref _oceanMesh, out _oceanMaterial);
            if (_oceanMaterial != null)
            {
                var depth = new Color[data.Vertices.Length];
                for (int i = 0; i < depth.Length; i++)
                {
                    float h = PlanetTerrainSampler.SampleHeight(data, data.Vertices[i].normalized);
                    depth[i] = new Color(Mathf.Clamp01((data.SeaLevel - h) * data.HeightScale / Mathf.Max(0.1f, shelfDepth)), 0, 0, 1);
                }
                _oceanMesh.colors = depth;
                _oceanMaterial.SetColor("_ShallowColor", shallowWater);
                _oceanMaterial.SetColor("_DeepColor", deepWater);
            }
            _clouds = CreateShell("CloudVolume", data, cloudTop, cloudShader, ref _cloudMesh, out _cloudMaterial);
            if (_cloudMaterial != null)
            {
                _cloudMaterial.SetFloat("_BottomRadius", cloudBottom);
                _cloudMaterial.SetFloat("_TopRadius", cloudTop);
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
            }
            _sourceBlock = new MaterialPropertyBlock();
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
            var vertices = new Vector3[data.Vertices.Length];
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                normals[i] = data.Vertices[i].normalized;
                vertices[i] = normals[i] * radius;
            }
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            // ★ 修复（防Bug回归）：壳层按自身球面校正每个三角形绕序，不能沿用受陡坡影响的面剔除结果。
            var triangles = (int[])data.Triangles.Clone();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i+1]], c = vertices[triangles[i+2]];
                if (Vector3.Dot(Vector3.Cross(b-a,c-a), a+b+c) < 0)
                { int t = triangles[i+1]; triangles[i+1] = triangles[i+2]; triangles[i+2] = t; }
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = triangles;
            mesh.RecalculateBounds();
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
            // 数据图层关闭天气遮挡，海面延续同一气候/矿石图，保持图层切换完整。
            // ★ 修复（防Bug回归）：靠近地表自动隐藏云层；拉远恢复，独立保留用户总开关与数据层规则。
            var camera = Camera.main;
            float altitude = camera != null ? transform.InverseTransformPoint(camera.transform.position).magnitude - _builder.WaterRadius : float.MaxValue;
            if (_clouds != null) _clouds.SetActive(showClouds && layer < 0.5f && altitude > hideCloudsBelowAltitude);
            if (_oceanMaterial != null)
            {
                _oceanMaterial.SetFloat("_LayerMode", layer);
                _oceanMaterial.SetFloat("_ClimateReady", _sourceBlock.GetFloat("_ClimateReady"));
                _oceanMaterial.SetTexture("_ClimateMap", _sourceBlock.GetTexture("_ClimateMap"));
                _oceanMaterial.SetTexture("_OreMap", _sourceBlock.GetTexture("_OreMap"));
            }
            if (_clouds != null && _clouds.activeSelf)
                _clouds.transform.Rotate(Vector3.up, cloudSpeed * Time.deltaTime, Space.Self);
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
