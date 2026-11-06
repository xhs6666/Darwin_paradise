using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;

namespace DarwinPark.UI
{
    public class GeneInspectorUI : MonoBehaviour
    {
        private GameObject _panel;
        private Text _titleText;
        private InputField _nameInput;
        private Transform _contentParent;
        private DarwinEntity _currentEntity;
        // ★ 适配：世界补给工具使用当前选择，避免按实体名称猜测种群或要求重复选择。
        public DarwinEntity CurrentEntity=>_currentEntity;

        private Button _favBtn;
        private Text _favBtnText;
        private Image _favIcon;
        private GameObject _tooltipPanel;
        private Text _tooltipText;
        private GameObject _treePanel;
        private Text _treeText;
        private GameObject _addGenePanel;
        private Transform _addGeneListParent;


        private GameObject _mutationContainer;


        private List<GeneEvaluation> _allMutations = new List<GeneEvaluation>();
        public float refreshInterval=.25f;
        public Vector2 infoCornerOffset=new Vector2(12,-12), addCornerOffset=new Vector2(12,225);
        public Vector2 infoPanelSize=new Vector2(520,560);
        private float _nextRefresh;
        private Text _baseInfo;
        private ScrollRect _scroll;
        private string _structure;
        private readonly List<System.Action> _rowRefresh=new List<System.Action>();

        private Dictionary<string, string> _attrNameMap = new Dictionary<string, string>()
        {
            {"growthSpeed", "成长速度"}, {"agingSpeed", "衰老速度"}, {"absorbEfficiency", "吸收效率"},
            {"baseStorage", "基础储存上限"}, {"nutrientStorage", "营养储存上限"}, {"conversionEfficiency", "转换效率"},
            {"repairEfficiency", "修复效率"}, {"repairCapability", "修复能力"}, {"moveSpeed", "移动速度"},
            {"senseRange", "感知范围"}, {"jumpAbility", "跳跃能力"}, {"attack", "攻击力"},
            {"eggDefense", "卵壳防御"}, {"hatchSpeed", "孵化速度"},
            {"environmentResistance", "环境抵抗力"}
            ,{"energy_block_absorption","能量块吸收速率"},{"energy_block_release","能量块吐出量"}
            ,{"territory_radius","领地半径"},{"defend_bonus","领地防御"},{"leadership_aura","领袖光环"},{"coordination","协同效率"}
            ,{"host_drain_rate","寄生吸取速率"},{"hide_from_host","寄生隐蔽"},{"gestationEfficiency","孕育效率"}
            // ★ 适配：新食性参数使用中文显示，避免实体面板只显示内部属性ID。
            ,{"digestion_efficiency","消化效率"},{"scavenge_rate","食腐吸收速率"}
        };

        private void Start()
        {
            EnsurePanel();
        }

        // ★ 修复（防Bug回归）：信息面板属于屏幕 HUD；旧版挂到没有相机的 WorldCanvas，尺寸/射线与底栏不一致。
        private bool EnsurePanel()
        {
            if (_panel != null) return true;
            if (!ServiceLocator.TryGet<UIRoot>(out var root) || root == null || root.MainCanvas == null) return false;
            BuildPanel(root.MainCanvas.transform);
            _panel.SetActive(false);
            return true;
        }

        private void BuildPanel(Transform canvas)
        {
            _panel = UIFactory.NewRect("GenePanel", canvas, typeof(Image));
            var rt = _panel.GetComponent<RectTransform>();
            // ★ 适配：信息面板放在屏幕左上，添加面板放在左下并避开底部控制栏。
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0,1);
            // ★ 修复（防Bug回归）：信息面板改为更宽、更矮，尺寸集中配置，避免下一帧又被旧固定尺寸覆盖。
            rt.anchoredPosition = FrameRateDisplay.ReserveTop(infoCornerOffset,canvas.GetComponent<Canvas>()); rt.sizeDelta = infoPanelSize;
            UIVisualTheme.Paint(_panel.GetComponent<Image>());
            // ★ 修复（防Bug回归）：面板背景必须挡住游戏射线与右键，避免编辑时拾取身后的星球。
            _panel.GetComponent<Image>().raycastTarget = true;

            var titleBg = UIFactory.NewRect("TitleBg", _panel.transform, typeof(Image));
            var trt = titleBg.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            // ★ 适配：标题只保留可编辑名称，压缩高度并给各按钮独立区域。
            trt.sizeDelta = new Vector2(0, 38);
            UIVisualTheme.Paint(titleBg.GetComponent<Image>(),"button");

            _favBtn = UIFactory.NewRect("FavBtn", titleBg.transform, typeof(Image), typeof(Button)).GetComponent<Button>();
            var frt = _favBtn.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0, 0.5f); frt.anchorMax = new Vector2(0, 0.5f); frt.pivot = new Vector2(0, 0.5f);
            frt.anchoredPosition = new Vector2(8, 0); frt.sizeDelta = new Vector2(28, 28);
            UIVisualTheme.StyleButton(_favBtn);
            _favBtnText = UIFactory.NewText("Star", _favBtn.transform, "☆", 20, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_favBtnText.rectTransform);
            _favBtnText.enabled=false;
            _favIcon=UIVisualTheme.AddIcon(_favBtn.transform,"star",new Vector2(22,22),Vector2.zero,new Vector2(.5f,.5f));
            _favBtn.onClick.AddListener(OnFavoriteClicked);

            var nameGO = UIFactory.NewRect("NameInput", titleBg.transform, typeof(Image), typeof(InputField));
            var nrt = nameGO.GetComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(44, 5); nrt.offsetMax = new Vector2(-126, -5);
            UIVisualTheme.Paint(nameGO.GetComponent<Image>(),"field");
            _nameInput = nameGO.GetComponent<InputField>(); _nameInput.characterLimit = 24;
            _titleText = UIFactory.NewText("Text", nameGO.transform, "", 16);
            UIFactory.Stretch(_titleText.rectTransform, 6, 1); _nameInput.textComponent = _titleText;
            _nameInput.onEndEdit.AddListener(RenameCurrentEntity);

            var saveTplGO = UIFactory.NewRect("SaveTplBtn", titleBg.transform, typeof(Image), typeof(Button));
            var strt = saveTplGO.GetComponent<RectTransform>();
            strt.anchorMin = new Vector2(1, 0.5f); strt.anchorMax = new Vector2(1, 0.5f); strt.pivot = new Vector2(1, 0.5f);
            strt.anchoredPosition = new Vector2(-40, 0); strt.sizeDelta = new Vector2(74, 28);
            var saveBtn = saveTplGO.GetComponent<Button>();
            var saveTxt = UIFactory.NewText("Txt", saveTplGO.transform, "存为模板", 14, TextAnchor.MiddleCenter);
            UIFactory.Stretch(saveTxt.rectTransform);
            UIVisualTheme.StyleButton(saveBtn,true);
            saveBtn.onClick.AddListener(() => { if (_currentEntity != null) EntityTemplateManager.SaveTemplate(_currentEntity); });

            // ★ 修复（防Bug回归）：面板挡住世界输入后提供显式关闭入口。
            var closePanel = UIFactory.NewButton("ClosePanel", titleBg.transform, "×", () =>
            {
                HideEntityInfo();
            }, new Vector2(26, 28));
            var closeRect = closePanel.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, .5f);
            closeRect.pivot = new Vector2(1, .5f); closeRect.anchoredPosition = new Vector2(-8, 0);

            var scrollGO = UIFactory.NewRect("ScrollView", _panel.transform, typeof(ScrollRect));
            var srt = scrollGO.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(10, 10); srt.offsetMax = new Vector2(-10, -46);

            var viewport = UIFactory.NewRect("Viewport", scrollGO.transform, typeof(Image), typeof(RectMask2D));
            UIFactory.Stretch(viewport.GetComponent<RectTransform>());
            // ★ 修复（防Bug回归）：列表只需矩形裁剪，不使用首次打开时可能遮住整块内容的模板遮罩材质。
            viewport.GetComponent<Image>().color = Color.clear;

            var contentGO = UIFactory.NewRect("Content", viewport.transform);
            var crt = contentGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, 0);
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(15, 15, 10, 10); vlg.spacing = 8;
            vlg.childControlHeight = true; vlg.childControlWidth = true; vlg.childForceExpandHeight = false;
            var fitter = contentGO.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _contentParent = contentGO.transform;

            var scroll = scrollGO.GetComponent<ScrollRect>();
            _scroll=scroll;scroll.scrollSensitivity=25;
            scroll.content = crt; scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.horizontal = false; scroll.vertical = true;

            _tooltipPanel = UIFactory.NewRect("Tooltip", canvas, typeof(Image));
            var ttrt = _tooltipPanel.GetComponent<RectTransform>();
            ttrt.sizeDelta = new Vector2(200, 100); ttrt.pivot = new Vector2(0, 1);
            UIVisualTheme.Paint(_tooltipPanel.GetComponent<Image>());
            _tooltipPanel.GetComponent<Image>().raycastTarget = false;
            _tooltipText = UIFactory.NewText("TText", _tooltipPanel.transform, "", 13, TextAnchor.UpperLeft);
            UIFactory.Stretch(_tooltipText.rectTransform, 10, 10);
            _tooltipPanel.SetActive(false);

            _treePanel = UIFactory.NewRect("TreePanel", canvas, typeof(Image));
            var trtrt = _treePanel.GetComponent<RectTransform>();
            trtrt.anchorMin = new Vector2(0.5f, 0.5f); trtrt.anchorMax = new Vector2(0.5f, 0.5f); trtrt.pivot = new Vector2(0.5f, 0.5f);
            trtrt.sizeDelta = new Vector2(400, 300);
            UIVisualTheme.Paint(_treePanel.GetComponent<Image>());
            _treeText = UIFactory.NewText("TreeText", _treePanel.transform, "", 16, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_treeText.rectTransform);
            _treePanel.SetActive(false);
            _treePanel.AddComponent<Button>().onClick.AddListener(() => _treePanel.SetActive(false));

            _addGenePanel = UIFactory.NewRect("AddGenePanel", canvas, typeof(Image));
            var agprt = _addGenePanel.GetComponent<RectTransform>();
            agprt.anchorMin=agprt.anchorMax=agprt.pivot=Vector2.zero;
            agprt.anchoredPosition=addCornerOffset;
            agprt.sizeDelta = new Vector2(350, 450);
            UIVisualTheme.Paint(_addGenePanel.GetComponent<Image>());
            var agTitle = UIFactory.NewText("Title", _addGenePanel.transform, "选择要添加/复制的基因", 18, TextAnchor.MiddleCenter);
            agTitle.rectTransform.anchorMin = new Vector2(0, 1); agTitle.rectTransform.anchorMax = new Vector2(1, 1); agTitle.rectTransform.sizeDelta = new Vector2(0, 40);
            // ★ 修复（防Bug回归）：标题置于面板内并避开关闭按钮；新增基因数量增多后明确提示滚动。
            agTitle.rectTransform.pivot=new Vector2(.5f,1);agTitle.rectTransform.offsetMin=new Vector2(8,-40);agTitle.rectTransform.offsetMax=new Vector2(-45,0);
            var agHint=UIFactory.NewText("Hint",_addGenePanel.transform,"滚轮向下查看更多基因",13,TextAnchor.MiddleCenter);
            agHint.rectTransform.anchorMin=Vector2.zero;agHint.rectTransform.anchorMax=new Vector2(1,0);agHint.rectTransform.pivot=new Vector2(.5f,0);agHint.rectTransform.sizeDelta=new Vector2(0,24);
            var closeBtn = UIFactory.NewButton("Close", _addGenePanel.transform, "X", () => _addGenePanel.SetActive(false), new Vector2(30, 30));
            var cbrt = closeBtn.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1, 1); cbrt.anchorMax = new Vector2(1, 1); cbrt.pivot = new Vector2(1, 1); cbrt.anchoredPosition = new Vector2(-10, -5);

            var agScroll = UIFactory.NewRect("Scroll", _addGenePanel.transform, typeof(ScrollRect));
            var agsrt = agScroll.GetComponent<RectTransform>();
            agsrt.anchorMin = Vector2.zero; agsrt.anchorMax = Vector2.one; agsrt.offsetMin = new Vector2(10, 28); agsrt.offsetMax = new Vector2(-10, -46);
            var agView = UIFactory.NewRect("View", agScroll.transform, typeof(Image), typeof(RectMask2D));
            UIFactory.Stretch(agView.GetComponent<RectTransform>());
            agView.GetComponent<RectTransform>().offsetMax=new Vector2(-20,0);
            agView.GetComponent<Image>().color = Color.clear;
            var agContent = UIFactory.NewRect("Content", agView.transform);
            var agcrt = agContent.GetComponent<RectTransform>();
            agcrt.anchorMin = new Vector2(0, 1); agcrt.anchorMax = new Vector2(1, 1); agcrt.pivot = new Vector2(0.5f, 1); agcrt.sizeDelta = new Vector2(0, 0);
            var agVlg = agContent.AddComponent<VerticalLayoutGroup>();
            agVlg.spacing = 5; agVlg.childControlHeight = false; agVlg.childControlWidth = true;
            var agFitter = agContent.AddComponent<ContentSizeFitter>(); agFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _addGeneListParent = agContent.transform;
            var agScrollRect = agScroll.GetComponent<ScrollRect>();
            agScrollRect.content = agcrt; agScrollRect.viewport = agView.GetComponent<RectTransform>(); agScrollRect.horizontal = false; agScrollRect.vertical = true;
            // ★ 新增：持续显示滚动条，避免新增条目在列表下方却无法被发现。
            var track=UIFactory.NewRect("Scrollbar",agScroll.transform,typeof(Image),typeof(Scrollbar));
            var trackRect=track.GetComponent<RectTransform>();trackRect.anchorMin=new Vector2(1,0);trackRect.anchorMax=Vector2.one;trackRect.pivot=Vector2.one;trackRect.sizeDelta=new Vector2(14,0);
            UIVisualTheme.Paint(track.GetComponent<Image>(),"track");
            var handle=UIFactory.NewRect("Handle",track.transform,typeof(Image));UIFactory.Stretch(handle.GetComponent<RectTransform>());
            UIVisualTheme.Paint(handle.GetComponent<Image>(),"fill");
            var bar=track.GetComponent<Scrollbar>();bar.handleRect=handle.GetComponent<RectTransform>();bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
            agScrollRect.verticalScrollbar=bar;agScrollRect.scrollSensitivity=30;agScrollRect.movementType=ScrollRect.MovementType.Clamped;
            _addGenePanel.SetActive(false);
        }

        private void OnFavoriteClicked()
        {
            if (_currentEntity != null && EntityFavoritesManager.Instance != null)
            {
                EntityFavoritesManager.Instance.ToggleFavorite(_currentEntity);
                UpdateFavBtnVisual();
            }
        }

        // ★ 新增：名称文本框失焦/回车提交，空名称回退，收藏列表同步显示新名字。
        public void RenameCurrentEntity(string value)
        {
            if (_currentEntity == null||!WorldAuthority.CanEdit) return;
            string name = (value ?? "").Trim();
            if (!string.IsNullOrEmpty(name)) LanEditActions.Rename(_currentEntity,name);
            _nameInput.SetTextWithoutNotify(_currentEntity.DisplayName);
            EntityFavoritesManager.Instance?.NotifyEntityRenamed();
        }

        private void UpdateFavBtnVisual()
        {
            if (_currentEntity == null) return;
            bool isFav = EntityFavoritesManager.Instance != null && EntityFavoritesManager.Instance.IsFavorite(_currentEntity);
            _favBtnText.text = isFav ? "★" : "☆";
            _favBtnText.color = isFav ? new Color(1f, 0.8f, 0.2f) : Color.white;
            // ★ 适配：收藏状态仍由原数据决定，改用轮廓/实心星资源避免字体符号差异。
            _favIcon.sprite=UIIconRegistry.Get(isFav?"star_filled":"star");
            _favIcon.color=isFav?UIVisualTheme.Favorite:UIVisualTheme.Muted;
        }

        private void Update()
        {
            // ★ 适配：覆盖菜单拥有输入时不处理世界操作。
            if (DarwinPark.Core.GameInputGate.Blocked) return;
            if (!EnsurePanel()) return;
            // ★ 适配：普通成员只读信息，隐藏添加入口并禁用数值/名称输入；服务端仍会独立校验所有请求。
            if(_nameInput!=null)_nameInput.interactable=WorldAuthority.CanEdit;
            foreach(var field in _panel.GetComponentsInChildren<InputField>(true))field.interactable=WorldAuthority.CanEdit;
            var addControl=_panel.transform.Find("ScrollView/Viewport/Content/MutTitleRow/AddBtn");if(addControl!=null)addControl.gameObject.SetActive(WorldAuthority.CanEdit);
            var templateControl=_panel.transform.Find("TitleBg/SaveTplBtn");if(templateControl!=null)templateControl.gameObject.SetActive(WorldAuthority.CanEdit);
            if(!WorldAuthority.CanEdit)_addGenePanel.SetActive(false);
            // ★ 适配：小窗口同时打开两个面板时压缩高度，保持左上/左下且不覆盖底栏。
            float canvasHeight=(_panel.transform.parent as RectTransform).rect.height;
            var addRect=_addGenePanel.GetComponent<RectTransform>();
            addRect.sizeDelta=new Vector2(350,Mathf.Min(450,Mathf.Max(150,canvasHeight*.35f)));
            var panelRect=_panel.GetComponent<RectTransform>();
            // ★ 适配：FPS固定左上，信息面板随屏幕缩放为它留出空间，避免名称/收藏按钮被盖住。
            panelRect.anchoredPosition=FrameRateDisplay.ReserveTop(infoCornerOffset,_panel.GetComponentInParent<Canvas>());
            float canvasWidth=(_panel.transform.parent as RectTransform).rect.width;
            panelRect.sizeDelta=new Vector2(Mathf.Min(infoPanelSize.x,Mathf.Max(160,canvasWidth-infoCornerOffset.x-24)),Mathf.Min(infoPanelSize.y,Mathf.Max(160,canvasHeight-addCornerOffset.y-24-(_addGenePanel.activeSelf?addRect.sizeDelta.y+16:0))));
            // ★ 修复（防Bug回归）：可见时刷新数值，关闭/实体销毁时所有附属面板一并关闭。
            if(_panel.activeSelf && _currentEntity==null)HideEntityInfo();
            if(!_panel.activeSelf){_addGenePanel.SetActive(false);_treePanel.SetActive(false);_tooltipPanel.SetActive(false);}
            if(Time.unscaledTime>=_nextRefresh){_nextRefresh=Time.unscaledTime+refreshInterval;RefreshDisplayedInfo();}
            if(WorldAuthority.RtsMode)return;
            // ★ 适配：投放模式的右键由独立工具取消，不能同时打开实体面板。
            if (EntityPlacementTool.Instance != null && EntityPlacementTool.Instance.IsPlacing) return;
            // ★ 修复（防Bug回归）：UI 上的右键必须属于 UI，收藏定位和基因编辑不应关闭面板或穿透拾取。
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool entityView = Camera.main != null && Camera.main.GetComponent<OrbitCamera>() is OrbitCamera orbit && orbit.EntityViewActive;
            if (!entityView && !pointerOverUI && Input.GetMouseButtonDown(1))
            {
                var pickCamera=Camera.main;
                if(pickCamera!=null)InspectAt(pickCamera.ScreenPointToRay(Input.mousePosition));
                else {HideEntityInfo();WorldResourceInspector.Instance?.Hide();}
            }
            // ★ 修复（防Bug回归）：Esc 优先收起添加子窗口，保留正在查看的实体，避免一次取消丢失编辑上下文。
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_addGenePanel.activeSelf) _addGenePanel.SetActive(false);
                else HideEntityInfo();
            }

            if (_tooltipPanel.activeSelf)
            {
                Vector2 mousePos = Input.mousePosition;
                float w = _tooltipPanel.GetComponent<RectTransform>().sizeDelta.x;
                float h = _tooltipPanel.GetComponent<RectTransform>().sizeDelta.y;
                float offX = 15f; if (mousePos.x + w + offX > Screen.width) offX = -w - 15f;
                float offY = -15f; if (mousePos.y - h + offY < 0) offY = 15f + h;
                _tooltipPanel.transform.position = mousePos + new Vector2(offX, offY);
            }
        }

        // ★ 适配：活体与没有碰撞体的尸体/能量块共用最近可见目标拾取，仍尊重地形遮挡。
        public bool InspectAt(Ray ray)
        {
            if(WorldInspectablePicker.Pick(ray,Camera.main!=null?Camera.main.farClipPlane:2000,out var entity,out var resource))
            {if(entity!=null)ShowEntityInfo(entity);else{HideEntityInfo();WorldResourceInspector.Ensure().Show(resource);}return true;}
            HideEntityInfo();WorldResourceInspector.Instance?.Hide();return false;
        }

        public void HideEntityInfo()
        {
            if(_panel!=null)_panel.SetActive(false);
            if(_addGenePanel!=null)_addGenePanel.SetActive(false);
            if(_treePanel!=null)_treePanel.SetActive(false);
            if(_tooltipPanel!=null)_tooltipPanel.SetActive(false);
            _currentEntity=null;
        }
        private string StructureKey(EntityGeneController genes)=>string.Join("|",genes.Genome.Genes.ConvertAll(g=>g.InstanceId+":"+g.DefinitionId));
        public void RefreshDisplayedInfo()
        {
            if(_panel==null || !_panel.activeSelf || _currentEntity==null)return;
            var genes=_currentEntity.GetComponent<EntityGeneController>();if(genes?.Evaluation==null)return;
            if(_baseInfo!=null)_baseInfo.text=BuildBaseInfo(_currentEntity);
            if(!_nameInput.isFocused)_nameInput.SetTextWithoutNotify(_currentEntity.DisplayName);
            UpdateFavBtnVisual();
            if(_structure!=StructureKey(genes))
            {
                foreach(var input in _panel.GetComponentsInChildren<InputField>())if(input.isFocused)return;
                float position=_scroll.verticalNormalizedPosition;ShowEntityInfo(_currentEntity);Canvas.ForceUpdateCanvases();_scroll.verticalNormalizedPosition=position;
                return;
            }
            foreach(var refresh in _rowRefresh)refresh();
        }

        private string BuildBaseInfo(DarwinEntity entity)
        {
            var genes=entity.GetComponent<EntityGeneController>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<color=#4EE5B5><b>【基础信息】</b></color>");
            var stats = entity.GetComponent<EntityStats>();
            if (stats != null)
            {
                if (stats.TryGet("health", out var hp)) sb.AppendLine($"生命值: <color=#FF6B6B>{hp:F1}</color>");
                if (stats.TryGet("nutrient", out var nut)) sb.AppendLine($"饱食度: <color=#FFD166>{nut:F1}</color>");
                // ★ 删除这一行: if (stats.TryGet("storedNutrient", out var stored)) sb.AppendLine($"储存营养: <color=#FFD166>{stored:F1}</color>");
                if (stats.TryGet("size", out var size)) sb.AppendLine($"体型: <color=#4EE5B5>{size:F2}</color>");
            }

            // ★ 新增：读取实体自带状态显示在基础信息里
            var geneCtrl = entity.GetComponent<EntityGeneController>();
            if (geneCtrl != null)
            {
                string ageStr = "幼年";
                if (geneCtrl.CurrentAge == AgeState.Adult) ageStr = "成年";
                else if (geneCtrl.CurrentAge == AgeState.Elder) ageStr = "老年";
                sb.AppendLine($"生命阶段: <color=#4EE5B5>{ageStr}</color>");
            }

            if (genes.IsGestating) sb.AppendLine("<color=#4EE5B5>特殊状态: 孕育后代中...</color>");
            // ★ 适配：实际活动状态读取独立组件，不再始终显示自由活动。
            sb.AppendLine("当前状态: "+EntityStatePresentation.Current(entity));

            if (PlanetClimateSystem.Instance != null)
            {
                var climate = PlanetClimateSystem.Instance.GetClimateAt(entity.transform.position);
                sb.AppendLine($"当前温度: {climate.Temperature:F1}°C | 湿度: {climate.Humidity:F1}%");
            }

            // ★ 适配：显示温度/溺水环境伤害状态，便于验证新基因是否生效。
            var hazard = entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>();
            if (hazard != null)
                sb.AppendLine($"环境状态: {hazard.GetStatusLine()}");

            sb.AppendLine("<color=#294A5B>──────────────────────────────</color>");

            return sb.ToString();
        }

        public void ShowEntityInfo(DarwinEntity entity)
        {
            if (entity == null || !EnsurePanel()) return;
            WorldResourceInspector.Instance?.Hide();
            if(_currentEntity!=entity){_addGenePanel.SetActive(false);_treePanel.SetActive(false);}
            // ★ 修复（防Bug回归）：切换对象前提交旧输入，避免失焦事件把旧名字写到新实体。
            if (_nameInput != null && _nameInput.isFocused) _nameInput.DeactivateInputField();
            _currentEntity = entity;
            var genes = entity.GetComponent<EntityGeneController>();
            if (genes == null || genes.Evaluation == null) { _panel?.SetActive(false); return; }
            _rowRefresh.Clear();_structure=StructureKey(genes);

            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            _tooltipPanel.SetActive(false);
            UpdateFavBtnVisual();
            _nameInput.SetTextWithoutNotify(entity.DisplayName);

            foreach (Transform child in _contentParent){child.gameObject.SetActive(false);Destroy(child.gameObject);}

            string baseInfo=BuildBaseInfo(entity);

            // ★ 修改：高度回调至 100，因为去掉了生命阶段
            var baseInfoText = UIFactory.NewText("BaseInfo", _contentParent, baseInfo, 14, TextAnchor.UpperLeft);
            _baseInfo=baseInfoText;
            // ★ 适配：新增环境状态行，按 9 行文本预留高度，避免内容被截断。
            baseInfoText.gameObject.AddComponent<LayoutElement>().minHeight = 198;
            // ★ 适配：摘要预留独立活动状态行，分栏数值和细进度条代替连续正文。
            baseInfoText.enabled=false;
            var refreshSummary=EntitySummaryView.Build(baseInfoText.transform,entity);
            _rowRefresh.Add(refreshSummary);refreshSummary();
            // ★ 修复（防Bug回归）：实体详情只承担观察与基因编辑，群体命令和决斗按钮挤占基因列表，按新版布局取消整块操作区。

            // 2. 核心基因
            var coreTitle = UIFactory.NewText("CoreTitle", _contentParent, "<color=#99AFBF><b>核心基因</b></color>", 14, TextAnchor.UpperLeft);
            coreTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 25;
            string[] requiredGenes = { "gene_growth", "gene_reproduction", "gene_nutrition" };

            _allMutations.Clear();

            foreach (var reqId in requiredGenes)
            {
                var evals = genes.Evaluation.Genes.FindAll(g => g.Definition != null && g.Definition.Id == reqId);
                if (evals.Count > 0)
                {
                    CreateGeneRow(evals[0], genes, _contentParent);
                    for (int i = 1; i < evals.Count; i++) _allMutations.Add(evals[i]);
                }
                else
                {
                    var missing = UIFactory.NewText("Missing", _contentParent, $"<color=#FF6B6B>■ 缺失 ({reqId})</color>", 14, TextAnchor.UpperLeft);
                    missing.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                }
            }

            var div2 = UIFactory.NewText("Div2", _contentParent, "<color=#294A5B>──────────────────────────────</color>", 14, TextAnchor.UpperLeft);
            div2.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            // 3. 变异基因
            var mutTitleRow = UIFactory.NewRect("MutTitleRow", _contentParent);
            mutTitleRow.AddComponent<LayoutElement>().preferredHeight = 30;
            var mtHlg = mutTitleRow.AddComponent<HorizontalLayoutGroup>();
            mtHlg.childAlignment = TextAnchor.MiddleLeft; mtHlg.spacing = 10;
            mtHlg.childControlWidth = false;

            var mutTitle = UIFactory.NewText("MutTitle", mutTitleRow.transform, "<color=#99AFBF><b>扩展基因</b></color>", 14, TextAnchor.MiddleLeft);
            mutTitle.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;

            var addBtn = UIFactory.NewButton("AddBtn", mutTitleRow.transform, "+", () => ShowAddGeneMenu(), new Vector2(30, 20));

            foreach (var eval in genes.Evaluation.Genes)
            {
                if (eval.Definition == null || System.Array.IndexOf(requiredGenes, eval.Definition.Id) >= 0) continue;
                _allMutations.Add(eval);
            }


            _mutationContainer = UIFactory.NewRect("MutContainer", _contentParent);
            var mvl = _mutationContainer.AddComponent<VerticalLayoutGroup>();
            mvl.spacing = 5; mvl.childControlHeight = true; mvl.childControlWidth = true; mvl.childForceExpandHeight = false;
            var mFitter = _mutationContainer.AddComponent<ContentSizeFitter>(); mFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RefreshMutations();
        }

        private void RefreshMutations()
        {
            if (_mutationContainer == null) return;
            foreach (Transform child in _mutationContainer.transform) Destroy(child.gameObject);

            if (_allMutations.Count == 0)
            {
                var noMut = UIFactory.NewText("NoMut", _mutationContainer.transform, "<color=#888888>暂无变异基因</color>", 14, TextAnchor.UpperLeft);
                noMut.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            // ★ 修复（防Bug回归）：一次列出所有变异基因，由现有 ScrollRect 滚轮浏览，不再按四条分页。
            foreach(var mutation in _allMutations)
                CreateGeneRow(mutation,_currentEntity.GetComponent<EntityGeneController>(),_mutationContainer.transform);
        }
        private void CreateGeneRow(GeneEvaluation eval, EntityGeneController controller, Transform parent)
        {
            if (eval == null || eval.Definition == null) return;

            // ★ 新增：行背景与图标只装饰原有基因行，不改变编辑、滚动或双击命中。
            var geneBlock = UIFactory.NewRect("Block_" + eval.Definition.Id + "_" + eval.Gene.InstanceId.Substring(0, 4), parent,typeof(Image));
            UIVisualTheme.Paint(geneBlock.GetComponent<Image>(),"button");geneBlock.GetComponent<Image>().raycastTarget=false;
            var blockLE = geneBlock.AddComponent<LayoutElement>();
            // ★ 适配：带标签的基因增加独立一行，基因倾向与实体能力不会互相覆盖。
            blockLE.minHeight=blockLE.preferredHeight=eval.Definition.EntityStateTags.Count>0?94:70;
            if(eval.Definition.EntityStateTags.Count>0)
            {
                var tag=UIFactory.NewText("EntityStateTags",geneBlock.transform,EntityStatePresentation.GeneTags(eval.Definition),12,TextAnchor.MiddleLeft);
                tag.rectTransform.anchorMin=new Vector2(0,1);tag.rectTransform.anchorMax=Vector2.one;tag.rectTransform.pivot=new Vector2(.5f,1);
                tag.rectTransform.anchoredPosition=new Vector2(0,-66);tag.rectTransform.sizeDelta=new Vector2(-16,20);
            }

            var row1 = UIFactory.NewRect("Row1", geneBlock.transform);
            var r1rt = row1.GetComponent<RectTransform>();
            r1rt.anchorMin = new Vector2(0, 1); r1rt.anchorMax = new Vector2(1, 1); r1rt.pivot = new Vector2(0, 1);
            r1rt.anchoredPosition=new Vector2(8,-6);r1rt.sizeDelta = new Vector2(-16, 28);

            var colorGO = UIFactory.NewRect("Color", row1.transform, typeof(Image));
            var crt = colorGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0.5f); crt.anchorMax = new Vector2(0, 0.5f); crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(24, 24);
            var geneIcon=colorGO.GetComponent<Image>();geneIcon.sprite=UIIconRegistry.Get(UIVisualTheme.GeneIcon(eval.Definition.Id));
            geneIcon.color=eval.IsActive?UIVisualTheme.Accent:UIVisualTheme.Muted;geneIcon.preserveAspect=true;geneIcon.raycastTarget=false;

            string activeStr = eval.IsActive ? "<color=#4EE5B5>生效中</color>" : "<color=#FF6B6B>失效</color>";
            var nameTxt = UIFactory.NewText("Name", row1.transform, $"<b>{eval.Definition.DisplayName}</b> {activeStr}", 14, TextAnchor.MiddleLeft);
            var ntrt = nameTxt.rectTransform;
            ntrt.anchorMin = new Vector2(0, 0); ntrt.anchorMax = new Vector2(1, 1);
            ntrt.offsetMin = new Vector2(34, 0); ntrt.offsetMax = new Vector2(0, 0);nameTxt.horizontalOverflow=HorizontalWrapMode.Overflow;

            // ★ 修复（防Bug回归）：标题文字不接收射线时整行无点击面，只剩 12 像素色块能触发提示/双击。
            var rowHit = row1.AddComponent<Image>(); rowHit.color = Color.clear; rowHit.raycastTarget = true;
            var trigger = row1.AddComponent<EventTrigger>();
            // ★ 修复（防Bug回归）：EventTrigger 会截获滚轮，必须显式转交父 ScrollRect，悬停基因行也能滚动。
            var entryScroll=new EventTrigger.Entry{eventID=EventTriggerType.Scroll};
            entryScroll.callback.AddListener(data=>row1.GetComponentInParent<ScrollRect>()?.OnScroll((PointerEventData)data));
            trigger.triggers.Add(entryScroll);
            var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener((data) => ShowGeneTooltip(eval));
            trigger.triggers.Add(entryEnter);

            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((data) => _tooltipPanel.SetActive(false));
            trigger.triggers.Add(entryExit);

            var entryClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entryClick.callback.AddListener((data) => {
                var pe = (PointerEventData)data;
                if (pe.clickCount == 2) ShowGeneTree(eval.Definition.Id);
            });
            trigger.triggers.Add(entryClick);

            var row2 = UIFactory.NewRect("Row2", geneBlock.transform);
            var r2rt = row2.GetComponent<RectTransform>();
            r2rt.anchorMin = new Vector2(0, 1); r2rt.anchorMax = new Vector2(1, 1); r2rt.pivot = new Vector2(0, 1);
            r2rt.sizeDelta = new Vector2(-16, 24); r2rt.anchoredPosition = new Vector2(8,-38);

            var valLabel = UIFactory.NewText("ValLabel", row2.transform, "值:", 14, TextAnchor.MiddleLeft);
            var vlrt = valLabel.rectTransform;
            vlrt.anchorMin = new Vector2(0, 0); vlrt.anchorMax = new Vector2(0, 1); vlrt.pivot = new Vector2(0, 0.5f);
            vlrt.sizeDelta = new Vector2(25, 0); vlrt.anchoredPosition = Vector2.zero;

            var inputGO = UIFactory.NewRect("Input", row2.transform, typeof(Image), typeof(InputField));
            var inrt = inputGO.GetComponent<RectTransform>();
            inrt.anchorMin = new Vector2(0, 0.5f); inrt.anchorMax = new Vector2(0, 0.5f); inrt.pivot = new Vector2(0, 0.5f);
            inrt.sizeDelta = new Vector2(50, 20); inrt.anchoredPosition = new Vector2(30, 0);
            UIVisualTheme.Paint(inputGO.GetComponent<Image>(),"field");

            var inputText = UIFactory.NewText("Text", inputGO.transform, eval.Gene.Value.ToString("F1"), 14, TextAnchor.MiddleCenter);
            UIFactory.Stretch(inputText.rectTransform, 2, 2);
            var inputField = inputGO.GetComponent<InputField>();
            inputField.textComponent = inputText; inputField.text = eval.Gene.Value.ToString("F1");
            inputField.contentType = InputField.ContentType.DecimalNumber;
            // ★ 适配：固定出生分工已锁定遗传值，界面不能再显示可编辑却立即回滚的输入框。
            inputField.interactable=controller.GetComponent<DarwinPark.Entities.Ecology.AntColonyMember>()?.Frozen!=true;

            string stateName = eval.State != null ? eval.State.DisplayName : "无状态";
            var stateTxt = UIFactory.NewText("StateTxt", row2.transform, $" | 状态: {stateName}", 14, TextAnchor.MiddleLeft);
            var strt = stateTxt.rectTransform;
            strt.anchorMin = new Vector2(0, 0); strt.anchorMax = new Vector2(1, 1); strt.pivot = new Vector2(0, 0.5f);
            strt.offsetMin = new Vector2(90, 0); strt.offsetMax = new Vector2(0, 0);

            inputField.onEndEdit.AddListener((string input) => {
                if(!WorldAuthority.CanEdit)return;
                if (float.TryParse(input, out float newValue))
                {
                    newValue = Mathf.Clamp(newValue, 0f, 100f);
                    if (Mathf.Abs(newValue - eval.Gene.Value) > 0.01f)
                    {
                        LanEditActions.SetGene(_currentEntity,eval.Gene.InstanceId,newValue);
                        ShowEntityInfo(_currentEntity);
                    }
                    else inputField.text = eval.Gene.Value.ToString("F1");
                }
                else inputField.text = eval.Gene.Value.ToString("F1");
            });
            // ★ 修复（防Bug回归）：就地更新显示节点，避免周期重建抢走输入焦点或重置滚动位置。
            _rowRefresh.Add(()=>{
                if(geneBlock==null || controller==null)return;
                // ★ 适配：表达索引随重算刷新，避免每个可见基因行重新扫描。
                var live=controller.GetEvaluation(eval.Gene);if(live==null)return;
                nameTxt.text=$"<b>{live.Definition.DisplayName}</b> "+(live.IsActive?"<color=#4EE5B5>生效中</color>":"<color=#FF6B6B>失效</color>");
                stateTxt.text=" | 状态: "+(live.State?.DisplayName??"无状态");
                if(!inputField.isFocused)inputField.SetTextWithoutNotify(live.Gene.Value.ToString("F1"));
                geneIcon.color=live.IsActive?UIVisualTheme.Accent:UIVisualTheme.Muted;
            });
        }

        private void ShowGeneTooltip(GeneEvaluation eval)
        {
            if (eval == null) return;
            // ★ 适配：提示使用最新表达索引，与原实例ID查询语义一致。
            eval=_currentEntity?.Genes.GetEvaluation(eval.Gene)??eval;
            _tooltipPanel.SetActive(true);
            _tooltipPanel.transform.SetAsLastSibling();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>{eval.Definition.DisplayName}</b>");
            sb.AppendLine("────────────");

            // 显示特殊状态
            var geneCtrl = _currentEntity?.GetComponent<EntityGeneController>();
            var logic = geneCtrl?.GetLogic(eval.Gene);
            if (logic != null)
            {
                string extraInfo = logic.GetTooltipInfo();
                if (!string.IsNullOrEmpty(extraInfo)) sb.AppendLine(extraInfo);
            }

            // ★ 修复：去掉 <s> 标签，直接展示计算后的最终数值
            if (eval.Definition.AttributeCurves != null && eval.Definition.AttributeCurves.Count > 0)
            {
                sb.AppendLine("<color=#888888>────────────</color>");
                float t = Mathf.Clamp01(eval.EffectiveStrength / 100f);

                foreach (var curve in eval.Definition.AttributeCurves)
                {
                    float baseVal = Mathf.Lerp(curve.minValue, curve.maxValue, t) * eval.Gene.CopyStrength;
                    string name = _attrNameMap.ContainsKey(curve.attributeId) ? _attrNameMap[curve.attributeId] : curve.attributeId;

                    // ★ 修复（防Bug回归）：提示的是本基因贡献，不能把整只实体的总属性写在每个基因下面，失效基因尤其不能显示其他基因的数值。
                    eval.AttributeContributions.TryGetValue(curve.attributeId,out float finalVal);

                    // 直接显示最终值
                    if (finalVal < baseVal)
                    {
                        // 如果被削弱了，显示黄色或红色提示
                        sb.AppendLine($"{name}: <color=#FF6B6B>+{finalVal:F2}</color>");
                    }
                    else if (finalVal > baseVal)
                    {
                        // 如果被增强了，显示高亮
                        sb.AppendLine($"{name}: <color=#4EE5B5>+{finalVal:F2}</color>");
                    }
                    else
                    {
                        sb.AppendLine($"{name}: +{finalVal:F2}");
                    }
                }

                // 归零提示
                if (eval.Definition.Id == "gene_nutrition" && geneCtrl != null)
                {
                    float finalAbsorb = geneCtrl.GetAttribute("absorbEfficiency");
                    if (finalAbsorb <= 0.01f && geneCtrl.Genome.HasDefinition("gene_locomotion"))
                    {
                        sb.AppendLine("<color=#FF6B6B>※ 受行动基因绝对压制，吸收效率为 0。</color>");
                    }
                }
            }

            if(!eval.IsActive)sb.AppendLine("<color=#FF6B6B>当前未生效，不提供属性。</color>");
            else if(GeneAttributeInheritance.Applies(eval.Definition)&&!eval.Definition.IsBaseGene)
                sb.AppendLine("<color=#99AFBF>共有行动能力由基础谱系统一提供。</color>");

            _tooltipText.text = sb.ToString();
            // ★ 修复（防Bug回归）：先确定提示框宽度再测量文本高度，否则旧宽度的换行结果可能裁掉属性。
            _tooltipPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(280,100);
            float height = _tooltipText.preferredHeight + 20;
            _tooltipPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(280, height);
        }


        private void ShowGeneTree(string currentGeneId)
        {
            _treePanel.SetActive(true);
            // ★ 修复（防Bug回归）：主面板打开后已置顶，子弹窗也必须在打开时上移，否则被其遮住。
            _treePanel.transform.SetAsLastSibling();
            var def = GeneDatabase.Get(currentGeneId);
            if (def == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<color=#4EE5B5><b>【基因树信息】</b></color>\n");
            string parentId = def.ParentGeneId;
            if (!string.IsNullOrEmpty(parentId)) sb.AppendLine($"▲ 父级: {GeneDatabase.Get(parentId)?.DisplayName ?? parentId}");
            else sb.AppendLine("▲ 父级: 根源基因（无）");
            sb.AppendLine($"\n<color=#FFD166>■ 当前: {def.DisplayName} (Tier {def.Tier})</color>");
            sb.AppendLine("\n▼ 变异方向 (子级):");
            bool hasChild = false;
            foreach (var g in GeneDatabase.All)
            {
                if (g.ParentGeneId == currentGeneId) { sb.AppendLine($"  └─ {g.DisplayName}"); hasChild = true; }
            }
            if (!hasChild) sb.AppendLine("  (暂无分支)");
            sb.AppendLine("\n<color=#888888>(点击此面板关闭)</color>");
            _treeText.text = sb.ToString();
        }

        private void ShowAddGeneMenu()
        {
            if (_currentEntity == null||!WorldAuthority.CanEdit) return;
            // ★ 适配：再次点击添加按钮收起。
            if(_addGenePanel.activeSelf){_addGenePanel.SetActive(false);return;}
            _addGenePanel.SetActive(true);
            _addGenePanel.transform.SetAsLastSibling();

            // ★ 修复（防Bug回归）：销毁延迟到帧末，先隐藏旧行避免重复布局；每次打开重建完整当前库。
            foreach (Transform child in _addGeneListParent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var definitions=GeneDatabase.All.OrderBy(d=>d.Tier).ThenBy(d=>d.DisplayName).ToArray();
            _addGenePanel.transform.Find("Title").GetComponent<Text>().text="添加 / 复制基因（"+definitions.Length+"种）";
            _addGeneListParent.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;

            foreach (var def in definitions)
            {
                var btnGO = UIFactory.NewButton("Btn_" + def.Id, _addGeneListParent, def.DisplayName+"  · T"+def.Tier, () => {

                    // ★ 核心修复 1：先获取组件，再使用
                    var geneCtrl = _currentEntity.GetComponent<EntityGeneController>();
                    if (geneCtrl == null) return;

                    // ★ 核心修复 2：只添加一次，去掉之前截图里重复的代码
                    if(!WorldAuthority.CanEdit)return;
                    // ★ 适配：客户端不直接改副本，提交到房主后由权威快照刷新。
                    LanEditActions.AddGene(_currentEntity,def.Id);

                    _addGenePanel.SetActive(false);
                    ShowEntityInfo(_currentEntity); // 刷新主界面
                }, new Vector2(300, 30));
                // ★ 适配：同一行内增加图标和加号，不改变左下添加面板及按钮大小。
                var label=btnGO.GetComponentInChildren<Text>();label.alignment=TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin=new Vector2(38,0);label.rectTransform.offsetMax=new Vector2(-24,0);
                UIVisualTheme.AddIcon(btnGO.transform,UIVisualTheme.GeneIcon(def.Id),new Vector2(24,24),new Vector2(19,0),new Vector2(0,.5f));
                var plus=UIFactory.NewText("Plus",btnGO.transform,"+",20,TextAnchor.MiddleCenter);
                plus.rectTransform.anchorMin=plus.rectTransform.anchorMax=new Vector2(1,.5f);plus.rectTransform.anchoredPosition=new Vector2(-16,0);plus.rectTransform.sizeDelta=new Vector2(22,26);
            }
        }
    }
}
