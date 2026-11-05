using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 修复（防Bug回归）：原IMGUI文本/默认勾选框与游戏Canvas混用导致焦点和输入不可靠；窗口统一用原生主题控件。
    public sealed class RandomSpawnWorkbench:MonoBehaviour
    {
        public static RandomSpawnWorkbench Instance {get;private set;}public bool Visible {get;private set;}
        public Vector2 windowSize=new(920,680);
        private RandomSpawnPanel _owner;private RandomSpawnRecipe _draft;private string _ratio="30",_editingId,_status="",_search="",_presetId;
        private bool _rules,_landOnly,_running,_blocked,_cursorVisible,_ruleEnabled=true;private CursorLockMode _cursorLock;
        private string _rulesStamp="";private bool _refreshQueued;private GameObject _blocker,_body,_footer;private RectTransform _panel,_geneContent;private Text _statusText,_sourceText;
        private InputField _searchInput,_conditionName;private float _nextCounts;private readonly List<Action> _countRefresh=new();
        private List<RandomSpawnPresetStore.Preset> _presets=new();
        private Button _sourceTab,_rulesTab,_useGenesButton;private readonly List<(Button button,string name)> _templateButtons=new();
        public static RandomSpawnWorkbench Ensure()=>Instance!=null?Instance:ServiceLocator.Get<UIRoot>().gameObject.AddComponent<RandomSpawnWorkbench>();
        private void Awake(){Instance=this;}
        public void Open(RandomSpawnPanel owner,bool rules=false)
        {
            if(!WorldAuthority.CanEdit||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||GameInputGate.Blocked&&!Visible)return;
            if(!Visible)
            {
                RuntimeDropdownDismissal.CloseOpenPopups();_running=ServiceLocator.Get<SimulationClock>().IsRunning;_blocked=GameInputGate.Blocked;
                if(!WorldAuthority.Online)ServiceLocator.Get<SimulationClock>().Pause();GameInputGate.Blocked=true;
                _cursorVisible=Cursor.visible;_cursorLock=Cursor.lockState;Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
                EntityPlacementTool.Instance?.CancelPlacement();FindFirstObjectByType<EntityPossessionInput>()?.Suspend();Visible=true;
            }
            _owner=owner;_draft=Copy(owner.Recipe);_landOnly=owner.LandOnly;_editingId=null;_presetId=null;_ruleEnabled=true;_status="";_rules=rules;_search="";
            ReloadPresets();BuildWindow();
        }
        public void Close()
        {
            if(!Visible)return;Visible=false;if(_blocker!=null){_blocker.SetActive(false);Destroy(_blocker);_blocker=null;}_countRefresh.Clear();
            GameInputGate.Blocked=WorldRuntimeGate.Ready?_blocked:true;Cursor.visible=_cursorVisible;Cursor.lockState=_cursorLock;
            if(_running&&!WorldAuthority.Online&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading)ServiceLocator.Get<SimulationClock>().Play();
        }
        private void Update()
        {
            if(!Visible)return;if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||!WorldAuthority.CanEdit){Close();return;}
            if(Input.GetKeyDown(KeyCode.Escape)&&string.IsNullOrEmpty(Input.compositionString)){GameInputGate.ConsumeEscape();Close();return;}
            if(_panel!=null){var canvas=(RectTransform)_panel.parent;_panel.sizeDelta=new Vector2(Mathf.Min(windowSize.x,canvas.rect.width-24),Mathf.Min(windowSize.y,canvas.rect.height-24));}
            if(Time.unscaledTime>=_nextCounts){_nextCounts=Time.unscaledTime+.5f;PopulationMaintenance.Refresh();
                // ★ 适配：客户端命令由房主异步确认，规则快照变更后刷新原生列表，不能停留在提交前的开关状态。
                if(_rules&&_rulesStamp!=JsonUtility.ToJson(PopulationMaintenance.Capture())){RebuildBody();return;}foreach(var refresh in _countRefresh)refresh();}
        }
        private void OnDisable()=>Close();private void OnDestroy(){Close();if(Instance==this)Instance=null;}
        private static RandomSpawnRecipe Copy(RandomSpawnRecipe source)=>JsonUtility.FromJson<RandomSpawnRecipe>(JsonUtility.ToJson(source));
        private static RandomSpawnRecipe Template(EntityTemplateData template)
        {
            var genome=template==null?RandomSpawnRecipe.DefaultGenome():new Genome();if(template!=null)foreach(var gene in template.genes)genome.Add(GeneInstance.Create(gene.definitionId,gene.value,gene.tier));
            return new RandomSpawnRecipe{method=RandomSpawnMethod.Template,name=template?.templateName??"默认实体",template=genome};
        }
        public void SelectTemplate(EntityTemplateData template){_draft=Template(template);_presetId=null;}
        public void UseGeneRandom(){if(_draft.method!=RandomSpawnMethod.Genes)_draft.name="基因条件随机";_draft.method=RandomSpawnMethod.Genes;_draft.template=null;}
        public void SetGene(string id,bool include,bool selected)
        {
            UseGeneRandom();var chosen=include?_draft.included:_draft.excluded;var other=include?_draft.excluded:_draft.included;
            if(selected){if(!chosen.Contains(id))chosen.Add(id);other.Remove(id);}else chosen.Remove(id);
        }
        public RandomSpawnRecipe Draft=>Copy(_draft);
        public bool ApplySelection()
        {try{_owner.SetRecipe(_draft);_owner.SetLandOnly(_landOnly);Close();return true;}catch(Exception ex){Status(ex.Message);return false;}}
        public bool SaveRule(float percent)
        {
            if(!float.IsFinite(percent)||percent<0||percent>=100){Status("目标比例请输入0～99之间的百分数");return false;}
            var rule=new PopulationMaintenanceRule{id=_editingId??Guid.NewGuid().ToString("N"),enabled=_ruleEnabled,ratio=percent/100f,landOnly=_landOnly,source=Draft};
            try
            {
                var catalog=GeneDatabase.All.Select(d=>d.Id).ToArray();rule.source.Validate(catalog);var validation=PopulationMaintenance.Capture();validation.rules.RemoveAll(r=>r.id==rule.id);validation.rules.Add(rule);PopulationMaintenance.Validate(validation,catalog);
                if(!LanEditActions.Submit(new LanCommand{kind="maintenanceSet",maintenanceRule=rule})){Status("无法保存规则，请检查权限或规则数量");return false;}
                _editingId=rule.id;Persist();_rules=true;RebuildBody();return true;
            }
            catch(Exception ex){Status(ex.Message);return false;}
        }
        // ★ 新增：命名条件保存成独立JSON，保存后即可在生成来源及维持比例列表中复用，不会自动开启比例规则。
        public bool SaveCondition(string name)
        {
            try
            {
                if(_draft.method!=RandomSpawnMethod.Genes)throw new InvalidDataException("请先选择基因条件随机方式");
                var source=Draft;source.name=(name??"").Trim();var preset=RandomSpawnPresetStore.Save(source,_presetId);_presetId=preset.id;_draft=Copy(preset.source);ReloadPresets();
                Status("基因条件已保存为 JSON："+Path.Combine(RandomSpawnPresetStore.DirectoryPath,preset.id+".json"));RebuildBody();return true;
            }
            catch(Exception ex){Status("保存失败："+ex.Message);return false;}
        }
        private void ReloadPresets(){try{_presets=RandomSpawnPresetStore.Load();}catch(Exception ex){Status("读取基因条件失败："+ex.Message);Debug.LogWarning("[生成条件] "+ex.Message);_presets=new();}}
        private void Status(string message){_status=message;if(_statusText!=null)_statusText.text=message;}
        private void Persist(){if(LanSession.Instance?.IsClient!=true){FindFirstObjectByType<WorldSessionManager>().SaveNow(_running);Status("规则已保存：不足时补充，超量时减少繁殖");}else Status("已发送给房主，等待同步");}
        private static RectTransform Rect(string name,Transform parent,float left,float top,float right,float bottom)
        {
            var rt=UIFactory.NewRect(name,parent).GetComponent<RectTransform>();rt.anchorMin=new Vector2(left,0);rt.anchorMax=new Vector2(right,1);rt.offsetMin=new Vector2(0,bottom);rt.offsetMax=new Vector2(0,-top);return rt;
        }
        private static void RowPlace(RectTransform rt,float a,float b,float top,float height)
        {rt.anchorMin=new Vector2(a,1);rt.anchorMax=new Vector2(b,1);rt.pivot=new Vector2(.5f,1);rt.offsetMin=new Vector2(4,-top-height);rt.offsetMax=new Vector2(-4,-top);}
        private static Text Label(Transform parent,string name,string value,float a,float b,float top,float height,int size=15)
        {var text=UIFactory.NewText(name,parent,value,size);RowPlace(text.rectTransform,a,b,top,height);return text;}
        private static Button Button(Transform parent,string name,string label,float a,float b,float top,float height,Action action,bool selected=false)
        {var button=UIFactory.NewButton(name,parent,label,action,Vector2.zero);RowPlace(button.GetComponent<RectTransform>(),a,b,top,height);Paint(button,selected);button.GetComponentInChildren<Text>().fontSize=14;return button;}
        private static void Paint(Button button,bool selected)
        {UIVisualTheme.StyleButton(button);if(selected)UIVisualTheme.Paint(button.image,"selected");var states=button.spriteState;states.selectedSprite=UIVisualTheme.SurfaceSprite(selected?"selected":"button");states.highlightedSprite=UIVisualTheme.SurfaceSprite(selected?"selected":"hover");button.spriteState=states;button.image.overrideSprite=null;button.GetComponentInChildren<Text>().color=selected?UIVisualTheme.Accent:UIVisualTheme.Text;}
        private static InputField Field(Transform parent,string name,string value,string hint,float a,float b,float top,float height)
        {
            var go=UIFactory.NewRect(name,parent,typeof(Image),typeof(InputField));RowPlace(go.GetComponent<RectTransform>(),a,b,top,height);var field=go.GetComponent<InputField>();field.targetGraphic=go.GetComponent<Image>();
            var text=UIFactory.NewText("Text",go.transform,"",14);UIFactory.Stretch(text.rectTransform);text.rectTransform.offsetMin=new Vector2(12,3);text.rectTransform.offsetMax=new Vector2(-12,-3);text.horizontalOverflow=HorizontalWrapMode.Overflow;
            var placeholder=UIFactory.NewText("Placeholder",go.transform,hint,14);UIFactory.Stretch(placeholder.rectTransform);placeholder.rectTransform.offsetMin=text.rectTransform.offsetMin;placeholder.rectTransform.offsetMax=text.rectTransform.offsetMax;placeholder.color=UIVisualTheme.Muted;
            field.textComponent=text;field.placeholder=placeholder;field.lineType=InputField.LineType.SingleLine;field.characterLimit=128;field.text=value;field.caretColor=UIVisualTheme.Accent;field.customCaretColor=true;field.selectionColor=new Color(UIVisualTheme.Accent.r,UIVisualTheme.Accent.g,UIVisualTheme.Accent.b,.35f);return field;
        }
        private static RectTransform Scroll(Transform parent,string name,float a,float b,float top,float bottom)
        {
            var viewport=Rect(name,parent,a,top,b,bottom);viewport.gameObject.AddComponent<RectMask2D>();var scroll=viewport.gameObject.AddComponent<ScrollRect>();var image=viewport.gameObject.AddComponent<Image>();image.color=Color.clear;
            var content=UIFactory.NewRect("Content",viewport).GetComponent<RectTransform>();content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(0,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(-18,0);
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
            var barObject=DefaultControls.CreateScrollbar(new DefaultControls.Resources());barObject.name="Scrollbar";barObject.transform.SetParent(viewport,false);var bar=barObject.GetComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
            var barRect=barObject.GetComponent<RectTransform>();barRect.anchorMin=new Vector2(1,0);barRect.anchorMax=Vector2.one;barRect.pivot=Vector2.one;barRect.offsetMin=new Vector2(-12,0);barRect.offsetMax=Vector2.zero;
            UIVisualTheme.Paint(barObject.GetComponent<Image>(),"track");UIVisualTheme.Paint(bar.handleRect.GetComponent<Image>(),"hover");scroll.verticalScrollbar=bar;return content;
        }
        private static void Empty(GameObject go){if(go!=null){go.SetActive(false);Destroy(go);}}
        private void BuildWindow()
        {
            Empty(_blocker);var canvas=ServiceLocator.Get<UIRoot>().MainCanvas.transform;
            _blocker=UIFactory.NewRect("RandomSpawnWorkbenchBlocker",canvas,typeof(Image));UIFactory.Stretch(_blocker.GetComponent<RectTransform>());_blocker.GetComponent<Image>().color=new Color(0,0,0,.65f);_blocker.transform.SetAsLastSibling();
            _panel=UIFactory.NewRect("RandomSpawnWorkbench",_blocker.transform,typeof(Image)).GetComponent<RectTransform>();_panel.anchorMin=_panel.anchorMax=new Vector2(.5f,.5f);_panel.sizeDelta=windowSize;UIVisualTheme.Paint(_panel.GetComponent<Image>());
            Label(_panel,"Title","随机生成 · 选择实体与维持比例",.015f,.86f,12,36,24);Button(_panel,"Close","关闭",.89f,.985f,12,30,Close);
            _sourceTab=Button(_panel,"SourceTab","生成来源（两种方式）",.015f,.5f,54,32,()=>{_rules=false;BuildWindow();},!_rules);
            _rulesTab=Button(_panel,"RulesTab","维持比例规则",.5f,.985f,54,32,()=>{_rules=true;BuildWindow();},_rules);RebuildBody();
        }
        private void RebuildBody()
        {
            if(!Visible||_panel==null)return;_rulesStamp=JsonUtility.ToJson(PopulationMaintenance.Capture());Paint(_sourceTab,!_rules);Paint(_rulesTab,_rules);_templateButtons.Clear();Empty(_body);Empty(_footer);_countRefresh.Clear();_body=Rect("Body",_panel,.015f,96,.985f,142).gameObject;
            if(_rules)DrawRules();else DrawSources();DrawFooter();
            // ★ 修复（防Bug回归）：同帧更换大量中文文本会扩充动态字体图集，下一帧统一更新旧文字的UV，避免按钮/页脚缺字。
            if(!_refreshQueued){_refreshQueued=true;StartCoroutine(RefreshText());}
        }
        private IEnumerator RefreshText()
        {
            yield return null;_refreshQueued=false;if(!Visible||_panel==null)yield break;
            var texts=_panel.GetComponentsInChildren<Text>();float scale=ServiceLocator.Get<UIRoot>().MainCanvas.scaleFactor;
            // 在生成网格前申请本页全部字形，避免逐个控件渲染期间反复扩图集使先前控件的UV过期。
            foreach(var group in texts.GroupBy(t=>(t.font,t.fontSize,t.fontStyle)))
            {
                string characters=string.Concat(group.Select(t=>t.text));group.Key.font.RequestCharactersInTexture(characters,group.Key.fontSize,group.Key.fontStyle);
                group.Key.font.RequestCharactersInTexture(characters,Mathf.RoundToInt(group.Key.fontSize*scale),group.Key.fontStyle);
            }
            foreach(var text in texts)text.SetAllDirty();Canvas.ForceUpdateCanvases();
        }
        private void DrawSources()
        {
            var left=Rect("Templates",_body.transform,0,0,.49f,0);var right=Rect("Conditions",_body.transform,.51f,0,1,0);
            Label(left,"Heading","方式一：完全按照实体模板",0,1,0,26,14);var templates=Scroll(left,"TemplatesList",0,1,32,34);float y=0;
            void AddTemplate(EntityTemplateData template){var source=Template(template);var button=Button(templates,"Template",source.name,0,1,y,32,()=>{SelectTemplate(template);RebuildBody();},_draft.method==RandomSpawnMethod.Template&&_draft.name==source.name);_templateButtons.Add((button,source.name));y+=38;}
            AddTemplate(null);foreach(var template in EntityTemplateManager.Templates)AddTemplate(template);templates.sizeDelta=new Vector2(-18,y);
            var note=Label(left,"Hint","完整复制模板基因与数值，不添加随机基因。",0,1,0,30,13);note.rectTransform.anchorMin=new Vector2(0,0);note.rectTransform.anchorMax=new Vector2(1,0);note.rectTransform.anchoredPosition=new Vector2(0,30);note.color=UIVisualTheme.Muted;
            Label(right,"Heading","方式二：基因条件，其余随机",0,1,0,26,14);_useGenesButton=Button(right,"UseGenes",_draft.method==RandomSpawnMethod.Genes?"已选择基因条件随机":"使用基因条件随机",0,1,30,32,()=>{UseGeneRandom();RebuildBody();},_draft.method==RandomSpawnMethod.Genes);
            // ★ 修复（防Bug回归）：输入框保持原生生命周期，筛选仅重建其下方列表，不因结果行增减丢失光标或中文输入状态。
            _searchInput=Field(right,"GeneSearch",_search,"搜索基因名称或编号",0,1,68,30);_searchInput.onValueChanged.AddListener(text=>{_search=text;DrawGenes();});
            _conditionName=Field(right,"ConditionName",_draft.method==RandomSpawnMethod.Genes?_draft.name:"","基因条件名称",0,.64f,104,30);
            Button(right,"SaveCondition","保存条件 JSON",.64f,1,104,30,()=>SaveCondition(_conditionName.text));
            float next=140;
            if(_presets.Count>0)
            {
                var go=DefaultControls.CreateDropdown(new DefaultControls.Resources());go.name="SavedConditions";go.transform.SetParent(right,false);var dropdown=go.GetComponent<Dropdown>();UIVisualTheme.Paint(go.GetComponent<Image>(),"field");RuntimeDropdownSkin.Apply(dropdown);foreach(var text in go.GetComponentsInChildren<Text>(true)){text.font=UIFactory.DefaultFont;text.color=UIVisualTheme.Text;text.fontSize=14;}RowPlace(dropdown.GetComponent<RectTransform>(),0,1,next,30);dropdown.options=new List<Dropdown.OptionData>{new("选择已保存的基因条件…")};dropdown.options.AddRange(_presets.Select(p=>new Dropdown.OptionData(p.source.name)));dropdown.RefreshShownValue();var arrow=go.transform.Find("Arrow").GetComponent<Image>();arrow.sprite=UIIconRegistry.Get("ui_icon_next");arrow.color=UIVisualTheme.Accent;arrow.preserveAspect=true;arrow.rectTransform.sizeDelta=new Vector2(14,14);arrow.rectTransform.localRotation=Quaternion.Euler(0,0,-90);
                dropdown.onValueChanged.AddListener(index=>{if(index<=0)return;var preset=_presets[index-1];_draft=Copy(preset.source);_presetId=preset.id;RebuildBody();});next+=36;
            }
            Label(right,"GeneHeader","基因",0,.62f,next,25,13);Label(right,"IncludeHeader","一定有",.62f,.81f,next,25,13);Label(right,"ExcludeHeader","一定没有",.81f,1,next,25,13);
            _geneContent=Scroll(right,"Genes",0,1,next+28,0);DrawGenes();
        }
        private void DrawGenes()
        {
            if(_geneContent==null)return;foreach(Transform child in _geneContent)Empty(child.gameObject);float y=0;
            foreach(var gene in GeneDatabase.All.OrderBy(g=>g.Tier).ThenBy(g=>g.Id).Where(g=>string.IsNullOrWhiteSpace(_search)||g.DisplayName.IndexOf(_search,StringComparison.OrdinalIgnoreCase)>=0||g.Id.IndexOf(_search,StringComparison.OrdinalIgnoreCase)>=0))
            {
                Label(_geneContent,"GeneLabel",gene.DisplayName,0,.62f,y,30,14);var include=Button(_geneContent,"Include_"+gene.Id,"有",.62f,.81f,y,30,null,_draft.method==RandomSpawnMethod.Genes&&_draft.included.Contains(gene.Id));
                var exclude=Button(_geneContent,"Exclude_"+gene.Id,"无",.81f,1,y,30,null,_draft.method==RandomSpawnMethod.Genes&&_draft.excluded.Contains(gene.Id));
                void Changed(bool has){SetGene(gene.Id,has,!(has?_draft.included:_draft.excluded).Contains(gene.Id));Paint(include,_draft.included.Contains(gene.Id));Paint(exclude,_draft.excluded.Contains(gene.Id));RefreshSourceLabel();Paint(_useGenesButton,true);_useGenesButton.GetComponentInChildren<Text>().text="已选择基因条件随机";foreach(var item in _templateButtons)Paint(item.button,false);if(string.IsNullOrWhiteSpace(_conditionName.text))_conditionName.SetTextWithoutNotify(_draft.name);}
                include.onClick.AddListener(()=>Changed(true));exclude.onClick.AddListener(()=>Changed(false));y+=36;
            }
            _geneContent.sizeDelta=new Vector2(-18,y);_geneContent.anchoredPosition=Vector2.zero;
        }
        // ★ 新增：模板/保存条件自动成为可配置行，只有主动启用才建规则，避免每个模板默认30%导致比例冲突。
        public List<RandomSpawnRecipe> MaintenanceSources()
        {
            var sources=new List<RandomSpawnRecipe>{Template(null)};sources.AddRange(EntityTemplateManager.Templates.Select(Template));sources.AddRange(_presets.Select(p=>Copy(p.source)));
            if(_draft.method==RandomSpawnMethod.Genes&&!sources.Any(s=>SameSource(s,_draft)))sources.Add(Draft);
            foreach(var rule in PopulationMaintenance.Rules)if(!sources.Any(s=>SameSource(s,rule.source)))sources.Add(Copy(rule.source));return sources;
        }
        private static bool SameSource(RandomSpawnRecipe a,RandomSpawnRecipe b)=>a.method==b.method&&(a.method==RandomSpawnMethod.Template?a.name==b.name&&a.Matches(b.template):a.included.OrderBy(id=>id).SequenceEqual(b.included.OrderBy(id=>id))&&a.excluded.OrderBy(id=>id).SequenceEqual(b.excluded.OrderBy(id=>id)));
        private static string Description(RandomSpawnRecipe source)
        {
            string Names(IEnumerable<string> ids)=>string.Join("、",ids.Select(id=>GeneDatabase.Get(id)?.DisplayName??id));
            return source.method==RandomSpawnMethod.Template?"实体模板 · 完全复制":"必有："+(source.included.Count==0?"不限":Names(source.included))+"；必无："+(source.excluded.Count==0?"不限":Names(source.excluded));
        }
        private void DrawRules()
        {
            Label(_body.transform,"RuleHint","模板与已保存条件自动列出 · 不足时生成，超量时减少繁殖",0,1,0,27,14);var content=Scroll(_body.transform,"MaintenanceSources",0,1,34,0);float y=0;
            foreach(var source in MaintenanceSources())
            {
                var rule=PopulationMaintenance.Rules.FirstOrDefault(r=>SameSource(r.source,source));var row=UIFactory.NewRect("MaintenanceRow",content,typeof(Image)).GetComponent<RectTransform>();RowPlace(row,0,1,y,104);UIVisualTheme.Paint(row.GetComponent<Image>(),"field");
                Label(row,"Name",source.name,0,.52f,4,28,16);var count=Label(row,"Count","",.52f,1,4,28,13);Action refresh=()=>{int total=EntityRegistry.All.Count(e=>EcologyQueries.Alive(e));int matched=EntityRegistry.All.Count(e=>EcologyQueries.Alive(e)&&source.Matches(e.Genes.Genome));count.text="当前 "+matched+" / "+total+"（"+(total>0?100f*matched/total:0).ToString("0.#")+"%）";};_countRefresh.Add(refresh);refresh();
                Label(row,"Conditions",Description(source),0,1,34,28,13);Label(row,"RatioLabel","目标比例",0,.14f,69,28,13);var ratio=Field(row,"Ratio",rule==null?"30":(rule.ratio*100).ToString("0.#",CultureInfo.InvariantCulture),"百分比",.14f,.25f,69,28);ratio.contentType=InputField.ContentType.DecimalNumber;Label(row,"Percent","%",.25f,.29f,69,28);
                Button(row,"ApplyRatio",rule==null?"启用维持":"保存比例",.3f,.47f,69,28,()=>{_draft=Copy(source);_editingId=rule?.id;_ruleEnabled=rule?.enabled??true;_landOnly=rule?.landOnly??_landOnly;if(float.TryParse(ratio.text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value))SaveRule(value);else Status("请输入有效比例");});
                if(rule!=null)
                {
                    Button(row,"RuleEnabled",rule.enabled?"已启用":"已停用",.48f,.64f,69,28,()=>{var edit=JsonUtility.FromJson<PopulationMaintenanceRule>(JsonUtility.ToJson(rule));edit.enabled=!edit.enabled;if(LanEditActions.Submit(new LanCommand{kind="maintenanceSet",maintenanceRule=edit})){Persist();RebuildBody();}else Status("无法切换，请检查比例冲突或权限");},rule.enabled);
                    Button(row,"DeleteRule","移除规则",.82f,1,69,28,()=>{if(LanEditActions.Submit(new LanCommand{kind="maintenanceRemove",target=rule.id})){if(_editingId==rule.id){_editingId=null;_ruleEnabled=true;}Persist();RebuildBody();}});
                }
                else Label(row,"Inactive","尚未启用",.48f,.64f,69,28,13);
                Button(row,"EditSource","查看来源",.65f,.81f,69,28,()=>{_draft=Copy(source);_editingId=rule?.id;_ruleEnabled=rule?.enabled??true;_ratio=(100*(rule?.ratio??.3f)).ToString("0.#",CultureInfo.InvariantCulture);_presetId=_presets.FirstOrDefault(p=>SameSource(p.source,source))?.id;_rules=false;BuildWindow();});y+=112;
            }
            content.sizeDelta=new Vector2(-18,y);
        }
        private void RefreshSourceLabel(){if(_sourceText!=null)_sourceText.text="当前来源："+(_draft.method==RandomSpawnMethod.Template?"模板 · "+_draft.name:"基因随机 · 必有"+_draft.included.Count+"项 / 必无"+_draft.excluded.Count+"项");}
        private void DrawFooter()
        {
            var rt=Rect("Footer",_panel,.015f,0,.985f,12);rt.anchorMax=new Vector2(.985f,0);rt.offsetMax=new Vector2(0,132);_footer=rt.gameObject;
            _sourceText=Label(rt,"CurrentSource","",0,1,0,28,14);RefreshSourceLabel();Button(rt,"ApplySelection","应用生成来源",0,.36f,32,32,()=>ApplySelection());Label(rt,"RatioLabel","维持",.36f,.42f,32,32,14);
            var ratio=Field(rt,"DraftRatio",_ratio,"比例",.42f,.49f,32,32);ratio.contentType=InputField.ContentType.DecimalNumber;ratio.onValueChanged.AddListener(value=>_ratio=value);Label(rt,"Percent","%",.49f,.52f,32,32,14);
            Button land=null;land=Button(rt,"LandOnly",_landOnly?"仅陆地":"全部地表",.52f,.66f,32,32,()=>{_landOnly=!_landOnly;land.GetComponentInChildren<Text>().text=_landOnly?"仅陆地":"全部地表";});
            Button(rt,"SaveRule",_editingId==null?"新增维持规则":"保存规则修改",.66f,1,32,32,()=>{if(float.TryParse(_ratio,NumberStyles.Float,CultureInfo.InvariantCulture,out float value))SaveRule(value);else Status("请输入有效比例");});
            _statusText=Label(rt,"Status",_status,0,1,72,52,12);_statusText.color=UIVisualTheme.Muted;
        }
    }
}
