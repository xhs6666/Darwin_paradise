using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：完整种群列表与详情独立于底栏；暂停/鼠标/输入门控成对恢复，旧档与灭绝种群也能查看。
    public sealed class PopulationWorkbench:MonoBehaviour
    {
        public static PopulationWorkbench Instance {get;private set;}
        public bool Visible {get;private set;}
        public string SelectedId {get;private set;}
        public int SelectedTab {get;private set;}
        public Vector2 windowSize=new Vector2(1120,700);
        public int groupsPerPage=10,membersPerPage=12;
        private readonly PopulationCharts _charts=new PopulationCharts();
        private bool _running,_blocked,_cursorVisible,_show=true,_expressed,_pie=true;
        private CursorLockMode _cursorLock;
        private GameObject _inputBlocker;
        private string _search="",_hex="",_status="";private Color _draft;
        private int _filter,_page,_memberPage;private float _next;
        private Vector2 _detailsScroll,_listScroll;
        private List<PopulationRecord> _all=new List<PopulationRecord>();private List<DarwinEntity> _members=new List<DarwinEntity>();
        private List<PopulationChartValue> _genes=new List<PopulationChartValue>(),_ages=new List<PopulationChartValue>();
        public static PopulationWorkbench Ensure()
        {
            if(Instance==null)ServiceLocator.Get<UIRoot>().gameObject.AddComponent<PopulationWorkbench>();return Instance;
        }
        private void Awake(){Instance=this;_pie=PlayerPrefs.GetInt("PopulationChartKind",0)==0;}
        public void Open(string id=null,int tab=0)
        {
            if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||GameInputGate.Blocked&&!Visible)return;
            if(!Visible)
            {
                _blocked=GameInputGate.Blocked;var clock=ServiceLocator.Get<SimulationClock>();_running=clock.IsRunning;if(!WorldAuthority.Online)clock.Pause();GameInputGate.Blocked=true;
                _cursorLock=Cursor.lockState;_cursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                // ★ 修复（防Bug回归）：IMGUI遮罩本身不参与EventSystem射线，另用透明层阻止点击穿透到底栏按钮。
                _inputBlocker=UIFactory.NewRect("PopulationWorkbenchInputBlocker",ServiceLocator.Get<UIRoot>().MainCanvas.transform,typeof(UnityEngine.UI.Image));UIFactory.Stretch(_inputBlocker.GetComponent<RectTransform>());_inputBlocker.GetComponent<UnityEngine.UI.Image>().color=Color.clear;_inputBlocker.transform.SetAsLastSibling();
                FindFirstObjectByType<EntityPossessionInput>()?.Suspend();EntityPlacementTool.Instance?.CancelPlacement();Visible=true;
            }
            RefreshNow();Select(id??_all.FirstOrDefault()?.id,tab);
        }
        public void Select(string id,int tab=0)
        {
            SelectedId=id;SelectedTab=Mathf.Clamp(tab,0,6);_memberPage=0;_detailsScroll=Vector2.zero;_status="";
            var record=PopulationArchive.Find(id);if(record!=null){_draft=ColonyAppearance.ColorFor(id);_hex="#"+ColorUtility.ToHtmlStringRGB(_draft);_show=record.visible;}RefreshNow();
        }
        public void RefreshNow()
        {
            _next=Time.unscaledTime+.5f;if(!WorldAuthority.IsReplica&&WorldRuntimeGate.Ready)PopulationArchive.Observe();
            _all=PopulationArchive.All.OrderByDescending(r=>r.current>0).ThenByDescending(r=>r.current).ThenBy(r=>r.firstSeen).ThenBy(r=>r.id).ToList();
            _members=PopulationArchive.Members(SelectedId).OrderBy(e=>e.DisplayName).ThenBy(EcologyIdentity.Of).ToList();_genes=PopulationDistributions.Genes(SelectedId,_expressed);_ages=PopulationDistributions.Ages(SelectedId);
        }
        public bool ApplyDisplay(string color,bool visible)
        {
            if(!PopulationArchive.SetDisplay(SelectedId,color,visible)){_status="请输入有效的#RRGGBB颜色；显示配置由房主保存";return false;}
            try{FindFirstObjectByType<WorldSessionManager>().SaveNow(_running);_status="显示配置已保存到当前世界";}
            catch(Exception ex){_status="颜色已应用，保存失败："+ex.Message;Debug.LogWarning("[种群配置] "+ex.Message);}
            return true;
        }
        public void Close()
        {
            if(!Visible)return;Visible=false;GameInputGate.Blocked=WorldRuntimeGate.Ready?_blocked:true;
            if(_inputBlocker!=null){_inputBlocker.SetActive(false);Destroy(_inputBlocker);_inputBlocker=null;}
            Cursor.lockState=_cursorLock;Cursor.visible=_cursorVisible;
            if(_running&&!WorldAuthority.Online&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading)ServiceLocator.Get<SimulationClock>().Play();
        }
        private void Update()
        {
            if(!Visible)return;if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading){Close();return;}
            if(Input.GetKeyDown(KeyCode.Escape)){GameInputGate.ConsumeEscape();Close();return;}
            if(Time.unscaledTime>=_next)RefreshNow();
        }
        private void OnDisable()=>Close();
        private void OnDestroy(){Close();_charts.Dispose();if(Instance==this)Instance=null;}
        // ★ 修复（防Bug回归）：档案日期沿用底栏的游戏日历（10秒/天、365天/年、30天/月），避免显示累计小数天数。
        public static string Day(float time)
        {
            int totalDays=Mathf.FloorToInt(Mathf.Max(0,time)/10f),dayOfYear=totalDays%365;
            return $"第 {totalDays/365} 年 {dayOfYear/30+1} 月 {dayOfYear%30+1} 日";
        }
        private void OnGUI()
        {
            if(!Visible)return;GUI.depth=-950;RuntimeMenuTheme.Apply();var previous=GUI.color;GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=previous;
            float width=Mathf.Min(windowSize.x,Screen.width-24),height=Mathf.Min(windowSize.y,Screen.height-24),left=(Screen.width-width)/2,top=(Screen.height-height)/2;
            GUI.Box(new Rect(left,top,width,height),GUIContent.none);
            GUI.Label(new Rect(left+18,top+12,width-130,32),"种群档案 · 全部种群",RuntimeMenuTheme.Title);
            if(GUI.Button(new Rect(left+width-90,top+12,72,30),"关闭")){Close();return;}
            float listWidth=Mathf.Min(250,width*.28f);
            GUILayout.BeginArea(new Rect(left+12,top+58,listWidth,height-72));DrawList();GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(left+listWidth+28,top+58,width-listWidth-44,height-72));DrawDetails(width-listWidth-44,height-72);GUILayout.EndArea();
        }
        private void DrawList()
        {
            GUILayout.Label("存活 "+_all.Count(r=>r.current>0)+" / 已记录 "+_all.Count,RuntimeMenuTheme.Muted);
            string search=GUILayout.TextField(_search,80);if(search!=_search){_search=search;_page=0;}
            GUILayout.Label("按名称或种群标识搜索",new GUIStyle(RuntimeMenuTheme.Muted){fontSize=11});
            int filter=GUILayout.Toolbar(_filter,new[]{"全部","存活","灭绝"});if(filter!=_filter){_filter=filter;_page=0;}
            var records=_all.Where(r=>(_filter==0||(_filter==1?r.current>0:r.current==0))&&(string.IsNullOrWhiteSpace(_search)||r.name.IndexOf(_search,StringComparison.OrdinalIgnoreCase)>=0||r.id.IndexOf(_search,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
            int pages=Mathf.Max(1,Mathf.CeilToInt(records.Count/(float)groupsPerPage));_page=Mathf.Clamp(_page,0,pages-1);
            _listScroll=GUILayout.BeginScrollView(_listScroll);
            foreach(var record in records.Skip(_page*groupsPerPage).Take(groupsPerPage))
            {
                var style=new GUIStyle(record.id==SelectedId?RuntimeMenuTheme.Selected:GUI.skin.button){alignment=TextAnchor.MiddleLeft,wordWrap=true,fontSize=14};
                if(GUILayout.Button(record.name+" · "+record.id.Substring(0,6)+"\n"+Mode(record)+"  "+(record.current>0?record.current+"个体":"已灭绝"),style,GUILayout.Height(52)))Select(record.id,SelectedTab);
            }
            if(records.Count==0)GUILayout.Label("没有符合条件的种群");GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();if(GUILayout.Button("上一页"))_page=Mathf.Max(0,_page-1);GUILayout.Label((_page+1)+" / "+pages,GUILayout.Width(55));if(GUILayout.Button("下一页"))_page=Mathf.Min(pages-1,_page+1);GUILayout.EndHorizontal();
        }
        private static string Mode(PopulationRecord record)=>record.mode==EcologySettings.Load().antState?"蚁群":"狼群";
        private void DrawDetails(float width,float height)
        {
            var record=PopulationArchive.Find(SelectedId);if(record==null){GUILayout.Label("从左侧选择种群。已灭绝种群也会保留历史档案。",new GUIStyle(GUI.skin.label){wordWrap=true});return;}
            GUILayout.Label(record.name+" · "+Mode(record),new GUIStyle(RuntimeMenuTheme.Title){fontSize=22});
            GUILayout.Label("种群编号 "+record.id.Substring(0,6),RuntimeMenuTheme.Muted);
            string[] tabs={"概览","基因","年龄","发展历史","衍生种群","成员","显示设置"};int tab=GUILayout.SelectionGrid(SelectedTab,tabs,width>=670?7:4,GUILayout.Height(width>=670?32:66));if(tab!=SelectedTab){SelectedTab=tab;_detailsScroll=Vector2.zero;}
            _detailsScroll=GUILayout.BeginScrollView(_detailsScroll);
            if(SelectedTab==0)DrawOverview(record);
            else if(SelectedTab==1)DrawGenes(record,width-20);
            else if(SelectedTab==2)DrawAges(record,width-20);
            else if(SelectedTab==3)DrawHistory(record,width-20);
            else if(SelectedTab==4)DrawDescendants(record);
            else if(SelectedTab==5)DrawMembers();
            else DrawSettings(record);
            if(!string.IsNullOrEmpty(_status))GUILayout.Label(_status,new GUIStyle(GUI.skin.label){wordWrap=true});
            GUILayout.EndScrollView();
        }
        private void DrawOverview(PopulationRecord record)
        {
            GUILayout.Space(8);GUILayout.Label("当前成员 "+record.current+"     历史峰值 "+record.peak+"     直接衍生种群 "+_all.Count(r=>r.parentId==record.id));
            GUILayout.Label((record.originKnown?"成立于 ":"首次观察于 ")+Day(record.firstSeen)+"     最后存活记录 "+Day(record.lastSeen));
            if(!record.originKnown)GUILayout.Label("此前的历史未记录，旧档不推测成立时间或来源。",RuntimeMenuTheme.Muted);
            GUILayout.Label("累计成员增加 "+record.increases+" / 流失 "+record.decreases+"（包含出生、死亡和迁移）",RuntimeMenuTheme.Muted);
            if(!string.IsNullOrEmpty(record.parentId)&&PopulationArchive.Find(record.parentId) is PopulationRecord parent)
            {if(GUILayout.Button("来源种群："+parent.name+"  →"))Select(parent.id,4);}
            else GUILayout.Label(record.originKnown?"来源：独立建立或来源未记录":"来源：此前未记录");
            if(record.current>0&&RuntimeMenuTheme.Button("定位王后 / 领袖",null,false,36))LocateLeader(record.id);
            GUILayout.Space(10);GUILayout.Label("最近发展",RuntimeMenuTheme.Muted);foreach(var item in record.events.Skip(Mathf.Max(0,record.events.Count-5)).Reverse())GUILayout.Label(Day(item.time)+"  "+item.text,new GUIStyle(GUI.skin.label){wordWrap=true});
        }
        public bool LocateLeader(string id)
        {
            var member=PopulationArchive.Members(id).FirstOrDefault();if(member==null)return false;var pop=member.GetComponent<PopulationBehavior>();var point=(pop.Leader??pop).transform.position;Close();EntityViewController.Instance?.ExitView();Camera.main?.GetComponent<OrbitCamera>()?.FocusOn(point);return true;
        }
        private void ChartMode()
        {
            int mode=GUILayout.Toolbar(_pie?0:1,new[]{"扇形图","柱状图"},GUILayout.Width(220));if((mode==0)!=_pie){_pie=mode==0;PlayerPrefs.SetInt("PopulationChartKind",mode);PlayerPrefs.Save();}
        }
        private void DrawGenes(PopulationRecord record,float width)
        {
            bool expressed=GUILayout.Toggle(_expressed,"只统计生效基因");if(expressed!=_expressed){_expressed=expressed;RefreshNow();}ChartMode();
            GUILayout.Label(_pie?"扇形：各基因携带 / 生效人次构成（同一实体可携带多种基因）":"柱状：各基因携带 / 生效个体数",new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
            DrawDistribution(record,_genes,width,true);
        }
        private void DrawAges(PopulationRecord record,float width)
        {
            ChartMode();GUILayout.Label("年龄阶段分布",RuntimeMenuTheme.Muted);DrawDistribution(record,_ages,width,false);
            if(_members.Count>0){float average=_members.Average(e=>e.GetComponent<EntityLifeCycle>()?.Progress??0);GUILayout.Label("平均寿命进度 "+(100*average/Mathf.Max(.01f,LifeCycleSettings.Load().deathProgress)).ToString("F1")+"%");}
        }
        private void DrawDistribution(PopulationRecord record,List<PopulationChartValue> values,float width,bool genes)
        {
            int denominator=_members.Count>0?_members.Count:record.lastLiving;if(record.current==0)GUILayout.Label("已灭绝：显示最后一次存活记录（"+denominator+"个体）",RuntimeMenuTheme.Muted);
            float height=_pie?240:Mathf.Max(120,values.Count*28);var area=GUILayoutUtility.GetRect(width,height,GUILayout.ExpandWidth(true));_charts.Draw(area,values,_pie);
            foreach(var value in values)
            {
                float rate=denominator==0?0:100f*value.Count/denominator;string ratio=genes?(_expressed?"生效率 ":"携带率 ")+rate.ToString("F1")+"%":rate.ToString("F1")+"%";
                if(genes&&_pie)ratio+=" · 构成 "+(100f*value.Count/Mathf.Max(1,values.Sum(v=>v.Count))).ToString("F1")+"%";
                var old=GUI.color;GUI.color=value.Color;GUILayout.Label("■ "+value.Label+"    "+value.Count+"个体    "+ratio);GUI.color=old;
            }
        }
        private void DrawHistory(PopulationRecord record,float width)
        {
            GUILayout.Label("成员规模随模拟时间变化（压缩采样保留起点和最新记录）",RuntimeMenuTheme.Muted);
            var samples=record.samples.Skip(Mathf.Max(0,record.samples.Count-16)).ToList();var values=samples.Select(s=>new PopulationChartValue{Label=Day(s.time),Count=s.members,Color=ColonyAppearance.ColorFor(record.id)}).ToList();
            var area=GUILayoutUtility.GetRect(width,Mathf.Max(100,values.Count*25));_charts.Draw(area,values,false,230);
            GUILayout.Label("事件记录（最近 "+PopulationArchiveSettings.Current.maximumEvents+" 条）",RuntimeMenuTheme.Muted);
            foreach(var item in record.events.AsEnumerable().Reverse())GUILayout.Label(Day(item.time)+"  "+item.text,new GUIStyle(GUI.skin.label){wordWrap=true});
            GUILayout.Space(8);GUILayout.Label("采样明细：时间 / 总数 / 幼年 / 成年 / 老年",RuntimeMenuTheme.Muted);
            foreach(var item in record.samples.AsEnumerable().Reverse())GUILayout.Label(Day(item.time)+"    "+item.members+" / "+item.infants+" / "+item.adults+" / "+item.elders);
        }
        private void DrawDescendants(PopulationRecord record)
        {
            if(!string.IsNullOrEmpty(record.parentId)&&PopulationArchive.Find(record.parentId) is PopulationRecord parent&&GUILayout.Button("← 来源："+parent.name))Select(parent.id,4);
            var descendants=new List<(PopulationRecord,int)>();var queue=new Queue<(string,int)>();var visited=new HashSet<string>{record.id};queue.Enqueue((record.id,0));
            while(queue.Count>0){var item=queue.Dequeue();foreach(var child in _all.Where(r=>r.parentId==item.Item1))if(visited.Add(child.id)){descendants.Add((child,item.Item2+1));queue.Enqueue((child.id,item.Item2+1));}}
            GUILayout.Label("衍生种群 "+descendants.Count+"（含后续世代）",RuntimeMenuTheme.Muted);
            foreach(var item in descendants){var child=item.Item1;if(GUILayout.Button("第"+item.Item2+"代 · "+child.name+" · "+child.id.Substring(0,6)+" · "+(child.current>0?child.current+"个体":"已灭绝"),GUILayout.Height(34)))Select(child.id,0);}
            if(descendants.Count==0)GUILayout.Label("尚未记录该种群建立新的衍生种群。");
        }
        private void DrawMembers()
        {
            GUILayout.Label("该种群全部存活成员 · "+_members.Count,RuntimeMenuTheme.Muted);int pages=Mathf.Max(1,Mathf.CeilToInt(_members.Count/(float)membersPerPage));_memberPage=Mathf.Clamp(_memberPage,0,pages-1);
            foreach(var entity in _members.Skip(_memberPage*membersPerPage).Take(membersPerPage))
            {
                string age=entity.Genes.CurrentAge==AgeState.Infant?"幼年":entity.Genes.CurrentAge==AgeState.Adult?"成年":"老年";
                if(GUILayout.Button(entity.DisplayName+"  ·  "+age+"  ·  定位",GUILayout.Height(30))){var point=entity.transform.position;Close();EntityViewController.Instance?.ExitView();Camera.main?.GetComponent<OrbitCamera>()?.FocusOn(point);}
            }
            GUILayout.BeginHorizontal();if(GUILayout.Button("上一页"))_memberPage=Mathf.Max(0,_memberPage-1);GUILayout.Label((_memberPage+1)+" / "+pages);if(GUILayout.Button("下一页"))_memberPage=Mathf.Min(pages-1,_memberPage+1);GUILayout.EndHorizontal();
        }
        private void DrawSettings(PopulationRecord record)
        {
            GUILayout.Label("该种群显示配置",new GUIStyle(RuntimeMenuTheme.Title){fontSize=19});GUILayout.Label("颜色同时用于活动范围和成员标记，随世界存档保存。",new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
            GUILayout.Label("十六进制颜色（#RRGGBB）");string hex=GUILayout.TextField(_hex,9);if(hex!=_hex){_hex=hex;if(ColorUtility.TryParseHtmlString(hex,out var color))_draft=color;}
            GUILayout.Label("红 "+Mathf.RoundToInt(_draft.r*255));float r=RuntimeMenuTheme.Slider(_draft.r,0,1);GUILayout.Label("绿 "+Mathf.RoundToInt(_draft.g*255));float g=RuntimeMenuTheme.Slider(_draft.g,0,1);GUILayout.Label("蓝 "+Mathf.RoundToInt(_draft.b*255));float b=RuntimeMenuTheme.Slider(_draft.b,0,1);
            if(r!=_draft.r||g!=_draft.g||b!=_draft.b){_draft=new Color(r,g,b);_hex="#"+ColorUtility.ToHtmlStringRGB(_draft);}
            var preview=GUILayoutUtility.GetRect(100,34);var old=GUI.color;GUI.color=_draft;GUI.DrawTexture(preview,Texture2D.whiteTexture);GUI.color=old;
            _show=GUILayout.Toggle(_show,"显示该种群范围与成员标记");
            bool enabled=GUI.enabled;GUI.enabled=!WorldAuthority.IsReplica&&WorldAuthority.CanEdit;
            if(RuntimeMenuTheme.Button("保存显示配置","save",true,38))ApplyDisplay(_hex,_show);GUI.enabled=enabled;
            if(RuntimeMenuTheme.Button("恢复默认配色",null,false,32)){_draft=ColonyAppearance.DefaultColorFor(record.id);_hex="#"+ColorUtility.ToHtmlStringRGB(_draft);}
            if(WorldAuthority.IsReplica)GUILayout.Label("联机成员可查看，显示配置由房主保存。",RuntimeMenuTheme.Muted);
        }
    }
}
