using DarwinPark.Core;
using DarwinPark.Environment;
using DarwinPark.Planet;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    public class SimulationControlPanel : MonoBehaviour
    {
        [Header("实体配置")]
        public GameObject EntityPrefab;

        private SimulationClock _clock;
        private Text _timeText;
        private Image _playIcon;
        private Text _playFallback;
        private Sprite _iconPlay, _iconPause;

        private int _currentPage = 0;
        private GameObject _page1Container;
        private GameObject _page2Container;
        private Button _tabPrevBtn;
        private Button _tabNextBtn;

        private GameObject _activeSpeedMenu;
        private GameObject _activeParamMenu;
        private string _activeParamId;
        private GameObject _bottomPanel;
        private readonly Vector3[] _panelCorners = new Vector3[4];

        private string _currentSelectedLayer = "default";

        private UIRoot _uiRoot;
        private bool _uiBuilt;
        private Coroutine _buildRoutine;

        private void OnEnable()
        {
            // ★ 修复（防Bug回归）：旧版在 Start 里直接 Get<UIRoot>，一旦 UIRoot 还没
            // Awake（脚本执行顺序/重复实例/快速进入播放模式）就直接 return，
            // 底部栏永远不构建，表现为“底部控制栏消失”。这里改成等待并重试。
            // ★ 修复（防Bug回归）：GameObject 禁用会终止协程，重新启用时必须恢复等待。
            if (!_uiBuilt) _buildRoutine = StartCoroutine(BuildWhenReady());
        }

        private System.Collections.IEnumerator BuildWhenReady()
        {
            // ★ 修复（防Bug回归）：初始化耗时不能让 HUD 永久放弃构建。
            while (isActiveAndEnabled)
            {
                if (ServiceLocator.TryGet<UIRoot>(out var root) && root != null && root.MainCanvas != null)
                {
                    _uiRoot = root;
                    BuildAll(root);
                    yield break;
                }
                yield return null;
            }

            Log.Warn(Log.Tag.UI, "底部控制栏停止等待：组件已禁用");
        }

        private void BuildAll(UIRoot root)
        {
            if (_uiBuilt) return;

            UIIconRegistry.Init();
            _iconPlay = UIIconRegistry.Get("time_play") ?? UIIconRegistry.Get("play");
            _iconPause = UIIconRegistry.Get("time_pause") ?? UIIconRegistry.Get("pause");

            _clock = ServiceLocator.Get<SimulationClock>();

            // ★ 修复：主 HUD 必须稳定压在其他 Canvas 之上，否则会被全屏面板盖住。
            root.MainCanvas.sortingOrder = Mathf.Max(root.MainCanvas.sortingOrder, 200);

            BuildUI(root.MainCanvas.transform);
            // ★ 修复（防Bug回归）：只在构建完成后标记成功，避免异常后留下半成品。
            _uiBuilt = true;

            // ★ 修复：强制刷新 Canvas，防止在某些 Game View 尺寸下底部栏使用旧布局。
            Canvas.ForceUpdateCanvases();
            KeepBottomPanelVisible();
            var br = _bottomPanel != null ? _bottomPanel.GetComponent<RectTransform>() : null;
            Log.Info(Log.Tag.UI,
                $"底部控制栏构建完成：screen={Screen.width}x{Screen.height}, panel={(_bottomPanel != null ? "ok" : "missing")}, " +
                $"rect={(br != null ? br.rect.ToString() : "null")}, pos={(br != null ? br.position.ToString() : "null")}, " +
                $"canvasScale={root.MainCanvas.scaleFactor:F2}");

            if (_clock != null)
            {
                _clock.OnRunningChanged -= OnRunningChanged;
                _clock.OnRunningChanged += OnRunningChanged;
            }
        }

        private void OnDisable()
        {
            // ★ 修复（防Bug回归）：防止组件反复启用后同时运行多个构建协程。
            if (_buildRoutine != null) StopCoroutine(_buildRoutine);
            _buildRoutine = null;
        }

        private void OnDestroy()
        {
            if (_buildRoutine != null) StopCoroutine(_buildRoutine);
            if (_clock != null) _clock.OnRunningChanged -= OnRunningChanged;
            DestroyMenus();
        }

        private void BuildUI(Transform canvas)
        {
            var bottomPanel = UIFactory.NewRect("BottomPanel", canvas, typeof(Image));
            _bottomPanel = bottomPanel;
            bottomPanel.transform.SetAsLastSibling();
            var prt = bottomPanel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0, 0);
            prt.anchorMax = new Vector2(1, 0);
            prt.pivot = new Vector2(0.5f, 0);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(0, 210f);
            bottomPanel.GetComponent<Image>().color = new Color(0.055f, 0.085f, 0.14f, 0.97f);

            // ★ 新增：顶部高亮线。旧版控制栏是深蓝黑，叠在黑色太空背景上几乎“隐形”，
            // 容易被误认为消失；加一条 2px 的青线，任何背景下都能看出栏体位置。
            var topLine = UIFactory.NewRect("TopLine", bottomPanel.transform, typeof(Image));
            var tlrt = topLine.GetComponent<RectTransform>();
            tlrt.anchorMin = new Vector2(0, 1);
            tlrt.anchorMax = new Vector2(1, 1);
            tlrt.pivot = new Vector2(0.5f, 1);
            tlrt.sizeDelta = new Vector2(0, 2);
            topLine.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.95f, 0.85f);
            topLine.GetComponent<Image>().raycastTarget = false;

            var tabBar = UIFactory.NewRect("TabBar", bottomPanel.transform);
            var tbrt = tabBar.GetComponent<RectTransform>();
            tbrt.anchorMin = new Vector2(0, 1);
            tbrt.anchorMax = new Vector2(1, 1);
            tbrt.pivot = new Vector2(0.5f, 1);
            tbrt.sizeDelta = new Vector2(0, 35);

            _tabPrevBtn = CreateIconButton("ui_icon_prev", tabBar.transform, () => SwitchPage(0));
            var prevRt = _tabPrevBtn.GetComponent<RectTransform>();
            prevRt.anchorMin = new Vector2(0, 0.5f);
            prevRt.anchorMax = new Vector2(0, 0.5f);
            prevRt.pivot = new Vector2(0, 0.5f);
            prevRt.anchoredPosition = new Vector2(20, 0);

            _tabNextBtn = CreateIconButton("ui_icon_next", tabBar.transform, () => SwitchPage(1));
            var nextRt = _tabNextBtn.GetComponent<RectTransform>();
            nextRt.anchorMin = new Vector2(0, 0.5f);
            nextRt.anchorMax = new Vector2(0, 0.5f);
            nextRt.pivot = new Vector2(0, 0.5f);
            nextRt.anchoredPosition = new Vector2(90, 0);

            var contentArea = UIFactory.NewRect("ContentArea", bottomPanel.transform);
            var cart = contentArea.GetComponent<RectTransform>();
            cart.anchorMin = new Vector2(0, 0);
            cart.anchorMax = new Vector2(1, 1);
            cart.offsetMin = new Vector2(10, 10);
            cart.offsetMax = new Vector2(-10, -35);

            // --- Page 1 ---
            _page1Container = UIFactory.NewRect("Page1", contentArea.transform);
            UIFactory.Stretch(_page1Container.GetComponent<RectTransform>());
            BuildPage1(_page1Container.transform);

            // --- Page 2 ---
            _page2Container = UIFactory.NewRect("Page2", contentArea.transform);
            UIFactory.Stretch(_page2Container.GetComponent<RectTransform>());
            BuildPage2(_page2Container.transform);
            _page2Container.SetActive(false);

            SwitchPage(0);
        }

        private Button CreateIconButton(string iconId, Transform parent, System.Action onClick)
        {
            var btnGO = UIFactory.NewRect(iconId, parent, typeof(Image), typeof(Button));
            var btn = btnGO.GetComponent<Button>();
            var bg = btnGO.GetComponent<Image>();
            bg.color = new Color(0.15f, 0.25f, 0.35f, 0.8f);
            var rt = btnGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60, 30);

            var sprite = UIIconRegistry.Get(iconId);
            if (sprite != null)
            {
                var iconGO = UIFactory.NewRect("Icon", btnGO.transform, typeof(Image));
                UIFactory.Stretch(iconGO.GetComponent<RectTransform>(), 5, 5);
                var iconImg = iconGO.GetComponent<Image>();
                iconImg.sprite = sprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            else
            {
                // ★ 修复：资源目录没有图标时，旧版按钮是一个无字深色块（看起来像消失）。
                string fallback = iconId.Contains("prev") ? "◀" : iconId.Contains("next") ? "▶" : "•";
                var txt = UIFactory.NewText("Fallback", btnGO.transform, fallback, 18, TextAnchor.MiddleCenter);
                UIFactory.Stretch(txt.rectTransform);
                txt.raycastTarget = false;
            }
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        private void SwitchPage(int targetPage)
        {
            _currentPage = Mathf.Clamp(targetPage, 0, 1);
            _page1Container.SetActive(_currentPage == 0);
            _page2Container.SetActive(_currentPage == 1);

            _tabPrevBtn.image.color = _currentPage == 0 ? new Color(0.2f, 0.6f, 0.8f, 0.8f) : new Color(0.15f, 0.25f, 0.35f, 0.8f);
            _tabNextBtn.image.color = _currentPage == 1 ? new Color(0.2f, 0.6f, 0.8f, 0.8f) : new Color(0.15f, 0.25f, 0.35f, 0.8f);
        }

        // ==========================================
        // 第一页：时间、图层与环境 (保留原版右键弹窗)
        // ==========================================
        private void BuildPage1(Transform parent)
        {
            var timeSec = CreateAbsoluteSection(parent, "TimeSec", 0, 300);
            BuildTimeSection(timeSec);

            CreateDivider(parent, 320);

            var layerSec = CreateAbsoluteSection(parent, "LayerSec", 340, 240);
            BuildLayerSection(layerSec);

            CreateDivider(parent, 600);

            var paramSec = CreateAbsoluteSection(parent, "ParamSec", 620, 200);
            BuildParamSection(paramSec);
        }

        private void BuildTimeSection(GameObject parent)
        {
            var btnGO = UIFactory.NewRect("PlayBtn", parent.transform, typeof(Image), typeof(Button), typeof(RightClickHandler));
            var brt = btnGO.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 0.5f);
            brt.anchorMax = new Vector2(0, 0.5f);
            brt.pivot = new Vector2(0, 0.5f);
            brt.sizeDelta = new Vector2(80, 80);
            brt.anchoredPosition = new Vector2(0, 0);

            var bg = btnGO.GetComponent<Image>();
            bg.color = new Color(0.1f, 0.2f, 0.3f, 0.8f);

            btnGO.GetComponent<Button>().onClick.AddListener(() => _clock?.Toggle());

            var iconGO = UIFactory.NewRect("Icon", btnGO.transform, typeof(Image));
            UIFactory.Stretch(iconGO.GetComponent<RectTransform>(), 15, 15);
            _playIcon = iconGO.GetComponent<Image>();
            _playIcon.sprite = _iconPause;
            _playIcon.preserveAspect = true;

            // ★ 适配：没有图标资源时用文字兜底，保证播放/暂停按钮始终可辨认。
            if (_iconPause == null && _iconPlay == null)
            {
                _playFallback = UIFactory.NewText("PlayFallback", btnGO.transform, "II", 26, TextAnchor.MiddleCenter);
                UIFactory.Stretch(_playFallback.rectTransform);
                _playFallback.raycastTarget = false;
            }

            btnGO.GetComponent<RightClickHandler>().OnRightClick += () => ToggleSpeedMenu(brt);

            var timeTextGO = UIFactory.NewText("TimeText", parent.transform, "0-00-00\n(第 0 天) 00:00", 16, TextAnchor.MiddleLeft);
            var trt = timeTextGO.rectTransform;
            trt.anchorMin = new Vector2(0, 0.5f);
            trt.anchorMax = new Vector2(0, 0.5f);
            trt.pivot = new Vector2(0, 0.5f);
            trt.sizeDelta = new Vector2(200, 80);
            trt.anchoredPosition = new Vector2(100, 0);
            _timeText = timeTextGO;
        }

        private void ToggleSpeedMenu(RectTransform targetBtn)
        {
            if (_activeSpeedMenu != null) { Destroy(_activeSpeedMenu); _activeSpeedMenu = null; return; }
            DestroyMenus();

            var root = ServiceLocator.Get<UIRoot>("需要 UIRoot").MainCanvas.transform;
            _activeSpeedMenu = UIFactory.NewRect("SpeedMenu", root, typeof(Image));
            var mrt = _activeSpeedMenu.GetComponent<RectTransform>();
            _activeSpeedMenu.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.95f);

            Vector3[] corners = new Vector3[4];
            targetBtn.GetWorldCorners(corners);
            mrt.position = corners[1] + new Vector3(0, 10, 0);
            mrt.pivot = new Vector2(0, 0);
            mrt.sizeDelta = new Vector2(120, 200);

            var vlg = _activeSpeedMenu.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 2;

            string[] speeds = { "暂停", "1x", "2x", "4x", "8x", "16x" };
            float[] values = { 0f, 1f, 2f, 4f, 8f, 16f };

            for (int i = 0; i < speeds.Length; i++)
            {
                float speedValue = values[i];
                var btn = UIFactory.NewButton("Speed_" + i, _activeSpeedMenu.transform, speeds[i], () => {
                    if (speedValue == 0) _clock.Pause();
                    else { _clock.SetTimeScale(speedValue); if (!_clock.IsRunning) _clock.Play(); }
                    DestroyMenus();
                }, new Vector2(100, 30));
                btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            }
        }





        private void BuildParamSection(GameObject parent)
        {
            var grid = parent.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(80, 80);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleCenter;

            CreateParamButton("btn_param_temp", "温度", parent.transform);
            CreateParamButton("btn_param_hum", "湿度", parent.transform);
            CreateParamButton("btn_param_terrain", "地形", parent.transform);
            CreateParamButton("btn_param_seed", "种子", parent.transform);
        }

        private void CreateParamButton(string iconId, string fallbackName, Transform parent)
        {
            var btnGO = UIFactory.NewRect(iconId, parent, typeof(Image), typeof(Button), typeof(RightClickHandler));
            var bg = btnGO.GetComponent<Image>();
            bg.color = new Color(0.15f, 0.25f, 0.35f, 0.8f);

            var sprite = UIIconRegistry.Get(iconId);
            if (sprite != null)
            {
                var iconGO = UIFactory.NewRect("Icon", btnGO.transform, typeof(Image));
                var irt = iconGO.GetComponent<RectTransform>();
                UIFactory.Stretch(irt, 20, 20);
                var iconImg = iconGO.GetComponent<Image>();
                iconImg.sprite = sprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            var txt = UIFactory.NewText("Name", btnGO.transform, fallbackName, 12, TextAnchor.LowerCenter);
            UIFactory.Stretch(txt.rectTransform, 2, 5);
            txt.raycastTarget = false;

            // ★ 修复（防Bug回归）：参数左键打开，右键只取消，不再把滑条释放冒泡到覆盖层的关闭按钮。
            btnGO.GetComponent<Button>().onClick.AddListener(() => ToggleParamPopup(iconId, fallbackName));
            btnGO.GetComponent<RightClickHandler>().OnRightClick += DestroyMenus;
        }

        private void ToggleParamPopup(string paramId, string paramName)
        {
            if (_activeParamMenu != null && _activeParamId==paramId) { DestroyMenus(); return; }
            DestroyMenus();

            var root = ServiceLocator.Get<UIRoot>("需要 UIRoot").MainCanvas.transform;

            _activeParamId=paramId;
            _activeParamMenu = UIFactory.NewRect("ParamMenuOverlay", root, typeof(Image), typeof(RightClickHandler));
            var oRt = _activeParamMenu.GetComponent<RectTransform>();
            oRt.anchorMin = Vector2.zero; oRt.anchorMax = Vector2.one; oRt.offsetMin = Vector2.zero; oRt.offsetMax = Vector2.zero;
            _activeParamMenu.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            // ★ 修复（防Bug回归）：透明根节点不挡住原按钮，允许再次点击关闭。
            _activeParamMenu.GetComponent<Image>().raycastTarget=false;
            _activeParamMenu.GetComponent<RightClickHandler>().OnRightClick += DestroyMenus;

            var menu = UIFactory.NewRect("ParamMenuContent", _activeParamMenu.transform, typeof(Image));
            var mrt = menu.GetComponent<RectTransform>();
            menu.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.15f, 0.95f);
            menu.GetComponent<Image>().raycastTarget = true;

            mrt.anchorMin = new Vector2(0.5f, 0);
            mrt.anchorMax = new Vector2(0.5f, 0);
            mrt.pivot = new Vector2(0.5f, 0);
            mrt.anchoredPosition = new Vector2(0, 225f);
            mrt.sizeDelta = new Vector2(420, 90);

            var title = UIFactory.NewText("Title", menu.transform, "设定" + paramName + " (全年平均)", 16, TextAnchor.MiddleCenter);
            title.color = new Color(0.8f, 0.9f, 1f);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            trt.anchoredPosition = new Vector2(0, -8); trt.sizeDelta = new Vector2(0, 20);

            // ★ 适配：参数草稿、文本输入与 Shift 应用由独立编辑器管理，种子不再落入空分支。
            var editor=menu.AddComponent<PlanetParameterEditor>();
            editor.Initialize(paramId, paramName);
            editor.CancelRequested=DestroyMenus;
        }
        private void DestroyMenus()
        {
            if (_activeSpeedMenu != null) Destroy(_activeSpeedMenu);
            if (_activeParamMenu != null) Destroy(_activeParamMenu);
            _activeSpeedMenu = null;
            _activeParamMenu = null;_activeParamId=null;
        }





        // ==========================================
        // 第二页：直接实例化独立脚本
        // ==========================================
        private void BuildPage2(Transform parent)
        {
            var page2GO = UIFactory.NewRect("EntitySpawnPanel", parent);
            UIFactory.Stretch(page2GO.GetComponent<RectTransform>());

            var spawnPanel = page2GO.AddComponent<EntitySpawnPanel>();
            spawnPanel.DefaultEntityPrefab = this.EntityPrefab;
        }

        // ---------------------------------------------
        // 工具方法
        // ---------------------------------------------
        private GameObject CreateAbsoluteSection(Transform parent, string name, float xOffset, float width)
        {
            var go = UIFactory.NewRect(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(xOffset, 0);
            rt.offsetMax = new Vector2(xOffset + width, 0);
            return go;
        }

        private void CreateDivider(Transform parent, float xOffset)
        {
            var div = UIFactory.NewRect("Divider", parent, typeof(Image));
            var rt = div.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.1f);
            rt.anchorMax = new Vector2(0, 0.9f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(xOffset, 0);
            rt.offsetMax = new Vector2(xOffset + 2, 0);
            div.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 0.3f);
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape))DestroyMenus();
            // ★ 修复：万一底部栏被外部脚本/场景切换销毁，自动重建，而不是永久消失。
            if (_uiBuilt && _bottomPanel == null && _uiRoot != null)
            {
                _uiBuilt = false;
                BuildAll(_uiRoot);
            }

            KeepBottomPanelVisible();

            if (_clock != null && _timeText != null)
            {
                // ★ 核心修复：统一 10秒=1天，365天=1年
                int totalDays = Mathf.FloorToInt(_clock.GameTime / 10f);

                int year = totalDays / 365;
                int dayOfYear = totalDays % 365;

                // 简单按 30天/月 计算
                int month = (dayOfYear / 30) + 1;
                int day = (dayOfYear % 30) + 1;

                // 一天内的时间进度 (0~10秒)
                float dayProgress = (_clock.GameTime % 10f) / 10f;
                int hour = Mathf.FloorToInt(dayProgress * 24f);
                int minute = Mathf.FloorToInt((dayProgress * 24f % 1f) * 60f);

                _timeText.text = $"第 {year} 年 {month} 月 {day} 日\n{hour:D2}:{minute:D2}";
            }
        }

        /// <summary>
        /// ★ 修复：底部栏必须始终贴在屏幕底部并位于 Canvas 其他 UI 之上。
        /// 只做幂等校正，不覆盖内容布局，避免每帧重建。
        /// </summary>
        private void KeepBottomPanelVisible()
        {
            if (_bottomPanel == null) return;

            if (!_bottomPanel.activeSelf) _bottomPanel.SetActive(true);

            var rt = _bottomPanel.GetComponent<RectTransform>();
            if (rt == null) return;

            bool layoutBroken =
                !Mathf.Approximately(rt.anchorMin.y, 0f) ||
                !Mathf.Approximately(rt.anchorMax.y, 0f) ||
                !Mathf.Approximately(rt.pivot.y, 0f) ||
                Mathf.Abs(rt.anchoredPosition.x) > 0.5f ||
                Mathf.Abs(rt.anchoredPosition.y) > 0.5f ||
                Mathf.Abs(rt.sizeDelta.y - 210f) > 0.5f;

            if (layoutBroken)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0f, 210f);
            }

            rt.GetWorldCorners(_panelCorners);
            // 如果底部栏整体被推到屏幕外，强制回到屏幕底部。
            bool offBottom = _panelCorners[0].y < -2f || _panelCorners[2].y < 20f;
            bool offSide = _panelCorners[0].x > 2f || _panelCorners[2].x < Screen.width - 2f;
            bool pushedUp = _panelCorners[0].y > Screen.height * 0.5f;
            if (offBottom || offSide || pushedUp)
            {
                rt.anchoredPosition = Vector2.zero;
                Canvas.ForceUpdateCanvases();
            }

            // ★ 修复（防Bug回归）：每帧抢最高层会把后打开的菜单盖住，表现为按钮没反应。
            // 底栏已在构建时排序；弹窗按打开顺序置顶，布局校正不再改兄弟顺序。
        }

        private void OnRunningChanged(bool running)
        {
            if (_playIcon != null) _playIcon.sprite = running ? _iconPause : _iconPlay;
            if (_playFallback != null) _playFallback.text = running ? "II" : "▶";
        }

        // ★ 补全：生成图层按钮的扫描逻辑（确保它存在）
        private void BuildLayerSection(GameObject parent)
        {
            // ★ 修复 1：先清空父节点下所有旧的子物体，防止重复生成
            foreach (Transform child in parent.transform) Destroy(child.gameObject);

            var grid = parent.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(60, 60);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            // ★ 修复 2：使用 HashSet 去重，防止扫描到相同名字的图片生成两遍
            HashSet<string> uniqueLayerIds = new HashSet<string>();

            foreach (var id in UIIconRegistry.GetAllIds())
            {
                if (id.StartsWith("layer_")) uniqueLayerIds.Add(id);
            }

            // 如果没扫到图片，塞入默认的三个图层
            if (uniqueLayerIds.Count == 0)
            {
                uniqueLayerIds.Add("layer_ore");
                uniqueLayerIds.Add("layer_terrain");
                uniqueLayerIds.Add("layer_climate");
            }

            foreach (var id in uniqueLayerIds)
            {
                string displayName = id.Replace("layer_", "");
                CreateLayerIcon(parent.transform, id, displayName);
            }
        }

        private void CreateLayerIcon(Transform parent, string iconId, string fallbackText)
        {
            var btnGO = UIFactory.NewRect(iconId, parent, typeof(Image), typeof(Button));
            var img = btnGO.GetComponent<Image>();
            var sprite = UIIconRegistry.Get(iconId);

            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.2f, 0.3f, 0.4f, 0.8f);
                var txt = UIFactory.NewText("Txt", btnGO.transform, fallbackText, 12, TextAnchor.MiddleCenter);
                UIFactory.Stretch(txt.rectTransform);
            }

            // 绑定点击事件
            btnGO.GetComponent<Button>().onClick.AddListener(() => OnLayerClicked(iconId));
        }

        private void OnLayerClicked(string layerId)
        {
            // ★ 核心修复：如果点击的是当前已选中的图层，则切回默认图层
            if (_currentSelectedLayer == layerId)
            {
                layerId = "default";
            }
            _currentSelectedLayer = layerId;

            Debug.Log($"[Layer] 切换到图层: {layerId}");

            // 1. 更新按钮高亮
            var images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject.name.StartsWith("layer_"))
                {
                    if (img.gameObject.name == layerId && layerId != "default")
                        img.color = new Color(0.2f, 0.8f, 1f); // 亮青色
                    else
                        img.color = new Color(0.2f, 0.3f, 0.4f, 0.8f); // 暗色
                }
            }

            // 2. 通知星球的 Shader 切换渲染模式
            var planet = GameObject.Find("Planet");
            if (planet != null)
            {
                var mr = planet.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var block = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(block);

                    if (layerId == "layer_ore")
                    {
                        block.SetFloat("_LayerMode", 3f);
                        if (DarwinPark.Planet.PlanetOreSystem.Instance != null)
                            block.SetTexture("_OreMap", DarwinPark.Planet.PlanetOreSystem.Instance.OreMapTexture);
                    }
                    else if (layerId == "layer_terrain") block.SetFloat("_LayerMode", 1f);
                    else if (layerId == "layer_climate")
                    {
                        // ★ 修复：切到气候图层时确保气候图已生成并绑定。
                        // 旧版没挂 PlanetClimateMap，Shader 只能采样默认灰图，表现为整颗星球发白。
                        var climateMap = DarwinPark.Planet.PlanetClimateMap.Instance;
                        if (climateMap == null)
                        {
                            Log.Error(Log.Tag.Planet, "找不到 PlanetClimateMap，无法显示气候图层");
                        }
                        else
                        {
                            if (!climateMap.IsReady) climateMap.GenerateClimateMap();
                            block.SetFloat("_LayerMode", 2f);
                            // ★ 修复：统一写回贴图 + _ClimateReady + 温度编码范围，
                            // 避免在生成后新建的 block 覆盖掉温度编码参数。
                            climateMap.ApplyToBlock(block);
                        }
                    }
                    else block.SetFloat("_LayerMode", 0f);

                    mr.SetPropertyBlock(block);
                }
            }
        }

    }
}
