using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：用超过引擎上限的真实网格、视频解码循环和设置按钮验证原始报错，不只断言代码选项。
    [InitializeOnLoad] public static class TerrainVideoSettingsRegression
    {
        private const string Key="DarwinPark.TerrainVideoSettings";
        private static bool _started;
        private static readonly List<string> Passed=new List<string>(),Problems=new List<string>();
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string Folder=>Path.GetFullPath("Artifacts/TerrainVideoSettingsReview");
        static TerrainVideoSettingsRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch()
        {
            Application.runInBackground=true;EditorApplication.isPaused=false;SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));game.Show();EditorApplication.EnterPlaymode();
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllText(Path.Combine(Folder,"progress.txt"),string.Join("\n",Passed));}
        private static object Field(object value,string name)=>value.GetType().GetField(name,Private).GetValue(value);
        private static void OnLog(string message,string stack,LogType type)
        {
            if(message.Contains("Fast Midphase")||message.Contains("WindowsVideoMedia")||message.Contains("Unreadable Track")||message.Contains("[主界面背景] 视频播放失败"))Problems.Add(message);
        }
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");Application.logMessageReceived+=OnLog;
            string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}
                if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            Application.logMessageReceived-=OnLog;
            if(Problems.Count>0)error=(error??"")+"\n"+string.Join("\n",Problems);
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));
            SessionState.SetBool(Key,false);Debug.Log("[TerrainVideoSettings] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static PlanetTerrainData Sphere(int sub,float radius)
        {
            int n=(sub+1)*(sub+1);var data=new PlanetTerrainData{Subdivisions=sub,Radius=radius,HeightScale=1,SeaLevel=0,Vertices=new Vector3[n*6],Triangles=new int[sub*sub*36],FaceHeights=new float[6][]};
            var normals=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};int triangle=0;
            for(int face=0;face<6;face++)
            {
                var normal=normals[face];var a=new Vector3(normal.y,normal.z,normal.x);var b=Vector3.Cross(normal,a);data.FaceHeights[face]=new float[n];
                for(int y=0;y<=sub;y++)for(int x=0;x<=sub;x++)data.Vertices[face*n+y*(sub+1)+x]=(normal+a*(2f*x/sub-1)+b*(2f*y/sub-1)).normalized*radius;
                for(int y=0;y<sub;y++)for(int x=0;x<sub;x++)
                {
                    int i=face*n+y*(sub+1)+x,j=i+sub+1;
                    data.Triangles[triangle++]=i;data.Triangles[triangle++]=i+1;data.Triangles[triangle++]=j;
                    data.Triangles[triangle++]=i+1;data.Triangles[triangle++]=j+1;data.Triangles[triangle++]=j;
                }
            }
            return data;
        }
        private static Rect ScreenRect(RectTransform rect)
        {var c=new Vector3[4];rect.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
        private static IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;}
        private static IEnumerator Run()
        {
            yield return null;yield return null;
            var menu=Object.FindFirstObjectByType<GameMainMenu>();var session=Object.FindFirstObjectByType<WorldSessionManager>();var clock=ServiceLocator.Get<SimulationClock>();
            var background=menu.GetComponent<MainMenuBackground>();var video=menu.GetComponent<VideoPlayer>();double start=EditorApplication.timeSinceStartup;
            while(video==null||!video.isPrepared||video.frame<1){if(EditorApplication.timeSinceStartup-start>20)throw new TimeoutException("菜单视频未输出首帧");yield return null;video=menu.GetComponent<VideoPlayer>();}
            Check(video.url.EndsWith(".webm")&&video.width==1280&&video.height==720,"1280×720 VP8视频实际解码输出");
            int loops=0;video.loopPointReached+=_=>loops++;start=EditorApplication.timeSinceStartup;
            while(loops<2){if(EditorApplication.timeSinceStartup-start>35)throw new TimeoutException("视频循环超时");yield return null;}
            Check(Problems.Count==0,"菜单视频完整循环两次无WindowsVideoMedia错误");
            for(int i=0;i<3;i++){background.enabled=false;yield return null;Check(!video.isPlaying,"停用背景暂停解码 "+i);background.enabled=true;yield return null;yield return null;Check(video.isPlaying,"重新启用背景恢复播放 "+i);}

            var testObject=new GameObject("超高精度碰撞验证");var terrain=testObject.AddComponent<PlanetTerrainMeshBuilder>();terrain.enabled=false;terrain.radius=20;terrain.heightScale=1;testObject.AddComponent<MeshCollider>();testObject.transform.position=new Vector3(1000,0,0);
            var collider=testObject.GetComponent<MeshCollider>();var original=collider.cookingOptions;
            terrain.ApplyGeneratedData(Sphere(8,20));Check((collider.cookingOptions&MeshColliderCookingOptions.UseFastMidphase)!=0,"普通网格使用快速碰撞");
            var dense=Sphere(420,20);int triangles=dense.Triangles.Length/3;Check(triangles>PlanetTerrainMeshBuilder.FastMidphaseTriangleLimit,"真实构造超上限网格："+triangles+"三角形");terrain.ApplyGeneratedData(dense);Physics.SyncTransforms();
            Check((collider.cookingOptions&MeshColliderCookingOptions.UseFastMidphase)==0&&collider.sharedMesh.triangles.Length/3==triangles,"超大网格保留精度并自动切换兼容碰撞");
            Check((collider.cookingOptions&~MeshColliderCookingOptions.UseFastMidphase)==(original&~MeshColliderCookingOptions.UseFastMidphase),"碰撞清理及焊接选项保持原值");
            foreach(var direction in new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back,new Vector3(1,1,1).normalized,new Vector3(-1,1,-1).normalized})
            {
                Check(collider.Raycast(new Ray(testObject.transform.position+direction*30,-direction),out var hit,50)&&Mathf.Abs(Vector3.Distance(hit.point,testObject.transform.position)-20)<.05f,"大网格实际地面射线命中 "+direction);
            }
            terrain.ApplyGeneratedData(Sphere(8,20));Check((collider.cookingOptions&MeshColliderCookingOptions.UseFastMidphase)!=0,"高精度切回普通网格可恢复快速结构");terrain.useFastMidphase=false;terrain.ApplyGeneratedData(Sphere(8,20));Check((collider.cookingOptions&MeshColliderCookingOptions.UseFastMidphase)==0,"Inspector手动禁用快速结构仍受尊重");
            var mesh=collider.sharedMesh;Object.DestroyImmediate(testObject);Object.DestroyImmediate(mesh);dense=null;GC.Collect();

            var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();builder.subdivisions=16;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(builder));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("设置快捷入口回归",11012026,.5f,20,44);start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>90)throw new TimeoutException("测试世界加载超时");yield return null;}
            Check(session.Current!=null&&!menu.Visible&&!video.isPlaying,"进入真实世界暂停菜单视频");
            var root=ServiceLocator.Get<UIRoot>();var control=Object.FindFirstObjectByType<SimulationControlPanel>();var shortcut=root.MainCanvas.transform.Find("BottomPanel/TabBar/SettingsShortcut").GetComponent<Button>();
            var page=typeof(SimulationControlPanel).GetMethod("SwitchPage",Private);var scaler=root.MainCanvas.GetComponent<CanvasScaler>();var oldMode=scaler.uiScaleMode;float oldScale=scaler.scaleFactor;scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            foreach(float scale in new[]{.7f,1f,1.4f})
            {
                scaler.scaleFactor=scale;yield return null;Canvas.ForceUpdateCanvases();var settingsRect=ScreenRect(shortcut.GetComponent<RectTransform>());var nextRect=ScreenRect((RectTransform)root.MainCanvas.transform.Find("BottomPanel/TabBar/ui_icon_next"));
                Check(settingsRect.xMin>nextRect.xMax&&settingsRect.xMax<=Screen.width,"左侧分页与设置入口不重叠 scale="+scale);
            }
            scaler.uiScaleMode=oldMode;scaler.scaleFactor=oldScale;
            foreach(int p in new[]{0,1})
            {
                page.Invoke(control,new object[]{p});clock.Play();yield return null;shortcut.onClick.Invoke();
                Check(menu.Visible&&(string)Field(menu,"_page")=="settings"&&GameInputGate.Blocked&&!clock.IsRunning&&!root.MainCanvas.enabled,"第"+(p+1)+"页快捷设置正确暂停并拦截底层输入");
                yield return Capture("settings-from-page"+(p+1));typeof(GameMainMenu).GetMethod("ReturnFromSettings",Private).Invoke(menu,null);
                Check(!menu.Visible&&clock.IsRunning&&root.MainCanvas.enabled&&!GameInputGate.Blocked,"快捷设置返回原运行状态和HUD 第"+(p+1)+"页");
            }
            clock.Pause();shortcut.onClick.Invoke();typeof(GameMainMenu).GetMethod("ReturnFromSettings",Private).Invoke(menu,null);Check(!clock.IsRunning&&!menu.Visible,"原先暂停时返回仍保持暂停");
            WorldAuthority.CanEdit=false;shortcut.onClick.Invoke();Check(menu.Visible&&(string)Field(menu,"_page")=="settings","普通成员可打开本地音量/画面设置");typeof(GameMainMenu).GetMethod("ReturnFromSettings",Private).Invoke(menu,null);WorldAuthority.CanEdit=true;
            GameInputGate.Blocked=true;shortcut.onClick.Invoke();Check(!menu.Visible,"其他覆盖UI打开时不叠加快捷设置");GameInputGate.Blocked=false;
            page.Invoke(control,new object[]{0});yield return Capture("shortcut-left-tabs");menu.ShowPause();menu.ReturnToMainMenu();yield return null;yield return null;Check(video.isPlaying,"返回主界面恢复同一视频实例");
            // ★ 适配：先移除声明RequireComponent的菜单，才能实际销毁背景组件并验收释放过程。
            var menuHost=menu.gameObject;Object.Destroy(menu);yield return null;Object.Destroy(background);yield return null;yield return null;
            Check(menuHost.GetComponent<VideoPlayer>()==null&&Problems.Count==0,"销毁背景释放解码器和输出，无Flush错误");
        }
    }
}
