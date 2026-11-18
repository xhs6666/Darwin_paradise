using UnityEngine;
using DarwinPark.Core; // ★ 确保引入 Core 命名空间

namespace DarwinPark.Planet
{
    public class SunSystem : MonoBehaviour
    {
        [Header("公转设置")]
        public Transform planet;
        public float distanceToPlanet = 800f;
        [Range(.12f,.8f)] public float solarAngularRadius = .34f;
        public Color solarRimColor = new Color(1f,.68f,.28f);
        public static Vector3 LightDirection {get;private set;}=Vector3.forward;

        [Header("光照强度配置")]
        [Range(0f, 5f)] public float sunLightIntensity = 1.5f;
        [Range(0f, 5f)] public float ambientLightIntensity = 1.0f;

        [Header("环境光颜色配置 (受控)")]
        public Color ambientSkyColor = new Color(0.4f, 0.5f, 0.7f);
        public Color ambientEquatorColor = new Color(0.3f, 0.3f, 0.3f);
        public Color ambientGroundColor = new Color(0.1f, 0.1f, 0.1f);

        private Light _sunLight;
        private Material _solarMaterial;
        private Mesh _solarMesh;

        // ★ 修复：确保这里是带下划线的 _angle
        private float _angle = 45f;
        // ★ 适配：太阳公转相位成对保存/恢复，避免读档昼夜跳变。
        public float OrbitAngle {get=>_angle;set=>_angle=Mathf.Repeat(value,360); }

        void Start()
        {
            if (planet == null) planet = GameObject.Find("Planet")?.transform;
            // ★ 新增：专服保留太阳相位与光合方向，不创建可见太阳、材质或额外灯光。
            if(DedicatedServerMode.Enabled)return;

            _sunLight = GetComponent<Light>();
            if (_sunLight == null) _sunLight = gameObject.AddComponent<Light>();
            _sunLight.type = LightType.Directional;
            _sunLight.color = Color.white;
            _sunLight.shadows = LightShadows.Soft;

            CreateSolarDisc();
            ApplyLighting();
        }

        private void CreateSolarDisc()
        {
            var shader = Resources.Load<Shader>("SolarDisc");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[太阳] 缺少可用的 SolarDisc 着色器。", this);
                return;
            }
            var visual = new GameObject("Sun_Visual_Model", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(transform, false);
            float offset=Mathf.Min(300f,distanceToPlanet*.25f);
            visual.transform.localPosition=Vector3.forward*offset;
            float discRadius=Mathf.Tan(solarAngularRadius*Mathf.Deg2Rad)*(distanceToPlanet-offset);
            _solarMesh = new Mesh
            {
                name = "SolarDisc",
                vertices = new[] {new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0)},
                triangles = new[] {0,1,2,0,2,3},
                bounds = new Bounds(Vector3.zero, Vector3.one * (discRadius*32))
            };
            visual.GetComponent<MeshFilter>().sharedMesh = _solarMesh;
            _solarMaterial = new Material(shader) {name = "SolarDisc (Runtime)"};
            _solarMaterial.SetFloat("_DiscRadius", discRadius);
            _solarMaterial.SetColor("_RimColor", solarRimColor);
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _solarMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void Update()
        {
            if (planet == null) return;

            float dt = 0f;
            if (ServiceLocator.TryGet<SimulationClock>(out var clock))
            {
                dt = clock.GameDeltaTime;
            }

            if (dt > 0)
            {
                // ★ 修复：全部改为带下划线的 _angle
                _angle += (360f / 10f) * dt; // 10秒转一圈
                if (_angle >= 360f) _angle -= 360f;
            }

            // ★ 修复：改用 _angle
            Quaternion rot = Quaternion.Euler(23.5f, _angle, 0);
            transform.position = planet.position + rot * (Vector3.back * distanceToPlanet);
            transform.LookAt(planet.position);
            LightDirection=-transform.forward;

            ApplyLighting();
        }

        private void ApplyLighting()
        {
            if (_sunLight != null) _sunLight.intensity = sunLightIntensity;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;
            RenderSettings.ambientIntensity = ambientLightIntensity;
        }

        private void OnDestroy()
        {
            if (_solarMaterial != null) Destroy(_solarMaterial);
            if (_solarMesh != null) Destroy(_solarMesh);
        }
    }
}
