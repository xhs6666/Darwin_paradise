using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：矿脉替换、投放预览、收藏及基因面板的播放模式回归。</summary>
    public static class EntityUIRegression
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        [MenuItem("Tools/Darwin Park/Validate Entity UI and Ore")]
        public static void Validate()
        {
            Require(Application.isPlaying, "请先进入播放模式");
            var control = UnityEngine.Object.FindFirstObjectByType<SimulationControlPanel>();
            Require(control != null, "控制栏未就绪");
            typeof(SimulationControlPanel).GetMethod("SwitchPage", Private).Invoke(control, new object[] {1});
            // ★ 修复：首次激活页面的 Start 在下一帧构建槽位，验收应等待真实初始化完成。
            double deadline = EditorApplication.timeSinceStartup + 5;
            EditorApplication.CallbackFunction waitForPage = null;
            waitForPage = () =>
            {
                if (!Application.isPlaying) { EditorApplication.update -= waitForPage; return; }
                bool ready = control != null && control.GetComponentsInChildren<EntitySpawnerUI>().Any(s => s.CustomGenome == null);
                if (!ready && EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= waitForPage;
                Require(ready, "实体生成页面初始化超时");
                ValidateReady();
            };
            EditorApplication.update += waitForPage;
        }

        private static void ValidateReady()
        {
            var report = new StringBuilder();
            var builder = UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var old = builder.GetComponent<OreGenerator>();
            Require(old == null || !old.enabled, "旧矿石生成器仍启用");
            Require(UnityEngine.Object.FindObjectsByType<OreNode>(FindObjectsSortMode.None).Length == 0, "场景还存在旧方块矿石");
            var ore = builder.GetComponent<PlanetOreSystem>();
            Require(ore != null && ore.enabled && ore == PlanetOreSystem.Instance && ore.OreMapTexture != null, "矿脉系统未挂载/未就绪");
            report.AppendLine("PASS: legacy OreGenerator disabled, OreNode count=0, PlanetOreSystem ready");
            float available = 0; Vector3 orePoint = default;
            for (int i = 0; i < 128 && available < 1; i++)
            {
                float phi = i * 2.39996323f, y = 1f - 2f * (i + .5f) / 128f;
                Vector3 d = new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(phi), y, Mathf.Sqrt(1-y*y)*Mathf.Sin(phi));
                orePoint = builder.transform.TransformPoint(d * builder.WaterRadius);
                available = ore.GetOreAmount(orePoint);
            }
            Require(available >= 1, "矿图未产生可采矿量");
            float extracted = ore.ConsumeOre(orePoint, .5f);
            Require(extracted > 0 && Mathf.Abs(available - extracted - ore.GetOreAmount(orePoint)) < .01f, "矿量查询/消耗不一致");
            report.AppendLine("PASS: mineral query/consumption uses PlanetOreSystem");

            var control = UnityEngine.Object.FindFirstObjectByType<SimulationControlPanel>();
            typeof(SimulationControlPanel).GetMethod("SwitchPage", Private).Invoke(control, new object[] {1});
            var slot = control.GetComponentsInChildren<EntitySpawnerUI>().First(s => s.CustomGenome == null);
            Require(slot.entityPrefab != null, "默认生成槽未绑定预制体");
            var initialFavorites = EntityFavoritesManager.Instance;
            initialFavorites.TogglePanel();
            var initialRoot = ServiceLocator.Get<UIRoot>();
            var initialList = initialRoot.MainCanvas.transform.Find("FavoritesPanel");
            Require(initialList != null && initialList.gameObject.activeInHierarchy, "收藏按钮未打开列表");
            if (initialList.GetComponentsInChildren<Button>().Length == 0)
                Require(initialList.Find("Content/Empty") != null, "首次空列表没有提示");
            initialFavorites.TogglePanel();
            report.AppendLine("PASS: favorites opens/closes with an explicit empty state");
            int before = EntityRegistry.All.Count;
            slot.OnPointerClick(new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left});
            var tool = EntityPlacementTool.Instance;
            Require(tool != null && tool.IsPlacing && EntityRegistry.All.Count == before, "单击未进入投放或预览错误注册实体");
            var preview = GameObject.Find("EntityPlacementPreview");
            if (preview == null) preview = Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == "EntityPlacementPreview" && g.scene.IsValid());
            Require(preview.GetComponentsInChildren<Collider>(true).Length == 0 && preview.GetComponentsInChildren<DarwinEntity>(true).Length == 0,
                "虚影包含实体/碰撞逻辑");
            tool.CancelPlacement(); Require(!tool.IsPlacing && !OrbitCamera.IsDraggingUI, "取消后输入锁未清理");
            report.AppendLine("PASS: click arms placement; preview has no collider/entity; cancel restores camera input");

            var camera = Camera.main;
            Require(EntityPlacementUtility.TryGetSurface(camera, new Vector2(camera.pixelWidth*.5f, camera.pixelHeight*.5f), out var point, out var normal), "中心射线未命中真实星球");
            var clock = ServiceLocator.Get<SimulationClock>(); clock.Pause();
            Require(EntityPlacementUtility.Spawn(null, point + normal * slot.spawnHeight), "投放失败");
            var entity = EntityRegistry.All.First(e => e != null && Vector3.Distance(e.transform.position, point + normal * slot.spawnHeight) < .1f);
            Require(EntityRegistry.All.Count == before + 1 && entity.GetComponent<EntityGeneController>().Genome.Genes.Count > 0, "默认生成缺少初始基因");
            var source = entity.GetComponent<EntityGeneController>().Genome;
            var copy = EntityPlacementUtility.CopyGenome(source);
            Require(copy != source && copy.Genes[0] != source.Genes[0] && copy.Genes[0].InstanceId != source.Genes[0].InstanceId, "模板基因未独立复制");
            report.AppendLine("PASS: placement spawns exactly one default entity with genes; template genomes are independent");

            int beforeDrag = EntityRegistry.All.Count;
            var drag = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = new Vector2(camera.pixelWidth*.5f, camera.pixelHeight*.5f), pointerId = -999 };
            slot.OnBeginDrag(drag); slot.OnDrag(drag);
            Require(EntityRegistry.All.Count == beforeDrag, "拖拽预览错误注册真实实体");
            slot.OnEndDrag(drag);
            Require(EntityRegistry.All.Count == beforeDrag + 1 && !OrbitCamera.IsDraggingUI, "拖拽松手没有生成唯一实体或输入未恢复");
            report.AppendLine("PASS: drag preview is visual-only; drop spawns exactly one entity and releases input");

            var inspector = UnityEngine.Object.FindFirstObjectByType<GeneInspectorUI>(); inspector.ShowEntityInfo(entity);
            var panel = (GameObject)typeof(GeneInspectorUI).GetField("_panel", Private).GetValue(inspector);
            var root = ServiceLocator.Get<UIRoot>();
            Require(panel.activeInHierarchy && panel.transform.parent == root.MainCanvas.transform, "基因面板不在屏幕 HUD 上");
            var geneRows = panel.GetComponentsInChildren<EventTrigger>();
            Require(geneRows.Length > 0 && geneRows.All(row => row.GetComponent<Image>() != null && row.GetComponent<Image>().raycastTarget), "基因行无悬浮/双击命中面");
            ((Button)typeof(GeneInspectorUI).GetField("_favBtn", Private).GetValue(inspector)).onClick.Invoke();
            var favorites = EntityFavoritesManager.Instance;
            Require(favorites.IsFavorite(entity), "基因面板收藏按钮无效");
            favorites.TogglePanel();
            var list = root.MainCanvas.transform.Find("FavoritesPanel");
            Require(list != null && list.gameObject.activeInHierarchy && list.GetComponentsInChildren<Button>().Length > 0, "收藏列表未显示条目");
            Canvas.ForceUpdateCanvases();
            var favoriteRow = list.GetComponentsInChildren<Button>().First();
            Require(favoriteRow.GetComponent<RectTransform>().rect.height >= 25f, "收藏条目被布局压成零高度");
            // 真正派发悬浮和双击事件，检查弹窗可见且位于主面板之上。
            var row = geneRows.First();
            ExecuteEvents.Execute<IPointerEnterHandler>(row.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            var tooltip = (GameObject)typeof(GeneInspectorUI).GetField("_tooltipPanel", Private).GetValue(inspector);
            Require(tooltip.activeInHierarchy && !tooltip.GetComponent<Image>().raycastTarget, "悬浮提示未显示或拦截自身射线");
            ExecuteEvents.Execute<IPointerClickHandler>(row.gameObject, new PointerEventData(EventSystem.current) {clickCount=2}, ExecuteEvents.pointerClickHandler);
            var tree = (GameObject)typeof(GeneInspectorUI).GetField("_treePanel", Private).GetValue(inspector);
            Require(tree.activeInHierarchy && tree.transform.GetSiblingIndex() > panel.transform.GetSiblingIndex(), "双击基因树未显示在主面板上方");
            tree.SetActive(false); tooltip.SetActive(false);
            panel.transform.Find("TitleBg/SaveTplBtn").GetComponent<Button>().onClick.Invoke();
            Require(EntityTemplateManager.Templates.Any(t => t.templateName == entity.DisplayName), "保存模板按钮无效");
            report.AppendLine("PASS: gene panel on MainCanvas; full-row hover/double-click hit targets; favorite/list/template buttons work");
            Directory.CreateDirectory("Artifacts/EntityUIReview");
            File.WriteAllText("Artifacts/EntityUIReview/runtime-checks.txt", report.ToString());
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/EntityUIReview/entity-ui.png"));
            Debug.Log("[EntityUIReview] " + report);
        }
    }
}
