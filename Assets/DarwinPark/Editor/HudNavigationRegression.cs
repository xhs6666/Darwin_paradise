using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：用实际 UGUI 事件、磁盘模板和摄像机下一帧位置验证底栏交互，不操作用户的世界或桌面。
    [InitializeOnLoad]public static class HudNavigationRegression
    {
        private const string Key="DarwinPark.HudNavigation";private static bool started;private static readonly List<string> Passed=new();
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string Folder=>Path.GetFullPath("Artifacts/HudNavigationReview");
        static HudNavigationRegression(){EditorApplication.update+=()=>{if(!started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Passed.Add(message);}
        private static void Page(SimulationControlPanel panel,int page)=>typeof(SimulationControlPanel).GetMethod("SwitchPage",Private).Invoke(panel,new object[]{page});
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Run()
        {
            var world=Object.FindFirstObjectByType<WorldSessionManager>();var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            typeof(WorldSessionManager).GetMethod("EnsureInitialized",Private).Invoke(world,null);planet.subdivisions=48;
            typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(world,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            NaturalPopulationPace.Current.enabled=false;world.Create("HUD navigation fixture",78031,.5f,20,44);
            double timeout=EditorApplication.timeSinceStartup+120;while(world.Busy){if(EditorApplication.timeSinceStartup>timeout)throw new TimeoutException("world load");yield return null;}
            ServiceLocator.Get<SimulationClock>().Pause();yield return null;var panel=Object.FindFirstObjectByType<SimulationControlPanel>();Page(panel,1);yield return null;yield return null;
            var root=ServiceLocator.Get<UIRoot>();var slot=root.MainCanvas.GetComponentsInChildren<EntitySpawnerUI>().First(s=>s.CustomGenome==null);
            slot.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});Check(PlacementQuantityPopup.Visible,"实际槽位右键打开数量弹窗");
            Page(panel,0);Check(!PlacementQuantityPopup.Visible,"切换第一页立即关闭根 Canvas 数量弹窗");yield return null;
            Page(panel,1);slot.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});slot.enabled=false;Check(!PlacementQuantityPopup.Visible,"来源槽位禁用立即关闭数量弹窗");slot.enabled=true;
            PlacementQuantityPopup.Show(slot);var reopened=Object.FindFirstObjectByType<PlacementQuantityPopup>();typeof(PlacementQuantityPopup).GetField("_source",Private).SetValue(reopened,null);yield return null;
            Check(!PlacementQuantityPopup.Visible,"脚本重载后失去来源的旧弹窗自动关闭");
            var entity=EntitySpawnCapture.Create(GeneMutator.CreateBaseGenome(),planet.transform.position+Vector3.up*(planet.radius+planet.heightScale),"HUD template fixture "+Guid.NewGuid().ToString("N"));int population=EntityRegistry.All.Count;
            EntityTemplateManager.SaveTemplate(entity);yield return null;
            var template=root.MainCanvas.GetComponentsInChildren<EntitySpawnerUI>().First(s=>s.TemplateName==entity.DisplayName);var delete=template.transform.Find("DeleteTemplate").GetComponent<Button>();delete.onClick.Invoke();
            Check(PlacementQuantityPopup.Visible&&EntityTemplateStore.Load().Any(t=>t.templateName==entity.DisplayName),"模板右上角按钮打开删除确认，确认前磁盘仍有模板");
            PlacementQuantityPopup.CloseAll();Check(EntityTemplateManager.Templates.Any(t=>t.templateName==entity.DisplayName),"取消删除保留模板");
            // 文件占用导致写入失败时，磁盘及内存均保留原模板。
            bool failed=false;using(var held=new FileStream(EntityTemplateStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read))try{EntityTemplateManager.DeleteTemplate(entity.DisplayName);}catch(IOException){failed=true;}
            Check(failed&&EntityTemplateManager.Templates.Any(t=>t.templateName==entity.DisplayName)&&EntityTemplateStore.Load().Any(t=>t.templateName==entity.DisplayName),"删除写盘失败不提前移除内存或磁盘模板");
            delete.onClick.Invoke();Object.FindFirstObjectByType<PlacementQuantityPopup>().Apply();yield return null;
            Check(!PlacementQuantityPopup.Visible&&!EntityTemplateManager.Templates.Any(t=>t.templateName==entity.DisplayName)&&!EntityTemplateStore.Load().Any(t=>t.templateName==entity.DisplayName),"确认删除写盘并立即刷新槽位和数量窗口");
            Check(EntityRegistry.All.Count==population&&entity!=null,"删除模板不删除已生成实体");
            Page(panel,0);yield return null;Canvas.ForceUpdateCanvases();var globe=Object.FindFirstObjectByType<PlanetGlobeMinimap>();globe.RefreshNow();
            var raw=globe.GetComponentInChildren<RawImage>();var texture=(Texture2D)raw.texture;
            Check(texture!=null&&texture.GetPixels32().Any(c=>c.a==0)&&texture.GetPixels32().Where(c=>c.a>0).Select(c=>c.r+"/"+c.g+"/"+c.b).Distinct().Count()>10,"球形图有透明圆外区域及真实地表多色数据");
            File.WriteAllBytes(Path.Combine(Folder,"globe.png"),texture.EncodeToPNG());
            var pageRect=(RectTransform)root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page1");var corners=new Vector3[4];pageRect.GetWorldCorners(corners);float pageRight=corners[2].x;((RectTransform)globe.transform).GetWorldCorners(corners);
            Check(corners[0].x>pageRight,"第一页给最右侧小地图保留独立空间，不覆盖旧卡片");
            var orbit=Camera.main.GetComponent<OrbitCamera>();planet.transform.SetPositionAndRotation(new Vector3(13,-7,22),Quaternion.Euler(17,65,9));
            var direction=new Vector3(.3f,.2f,-1).normalized;var rotation=Quaternion.LookRotation(-direction,Vector3.up);orbit.RestoreView(planet.transform.position+direction*700,rotation,700);
            Vector3 before=orbit.transform.position;float distance=Vector3.Distance(before,planet.transform.position);
            var center=RectTransformUtility.WorldToScreenPoint(null,raw.rectTransform.TransformPoint(raw.rectTransform.rect.center));var edge=RectTransformUtility.WorldToScreenPoint(null,raw.rectTransform.TransformPoint(raw.rectTransform.rect.center+new Vector2(raw.rectTransform.rect.width*.22f,raw.rectTransform.rect.height*.12f)));
            var pointer=new PointerEventData(EventSystem.current){position=center,button=PointerEventData.InputButton.Left};
            ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.beginDragHandler);pointer.position=edge;ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.dragHandler);
            Check(globe.Dragging&&OrbitCamera.IsDraggingUI&&Vector3.Distance(before,orbit.transform.position)>1,"实际 UGUI 拖动事件即时改变主摄像机位置");
            Check(Mathf.Abs(Vector3.Distance(orbit.transform.position,planet.transform.position)-distance)<.01f,"小地图导航保留缩放距离并使用移动后的星球中心");
            var moved=orbit.transform.position;yield return null;Check(Vector3.Distance(moved,orbit.transform.position)<.01f,"摄像机缓存同步，下一帧不跳回原处");
            ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.endDragHandler);Check(!globe.Dragging&&!OrbitCamera.IsDraggingUI,"松开小地图释放世界拖动锁");
            foreach(float pole in new[]{90f,-90f})
            {
                var pose=Quaternion.Euler(pole,17,25);orbit.RestoreView(planet.transform.position+pose*Vector3.back*700,pose,700);
                pointer.position=center;ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.beginDragHandler);
                pointer.position=edge;ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.dragHandler);var poleMoved=orbit.transform.rotation;
                Check(Quaternion.Angle(pose,poleMoved)>1,"小地图在极点仍能拖动："+pole);
                pointer.position=center;ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.dragHandler);Check(Quaternion.Angle(pose,orbit.transform.rotation)<.01f,"小地图极点反向拖回不被限位或重建姿态："+pole);
                ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.endDragHandler);
            }
            pointer.position=center;ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(raw.gameObject,pointer,ExecuteEvents.beginDragHandler);Page(panel,1);
            Check(!globe.gameObject.activeInHierarchy&&!OrbitCamera.IsDraggingUI,"切到第二页隐藏小地图并结束未完成拖动");
            Page(panel,0);yield return null;Check(globe.gameObject.activeInHierarchy,"回到第一页恢复小地图");
        }
    }
}
