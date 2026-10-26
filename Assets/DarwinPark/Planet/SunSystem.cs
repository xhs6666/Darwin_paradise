using UnityEngine;
using DarwinPark.Core; // ★ 确保引入 Core 命名空间

namespace DarwinPark.Planet
{
    public class SunSystem : MonoBehaviour
    {
        [Header("公转设置")]
        public Transform planet;
        public float distanceToPlanet = 800f;

        [Header("光照强度配置")]
        [Range(0f, 5f)] public float sunLightIntensity = 1.5f;
        [Range(0f, 5f)] public float ambientLightIntensity = 1.0f;

        [Header("环境光颜色配置 (受控)")]
        public Color ambientSkyColor = new Color(0.4f, 0.5f, 0.7f);
        public Color ambientEquatorColor = new Color(0.3f, 0.3f, 0.3f);
        public Color ambientGroundColor = new Color(0.1f, 0.1f, 0.1f);

        private Light _sunLight;
        private Transform _sunVisual;

        // ★ 修复：确保这里是带下划线的 _angle
        private float _angle = 45f;
        // ★ 适配：太阳公转相位成对保存/恢复，避免读档昼夜跳变。
        public float OrbitAngle {get=>_angle;set=>_angle=Mathf.Repeat(value,360); }

        void Start()
        {
            if (planet == null) planet = GameObject.Find("Planet")?.transform;

            _sunLight = GetComponent<Light>();
            if (_sunLight == null) _sunLight = gameObject.AddComponent<Light>();
            _sunLight.type = LightType.Directional;
            _sunLight.color = Color.white;
            _sunLight.shadows = LightShadows.Soft;

            GameObject sunGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sunGO.name = "Sun_Visual_Model";
            Destroy(sunGO.GetComponent<Collider>());
            _sunVisual = sunGO.transform;
            var renderer = sunGO.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(1f, 0.95f, 0.8f);
            renderer.material = mat;
            sunGO.transform.localScale = Vector3.one * 60f;

            ApplyLighting();
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

            if (_sunVisual != null) _sunVisual.position = transform.position + transform.forward * 300f;

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
    }
}