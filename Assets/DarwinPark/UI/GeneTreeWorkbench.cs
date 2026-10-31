using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using DarwinPark.Genetics;
using DarwinPark.Core;

namespace DarwinPark.UI
{
    // ★ 新增：运行时基因树工作台，流程图与层级列表共用同一文档，不依赖 UnityEditor。
    public sealed class GeneTreeWorkbench : MonoBehaviour
    {
        public bool Visible {get;private set;}
        private GeneTreeDocument _doc;
        private Dictionary<string,string> _catalog;
        private string _folder="",_status="",_selected,_linkFrom,_dragId,_catalogDrag;
        private string _kind="evolution",_value="100",_raw="";
        private GeneTreeEdge _edge;
        private Vector2 _listScroll,_rawScroll,_dragOffset;
        private readonly GeneGraphViewport _view=new GeneGraphViewport();
        private bool _fitPending;
        private bool _hierarchy,_dirty,_advanced;
        private readonly GeneValueForm _form=new GeneValueForm();
        private Rect _canvas;
        private readonly Vector2 _nodeSize=new Vector2(170,65);
        private readonly Dictionary<string,(string raw,string name)> _labels=new Dictionary<string,(string,string)>();
        private Texture2D _grid;
        private string GeneName(string id)
        {
            if(!_catalog.TryGetValue(id,out var raw))return id;
            if(_labels.TryGetValue(id,out var label)&&label.raw==raw)return label.name;
            var definition=JsonUtility.FromJson<GeneDefinitionJson>(raw);
            string name=string.IsNullOrEmpty(definition.displayName)?id:definition.displayName;
            _labels[id]=(raw,name);return name;
        }
        public void Open()
        {
            if(!WorldAuthority.CanEdit)return;
            try { _folder=DarwinPark.Core.GamePaths.Mods; _catalog=GeneTreeStorage.ReadCatalog();_doc=GeneTreeStorage.Load();GeneTreeStorage.Validate(_doc,_catalog); _selected=null;_edge=null;_linkFrom=null;_dragId=null;_catalogDrag=null;_dirty=false;_fitPending=true;Visible=true;_status="配置目录："+GeneTreeStorage.Folder; }
            catch(Exception ex){_status=ex.Message;Debug.LogError(ex);}
        }
        public void Close()=>Visible=false;
        public bool HasUnsavedChanges => _dirty;
        public void Draw(Rect screen,Action close)
        {
            // ★ 适配：工具保持原来的三栏位置、缩放坐标与拖动事件，只统一表现层。
            RuntimeMenuTheme.Apply();
            GUI.Box(screen,GUIContent.none);
            GUILayout.BeginArea(new Rect(screen.x+16,screen.y+12,screen.width-32,screen.height-24));
            GUILayout.BeginHorizontal();GUILayout.Label("基因树工具",GUILayout.Width(140));
            if(GUILayout.Button(_hierarchy?"切换流程图":"切换层级列表",GUILayout.Width(150)))_hierarchy=!_hierarchy;
            if(GUILayout.Button("保存并应用",RuntimeMenuTheme.Primary,GUILayout.Width(110)))Save();
            if(GUILayout.Button("重新读取",GUILayout.Width(100))){if(!_dirty)Open();else _status="有未保存修改，请先保存，或点放弃修改。";}
            if(GUILayout.Button("放弃修改",GUILayout.Width(100))){_dirty=false;Open();}
            if(GUILayout.Button("返回主界面",GUILayout.Width(120))){if(_dirty)_status="请先保存，或放弃修改后返回。";else close();}
            GUILayout.EndHorizontal();
            GUILayout.Label("左侧拖入 · 拖动节点 · Shift左键源→目标建立下级 · 右键节点/连线编辑 · 红=抑制 / 绿=促进");
            GUILayout.BeginHorizontal();GUILayout.Label("导入文件夹",GUILayout.Width(90));_folder=GUILayout.TextField(_folder);
            if(GUILayout.Button("读取 JSON",GUILayout.Width(110)))Try(()=>{var imported=GeneTreeStorage.ReadCatalog(_folder);foreach(var item in imported)_catalog[item.Key]=item.Value;_dirty=true;_status="已读取 "+imported.Count+" 个基因";});GUILayout.EndHorizontal();
            GUILayout.Label((_dirty?"● 未保存  ":"")+_status);
            // ★ 适配：显式按钮和百分比解决大树只能滚动、无法缩小查看全貌的问题。
            GUILayout.BeginHorizontal();GUI.enabled=!_hierarchy;
            if(GUILayout.Button("− 缩小",GUILayout.Width(85)))_view.SetZoom(_view.Zoom/1.2f,_canvas.size*.5f);
            GUILayout.Label(Mathf.RoundToInt(_view.Zoom*100)+"%",GUILayout.Width(55));
            if(GUILayout.Button("＋ 放大",GUILayout.Width(85)))_view.SetZoom(_view.Zoom*1.2f,_canvas.size*.5f);
            if(GUILayout.Button("适应全图",GUILayout.Width(100)))_fitPending=true;
            if(GUILayout.Button("100%",GUILayout.Width(70)))_view.SetZoom(1,_canvas.size*.5f);
            GUI.enabled=true;GUILayout.Label("画布内滚轮缩放 · 中键拖动画布");GUILayout.EndHorizontal();
            GUILayout.EndArea();
            // ★ 修复（防Bug回归）：增加缩放行后同步下移三栏，避免画布覆盖按钮和百分比。
            float top=screen.y+200,bottom=screen.height-220;
            var left=new Rect(screen.x+16,top,225,bottom);GUI.Box(left,"基因文件");
            GUILayout.BeginArea(new Rect(left.x+8,left.y+25,left.width-16,left.height-34));_listScroll=GUILayout.BeginScrollView(_listScroll);
            foreach(var id in _catalog.Keys.OrderBy(x=>x))
            {
                var rowStyle=new GUIStyle(id==_selected?RuntimeMenuTheme.Selected:GUI.skin.button){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(38,8,3,3)};
                GUILayout.Label(GeneName(id),rowStyle,GUILayout.Height(32));var row=GUILayoutUtility.GetLastRect();
                RuntimeMenuTheme.Icon(new Rect(row.x+6,row.y+4,24,24),UIVisualTheme.GeneIcon(id),id==_selected?UIVisualTheme.Accent:UIVisualTheme.Muted);
                if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&row.Contains(Event.current.mousePosition)){_catalogDrag=id;Event.current.Use();}
            }
            GUILayout.EndScrollView();GUILayout.EndArea();
            _canvas=new Rect(left.xMax+10,top,Mathf.Max(250,screen.width-580),bottom);GUI.Box(_canvas,GUIContent.none);
            if(_hierarchy)DrawHierarchy();else DrawGraph();
            DrawInspector(new Rect(_canvas.xMax+10,top,screen.xMax-_canvas.xMax-26,bottom));
            var ev=Event.current;
            if(_catalogDrag!=null)
            {
                GUI.Label(new Rect(ev.mousePosition+new Vector2(12,8),new Vector2(210,30)),_catalogDrag,GUI.skin.box);
                if(ev.type==EventType.MouseUp)
                {
                    if(_canvas.Contains(ev.mousePosition)&&!_hierarchy)AddNode(_catalogDrag,_view.ToGraph(ev.mousePosition-_canvas.position));
                    _catalogDrag=null;ev.Use();
                }
            }
            if(ev.type==EventType.KeyDown&&ev.keyCode==KeyCode.Escape){_linkFrom=null;_dragId=null;_catalogDrag=null;ev.Use();}
        }
        private void AddNode(string id,Vector2 at)
        {
            if(_doc.nodes.Any(n=>n.id==id)){_status="该基因已在画布中";return;}
            _doc.nodes.Add(new GeneTreeNode{id=id,position=at});Select(id);_dirty=true;
        }
        private void Select(string id){
            // ★ 修复（防Bug回归）：切换节点前保留当前表单，非法输入时不切走并显示原因。
            if(_selected!=null&&_selected!=id){try{CommitSelected();}catch(Exception ex){_status=ex.Message;return;}}
            _selected=id;_edge=null;_raw=_catalog[id];_rawScroll=Vector2.zero;_form.Load(_raw);_advanced=false;}
        private void DrawGraph()
        {
            if(_fitPending){_view.Fit(_doc.nodes,_nodeSize,_canvas.size);_fitPending=false;}
            var input=Event.current;
            if(_canvas.Contains(input.mousePosition)&&input.type==EventType.ScrollWheel)
            {_view.SetZoom(_view.Zoom*Mathf.Pow(1.12f,-input.delta.y),input.mousePosition-_canvas.position);input.Use();}
            if(_canvas.Contains(input.mousePosition)&&input.type==EventType.MouseDrag&&input.button==2)
            {_view.Scroll=Vector2.Max(Vector2.zero,_view.Scroll-input.delta);input.Use();}
            Vector2 extent=new Vector2(3000,2400);
            foreach(var node in _doc.nodes)extent=Vector2.Max(extent,node.position+_nodeSize+Vector2.one*80);
            float zoom=_view.Zoom;
            _view.Scroll=GUI.BeginScrollView(_canvas,_view.Scroll,new Rect(Vector2.zero,Vector2.Max(extent*zoom,_canvas.size-Vector2.one*18)));
            // ★ 修复（防Bug回归）：绘制和命中统一除以缩放值，缩小时拖动、选线和拖入不会发生偏移。
            Vector2 mouse=Event.current.mousePosition/zoom;
            // ★ 新增：使用平铺点阵纹理代替大量网格线，低缩放时也不会堆叠成亮色背景。
            if(_grid==null)_grid=Resources.Load<Texture2D>("UI/Surfaces/grid");
            if(_grid!=null)GUI.DrawTextureWithTexCoords(new Rect(Vector2.zero,extent*zoom),_grid,new Rect(0,0,extent.x/40,extent.y/40));
            foreach(var edge in _doc.edges)
            {
                var from=_doc.nodes.Find(n=>n.id==edge.from);var to=_doc.nodes.Find(n=>n.id==edge.to);if(from==null||to==null)continue;
                Vector2 a,b;
                if(edge.kind=="evolution"){a=from.position+new Vector2(85,65);b=to.position+new Vector2(85,0);}
                else {bool right=to.position.x>=from.position.x;a=from.position+new Vector2(right?170:0,32);b=to.position+new Vector2(right?0:170,32);}
                var color=edge.kind=="suppress"?UIVisualTheme.Danger:edge.kind=="promote"?UIVisualTheme.Accent:edge.kind=="error"?new Color(1,.72f,.25f):new Color(.52f,.69f,.91f);
                if(edge.kind=="error"||edge.kind=="suppress"||edge.kind=="promote")DashedLine(a*zoom,b*zoom,color,2);else Line(a*zoom,b*zoom,color,2);
                Vector2 d=(b-a).normalized;Line(b*zoom,(b-d*14+new Vector2(-d.y,d.x)*7)*zoom,color,2);Line(b*zoom,(b-d*14-new Vector2(-d.y,d.x)*7)*zoom,color,2);
                string relation=edge.kind=="evolution"?"进化":edge.kind=="error"?"出错":edge.kind=="suppress"?"抑制":"促进";
                var relationStyle=new GUIStyle(GUI.skin.label){fontSize=Mathf.Max(6,Mathf.RoundToInt(14*zoom))};relationStyle.normal.textColor=color;
                GUI.Label(new Rect((a+b)*.5f*zoom,new Vector2(110,23)*zoom),relation+" "+edge.value,relationStyle);
                if(Event.current.type==EventType.MouseDown&&Event.current.button==1&&Distance(mouse,a,b)<12/zoom)
                {try{CommitSelected();_edge=edge;_selected=null;_kind=edge.kind;_value=edge.value.ToString(System.Globalization.CultureInfo.InvariantCulture);}catch(Exception ex){_status=ex.Message;}Event.current.Use();}
            }
            foreach(var node in _doc.nodes)
            {
                var rect=new Rect(node.position*zoom,_nodeSize*zoom);Color old=GUI.color;
                if(node.error)
                {
                    var diamond=Resources.Load<Texture2D>("UI/Surfaces/"+(node.id==_selected?"diamond_selected":"diamond"));
                    GUI.DrawTexture(new Rect(rect.center-new Vector2(90,40)*zoom,new Vector2(180,80)*zoom),diamond);
                    var textStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.Max(6,Mathf.RoundToInt(14*zoom)),padding=new RectOffset(Mathf.RoundToInt(24*zoom),0,0,0)};
                    GUI.Label(rect,GeneName(node.id),textStyle);
                    RuntimeMenuTheme.Icon(new Rect(rect.x+14*zoom,rect.center.y-12*zoom,24*zoom,24*zoom),UIVisualTheme.GeneIcon(node.id),node.id==_selected?UIVisualTheme.Accent:UIVisualTheme.Muted);
                }
                else
                {
                    var style=Centered(zoom);style.normal.background=UIVisualTheme.SurfaceSprite(node.id==_selected?"selected":"button").texture;
                    style.padding.left=Mathf.RoundToInt(32*zoom);
                    GUI.Box(rect,GeneName(node.id),style);
                    RuntimeMenuTheme.Icon(new Rect(rect.x+10*zoom,rect.center.y-13*zoom,26*zoom,26*zoom),UIVisualTheme.GeneIcon(node.id),node.id==_selected?UIVisualTheme.Accent:UIVisualTheme.Muted);
                }
                GUI.color=old;
                var ev=Event.current;
                if(ev.type==EventType.MouseDown&&rect.Contains(ev.mousePosition))
                {
                    if(ev.button==1){Select(node.id);_linkFrom=node.id;_status="右侧设置节点，或选择关系类型后点目标节点建立连线";}
                    else if(ev.button==0)
                    {
                        if(_linkFrom!=null&&_linkFrom!=node.id){Connect(_linkFrom,node.id,ev.shift?"evolution":_kind);_linkFrom=null;}
                        else if(ev.shift){_linkFrom=node.id;_kind="evolution";_status="再点目标节点建立下级关系";}
                        else{Select(node.id);_dragId=node.id;_dragOffset=mouse-node.position;}
                    }
                    ev.Use();
                }
            }
            if(Event.current.type==EventType.MouseDrag&&_dragId!=null){var n=_doc.nodes.Find(n=>n.id==_dragId);n.position=Vector2.Max(Vector2.zero,mouse-_dragOffset);_dirty=true;Event.current.Use();}
            if(Event.current.type==EventType.MouseUp)_dragId=null;
            GUI.EndScrollView();
        }
        private void Connect(string from,string to,string kind)
        {
            var edge=new GeneTreeEdge{from=from,to=to,kind=kind,value=100};
            if(_doc.edges.Any(e=>e.from==from&&e.to==to&&e.kind==kind)){_status="关系已存在";return;}
            _doc.edges.Add(edge);
            try{GeneTreeStorage.Validate(_doc,_catalog);if(kind=="error")_doc.nodes.Find(n=>n.id==to).error=true;_dirty=true;_status="已连接 "+from+" → "+to;}
            catch(Exception ex){_doc.edges.Remove(edge);_status=ex.Message;}
        }
        private Vector2 _hierarchyScroll;
        private void DrawHierarchy()
        {
            GUILayout.BeginArea(_canvas);_hierarchyScroll=GUILayout.BeginScrollView(_hierarchyScroll);
            var seen=new HashSet<string>();foreach(var n in _doc.nodes.Where(n=>!_doc.edges.Any(e=>e.kind=="evolution"&&e.to==n.id)))Branch(n.id,0,seen);
            foreach(var n in _doc.nodes)if(!seen.Contains(n.id))Branch(n.id,0,seen);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private void Branch(string id,int depth,HashSet<string> seen)
        {
            if(!seen.Add(id))return;GUILayout.BeginHorizontal();GUILayout.Space(depth*22);
            if(GUILayout.Button((_doc.nodes.Find(n=>n.id==id).error?"◇ ":"□ ")+GeneName(id),id==_selected?RuntimeMenuTheme.Selected:GUI.skin.button,GUILayout.Height(30)))Select(id);GUILayout.EndHorizontal();
            foreach(var e in _doc.edges.Where(e=>e.from==id))
            {if(e.kind=="evolution")Branch(e.to,depth+1,seen);else GUILayout.Label(new string(' ',(depth+1)*3)+e.kind+" → "+e.to+" ("+e.value+")");}
        }
        private void DrawInspector(Rect rect)
        {
            GUI.Box(rect,"属性 / 右键设置");GUILayout.BeginArea(new Rect(rect.x+8,rect.y+28,rect.width-16,rect.height-40));
            GUILayout.Label("关系类型");int index=Array.IndexOf(new[]{"evolution","error","suppress","promote"},_kind);
            int next=GUILayout.SelectionGrid(Mathf.Max(0,index),new[]{"向下进化","侧向出错","红色抑制","绿色促进"},2);_kind=new[]{"evolution","error","suppress","promote"}[next];
            if(_edge!=null)
            {
                GUILayout.Label(_edge.from+" → "+_edge.to);_value=GUILayout.TextField(_value);
                if(GUILayout.Button("应用关系数值"))Try(()=>{if(!float.TryParse(_value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var v)||v<0||float.IsNaN(v)||float.IsInfinity(v))throw new Exception("请输入非负有限数值");string oldKind=_edge.kind;_edge.kind=_kind;try{GeneTreeStorage.Validate(_doc,_catalog);}catch{_edge.kind=oldKind;throw;}_edge.value=v;if(_kind=="error")_doc.nodes.Find(n=>n.id==_edge.to).error=true;_dirty=true;});
                if(GUILayout.Button("删除关系")){_doc.edges.Remove(_edge);_edge=null;_dirty=true;}
            }
            else if(_selected!=null)
            {
                GUILayout.Label(_selected);var node=_doc.nodes.Find(n=>n.id==_selected);bool error=GUILayout.Toggle(node.error,"菱形：出错基因");if(error!=node.error){node.error=error;_dirty=true;}
                if(GUILayout.Button("以此为源建立所选关系")){_linkFrom=_selected;_status="点击目标节点";}
                bool advanced=GUILayout.Toggle(_advanced,"高级 JSON 编辑");
                if(advanced!=_advanced){try{if(advanced)_raw=_form.Export();else _form.Load(_raw);_advanced=advanced;}catch(Exception ex){_status=ex.Message;}}
                _rawScroll=GUILayout.BeginScrollView(_rawScroll,GUILayout.Height(Mathf.Max(80,rect.height-400)));
                if(_advanced){string edited=GUILayout.TextArea(_raw,GUILayout.ExpandHeight(true));if(edited!=_raw){_raw=edited;_dirty=true;}}
                else{GUI.changed=false;_form.Draw();if(GUI.changed)_dirty=true;}
                GUILayout.EndScrollView();
                if(GUILayout.Button("应用基因数值",RuntimeMenuTheme.Primary))Try(()=>{if(!_advanced)_raw=_form.Export();var d=JsonUtility.FromJson<GeneDefinitionJson>(_raw);if(d==null||d.id!=_selected)throw new Exception("id 不能更改");if(!Enum.TryParse<GeneCategory>(d.category,true,out _))throw new Exception("category 无效");_form.Load(_raw);_catalog[_selected]=_raw;_dirty=true;_status="已应用节点配置，请保存";});
                if(GUILayout.Button("移出画布")){_doc.nodes.Remove(node);_doc.edges.RemoveAll(e=>e.from==node.id||e.to==node.id);_selected=null;_dirty=true;}
            }
            GUILayout.FlexibleSpace();GUILayout.Label("影响值单位与原配置 coefficient 相同。进化/出错数值为相对选择权重。未保存的 JSON 文本需先应用。");GUILayout.EndArea();
        }
        private void CommitSelected()
        {
            if(_selected==null)return;
            if(!_advanced)_raw=_form.Export();
            var definition=JsonUtility.FromJson<GeneDefinitionJson>(_raw);
            if(definition==null||definition.id!=_selected)throw new Exception("当前 JSON id 无效，未保存");
            _catalog[_selected]=_raw;
        }
        private void Save()=>Try(()=>{
            if(!WorldAuthority.CanEdit)return;
            CommitSelected();
            if(LanSession.Instance?.IsClient==true){LanEditActions.Profile(_catalog,_doc);_dirty=false;_status="已提交管理员应用";return;}
            GeneTreeStorage.Save(_doc,_catalog);
            // ★ 修复（防Bug回归）：世界快照存在时也要更新内存配置，否则重新加载仍读旧快照，管理员的修改不会同步给成员。
            if(GeneRuntimeProfile.Catalog!=null){GeneRuntimeProfile.Catalog=_catalog;GeneRuntimeProfile.Tree=_doc;}
            GeneDatabase.Reload();GeneDatabase.EnsureLoaded();foreach(var entity in DarwinPark.Entities.EntityRegistry.All)entity.Genes.Recalculate();_dirty=false;_status="已保存并应用："+GeneTreeStorage.Folder;});
        private void Try(Action action){try{action();}catch(Exception ex){_status=ex.Message;Debug.LogWarning("[基因树] "+ex.Message);}}
        private static GUIStyle Centered(float zoom)=>new GUIStyle(GUI.skin.box){alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=Mathf.Max(6,Mathf.RoundToInt(14*zoom)),padding=new RectOffset(2,2,2,2)};
        private static float Distance(Vector2 p,Vector2 a,Vector2 b){var v=b-a;float t=v.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude):0;return Vector2.Distance(p,a+v*t);}
        private static void Line(Vector2 a,Vector2 b,Color color,float width)
        {var matrix=GUI.matrix;var old=GUI.color;GUI.color=color;GUIUtility.RotateAroundPivot(Vector2.SignedAngle(Vector2.right,b-a),a);GUI.DrawTexture(new Rect(a.x,a.y-width*.5f,(b-a).magnitude,width),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=old;}
        private static void DashedLine(Vector2 a,Vector2 b,Color color,float width)
        {
            float length=Vector2.Distance(a,b);var direction=(b-a).normalized;
            for(float at=0;at<length;at+=16)Line(a+direction*at,a+direction*Mathf.Min(at+9,length),color,width);
        }
    }
}
