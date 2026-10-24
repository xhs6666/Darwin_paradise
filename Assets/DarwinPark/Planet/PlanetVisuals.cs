using UnityEngine;

namespace DarwinPark.Planet
{
    public class PlanetVisuals : MonoBehaviour
    {
        public float starRadius = 2500f;
        public int starCount = 1800;
        // ★ 修复（防Bug回归）：原来 8 米的白色方块星星遮挡画面，改为可调的小型静态星点。
        public float starSize = 1.2f;

        void Start()
        {
            // ★ 适配：海面、大气和体积云由场景上的 PlanetWeatherVisuals 独立管理。
            SetupStars();
        }

        private void SetupStars()
        {
            if (GameObject.Find("Starfield") != null) return;

            GameObject starfield = new GameObject("Starfield");
            var ps = starfield.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startColor = Color.white;
            main.startSize = starSize;
            main.startLifetime = float.MaxValue;
            main.loop = false;
            main.startSpeed = 0f;
            main.maxParticles = starCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0, (short)starCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = starRadius;
            shape.radiusThickness = 0f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            renderer.material.color = Color.white;

            starfield.transform.position = transform.position;
            ps.Play();
        }
    }
}