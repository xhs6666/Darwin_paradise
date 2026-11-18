using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：在真正 OnGUI 上下文中执行菜单事件，不再用 GUI 外的 Ignore 事件冒充右键验收。
    public sealed class MenuContextHarness:EditorWindow
    {
        public GameMainMenu Owner;public int Paints;public string Failure;public Action Pending;
        private void OnGUI()
        {
            if(Owner==null)return;
            try
            {
                if(Pending!=null&&Event.current.type==EventType.Repaint){var action=Pending;Pending=null;action();return;}
                typeof(GameMainMenu).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Owner,null);
                if(Event.current.type==EventType.Repaint)Paints++;
            }
            catch(Exception error){Failure=(error.InnerException??error).ToString();}
        }
        public void Mouse(Vector2 screen,int button)
        {
            var previous=Event.current;
            try
            {
                Event.current=new Event{type=EventType.MouseDown,button=button,mousePosition=GUIUtility.ScreenToGUIPoint(screen)};
                if(Event.current.rawType!=EventType.MouseDown)throw new InvalidOperationException("GUI context did not expose raw MouseDown");
                typeof(GameMainMenu).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Owner,null);
            }
            finally{Event.current=previous;}
        }
    }
    [InitializeOnLoad]public static class MenuPoleRegression
    {
        private const string Key="DarwinPark.MenuPole";private static bool started;private static readonly List<string> Passed=new();
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string Folder=>Path.GetFullPath("Artifacts/MenuPoleReview");
        static MenuPoleRegression(){EditorApplication.update+=()=>{if(!started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Passed.Add(message);}
        private static object Get(object owner,string name)=>owner.GetType().GetField(name,Private).GetValue(owner);
        private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Private).SetValue(owner,value);
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Paint(MenuContextHarness window)
        {
            int previous=window.Paints;double deadline=EditorApplication.timeSinceStartup+15;
            while(window.Paints<=previous){if(window.Failure!=null)throw new InvalidOperationException(window.Failure);if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("IMGUI repaint");window.Repaint();yield return null;}
        }
        private static IEnumerator Click(MenuContextHarness window,Vector2 screen)
        {
            bool done=false;window.Pending=()=>{window.Mouse(screen,1);done=true;};double deadline=EditorApplication.timeSinceStartup+15;
            while(!done){if(window.Failure!=null)throw new InvalidOperationException(window.Failure);if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("IMGUI click");window.Repaint();yield return null;}
        }
        private static IEnumerator Run()
        {
            yield return null;CameraChecks();var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.enabled=false;
            var source=WorldSaveStore.Read("Artifacts/WaterHazardReview/world-input.json");var entries=new List<WorldSaveEntry>();
            string fixture=Path.Combine(Folder,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
            for(int i=0;i<10;i++){string path=Path.Combine(fixture,i+".json");File.Copy("Artifacts/WaterHazardReview/world-input.json",path);entries.Add(new WorldSaveEntry{path=path,name="存档点击测试 "+i,data=source});}
            Set(menu,"_saves",entries);Set(menu,"_page","saves");Set(menu,"_visible",true);Set(menu,"_scroll",Vector2.zero);
            var window=ScriptableObject.CreateInstance<MenuContextHarness>();window.Owner=menu;window.position=new Rect(30,30,Mathf.Max(900,Screen.width+20),Mathf.Max(700,Screen.height+20));window.Show();
            yield return Paint(window);
            var rects=(Dictionary<string,Rect>)Get(menu,"_saveCardRects");Check(rects.Count==10,"真实 Repaint 缓存每张存档卡片区域");
            for(int part=0;part<3;part++)
            {
                Set(menu,"_page","saves");Set(menu,"_scroll",Vector2.zero);yield return Paint(window);var card=rects[entries[0].path];
                float y=part==0?card.yMin+12:part==1?card.yMin+38:card.yMax-18;
                yield return Click(window,new Vector2(card.center.x,y));
                Check((string)Get(menu,"_page")=="savedetails"&&((WorldSaveDetailsPage)Get(menu,"_saveDetails")).Entry.path==entries[0].path,"真实 GUI 右键打开详情："+new[]{"标题","元数据","进入按钮"}[part]);
                Check(!Object.FindFirstObjectByType<WorldSessionManager>().Busy,"右键详情不触发进入世界");
            }
            Set(menu,"_page","saves");Set(menu,"_scroll",new Vector2(0,250));yield return Paint(window);
            var viewport=(Rect)Get(menu,"_saveHitViewport");var visible=entries.First(e=>rects[e.path].yMin>viewport.yMin+2&&rects[e.path].yMax<viewport.yMax-2);
            yield return Click(window,rects[visible.path].center);Check((string)Get(menu,"_page")=="savedetails"&&((WorldSaveDetailsPage)Get(menu,"_saveDetails")).Entry.path==visible.path,"滚动后右键打开当前可见卡片对应的详情");
            Set(menu,"_page","saves");yield return Paint(window);var hidden=entries.Last(e=>!viewport.Contains(rects[e.path].center));yield return Click(window,rects[hidden.path].center);Check((string)Get(menu,"_page")=="saves","裁切区域外的卡片不能被右键命中");
            yield return Click(window,viewport.min-Vector2.one*20);Check((string)Get(menu,"_page")=="saves","菜单背景右键不打开任何存档");
            window.Close();
        }
        private static void CameraChecks()
        {
            var center=new GameObject("PoleOrbitCenter");center.transform.position=new Vector3(20,-3,7);var camera=new GameObject("PoleOrbitCamera");var orbit=camera.AddComponent<OrbitCamera>();orbit.enabled=false;orbit.target=center.transform;
            void Pose(float pitch){var rotation=Quaternion.Euler(pitch,0,0);orbit.RestoreView(center.transform.position+rotation*Vector3.back*800,rotation,800);}
            Pose(80);orbit.RotateOrbit(0,20);var relative=(camera.transform.position-center.transform.position).normalized;
            Check(relative.y>.9f&&relative.z>0,"世界拖动可越过北极，不再卡在85度");
            orbit.RotateOrbit(0,-20);Check(Quaternion.Angle(camera.transform.rotation,Quaternion.Euler(80,0,0))<.001f,"越过北极后反向拖动可原路返回");
            Pose(-80);orbit.RotateOrbit(0,-20);relative=(camera.transform.position-center.transform.position).normalized;Check(relative.y<-.9f&&relative.z>0,"世界拖动可越过南极");
            Pose(90);var position=camera.transform.position;orbit.RotateOrbit(15,0);Check(Vector3.Distance(position,camera.transform.position)>1,"恰在极点时横向拖动仍产生位移");
            Pose(0);for(int i=0;i<720;i++){position=camera.transform.position;orbit.RotateOrbit(0,1);if(Vector3.Distance(position,camera.transform.position)<1)throw new InvalidOperationException("连续过极点停滞于步 "+i);}
            Check(Quaternion.Angle(camera.transform.rotation,Quaternion.identity)<.01f&&Mathf.Abs(Vector3.Distance(camera.transform.position,center.transform.position)-800)<.01f,"连续720次旋转跨越南北极，距离稳定且两圈回到原姿态 angle="+Quaternion.Angle(camera.transform.rotation,Quaternion.identity)+" radius="+Vector3.Distance(camera.transform.position,center.transform.position));
            Pose(90);var saved=camera.transform.rotation;bool blocked=GameInputGate.Blocked;GameInputGate.Blocked=false;typeof(OrbitCamera).GetMethod("LateUpdate",Private).Invoke(orbit,null);GameInputGate.Blocked=blocked;
            Check(Quaternion.Angle(saved,camera.transform.rotation)<.001f,"极点读档姿态在下一帧不被欧拉重建改变");
            orbit.ReturnToEntity(center.transform.position+Vector3.up*320);position=camera.transform.position;orbit.RotateOrbit(5,5);Check(Vector3.Distance(position,camera.transform.position)>1,"聚焦极点实体后仍能拖离");
            Object.Destroy(camera);Object.Destroy(center);
        }
    }
}
