using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DarwinPark.Planet
{
    public class OreGenerator : MonoBehaviour
    {
        [Header("生成设置")]
        public int maxOreCount = 150;
        public float minSize = 1.5f;
        public float maxSize = 4f;
        public Material oreMaterial;

        [Header("刷新设置")]
        public float refreshInterval = 10f;
        public int refreshAmount = 5;

        private float _planetRadius = 300f;
        private float _timer;
        private List<GameObject> _activeOres = new List<GameObject>();
        private bool _isReady = false;

        private IEnumerator Start()
        {
            var builder = GetComponent<PlanetTerrainMeshBuilder>();
            if (builder != null) _planetRadius = builder.radius;

            // ★ 核心修复：等待 1 帧，确保 PlanetTerrainMeshBuilder 的 MeshCollider 已经挂载完成
            yield return null;

            _isReady = true;
            for (int i = 0; i < maxOreCount / 2; i++)
            {
                GenerateSingleOre();
            }
            Debug.Log($"[OreGen] 初始生成了一半矿石 (共 {maxOreCount / 2} 块)");
        }

        private void Update()
        {
            if (!_isReady) return;

            _timer += Time.deltaTime;
            if (_timer >= refreshInterval)
            {
                _timer = 0;
                RefreshOres();
            }
        }

        private void RefreshOres()
        {
            _activeOres.RemoveAll(ore => ore == null);

            if (_activeOres.Count < maxOreCount)
            {
                int countToSpawn = Mathf.Min(refreshAmount, maxOreCount - _activeOres.Count);
                for (int i = 0; i < countToSpawn; i++)
                {
                    GenerateSingleOre();
                }
            }
        }

        private void GenerateSingleOre()
        {
            Vector3 randomDir = Random.onUnitSphere;
            Ray ray = new Ray(transform.position, randomDir);

            // 射线检测地形
            if (Physics.Raycast(ray, out RaycastHit hit, _planetRadius * 2f))
            {
                if (hit.collider is MeshCollider)
                {
                    CreateOre(hit.point, hit.normal);
                }
            }
        }

        private void CreateOre(Vector3 position, Vector3 normal)
        {
            GameObject ore = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ore.name = "OreNode";
            ore.transform.position = position;
            ore.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(Random.Range(-15, 15), Random.Range(0, 360), Random.Range(-15, 15));
            ore.transform.localScale = Vector3.one * Random.Range(minSize, maxSize);

            var renderer = ore.GetComponent<Renderer>();
            if (oreMaterial != null) renderer.material = oreMaterial;
            else
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                Color defaultColor = Random.value > 0.5f ? new Color(0.6f, 0.5f, 0.2f) : new Color(0.3f, 0.6f, 0.6f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", defaultColor);
                else mat.color = defaultColor;
                renderer.material = mat;
            }

            var node = ore.AddComponent<OreNode>();
            node.maxNutrient = Random.Range(50f, 150f);
            node.currentNutrient = node.maxNutrient;

            _activeOres.Add(ore);
        }
    }
}