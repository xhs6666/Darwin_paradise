using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：接缝坡度、极近相机与真实指针 ID 投放链路回归，避免只检验网格闭合或虚构指针。</summary>
    public static class SurfaceInteractionRegression
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Darwin Park/Validate Surface Interactions")]
        public static void Validate()
        {
            Check(Application.isPlaying,"请先播放并等待星球生成");
            var control=UnityEngine.Object.FindFirstObjectByType<SimulationControlPanel>();
            typeof(SimulationControlPanel).GetMethod("SwitchPage",Private).Invoke(control,new object[]{1});
            double deadline=EditorApplication.timeSinceStartup+5;
            EditorApplication.CallbackFunction wait=null;
            wait=()=>{
                if(!Application.isPlaying){EditorApplication.update-=wait;return;}
                if(!control.GetComponentsInChildren<EntitySpawnerUI>().Any() && EditorApplication.timeSinceStartup<deadline)return;
                EditorApplication.update-=wait;Run();
            };
            EditorApplication.update+=wait;
        }
        private static void Run()
        {
            var report=new StringBuilder();
            var b=UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var c=Camera.main;var orbit=c.GetComponent<OrbitCamera>();
            var oldPos=c.transform.position;var oldRot=c.transform.rotation;
            var clock=ServiceLocator.Get<SimulationClock>();bool running=clock.IsRunning;float scale=clock.TimeScale;
            var saved=new System.Collections.Generic.Dictionary<string,object>();
            foreach(string field in new[]{"x","y","targetDistance","_isFocusing","targetPosition","targetRotation"})saved[field]=typeof(OrbitCamera).GetField(field,Private).GetValue(orbit);
            float oldDistance=orbit.distance;
            var initial=EntityRegistry.All.ToArray();
            var module=EventSystem.current.currentInputModule as PointerInputModule;PointerEventData cached=null;GameObject previousEnter=null;
            try
            {
                clock.Pause();Check(Mathf.Abs(orbit.minimumSurfaceRadius-b.WaterRadius)<.01f,"海面半径未绑定相机");
                // ★ 修复（防Bug回归）：拓扑闭合并不代表绕序正确，高细分时逐三角校验真实碰撞朝向。
                for(int i=0;i<b.Data.Triangles.Length;i+=3)
                {
                    var a=b.Data.Vertices[b.Data.Triangles[i]];
                    var v=b.Data.Vertices[b.Data.Triangles[i+1]];
                    var w=b.Data.Vertices[b.Data.Triangles[i+2]];
                    Check(Vector3.Dot(Vector3.Cross(v-a,w-a),a+v+w)>0,"地形三角形朝向反转");
                }
                report.AppendLine("PASS: every high-resolution terrain triangle faces outward");
                var keep=typeof(OrbitCamera).GetMethod("KeepAboveSurface",Private);
                for(int i=0;i<96;i++)
                {
                    float y=1-2f*(i+.5f)/96f, phi=i*2.39996323f;
                    var d=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(phi),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(phi));
                    c.transform.SetPositionAndRotation(b.transform.position+d*(b.WaterRadius-8),Quaternion.LookRotation(-d));
                    orbit.distance=orbit.minDistance;keep.Invoke(orbit,null);
                    float radius=Vector3.Distance(c.transform.position,b.transform.position);
                    Check(radius>=b.WaterRadius+orbit.surfaceClearance-.01f,"极近相机进入海面");
                    Check(Vector3.Dot((c.transform.position-b.transform.position).normalized,d)>.999f,"最近限制将镜头翻到另一侧");
                    var screenPoint=new Vector2(c.pixelWidth*.5f,c.pixelHeight*.5f);
                    var ray=c.ScreenPointToRay(screenPoint);
                    bool direct=b.GetComponent<MeshCollider>().Raycast(ray,out var diagnostic,c.farClipPlane);
                    Check(EntityPlacementUtility.TryGetSurface(c,screenPoint,out var point,out _),$"最近视角丢失正面地表拾取 i={i}, radius={radius}, direct={direct}, normalDot={Vector3.Dot(diagnostic.normal,-ray.direction)}, rayDot={Vector3.Dot(ray.direction,-d)}, near={c.nearClipPlane}");
                    Check(Vector3.Dot(point-b.transform.position,d)>0,"拾取落在星球背面");
                    Check(radius>=Vector3.Distance(point,b.transform.position)+orbit.surfaceClearance-.02f,"相机进入山体");
                }
                report.AppendLine("PASS: 96 close-up directions stay above sea/terrain; no hemisphere flip; centre picks visible front surface");
                c.transform.SetPositionAndRotation(oldPos,oldRot);orbit.distance=oldDistance;
                var from=(oldPos-b.transform.position).normalized;
                orbit.FocusOn(b.transform.position-from*(b.WaterRadius+1));
                for(int i=0;i<240;i++)
                {
                    typeof(OrbitCamera).GetMethod("LateUpdate",Private).Invoke(orbit,null);
                    Check(Vector3.Distance(c.transform.position,b.transform.position)>=b.WaterRadius+orbit.surfaceClearance-.01f,"跨星球聚焦穿入海面");
                }
                Check(Vector3.Dot((c.transform.position-b.transform.position).normalized,from)<-.9f,"对侧圆弧聚焦未完成");
                report.AppendLine("PASS: opposite-side focus follows an exterior arc without crossing the planet");
                c.transform.SetPositionAndRotation(oldPos,oldRot);
                foreach(var field in saved)typeof(OrbitCamera).GetField(field.Key,Private).SetValue(orbit,field.Value);orbit.distance=oldDistance;
                Canvas.ForceUpdateCanvases();
                var slot=UnityEngine.Object.FindObjectsByType<EntitySpawnerUI>(FindObjectsSortMode.None).First(s=>s.isActiveAndEnabled && s.CustomGenome==null);
                var screen=new Vector2(c.pixelWidth*.5f,c.pixelHeight*.5f);
                Check(!EntityPlacementUtility.IsPointerOverUI(screen),"世界中心被 UI 覆盖");
                if(module!=null)
                {
                    object[] args={-1,null,true};typeof(PointerInputModule).GetMethod("GetPointerData",Private).Invoke(module,args);
                    cached=(PointerEventData)args[1];previousEnter=cached.pointerEnter;cached.pointerEnter=slot.gameObject;
                    Check(EventSystem.current.IsPointerOverGameObject(-1),"未构造真实鼠标缓存 UI 命中");
                }
                int count=EntityRegistry.All.Count;
                var drag=new PointerEventData(EventSystem.current){position=screen,button=PointerEventData.InputButton.Left,pointerId=-1,pointerEnter=slot.gameObject};
                slot.OnBeginDrag(drag);slot.OnDrag(drag);slot.OnEndDrag(drag);
                Check(EntityRegistry.All.Count==count+1 && !OrbitCamera.IsDraggingUI,"真实鼠标缓存 UI 导致地面松手丢失");
                count=EntityRegistry.All.Count;
                slot.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
                Check(EntityPlacementTool.Instance.TryPlaceAt(screen) && EntityRegistry.All.Count==count+1 && EntityPlacementTool.Instance.IsPlacing,"连续投放失败或意外退出");
                EntityPlacementTool.Instance.CancelPlacement();
                var uiScreen=RectTransformUtility.WorldToScreenPoint(null,slot.transform.position);
                Check(EntityPlacementUtility.IsPointerOverUI(uiScreen),"槽位 UI 边界检测失败");
                count=EntityRegistry.All.Count;
                drag.position=uiScreen;slot.OnBeginDrag(drag);slot.OnEndDrag(drag);
                Check(EntityRegistry.All.Count==count,"松手在 UI 上仍然生成实体");
                report.AppendLine("PASS: pointer -1 with stale slot hover still drops exactly one entity; click confirmation works; UI drop rejects; input released");
                // 合成折痕用于证明跨面处理修正了坡度，而非只把边缘高度焊起来。
                var synthetic=new PlanetTerrainData{Subdivisions=32,FaceHeights=new float[6][]};
                for(int face=0;face<6;face++)
                {
                    synthetic.FaceHeights[face]=new float[33*33];
                    for(int y=0;y<=32;y++)for(int x=0;x<=32;x++)
                        synthetic.FaceHeights[face][y*33+x]=.55f+.01f*PlanetSeamContinuity.Direction(face,x,y,32).y+(x==0||x==32||y==0||y==32?.02f:0);
                }
                float before=PlanetSeamContinuity.MeanCurvature(synthetic);
                PlanetSeamContinuity.Smooth(synthetic,b.erosion);float after=PlanetSeamContinuity.MeanCurvature(synthetic);
                Check(after<before*.5f,"跨面折痕未减弱");
                report.AppendLine($"PASS: seam crease curvature {before:F6} -> {after:F6}; actual mean edge curvature {PlanetSeamContinuity.MeanCurvature(b.Data):F6}");
            }
            finally
            {
                if(cached!=null)cached.pointerEnter=previousEnter;
                EntityPlacementTool.Instance?.CancelPlacement();
                c.transform.SetPositionAndRotation(oldPos,oldRot);orbit.distance=oldDistance;
                foreach(var field in saved)typeof(OrbitCamera).GetField(field.Key,Private).SetValue(orbit,field.Value);
                foreach(var entity in EntityRegistry.All.ToArray())if(!initial.Contains(entity)){entity.gameObject.SetActive(false);UnityEngine.Object.Destroy(entity.gameObject);}
                clock.SetTimeScale(scale);if(running)clock.Play();else clock.Pause();
            }
            Directory.CreateDirectory("Artifacts/SurfaceInteractionReview");
            File.WriteAllText("Artifacts/SurfaceInteractionReview/runtime-checks.txt",report.ToString());
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/SurfaceInteractionReview/planet.png"));
            Debug.Log("[SurfaceInteractionReview] "+report);
        }
    }
}
