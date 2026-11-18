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
    public partial class GeneInspectorUI : MonoBehaviour
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
            {"visionRange", "观察视野"},
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



        // ✦ 补充：保留原组件与场景绑定，布局和世界交互通过回调分离。
        private void BuildPanel(Transform canvas)
        {
            var view=GeneInspectorLayout.Build(canvas,infoCornerOffset,infoPanelSize,addCornerOffset,OnFavoriteClicked,RenameCurrentEntity,HideEntityInfo,()=>_currentEntity);
            _panel=view.panel;
            _favBtn=view.favBtn;
            _favBtnText=view.favBtnText;
            _favIcon=view.favIcon;
            _nameInput=view.nameInput;
            _titleText=view.titleText;
            _contentParent=view.contentParent;
            _scroll=view.scroll;
            _tooltipPanel=view.tooltipPanel;
            _tooltipText=view.tooltipText;
            _treePanel=view.treePanel;
            _treeText=view.treeText;
            _addGenePanel=view.addGenePanel;
            _addGeneListParent=view.addGeneListParent;
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
            if(_nameInput!=null)_nameInput.interactable=WorldAuthority.CanEdit&&_currentCorpse==null;
            foreach(var field in _panel.GetComponentsInChildren<InputField>(true))field.interactable=WorldAuthority.CanEdit&&_currentCorpse==null;
            var addControl=_panel.transform.Find("ScrollView/Viewport/Content/MutTitleRow/AddBtn");if(addControl!=null)addControl.gameObject.SetActive(WorldAuthority.CanEdit&&_currentCorpse==null);
            var templateControl=_panel.transform.Find("TitleBg/SaveTplBtn");if(templateControl!=null)templateControl.gameObject.SetActive(WorldAuthority.CanEdit&&_currentCorpse==null);
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
            if(_panel.activeSelf && _currentEntity==null&&_currentCorpse==null)HideEntityInfo();
            if(!_panel.activeSelf){_addGenePanel.SetActive(false);_treePanel.SetActive(false);_tooltipPanel.SetActive(false);}
            if(Time.unscaledTime>=_nextRefresh){_nextRefresh=Time.unscaledTime+refreshInterval;RefreshDisplayedInfo();}
            if(WorldAuthority.RtsMode)return;
            // ★ 适配：投放模式的右键由独立工具取消，不能同时打开实体面板。
            if (EntityPlacementTool.Instance != null && EntityPlacementTool.Instance.IsPlacing) return;
            // ★ 修复（防Bug回归）：UI 上的右键必须属于 UI，收藏定位和基因编辑不应关闭面板或穿透拾取。
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool entityView = Camera.main != null && Camera.main.GetComponent<OrbitCamera>() is OrbitCamera orbit && orbit.EntityViewActive;
            if (!entityView && !pointerOverUI && Input.GetMouseButtonDown(1)&&!Input.GetKey(KeyCode.LeftAlt)&&!Input.GetKey(KeyCode.RightAlt))
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
            _currentEntity=null;_currentCorpse=null;_corpseEvaluation=null;
        }
        private string StructureKey(EntityGeneController genes)=>string.Join("|",genes.Genome.Genes.ConvertAll(g=>g.InstanceId+":"+g.DefinitionId));
        public void RefreshDisplayedInfo()
        {
            if(_currentCorpse!=null){RefreshCorpse();return;}
            if(_panel==null || !_panel.activeSelf || _currentEntity==null)return;
            var genes=_currentEntity.GetComponent<EntityGeneController>();if(genes?.Evaluation==null)return;
            if(_baseInfo!=null)_baseInfo.text=EntityInfoText.BaseInfo(_currentEntity);
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



        public void ShowEntityInfo(DarwinEntity entity)
        {
            if (entity == null || !EnsurePanel()) return;
            WorldResourceInspector.Instance?.Hide();
            if(_currentEntity!=entity){_addGenePanel.SetActive(false);_treePanel.SetActive(false);}
            // ★ 修复（防Bug回归）：切换对象前提交旧输入，避免失焦事件把旧名字写到新实体。
            if (_nameInput != null && _nameInput.isFocused) _nameInput.DeactivateInputField();
            _currentEntity = entity;_currentCorpse=null;_nameInput.readOnly=false;_favBtn.interactable=true;
            var genes = entity.GetComponent<EntityGeneController>();
            if (genes == null || genes.Evaluation == null) { _panel?.SetActive(false); return; }
            _rowRefresh.Clear();_structure=StructureKey(genes);

            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            _tooltipPanel.SetActive(false);
            UpdateFavBtnVisual();
            _nameInput.SetTextWithoutNotify(entity.DisplayName);

            foreach (Transform child in _contentParent){child.gameObject.SetActive(false);Destroy(child.gameObject);}

            var inspected=GeneFailureReasons.Inspect(genes.Genome,genes.Evaluation);
            string baseInfo=EntityInfoText.BaseInfo(entity);

            // ★ 修改：高度回调至 100，因为去掉了生命阶段
            var baseInfoText = UIFactory.NewText("BaseInfo", _contentParent, baseInfo, 14, TextAnchor.UpperLeft);
            _baseInfo=baseInfoText;
            // ★ 适配：新增环境状态行，按 9 行文本预留高度，避免内容被截断。
            baseInfoText.gameObject.AddComponent<LayoutElement>().minHeight = 198;
            // ★ 适配：摘要预留独立活动状态行，分栏数值和细进度条代替连续正文。
            baseInfoText.enabled=false;
            var refreshSummary=EntitySummaryView.Build(baseInfoText.transform,entity);
            _rowRefresh.Add(refreshSummary);refreshSummary();
            // ★ 适配：编辑入口只使用摘要标题的空白区域，不改原布局。
            EntityEditPopup.AddEntry(baseInfoText.transform,entity);
            // ★ 修复（防Bug回归）：实体详情只承担观察与基因编辑，群体命令和决斗按钮挤占基因列表，按新版布局取消整块操作区。

            // 2. 核心基因
            var coreTitle = UIFactory.NewText("CoreTitle", _contentParent, "<color=#99AFBF><b>核心基因</b></color>", 14, TextAnchor.UpperLeft);
            coreTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 25;
            string[] requiredGenes = { "gene_growth", "gene_reproduction", "gene_nutrition" };

            _allMutations.Clear();

            foreach (var reqId in requiredGenes)
            {
                var evals = inspected.FindAll(g => g.Definition != null && g.Definition.Id == reqId);
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

            foreach (var eval in inspected)
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

            var inspectedGenome=_currentEntity?.Genes.Genome??_currentCorpse?.State.source?.genome;
            if(inspectedGenome!=null)foreach(var reason in GeneFailureReasons.Explain(inspectedGenome,eval))sb.AppendLine("<color=#FF6B6B>• "+reason+"</color>");
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

    }
}
