using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using DarwinPark.Genetics;

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
        private Vector2 _listScroll,_graphScroll,_rawScroll,_dragOffset;
        private bool _hierarchy,_dirty,_advanced;
        private readonly GeneValueForm _form=new GeneValueForm();
        private Rect _canvas;
        private readonly Vector2 _nodeSize=new Vector2(170,65);
        public void Open()
        {
            try { _folder=DarwinPark.Core.GamePaths.Mods; _catalog=GeneTreeStorage.ReadCatalog();_doc=GeneTreeStorage.Load();GeneTreeStorage.Validate(_doc,_catalog); _selected=null;_edge=null;_linkFrom=null;_dragId=null;_catalogDrag=null;_dirty=false;Visible=true;_status="配置目录："+GeneTreeStorage.Folder; }
            catch(Exception ex){_status=ex.Message;Debug.LogError(ex);}
        }
        public void Close()=>Visible=false;
        public bool HasUnsavedChanges => _dirty;
        public void Draw(Rect screen,Action close)
        {
            GUI.Box(screen,GUIContent.none);
            GUILayout.BeginArea(new Rect(screen.x+16,screen.y+12,screen.width-32,screen.height-24));
            GUILayout.BeginHorizontal();GUILayout.Label("基因树工具",GUILayout.Width(140));
            if(GUILayout.Button(_hierarchy?"切换流程图":"切换层级列表",GUILayout.Width(150)))_hierarchy=!_hierarchy;
            if(GUILayout.Button("保存并应用",GUILayout.Width(110)))Save();
            if(GUILayout.Button("重新读取",GUILayout.Width(100))){if(!_dirty)Open();else _status="有未保存修改，请先保存，或点放弃修改。";}
            if(GUILayout.Button("放弃修改",GUILayout.Width(100))){_dirty=false;Open();}
            if(GUILayout.Button("返回主界面",GUILayout.Width(120))){if(_dirty)_status="请先保存，或放弃修改后返回。";else close();}
            GUILayout.EndHorizontal();
            GUILayout.Label("左侧拖入 · 拖动节点 · Shift左键源→目标建立下级 · 右键节点/连线编辑 · 红=抑制 / 绿=促进");
            GUILayout.BeginHorizontal();GUILayout.Label("导入文件夹",GUILayout.Width(90));_folder=GUILayout.TextField(_folder);
            if(GUILayout.Button("读取 JSON",GUILayout.Width(110)))Try(()=>{var imported=GeneTreeStorage.ReadCatalog(_folder);foreach(var item in imported)_catalog[item.Key]=item.Value;_dirty=true;_status="已读取 "+imported.Count+" 个基因";});GUILayout.EndHorizontal();
            GUILayout.Label((_dirty?"● 未保存  ":"")+_status);
            GUILayout.EndArea();
            float top=screen.y+160,bottom=screen.height-180;
            var left=new Rect(screen.x+16,top,225,bottom);GUI.Box(left,"基因文件");
            GUILayout.BeginArea(new Rect(left.x+8,left.y+25,left.width-16,left.height-34));_listScroll=GUILayout.BeginScrollView(_listScroll);
            foreach(var id in _catalog.Keys.OrderBy(x=>x))
            {
                GUILayout.Label(id,GUI.skin.button,GUILayout.Height(32));var row=GUILayoutUtility.GetLastRect();
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
                    if(_canvas.Contains(ev.mousePosition))AddNode(_catalogDrag,ev.mousePosition-_canvas.position+_graphScroll);
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
            _graphScroll=GUI.BeginScrollView(_canvas,_graphScroll,new Rect(0,0,3000,2400));
            for(int x=0;x<3000;x+=40)Line(new Vector2(x,0),new Vector2(x,2400),new Color(1,1,1,.04f),1);
            for(int y=0;y<2400;y+=40)Line(new Vector2(0,y),new Vector2(3000,y),new Color(1,1,1,.04f),1);
            foreach(var edge in _doc.edges)
            {
                var from=_doc.nodes.Find(n=>n.id==edge.from);var to=_doc.nodes.Find(n=>n.id==edge.to);if(from==null||to==null)continue;
                Vector2 a,b;
                if(edge.kind=="evolution"){a=from.position+new Vector2(85,65);b=to.position+new Vector2(85,0);}
                else {bool right=to.position.x>=from.position.x;a=from.position+new Vector2(right?170:0,32);b=to.position+new Vector2(right?0:170,32);}
                var color=edge.kind=="suppress"?new Color(1,.3f,.3f):edge.kind=="promote"?new Color(.3f,1,.5f):edge.kind=="error"?new Color(1,.7f,.25f):new Color(.4f,.8f,1);
                Line(a,b,color,3);Vector2 d=(b-a).normalized;Line(b,b-d*14+new Vector2(-d.y,d.x)*7,color,3);Line(b,b-d*14-new Vector2(-d.y,d.x)*7,color,3);
                GUI.Label(new Rect((a+b)*.5f,new Vector2(110,23)),edge.kind+" "+edge.value);
                if(Event.current.type==EventType.MouseDown&&Event.current.button==1&&Distance(Event.current.mousePosition,a,b)<12)
                {try{CommitSelected();_edge=edge;_selected=null;_kind=edge.kind;_value=edge.value.ToString(System.Globalization.CultureInfo.InvariantCulture);}catch(Exception ex){_status=ex.Message;}Event.current.Use();}
            }
            foreach(var node in _doc.nodes)
            {
                var rect=new Rect(node.position,_nodeSize);Color old=GUI.color;GUI.color=node.id==_selected?new Color(.4f,1,.9f):Color.white;
                if(node.error)
                {
                    Vector2 c=rect.center;Line(c+new Vector2(0,-40),c+new Vector2(90,0),Color.yellow,2);Line(c+new Vector2(90,0),c+new Vector2(0,40),Color.yellow,2);Line(c+new Vector2(0,40),c-new Vector2(90,0),Color.yellow,2);Line(c-new Vector2(90,0),c-new Vector2(0,40),Color.yellow,2);GUI.Label(rect,node.id,Centered());
                }
                else
                {
                    GUI.Box(rect,node.id,Centered());
                    // ★ 修复（防Bug回归）：默认 GUI.skin 的暗色 Box 没有清晰外框，显式绘制四边。
                    var border=node.id==_selected?new Color(.3f,1,.85f):new Color(.45f,.65f,.75f);
                    Line(rect.min,new Vector2(rect.xMax,rect.yMin),border,2);Line(new Vector2(rect.xMax,rect.yMin),rect.max,border,2);
                    Line(rect.max,new Vector2(rect.xMin,rect.yMax),border,2);Line(new Vector2(rect.xMin,rect.yMax),rect.min,border,2);
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
                        else{Select(node.id);_dragId=node.id;_dragOffset=ev.mousePosition-node.position;}
                    }
                    ev.Use();
                }
            }
            if(Event.current.type==EventType.MouseDrag&&_dragId!=null){var n=_doc.nodes.Find(n=>n.id==_dragId);n.position=Vector2.Max(Vector2.zero,Event.current.mousePosition-_dragOffset);_dirty=true;Event.current.Use();}
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
            if(GUILayout.Button((_doc.nodes.Find(n=>n.id==id).error?"◇ ":"□ ")+id,GUILayout.Height(30)))Select(id);GUILayout.EndHorizontal();
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
                if(GUILayout.Button("应用基因数值"))Try(()=>{if(!_advanced)_raw=_form.Export();var d=JsonUtility.FromJson<GeneDefinitionJson>(_raw);if(d==null||d.id!=_selected)throw new Exception("id 不能更改");if(!Enum.TryParse<GeneCategory>(d.category,true,out _))throw new Exception("category 无效");_form.Load(_raw);_catalog[_selected]=_raw;_dirty=true;_status="已应用节点配置，请保存";});
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
            CommitSelected();
            GeneTreeStorage.Save(_doc,_catalog);GeneDatabase.Reload();GeneDatabase.EnsureLoaded();foreach(var entity in DarwinPark.Entities.EntityRegistry.All)entity.Genes.Recalculate();_dirty=false;_status="已保存并应用："+GeneTreeStorage.Folder;});
        private void Try(Action action){try{action();}catch(Exception ex){_status=ex.Message;Debug.LogWarning("[基因树] "+ex.Message);}}
        private static GUIStyle Centered()=>new GUIStyle(GUI.skin.box){alignment=TextAnchor.MiddleCenter,wordWrap=true};
        private static float Distance(Vector2 p,Vector2 a,Vector2 b){var v=b-a;float t=v.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude):0;return Vector2.Distance(p,a+v*t);}
        private static void Line(Vector2 a,Vector2 b,Color color,float width)
        {var matrix=GUI.matrix;var old=GUI.color;GUI.color=color;GUIUtility.RotateAroundPivot(Vector2.SignedAngle(Vector2.right,b-a),a);GUI.DrawTexture(new Rect(a.x,a.y-width*.5f,(b-a).magnitude,width),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=old;}
    }
}
