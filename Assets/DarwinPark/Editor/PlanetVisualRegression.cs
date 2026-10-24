using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Planet;
using UnityEditor;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：真实播放场景验收入口，验证几何闭合、HUD、海面与天气 Shader 并输出画面。</summary>
    public static class PlanetVisualRegression
    {
        [MenuItem("Tools/Darwin Park/Validate Current Planet")]
        public static void Validate()
        {
            if (!Application.isPlaying) { Debug.LogError("请先进入播放模式再验收。"); return; }
            var builder = UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            if (builder == null || builder.Data == null) throw new InvalidOperationException("地形未就绪");
            var report = new StringBuilder();
            // ★ 修复（防Bug回归）：GUID 存在不代表脚本仍可加载，运行时遍历场景检查实际缺失组件。
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go.scene.IsValid() && GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                    throw new InvalidOperationException($"场景含缺失脚本: {go.name}");
            report.AppendLine("PASS: no missing MonoBehaviour scripts in loaded scene");
            var data = builder.Data;
            var edges = new Dictionary<(int, int), int>();
            for (int i = 0; i < data.Triangles.Length; i += 3)
            for (int side = 0; side < 3; side++)
            {
                int a = data.Triangles[i + side], b = data.Triangles[i + (side + 1) % 3];
                var key = (Math.Min(a, b), Math.Max(a, b));
                edges.TryGetValue(key, out int count); edges[key] = count + 1;
            }
            foreach (var pair in edges) if (pair.Value != 2) throw new InvalidOperationException("网格存在开放边或非流形边");
            int euler = data.Vertices.Length - edges.Count + data.Triangles.Length / 3;
            if (euler != 2) throw new InvalidOperationException("球面拓扑不闭合");
            report.AppendLine($"PASS: closed welded sphere V={data.Vertices.Length}, E={edges.Count}, Euler={euler}");
            if (!ServiceLocator.TryGet<UIRoot>(out var root) || root.MainCanvas == null)
                throw new InvalidOperationException("MainCanvas 缺失");
            var panel = root.MainCanvas.transform.Find("BottomPanel") as RectTransform;
            if (panel == null || !panel.gameObject.activeInHierarchy || panel.rect.height <= 0)
                throw new InvalidOperationException("底部栏不可见");
            report.AppendLine($"PASS: BottomPanel active, rect={panel.rect}, canvas enabled={root.MainCanvas.enabled}");
            var weather = builder.GetComponent<PlanetWeatherVisuals>();
            if (weather == null) throw new InvalidOperationException("天气组件未挂载");
            foreach (var shader in new[] { weather.oceanShader, weather.cloudShader, weather.atmosphereShader,
                builder.GetComponent<MeshRenderer>().sharedMaterial.shader })
            {
                if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException($"Shader 无效: {shader}");
                report.AppendLine($"PASS: shader {shader.name}");
            }
            foreach (string name in new[] { "OceanSurface", "CloudVolume", "AtmosphericScattering" })
                if (builder.transform.Find(name) == null) throw new InvalidOperationException($"视觉层缺失: {name}");
            string folder = Path.GetFullPath("Temp/PlanetReview");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "runtime-checks.txt"), report.ToString());
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "planet-with-clouds.png"));
            Debug.Log("[PlanetReview] " + report);
        }

        [MenuItem("Tools/Darwin Park/Capture Terrain Without Clouds")]
        public static void CaptureTerrain()
        {
            var weather = UnityEngine.Object.FindFirstObjectByType<PlanetWeatherVisuals>();
            if (!Application.isPlaying || weather == null) return;
            weather.showClouds = false;
            // 等两个编辑器更新，确保 LateUpdate 已关闭云层，再捕获实际游戏输出。
            EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            {
                Directory.CreateDirectory("Temp/PlanetReview");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/PlanetReview/planet-terrain.png"));
            };
        }
    }
}
