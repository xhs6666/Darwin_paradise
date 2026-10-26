using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：参数草稿、连续批量投放、实时滚动面板与准星切换攻击目标的播放验收。</summary>
    public static class InteractionUXRegression
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private static void Call(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
        private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
        [MenuItem("Tools/Darwin Park/Validate Interaction UX")]
        public static void Validate()
        {
            Check(Application.isPlaying,"请先进入播放模式");
            var control=UnityEngine.Object.FindFirstObjectByType<SimulationControlPanel>();
            typeof(SimulationControlPanel).GetMethod("SwitchPage",Private).Invoke(control,new object[]{1});
            EditorApplication.delayCall+=()=>Run(control);
        }
        private static DarwinEntity Spawn(Vector3 position,bool moving)
        {
            var before=EntityRegistry.All.ToArray();var genome=new Genome();
            genome.Add(GeneInstance.Create("gene_growth",10,0));if(moving)genome.Add(GeneInstance.Create("gene_locomotion",75,0));
            EntityPlacementUtility.Spawn(genome,position);var e=EntityRegistry.All.First(x=>!before.Contains(x));
            e.GetComponent<EntityGeneController>().mutationChance=0;e.GetComponent<EntityGeneController>().enabled=false;e.GetComponent<EntityVitals>().enabled=false;e.GetComponent<EntityEnvironmentHazard>().enabled=false;
            e.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.None;return e;
        }
        private static void Run(SimulationControlPanel control)
        {
            var report=new StringBuilder();var clock=ServiceLocator.Get<SimulationClock>();bool running=clock.IsRunning;float scale=clock.TimeScale;
            var initial=EntityRegistry.All.ToArray();var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();
            var oldPos=camera.transform.position;var oldRot=camera.transform.rotation;float oldDistance=orbit.distance;
            var orbitState=new System.Collections.Generic.Dictionary<string,object>();
            foreach(string field in new[]{"x","y","targetDistance","_isFocusing","targetPosition","targetRotation"})orbitState[field]=typeof(OrbitCamera).GetField(field,Private).GetValue(orbit);
            var inspector=UnityEngine.Object.FindFirstObjectByType<GeneInspectorUI>();var tool=EntityPlacementTool.Instance;
            var root=ServiceLocator.Get<UIRoot>();var b=UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            EntitySpawnerUI slot=null;GameObject obstacle=null;int oldCount=1;float oldTemp=ClimateDataHub.BaseTemperature;
            try
            {
                clock.Play();clock.SetTimeScale(1);
                // 参数必须通过实际按钮打开，拖动后的点击不能关闭覆盖层。
                var temp=root.MainCanvas.transform.GetComponentsInChildren<Button>(true).First(x=>x.name=="btn_param_temp");
                temp.onClick.Invoke();var editor=UnityEngine.Object.FindFirstObjectByType<PlanetParameterEditor>();Check(editor!=null,"左键未打开参数");
                var slider=editor.GetComponentInChildren<Slider>();slider.value=23;
                ExecuteEvents.ExecuteHierarchy(slider.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Check(editor.gameObject.activeInHierarchy && Field<GameObject>(control,"_activeParamMenu")!=null,"滑条松手导致参数面板关闭");
                Check(Mathf.Abs(ClimateDataHub.BaseTemperature-oldTemp)<.01f,"草稿未确认就改变环境");
                editor.Apply();Check(Mathf.Abs(ClimateDataHub.BaseTemperature-23)<.01f,"草稿无法确认");editor.CancelRequested();
                report.AppendLine("PASS: parameter button left click opens; slider release keeps draft panel; Apply commits; cancel closes");
                slot=UnityEngine.Object.FindObjectsByType<EntitySpawnerUI>(FindObjectsSortMode.None).First(x=>x.isActiveAndEnabled && x.CustomGenome==null);
                oldCount=slot.placementCount;
                slot.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});
                var quantity=UnityEngine.Object.FindFirstObjectByType<PlacementQuantityPopup>();Check(quantity!=null,"右键未打开数量设置");
                quantity.GetComponentInChildren<InputField>().text="3";quantity.Apply();Check(slot.placementCount==3,"数量未应用到槽位");
                slot.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
                int count=EntityRegistry.All.Count;var centre=new Vector2(camera.pixelWidth*.5f,camera.pixelHeight*.5f);
                Check(tool.TryPlaceAt(centre) && tool.IsPlacing && EntityRegistry.All.Count==count+3,"首次批量投放后丢失模式/数量错误");
                Check(tool.TryPlaceAt(centre) && tool.IsPlacing && EntityRegistry.All.Count==count+6,"第二次连续批量投放失败");
                tool.CancelPlacement();Check(!tool.IsPlacing && !OrbitCamera.IsDraggingUI,"取消没有释放输入");
                report.AppendLine("PASS: slot right click sets count=3; two world clicks spawn 3+3 independent entities and keep placing; cancel releases input");
                // 找到陆地，避免准星测试实体被水中门控阻止行动。
                Vector3 up=Vector3.up;float groundRadius=b.WaterRadius+10;
                for(int i=0;i<512;i++)
                {
                    float y=1-2f*(i+.5f)/512,angle=i*2.39996323f;
                    var d=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));
                    float r=b.radius+PlanetTerrainSampler.SampleHeight(b.Data,d)*b.heightScale;
                    if(r>b.WaterRadius+3){up=b.transform.TransformDirection(d);groundRadius=r;break;}
                }
                var pos=b.transform.position+up*(groundRadius+4);var tangent=Vector3.Cross(up,Vector3.right).normalized;var side=Vector3.Cross(up,tangent);
                var hunter=Spawn(pos,true);var a=Spawn(pos+tangent*1.3f,false);var other=Spawn(pos+side*1.3f,false);Physics.SyncTransforms();
                inspector.ShowEntityInfo(hunter);var genes=hunter.Genes;
                foreach(var def in GeneDatabase.All)if(def.Id!="gene_growth")genes.Genome.Add(GeneInstance.Create(def.Id,50,def.Tier));
                genes.Recalculate();inspector.RefreshDisplayedInfo();Canvas.ForceUpdateCanvases();
                var panel=root.MainCanvas.transform.Find("GenePanel");var viewport=panel.GetComponentInChildren<ScrollRect>();
                Check(panel.GetComponent<RectTransform>().anchorMin==new Vector2(0,1),"信息面板未放左上");
                Check(viewport.vertical && !viewport.horizontal && panel.Find("ScrollView/Viewport/Content/PageCtrl")==null,"基因仍使用分页按钮");
                Check(panel.GetComponentsInChildren<InputField>().Length>=genes.Genome.Genes.Count,"滚动列表没有列出全部基因");
                var baseText=panel.GetComponentsInChildren<Text>().First(x=>x.name=="BaseInfo");hunter.GetComponent<EntityStats>().Modify("health",-12,false);
                var action=genes.Genome.Genes.First(x=>x.DefinitionId=="gene_locomotion");action.Value=84;genes.Recalculate();
                var unchanged=baseText;float scrollPosition=.3f;viewport.verticalNormalizedPosition=scrollPosition;
                inspector.RefreshDisplayedInfo();Check(baseText==unchanged && baseText.text.Contains("88.0"),"健康值未就地刷新");
                Check(panel.GetComponentsInChildren<InputField>().Any(x=>x.text=="84.0") && Mathf.Abs(viewport.verticalNormalizedPosition-scrollPosition)<.01f,"基因数值未刷新或滚动位置重置");
                typeof(GeneInspectorUI).GetMethod("ShowAddGeneMenu",Private).Invoke(inspector,null);Call(inspector,"Update");
                var add=root.MainCanvas.transform.Find("AddGenePanel");Check(add.GetComponent<RectTransform>().anchorMin==Vector2.zero,"添加面板未放左下");
                inspector.HideEntityInfo();Check(!panel.gameObject.activeSelf && !add.gameObject.activeSelf,"关闭信息未同步关闭添加面板");
                report.AppendLine("PASS: top-left info / bottom-left add panel; all genes in vertical scroll; live health/gene values without recreating text or resetting scroll; close hides child panels");
                // 清除面板测试添加的其他基因，保证攻击值仅来自指定行动基因。
                genes.Genome.Genes.RemoveAll(x=>x.DefinitionId!="gene_growth" && x!=action);genes.Recalculate();
                var manual=hunter.gameObject.AddComponent<EntityManualControl>();manual.BeginControl(b.transform);manual.SetInput(Vector3.zero,true);
                var rayA=new Ray(pos-tangent*5,tangent);manual.SetAim(rayA);
                Check(EntityAttackAim.Pick(hunter.gameObject,rayA,200)==a,"准星被自身遮挡或选择了附近非准星目标");
                var aStats=a.GetComponent<EntityStats>();var bStats=other.GetComponent<EntityStats>();
                genes.GetLogic(action).OnUpdate(.1f);aStats.TryGet("health",out float aHp);bStats.TryGet("health",out float bHp);
                Check(aHp<100 && bHp==100,"首次攻击伤及附近其他实体");
                manual.SetAim(new Ray(pos-side*5,side));genes.GetLogic(action).OnUpdate(1.1f);
                aStats.TryGet("health",out float aAfter);bStats.TryGet("health",out float bAfter);
                Check(aAfter==aHp && bAfter<bHp,"攻击目标锁定旧实体，无法按准星切换");
                manual.SetAim(new Ray(pos,up));genes.GetLogic(action).OnUpdate(1.1f);
                bStats.TryGet("health",out float missed);Check(missed==bAfter,"空准星仍自动伤害附近目标");
                obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=pos+tangent*.7f;obstacle.transform.localScale=Vector3.one*.2f;Physics.SyncTransforms();
                Check(EntityAttackAim.Pick(hunter.gameObject,rayA,200)==null,"准星穿过障碍攻击");
                report.AppendLine("PASS: aim ignores own collider; only aimed A damaged; aim switches to B after cooldown; empty aim misses; solid obstacle blocks");
                Directory.CreateDirectory("Artifacts/InteractionUXReview");File.WriteAllText("Artifacts/InteractionUXReview/runtime-checks.txt",report.ToString());Debug.Log("[InteractionUXReview] "+report);
            }
            finally
            {
                tool.CancelPlacement();if(slot!=null)slot.placementCount=oldCount;
                inspector.HideEntityInfo();Call(control,"DestroyMenus");if(obstacle!=null)UnityEngine.Object.Destroy(obstacle);
                foreach(var e in EntityRegistry.All.ToArray())if(!initial.Contains(e)){e.gameObject.SetActive(false);UnityEngine.Object.Destroy(e.gameObject);}
                ClimateDataHub.BaseTemperature=oldTemp;if(ServiceLocator.TryGet<DarwinPark.Environment.EnvironmentSystem>(out var env))env.BaseTemperature=oldTemp;
                camera.transform.SetPositionAndRotation(oldPos,oldRot);orbit.distance=oldDistance;
                foreach(var pair in orbitState)typeof(OrbitCamera).GetField(pair.Key,Private).SetValue(orbit,pair.Value);
                clock.SetTimeScale(scale>0?scale:1);if(running)clock.Play();else clock.Pause();
            }
        }
    }
}
