using DarwinPark.Core;
using DarwinPark.Environment;
using DarwinPark.Planet;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    public partial class SimulationControlPanel : MonoBehaviour
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
        private PlanetGlobeMinimap _minimap;
        private Button _tabPrevBtn;
        private Button _tabNextBtn;

        private GameObject _activeSpeedMenu;
        private GameObject _activeParamMenu;
        private string _activeParamId;
        private GameObject _bottomPanel;
        private bool _bottomPanelHidden;
        private readonly Vector3[] _panelCorners = new Vector3[4];
        // ★ 新增：提供底栏实际屏幕边界，让混用IMGUI的RTS栏跟随Canvas缩放，而不是猜测固定高度。
        public bool TryGetScreenRect(out Rect rect)
        {
            rect=default;if(_bottomPanel==null||!_bottomPanel.activeInHierarchy)return false;
            _bottomPanel.GetComponent<RectTransform>().GetWorldCorners(_panelCorners);
            var min=RectTransformUtility.WorldToScreenPoint(null,_panelCorners[0]);var max=RectTransformUtility.WorldToScreenPoint(null,_panelCorners[2]);
            rect=Rect.MinMaxRect(min.x,min.y,max.x,max.y);return true;
        }

        private string _currentSelectedLayer = "default";
        public string CurrentLayer=>_currentSelectedLayer;
        private readonly List<string> _layerOrder=new List<string>();

        private UIRoot _uiRoot;
        private bool _uiBuilt;
        private Coroutine _buildRoutine;
        private SimulationAuthorityView _authorityView;

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

        // ★ 修复（防Bug回归）：运行时按钮的非持久回调可能在脚本重载后丢失；读档前重建本世界控件，不能仅信任_uiBuilt。
        public void RebuildForWorld()
        {
            if(!ServiceLocator.TryGet<UIRoot>(out var root))return;
            if(_buildRoutine!=null){StopCoroutine(_buildRoutine);_buildRoutine=null;}
            if(_clock!=null)_clock.OnRunningChanged-=OnRunningChanged;
            DestroyMenus();
            if(_bottomPanel!=null){_bottomPanel.SetActive(false);Destroy(_bottomPanel);_bottomPanel=null;}
            // 重建只更新回调，保留读档前的分页，不能把第二页窗口的使用者留在已停用的旧控件上。
            int page=_currentPage;_uiBuilt=false;_uiRoot=root;_layerOrder.Clear();BuildAll(root);SwitchPage(page);
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
            _authorityView=new SimulationAuthorityView(_bottomPanel.transform,_page2Container.transform,_tabNextBtn);
            // ★ 修复（防Bug回归）：不再安装免费补给UI；能量块仅由实际生态转换或营养利用基因消耗营养吐出。
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
                // ★ 修复（防Bug回归）：读档先恢复暂停再重建HUD，必须立即同步当前状态，不能等待下一次事件才改掉默认暂停图标。
                OnRunningChanged(_clock.IsRunning);
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
            // ★ 适配：栏体保留全宽底部原矩形，只替换皮肤与分隔线。
            UIVisualTheme.Paint(bottomPanel.GetComponent<Image>());

            // ★ 新增：顶部高亮线。旧版控制栏是深蓝黑，叠在黑色太空背景上几乎“隐形”，
            // 容易被误认为消失；加一条 2px 的青线，任何背景下都能看出栏体位置。
            var topLine = UIFactory.NewRect("TopLine", bottomPanel.transform, typeof(Image));
            var tlrt = topLine.GetComponent<RectTransform>();
            tlrt.anchorMin = new Vector2(0, 1);
            tlrt.anchorMax = new Vector2(1, 1);
            tlrt.pivot = new Vector2(0.5f, 1);
            tlrt.sizeDelta = new Vector2(0, 2);
            topLine.GetComponent<Image>().color = UIVisualTheme.Border;
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

            // ★ 修复（防Bug回归）：按键控制属于设置页左侧分页，底栏仅保留原翻页按钮。

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

            // ★ 新增：全地图统计是只读公共信息，独立于两页创造/管理权限，成员也可筛选查看。
            var statistics=UIFactory.NewRect("MapEntityStatistics",_page1Container.transform,typeof(Image));
            statistics.AddComponent<MapEntityStatisticsUI>();
            // ★ 适配：规则独立卡片位于第一页，不改变统计/筛选和第二页种群档案布局。
            var rules=UIFactory.NewRect("SimulationRules",_page1Container.transform);rules.AddComponent<SimulationRulesPanel>();
            // ★ 新增：地图独占第一页右侧，原控件路径不变；统计/规则继续在剩余宽度中自适应。
            var globe=UIFactory.NewRect("GlobeMinimap",contentArea.transform,typeof(Image));_minimap=globe.AddComponent<PlanetGlobeMinimap>();
            _page1Container.GetComponent<RectTransform>().offsetMax=new Vector2(-_minimap.ReservedWidth,0);

            SwitchPage(0);
        }

        private Button CreateIconButton(string iconId, Transform parent, System.Action onClick)
        {
            var btnGO = UIFactory.NewRect(iconId, parent, typeof(Image), typeof(Button));
            var btn = btnGO.GetComponent<Button>();
            var bg = btnGO.GetComponent<Image>();
            UIVisualTheme.StyleButton(btn);
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
            // ★ 修复（防Bug回归）：分页必须同时结束根 Canvas 上的弹窗和投放模式。
            PlacementQuantityPopup.CloseAll();EntityPlacementTool.Instance?.CancelPlacement();DestroyMenus();
            // ★ 适配：页面切换与创造权限分离，创造控件单独屏蔽。
            _currentPage = Mathf.Clamp(targetPage, 0, 1);
            _page1Container.SetActive(_currentPage == 0);
            _page2Container.SetActive(_currentPage == 1);
            if(_minimap!=null)_minimap.gameObject.SetActive(_currentPage==0);

            UIVisualTheme.Paint(_tabPrevBtn.image,_currentPage==0?"selected":"button");
            UIVisualTheme.Paint(_tabNextBtn.image,_currentPage==1?"selected":"button");
        }

        // ==========================================
        // 第一页：时间、图层与环境 (保留原版右键弹窗)
        // ==========================================
        private void BuildPage1(Transform parent)
        {
            var timeSec = CreateAbsoluteSection(parent, "TimeSec", 0, 300);
            BuildTimeSection(timeSec);

            UIFactory.CreateDivider(parent, 320);

            var layerSec = CreateAbsoluteSection(parent, "LayerSec", 340, 240);
            BuildLayerSection(layerSec);

            UIFactory.CreateDivider(parent, 600);

            var paramSec = CreateAbsoluteSection(parent, "ParamSec", 620, 200);
            BuildParamSection(paramSec);
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
            // ★ 适配：种群列表属于第二页的独立只读入口，不随创造权限隐藏。
            var population=UIFactory.NewRect("PopulationList",parent,typeof(Image));population.AddComponent<MapPopulationListUI>();
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


        private void Update()
        {
            // ★ 适配：只在主动性能采样期间计时，定位完整场景的实际消耗。
            using var performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.BottomUI);
            if(WorldRuntimeGate.Ready&&!GameInputGate.Blocked)
            {
                if(ControlBindings.Down(ControlAction.ToggleBottomBar))
                {
                    HandleVisibilityKey(ControlBindings.Get(ControlAction.ToggleBottomBar),TypingInField());
                }
                else if(ControlBindings.Down(ControlAction.ClearLayer))
                {
                    if(!TypingInField())HandleLayerKey(ControlBindings.Get(ControlAction.ClearLayer));
                }
                else if(ControlBindings.Down(ControlAction.CycleLayer))HandleLayerKey(ControlBindings.Get(ControlAction.CycleLayer));
            }
            if(_currentSelectedLayer=="layer_population"&&!PopulationRangeLayer.Visible)SetLayer("default");
            // ★ 适配：普通成员看不到创造页，作弊权限撤回后立即关闭修改弹窗。
            if(_uiBuilt)_authorityView?.Apply(WorldAuthority.CanEdit,DestroyMenus);
            if(Input.GetKeyDown(KeyCode.Escape))DestroyMenus();
            // ★ 修复：万一底部栏被外部脚本/场景切换销毁，自动重建，而不是永久消失。
            if (_uiBuilt && _bottomPanel == null && _uiRoot != null)
            {
                _uiBuilt = false;
                BuildAll(_uiRoot);
            }

            KeepBottomPanelVisible();

            if (!_bottomPanelHidden && _clock != null && _timeText != null)
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

        private static bool TypingInField()
        {
            var selection=EventSystem.current?.currentSelectedGameObject;
            return selection!=null&&selection.GetComponentInParent<InputField>()?.isFocused==true;
        }

        public bool HandleVisibilityKey(KeyCode key,bool typing)
        {
            if(key!=ControlBindings.Get(ControlAction.ToggleBottomBar)||typing||GameInputGate.Blocked||!WorldRuntimeGate.Ready)return false;
            _bottomPanelHidden=!_bottomPanelHidden;
            if(_bottomPanelHidden)DestroyMenus();
            KeepBottomPanelVisible();
            return true;
        }

        /// <summary>
        /// ★ 修复：底部栏必须始终贴在屏幕底部并位于 Canvas 其他 UI 之上。
        /// 只做幂等校正，不覆盖内容布局，避免每帧重建。
        /// </summary>
        private void KeepBottomPanelVisible()
        {
            if (_bottomPanel == null) return;
            if (_bottomPanelHidden)
            {
                if(_bottomPanel.activeSelf)_bottomPanel.SetActive(false);
                return;
            }

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



        // ★ 补全：生成图层按钮的扫描逻辑（确保它存在）








    }
}
