using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;
using System.Collections.Generic;
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

        private Button _favBtn;
        private Text _favBtnText;
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
            rt.anchoredPosition = infoCornerOffset; rt.sizeDelta = new Vector2(420, 700);
            _panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            // ★ 修复（防Bug回归）：面板背景必须挡住游戏射线与右键，避免编辑时拾取身后的星球。
            _panel.GetComponent<Image>().raycastTarget = true;

            var titleBg = UIFactory.NewRect("TitleBg", _panel.transform, typeof(Image));
            var trt = titleBg.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            // ★ 适配：标题只保留可编辑名称，压缩高度并给各按钮独立区域。
            trt.sizeDelta = new Vector2(0, 38);
            titleBg.GetComponent<Image>().color = new Color(0.1f, 0.2f, 0.3f, 0.9f);

            _favBtn = UIFactory.NewRect("FavBtn", titleBg.transform, typeof(Image), typeof(Button)).GetComponent<Button>();
            var frt = _favBtn.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0, 0.5f); frt.anchorMax = new Vector2(0, 0.5f); frt.pivot = new Vector2(0, 0.5f);
            frt.anchoredPosition = new Vector2(8, 0); frt.sizeDelta = new Vector2(28, 28);
            _favBtn.GetComponent<Image>().color = new Color(0.15f, 0.25f, 0.35f, 0.9f);
            _favBtnText = UIFactory.NewText("Star", _favBtn.transform, "☆", 20, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_favBtnText.rectTransform);
            _favBtn.onClick.AddListener(OnFavoriteClicked);

            var nameGO = UIFactory.NewRect("NameInput", titleBg.transform, typeof(Image), typeof(InputField));
            var nrt = nameGO.GetComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(44, 5); nrt.offsetMax = new Vector2(-126, -5);
            nameGO.GetComponent<Image>().color = new Color(.07f,.13f,.19f,.8f);
            _nameInput = nameGO.GetComponent<InputField>(); _nameInput.characterLimit = 24;
            _titleText = UIFactory.NewText("Text", nameGO.transform, "", 16);
            UIFactory.Stretch(_titleText.rectTransform, 6, 1); _nameInput.textComponent = _titleText;
            _nameInput.onEndEdit.AddListener(RenameCurrentEntity);

            var saveTplGO = UIFactory.NewRect("SaveTplBtn", titleBg.transform, typeof(Image), typeof(Button));
            var strt = saveTplGO.GetComponent<RectTransform>();
            strt.anchorMin = new Vector2(1, 0.5f); strt.anchorMax = new Vector2(1, 0.5f); strt.pivot = new Vector2(1, 0.5f);
            strt.anchoredPosition = new Vector2(-40, 0); strt.sizeDelta = new Vector2(74, 28);
            saveTplGO.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.3f, 0.9f);
            var saveBtn = saveTplGO.GetComponent<Button>();
            var saveTxt = UIFactory.NewText("Txt", saveTplGO.transform, "存为模板", 14, TextAnchor.MiddleCenter);
            UIFactory.Stretch(saveTxt.rectTransform);
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

            var viewport = UIFactory.NewRect("Viewport", scrollGO.transform, typeof(Image), typeof(Mask));
            UIFactory.Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);

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
            _tooltipPanel.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.2f, 0.95f);
            _tooltipPanel.GetComponent<Image>().raycastTarget = false;
            _tooltipText = UIFactory.NewText("TText", _tooltipPanel.transform, "", 13, TextAnchor.UpperLeft);
            UIFactory.Stretch(_tooltipText.rectTransform, 10, 10);
            _tooltipPanel.SetActive(false);

            _treePanel = UIFactory.NewRect("TreePanel", canvas, typeof(Image));
            var trtrt = _treePanel.GetComponent<RectTransform>();
            trtrt.anchorMin = new Vector2(0.5f, 0.5f); trtrt.anchorMax = new Vector2(0.5f, 0.5f); trtrt.pivot = new Vector2(0.5f, 0.5f);
            trtrt.sizeDelta = new Vector2(400, 300);
            _treePanel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.98f);
            _treeText = UIFactory.NewText("TreeText", _treePanel.transform, "", 16, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_treeText.rectTransform);
            _treePanel.SetActive(false);
            _treePanel.AddComponent<Button>().onClick.AddListener(() => _treePanel.SetActive(false));

            _addGenePanel = UIFactory.NewRect("AddGenePanel", canvas, typeof(Image));
            var agprt = _addGenePanel.GetComponent<RectTransform>();
            agprt.anchorMin=agprt.anchorMax=agprt.pivot=Vector2.zero;
            agprt.anchoredPosition=addCornerOffset;
            agprt.sizeDelta = new Vector2(350, 450);
            _addGenePanel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.98f);
            var agTitle = UIFactory.NewText("Title", _addGenePanel.transform, "选择要添加/复制的基因", 18, TextAnchor.MiddleCenter);
            agTitle.rectTransform.anchorMin = new Vector2(0, 1); agTitle.rectTransform.anchorMax = new Vector2(1, 1); agTitle.rectTransform.sizeDelta = new Vector2(0, 40);
            var closeBtn = UIFactory.NewButton("Close", _addGenePanel.transform, "X", () => _addGenePanel.SetActive(false), new Vector2(30, 30));
            var cbrt = closeBtn.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1, 1); cbrt.anchorMax = new Vector2(1, 1); cbrt.pivot = new Vector2(1, 1); cbrt.anchoredPosition = new Vector2(-10, -5);

            var agScroll = UIFactory.NewRect("Scroll", _addGenePanel.transform, typeof(ScrollRect));
            var agsrt = agScroll.GetComponent<RectTransform>();
            agsrt.anchorMin = Vector2.zero; agsrt.anchorMax = Vector2.one; agsrt.offsetMin = new Vector2(10, 10); agsrt.offsetMax = new Vector2(-10, -50);
            var agView = UIFactory.NewRect("View", agScroll.transform, typeof(Image), typeof(Mask));
            UIFactory.Stretch(agView.GetComponent<RectTransform>());
            agView.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            var agContent = UIFactory.NewRect("Content", agView.transform);
            var agcrt = agContent.GetComponent<RectTransform>();
            agcrt.anchorMin = new Vector2(0, 1); agcrt.anchorMax = new Vector2(1, 1); agcrt.pivot = new Vector2(0.5f, 1); agcrt.sizeDelta = new Vector2(0, 0);
            var agVlg = agContent.AddComponent<VerticalLayoutGroup>();
            agVlg.spacing = 5; agVlg.childControlHeight = false; agVlg.childControlWidth = true;
            var agFitter = agContent.AddComponent<ContentSizeFitter>(); agFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _addGeneListParent = agContent.transform;
            var agScrollRect = agScroll.GetComponent<ScrollRect>();
            agScrollRect.content = agcrt; agScrollRect.viewport = agView.GetComponent<RectTransform>(); agScrollRect.horizontal = false; agScrollRect.vertical = true;
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
            if (_currentEntity == null) return;
            string name = (value ?? "").Trim();
            if (!string.IsNullOrEmpty(name)) _currentEntity.DisplayName = name;
            _nameInput.SetTextWithoutNotify(_currentEntity.DisplayName);
            EntityFavoritesManager.Instance?.NotifyEntityRenamed();
        }

        private void UpdateFavBtnVisual()
        {
            if (_currentEntity == null) return;
            bool isFav = EntityFavoritesManager.Instance != null && EntityFavoritesManager.Instance.IsFavorite(_currentEntity);
            _favBtnText.text = isFav ? "★" : "☆";
            _favBtnText.color = isFav ? new Color(1f, 0.8f, 0.2f) : Color.white;
        }

        private void Update()
        {
            // ★ 适配：覆盖菜单拥有输入时不处理世界操作。
            if (DarwinPark.Core.GameInputGate.Blocked) return;
            if (!EnsurePanel()) return;
            // ★ 适配：小窗口同时打开两个面板时压缩高度，保持左上/左下且不覆盖底栏。
            float canvasHeight=(_panel.transform.parent as RectTransform).rect.height;
            var addRect=_addGenePanel.GetComponent<RectTransform>();
            addRect.sizeDelta=new Vector2(350,Mathf.Min(450,Mathf.Max(150,canvasHeight*.35f)));
            var panelRect=_panel.GetComponent<RectTransform>();
            panelRect.sizeDelta=new Vector2(420,Mathf.Min(700,Mathf.Max(160,canvasHeight-addCornerOffset.y-24-(_addGenePanel.activeSelf?addRect.sizeDelta.y+16:0))));
            // ★ 修复（防Bug回归）：可见时刷新数值，关闭/实体销毁时所有附属面板一并关闭。
            if(_panel.activeSelf && _currentEntity==null)HideEntityInfo();
            if(!_panel.activeSelf){_addGenePanel.SetActive(false);_treePanel.SetActive(false);_tooltipPanel.SetActive(false);}
            if(Time.unscaledTime>=_nextRefresh){_nextRefresh=Time.unscaledTime+refreshInterval;RefreshDisplayedInfo();}
            // ★ 适配：投放模式的右键由独立工具取消，不能同时打开实体面板。
            if (EntityPlacementTool.Instance != null && EntityPlacementTool.Instance.IsPlacing) return;
            // ★ 修复（防Bug回归）：UI 上的右键必须属于 UI，收藏定位和基因编辑不应关闭面板或穿透拾取。
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool entityView = Camera.main != null && Camera.main.GetComponent<OrbitCamera>() is OrbitCamera orbit && orbit.EntityViewActive;
            if (!entityView && !pointerOverUI && Input.GetMouseButtonDown(1))
            {
                if (TryPickEntity(out var entity)) ShowEntityInfo(entity);
                else HideEntityInfo();
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

        private bool TryPickEntity(out DarwinEntity entity)
        {
            entity = null;
            var cam = Camera.main; if (cam == null) return false;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 2000f)) return false;
            entity = hit.collider.GetComponentInParent<DarwinEntity>();
            return entity != null;
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
            sb.AppendLine("<color=#6EE7FF><b>【基础信息】</b></color>");
            var stats = entity.GetComponent<EntityStats>();
            if (stats != null)
            {
                if (stats.TryGet("health", out var hp)) sb.AppendLine($"生命值: <color=#FF6B6B>{hp:F1}</color>");
                if (stats.TryGet("nutrient", out var nut)) sb.AppendLine($"饱食度: <color=#FFD166>{nut:F1}</color>");
                // ★ 删除这一行: if (stats.TryGet("storedNutrient", out var stored)) sb.AppendLine($"储存营养: <color=#FFD166>{stored:F1}</color>");
                if (stats.TryGet("size", out var size)) sb.AppendLine($"体型: <color=#6EE7FF>{size:F2}</color>");
            }

            // ★ 新增：读取实体自带状态显示在基础信息里
            var geneCtrl = entity.GetComponent<EntityGeneController>();
            if (geneCtrl != null)
            {
                string ageStr = "幼年";
                if (geneCtrl.CurrentAge == AgeState.Adult) ageStr = "成年";
                else if (geneCtrl.CurrentAge == AgeState.Elder) ageStr = "老年";
                sb.AppendLine($"生命阶段: <color=#06D6A0>{ageStr}</color>");
            }

            if (genes.IsGestating) sb.AppendLine("<color=#06D6A0>特殊状态: 孕育后代中...</color>");
            else sb.AppendLine("当前状态: 自由活动");

            if (PlanetClimateSystem.Instance != null)
            {
                var climate = PlanetClimateSystem.Instance.GetClimateAt(entity.transform.position);
                sb.AppendLine($"当前温度: {climate.Temperature:F1}°C | 湿度: {climate.Humidity:F1}%");
            }

            // ★ 适配：显示温度/溺水环境伤害状态，便于验证新基因是否生效。
            var hazard = entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>();
            if (hazard != null)
                sb.AppendLine($"环境状态: {hazard.GetStatusLine()}");

            sb.AppendLine("<color=#3A5A7E>──────────────────────────────</color>");

            return sb.ToString();
        }

        public void ShowEntityInfo(DarwinEntity entity)
        {
            if (entity == null || !EnsurePanel()) return;
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
            baseInfoText.gameObject.AddComponent<LayoutElement>().preferredHeight = 175;

            // 2. 核心基因
            var coreTitle = UIFactory.NewText("CoreTitle", _contentParent, "<color=#6EE7FF><b>【核心必须基因】</b></color>", 14, TextAnchor.UpperLeft);
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

            var div2 = UIFactory.NewText("Div2", _contentParent, "<color=#3A5A7E>──────────────────────────────</color>", 14, TextAnchor.UpperLeft);
            div2.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            // 3. 变异基因
            var mutTitleRow = UIFactory.NewRect("MutTitleRow", _contentParent);
            mutTitleRow.AddComponent<LayoutElement>().preferredHeight = 30;
            var mtHlg = mutTitleRow.AddComponent<HorizontalLayoutGroup>();
            mtHlg.childAlignment = TextAnchor.MiddleLeft; mtHlg.spacing = 10;
            mtHlg.childControlWidth = false;

            var mutTitle = UIFactory.NewText("MutTitle", mutTitleRow.transform, "<color=#6EE7FF><b>【变异/其他基因】</b></color>", 14, TextAnchor.MiddleLeft);
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

            var geneBlock = UIFactory.NewRect("Block_" + eval.Definition.Id + "_" + eval.Gene.InstanceId.Substring(0, 4), parent);
            var blockLE = geneBlock.AddComponent<LayoutElement>();
            blockLE.preferredHeight = 40;

            var row1 = UIFactory.NewRect("Row1", geneBlock.transform);
            var r1rt = row1.GetComponent<RectTransform>();
            r1rt.anchorMin = new Vector2(0, 1); r1rt.anchorMax = new Vector2(1, 1); r1rt.pivot = new Vector2(0, 1);
            r1rt.sizeDelta = new Vector2(0, 20);

            var colorGO = UIFactory.NewRect("Color", row1.transform, typeof(Image));
            var crt = colorGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0.5f); crt.anchorMax = new Vector2(0, 0.5f); crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(12, 12);
            string colorHex = eval.State != null ? ColorUtility.ToHtmlStringRGB(eval.State.GetDisplayColor(eval.Gene.Value)) : "888888";
            if (ColorUtility.TryParseHtmlString("#" + colorHex, out var col)) colorGO.GetComponent<Image>().color = col;

            string activeStr = eval.IsActive ? "<color=#06D6A0>生效中</color>" : "<color=#FF6B6B>失效</color>";
            var nameTxt = UIFactory.NewText("Name", row1.transform, $"<b>{eval.Definition.DisplayName}</b> {activeStr}", 14, TextAnchor.MiddleLeft);
            var ntrt = nameTxt.rectTransform;
            ntrt.anchorMin = new Vector2(0, 0); ntrt.anchorMax = new Vector2(1, 1);
            ntrt.offsetMin = new Vector2(20, 0); ntrt.offsetMax = new Vector2(0, 0);

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
            r2rt.anchorMin = new Vector2(0, 0); r2rt.anchorMax = new Vector2(1, 0); r2rt.pivot = new Vector2(0, 0);
            r2rt.sizeDelta = new Vector2(0, 20); r2rt.anchoredPosition = Vector2.zero;

            var valLabel = UIFactory.NewText("ValLabel", row2.transform, "值:", 14, TextAnchor.MiddleLeft);
            var vlrt = valLabel.rectTransform;
            vlrt.anchorMin = new Vector2(0, 0); vlrt.anchorMax = new Vector2(0, 1); vlrt.pivot = new Vector2(0, 0.5f);
            vlrt.sizeDelta = new Vector2(25, 0); vlrt.anchoredPosition = Vector2.zero;

            var inputGO = UIFactory.NewRect("Input", row2.transform, typeof(Image), typeof(InputField));
            var inrt = inputGO.GetComponent<RectTransform>();
            inrt.anchorMin = new Vector2(0, 0.5f); inrt.anchorMax = new Vector2(0, 0.5f); inrt.pivot = new Vector2(0, 0.5f);
            inrt.sizeDelta = new Vector2(50, 20); inrt.anchoredPosition = new Vector2(30, 0);
            inputGO.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.2f, 1f);

            var inputText = UIFactory.NewText("Text", inputGO.transform, eval.Gene.Value.ToString("F1"), 14, TextAnchor.MiddleCenter);
            UIFactory.Stretch(inputText.rectTransform, 2, 2);
            var inputField = inputGO.GetComponent<InputField>();
            inputField.textComponent = inputText; inputField.text = eval.Gene.Value.ToString("F1");
            inputField.contentType = InputField.ContentType.DecimalNumber;

            string stateName = eval.State != null ? eval.State.DisplayName : "无状态";
            var stateTxt = UIFactory.NewText("StateTxt", row2.transform, $" | 状态: {stateName}", 14, TextAnchor.MiddleLeft);
            var strt = stateTxt.rectTransform;
            strt.anchorMin = new Vector2(0, 0); strt.anchorMax = new Vector2(1, 1); strt.pivot = new Vector2(0, 0.5f);
            strt.offsetMin = new Vector2(90, 0); strt.offsetMax = new Vector2(0, 0);

            inputField.onEndEdit.AddListener((string input) => {
                if (float.TryParse(input, out float newValue))
                {
                    newValue = Mathf.Clamp(newValue, 0f, 100f);
                    if (Mathf.Abs(newValue - eval.Gene.Value) > 0.01f)
                    {
                        eval.Gene.Value = newValue;
                        controller.Recalculate();
                        ShowEntityInfo(_currentEntity);
                    }
                    else inputField.text = eval.Gene.Value.ToString("F1");
                }
                else inputField.text = eval.Gene.Value.ToString("F1");
            });
            // ★ 修复（防Bug回归）：就地更新显示节点，避免周期重建抢走输入焦点或重置滚动位置。
            _rowRefresh.Add(()=>{
                if(geneBlock==null || controller==null)return;
                var live=controller.Evaluation?.Genes.Find(g=>g.Gene.InstanceId==eval.Gene.InstanceId);if(live==null)return;
                nameTxt.text=$"<b>{live.Definition.DisplayName}</b> "+(live.IsActive?"<color=#06D6A0>生效中</color>":"<color=#FF6B6B>失效</color>");
                stateTxt.text=" | 状态: "+(live.State?.DisplayName??"无状态");
                if(!inputField.isFocused)inputField.SetTextWithoutNotify(live.Gene.Value.ToString("F1"));
                if(live.State!=null)colorGO.GetComponent<Image>().color=live.State.GetDisplayColor(live.Gene.Value);
            });
        }

        private void ShowGeneTooltip(GeneEvaluation eval)
        {
            if (eval == null) return;
            eval=_currentEntity?.Genes.Evaluation?.Genes.Find(g=>g.Gene.InstanceId==eval.Gene.InstanceId)??eval;
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

                    float finalVal = geneCtrl != null ? geneCtrl.GetAttribute(curve.attributeId) : baseVal;

                    // 直接显示最终值
                    if (finalVal < baseVal)
                    {
                        // 如果被削弱了，显示黄色或红色提示
                        sb.AppendLine($"{name}: <color=#FF6B6B>+{finalVal:F2}</color>");
                    }
                    else if (finalVal > baseVal)
                    {
                        // 如果被增强了，显示高亮
                        sb.AppendLine($"{name}: <color=#06D6A0>+{finalVal:F2}</color>");
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

            _tooltipText.text = sb.ToString();
            float height = _tooltipText.preferredHeight + 20;
            _tooltipPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(240, height);
        }


        private void ShowGeneTree(string currentGeneId)
        {
            _treePanel.SetActive(true);
            // ★ 修复（防Bug回归）：主面板打开后已置顶，子弹窗也必须在打开时上移，否则被其遮住。
            _treePanel.transform.SetAsLastSibling();
            var def = GeneDatabase.Get(currentGeneId);
            if (def == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<color=#6EE7FF><b>【基因树信息】</b></color>\n");
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
            if (_currentEntity == null) return;
            // ★ 适配：再次点击添加按钮收起。
            if(_addGenePanel.activeSelf){_addGenePanel.SetActive(false);return;}
            _addGenePanel.SetActive(true);
            _addGenePanel.transform.SetAsLastSibling();

            foreach (Transform child in _addGeneListParent) Destroy(child.gameObject);

            foreach (var def in GeneDatabase.All)
            {
                var btnGO = UIFactory.NewButton("Btn_" + def.Id, _addGeneListParent, def.DisplayName, () => {

                    // ★ 核心修复 1：先获取组件，再使用
                    var geneCtrl = _currentEntity.GetComponent<EntityGeneController>();
                    if (geneCtrl == null) return;

                    // ★ 核心修复 2：只添加一次，去掉之前截图里重复的代码
                    var newGene = GeneInstance.Create(def.Id, 50f, def.Tier);
                    geneCtrl.Genome.Add(newGene);

                    // ★ 核心修复 3：强制重算，确保新基因逻辑立刻挂载生效
                    geneCtrl.Recalculate();

                    _addGenePanel.SetActive(false);
                    ShowEntityInfo(_currentEntity); // 刷新主界面
                }, new Vector2(300, 30));
            }
        }
    }
}
