using System;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：两列选择界面独立于底栏大小，模板与基因随机来源互斥；输入/鼠标/暂停均成对恢复。
    public sealed class RandomSpawnWorkbench:MonoBehaviour
    {
        public static RandomSpawnWorkbench Instance {get;private set;}public bool Visible {get;private set;}
        public Vector2 windowSize=new(920,680);
        private RandomSpawnPanel _owner;private RandomSpawnRecipe _draft;private string _ratio="30",_editingId,_status="",_search="";private bool _rules,_landOnly,_running,_blocked,_cursorVisible,_ruleEnabled=true;private CursorLockMode _cursorLock;private GameObject _blocker;private Vector2 _leftScroll,_rightScroll,_rulesScroll;
        public static RandomSpawnWorkbench Ensure()=>Instance!=null?Instance:ServiceLocator.Get<UIRoot>().gameObject.AddComponent<RandomSpawnWorkbench>();
        private void Awake(){Instance=this;}
        public void Open(RandomSpawnPanel owner,bool rules=false)
        {
            if(!WorldAuthority.CanEdit||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||GameInputGate.Blocked&&!Visible)return;
            if(!Visible)
            {
                RuntimeDropdownDismissal.CloseOpenPopups();_running=ServiceLocator.Get<SimulationClock>().IsRunning;_blocked=GameInputGate.Blocked;if(!WorldAuthority.Online)ServiceLocator.Get<SimulationClock>().Pause();GameInputGate.Blocked=true;
                _cursorVisible=Cursor.visible;_cursorLock=Cursor.lockState;Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
                _blocker=UIFactory.NewRect("RandomSpawnWorkbenchBlocker",ServiceLocator.Get<UIRoot>().MainCanvas.transform,typeof(Image));UIFactory.Stretch(_blocker.GetComponent<RectTransform>());_blocker.GetComponent<Image>().color=Color.clear;_blocker.transform.SetAsLastSibling();
                EntityPlacementTool.Instance?.CancelPlacement();FindFirstObjectByType<EntityPossessionInput>()?.Suspend();Visible=true;
            }
            _owner=owner;_draft=JsonUtility.FromJson<RandomSpawnRecipe>(JsonUtility.ToJson(owner.Recipe));_landOnly=owner.LandOnly;_editingId=null;_ruleEnabled=true;_status="";_rules=rules;
        }
        public void Close()
        {
            if(!Visible)return;Visible=false;if(_blocker!=null){_blocker.SetActive(false);Destroy(_blocker);_blocker=null;}
            GameInputGate.Blocked=WorldRuntimeGate.Ready?_blocked:true;Cursor.visible=_cursorVisible;Cursor.lockState=_cursorLock;
            if(_running&&!WorldAuthority.Online&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading)ServiceLocator.Get<SimulationClock>().Play();
        }
        private void Update(){if(!Visible)return;if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||!WorldAuthority.CanEdit){Close();return;}if(Input.GetKeyDown(KeyCode.Escape)){GameInputGate.ConsumeEscape();Close();}}
        private void OnDisable()=>Close();private void OnDestroy(){Close();if(Instance==this)Instance=null;}
        public void SelectTemplate(EntityTemplateData template)
        {
            var genome=template==null?RandomSpawnRecipe.DefaultGenome():new Genome();if(template!=null)foreach(var gene in template.genes)genome.Add(GeneInstance.Create(gene.definitionId,gene.value,gene.tier));
            _draft=new RandomSpawnRecipe{method=RandomSpawnMethod.Template,name=template?.templateName??"默认实体",template=genome};
        }
        public void UseGeneRandom(){_draft.method=RandomSpawnMethod.Genes;_draft.name="基因条件随机";_draft.template=null;}
        public void SetGene(string id,bool include,bool selected)
        {
            UseGeneRandom();var chosen=include?_draft.included:_draft.excluded;var other=include?_draft.excluded:_draft.included;
            if(selected){if(!chosen.Contains(id))chosen.Add(id);other.Remove(id);}else chosen.Remove(id);
        }
        public RandomSpawnRecipe Draft=>JsonUtility.FromJson<RandomSpawnRecipe>(JsonUtility.ToJson(_draft));
        public bool ApplySelection()
        {
            try{_owner.SetRecipe(_draft);_owner.SetLandOnly(_landOnly);Close();return true;}catch(Exception ex){_status=ex.Message;return false;}
        }
        public bool SaveRule(float percent)
        {
            if(!float.IsFinite(percent)||percent<0||percent>=100){_status="目标比例请输入0～99之间的百分数";return false;}
            var rule=new PopulationMaintenanceRule{id=_editingId??Guid.NewGuid().ToString("N"),enabled=_ruleEnabled,ratio=percent/100f,landOnly=_landOnly,source=Draft};
            try
            {
                var catalog=GeneDatabase.All.Select(d=>d.Id).ToArray();rule.source.Validate(catalog);var validation=PopulationMaintenance.Capture();validation.rules.RemoveAll(r=>r.id==rule.id);validation.rules.Add(rule);PopulationMaintenance.Validate(validation,catalog);
                if(!LanEditActions.Submit(new LanCommand{kind="maintenanceSet",maintenanceRule=rule})){_status="无法保存规则，请检查权限或规则数量";return false;}
                _editingId=rule.id;Persist();_rules=true;return true;
            }
            catch(Exception ex){_status=ex.Message;return false;}
        }
        private void Persist(){if(LanSession.Instance?.IsClient!=true){FindFirstObjectByType<WorldSessionManager>().SaveNow(_running);_status="规则已保存：不足时补充，超量时减少繁殖";}else _status="已发送给房主，等待同步";}
        private void OnGUI()
        {
            if(!Visible)return;GUI.depth=-960;RuntimeMenuTheme.Apply();var old=GUI.color;GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
            float width=Mathf.Min(windowSize.x,Screen.width-24),height=Mathf.Min(windowSize.y,Screen.height-24),x=(Screen.width-width)*.5f,y=(Screen.height-height)*.5f;GUI.Box(new Rect(x,y,width,height),GUIContent.none);GUI.Label(new Rect(x+18,y+12,width-130,32),"随机生成 · 选择实体与维持比例",RuntimeMenuTheme.Title);if(GUI.Button(new Rect(x+width-90,y+12,72,30),"关闭")){Close();return;}
            GUILayout.BeginArea(new Rect(x+16,y+54,width-32,40));_rules=GUILayout.Toolbar(_rules?1:0,new[]{"生成来源（两种方式）","维持比例规则"})==1;GUILayout.EndArea();
            float bottom=y+height-136,contentHeight=height-248;
            if(!_rules)
            {
                float half=(width-48)*.5f;
                GUILayout.BeginArea(new Rect(x+16,y+100,half,contentHeight));GUILayout.Label("方式一：完全按照实体模板",RuntimeMenuTheme.Subtitle);_leftScroll=GUILayout.BeginScrollView(_leftScroll);if(GUILayout.Button("默认实体",_draft.method==RandomSpawnMethod.Template&&_draft.name=="默认实体"?RuntimeMenuTheme.Selected:GUI.skin.button))SelectTemplate(null);foreach(var template in EntityTemplateManager.Templates)if(GUILayout.Button(template.templateName,_draft.method==RandomSpawnMethod.Template&&_draft.name==template.templateName?RuntimeMenuTheme.Selected:GUI.skin.button))SelectTemplate(template);GUILayout.Label("复制模板的全部基因与数值，不添加其他随机基因。",new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});GUILayout.EndScrollView();GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(x+32+half,y+100,half,contentHeight));GUILayout.Label("方式二：基因条件，其余随机",RuntimeMenuTheme.Subtitle);if(GUILayout.Button(_draft.method==RandomSpawnMethod.Genes?"✓ 正在使用基因条件随机":"使用基因条件随机"))UseGeneRandom();_search=GUILayout.TextField(_search);GUILayout.BeginHorizontal();GUILayout.Label("基因",RuntimeMenuTheme.Muted,GUILayout.ExpandWidth(true));GUILayout.Label("一定有",RuntimeMenuTheme.Muted,GUILayout.Width(64));GUILayout.Label("一定没有",RuntimeMenuTheme.Muted,GUILayout.Width(64));GUILayout.EndHorizontal();_rightScroll=GUILayout.BeginScrollView(_rightScroll);
                foreach(var gene in GeneDatabase.All.OrderBy(g=>g.Tier).ThenBy(g=>g.Id).Where(g=>string.IsNullOrEmpty(_search)||g.DisplayName.Contains(_search)||g.Id.Contains(_search)))
                {
                    GUILayout.BeginHorizontal();GUILayout.Label(gene.DisplayName,GUILayout.MinWidth(110),GUILayout.ExpandWidth(true));bool has=_draft.included.Contains(gene.Id),absent=_draft.excluded.Contains(gene.Id);bool next=GUILayout.Toggle(has,"有",GUILayout.Width(64));if(next!=has)SetGene(gene.Id,true,next);next=GUILayout.Toggle(absent,"无",GUILayout.Width(64));if(next!=absent)SetGene(gene.Id,false,next);GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();GUILayout.EndArea();
            }
            else
            {
                GUILayout.BeginArea(new Rect(x+16,y+100,width-32,contentHeight));GUILayout.BeginHorizontal();GUILayout.Label("少于目标：直接补充 · 高于目标：延长匹配实体的新孕育间隔",new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true},GUILayout.ExpandWidth(true));if(GUILayout.Button("新增规则",GUILayout.Width(100))){_editingId=null;_ruleEnabled=true;_rules=false;}GUILayout.EndHorizontal();_rulesScroll=GUILayout.BeginScrollView(_rulesScroll);
                foreach(var rule in PopulationMaintenance.Rules.ToArray())
                {
                    int count=PopulationMaintenance.Count(rule);GUILayout.BeginHorizontal();bool enabled=GUILayout.Toggle(rule.enabled,"",GUILayout.Width(24));if(enabled!=rule.enabled){var edit=JsonUtility.FromJson<PopulationMaintenanceRule>(JsonUtility.ToJson(rule));edit.enabled=enabled;if(LanEditActions.Submit(new LanCommand{kind="maintenanceSet",maintenanceRule=edit}))Persist();}
                    GUILayout.Label(rule.source.name+" · 目标 "+(rule.ratio*100).ToString("0.#")+"% · 当前 "+count+" / "+PopulationMaintenance.Total+"（"+(PopulationMaintenance.Total>0?100f*count/PopulationMaintenance.Total:0).ToString("0.#")+"%）",new GUIStyle(GUI.skin.label){wordWrap=true},GUILayout.ExpandWidth(true));
                    if(GUILayout.Button("编辑",GUILayout.Width(60))){_draft=JsonUtility.FromJson<RandomSpawnRecipe>(JsonUtility.ToJson(rule.source));_editingId=rule.id;_ruleEnabled=rule.enabled;_landOnly=rule.landOnly;_ratio=(rule.ratio*100).ToString("0.#");_rules=false;}
                    if(GUILayout.Button("删除",GUILayout.Width(60))&&LanEditActions.Submit(new LanCommand{kind="maintenanceRemove",target=rule.id})){Persist();if(_editingId==rule.id)_editingId=null;}GUILayout.EndHorizontal();
                }
                if(PopulationMaintenance.Rules.Count==0)GUILayout.Label("尚无维持规则。先选择左侧模板方式或右侧基因方式，再填写下方比例。");GUILayout.EndScrollView();GUILayout.EndArea();
            }
            GUILayout.BeginArea(new Rect(x+16,bottom,width-32,124));GUILayout.Label("当前来源："+(_draft.method==RandomSpawnMethod.Template?"模板 · "+_draft.name:"基因随机 · 必有"+_draft.included.Count+"项 / 必无"+_draft.excluded.Count+"项"),RuntimeMenuTheme.Muted);
            GUILayout.BeginHorizontal();if(GUILayout.Button("应用生成来源",GUILayout.Height(32)))ApplySelection();GUILayout.Label("维持",GUILayout.Width(35));_ratio=GUILayout.TextField(_ratio,5,GUILayout.Width(55));GUILayout.Label("%",GUILayout.Width(20));if(GUILayout.Button(_landOnly?"仅陆地":"全部地表",GUILayout.Width(110)))_landOnly=!_landOnly;if(GUILayout.Button(_editingId==null?"新增维持规则":"保存规则修改",GUILayout.Height(32))){if(float.TryParse(_ratio,out float percent))SaveRule(percent);else _status="请输入有效比例";}GUILayout.EndHorizontal();GUILayout.Label(_status,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});GUILayout.EndArea();
        }
    }
}
