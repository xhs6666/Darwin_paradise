using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：实体操控、改名、紧凑布局和捕食收支的实际播放回归。</summary>
    public static class EntityControlRegression
    {
        private const BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        private static void Call(object o,string method) => o.GetType().GetMethod(method,Private).Invoke(o,null);
        [MenuItem("Tools/Darwin Park/Validate Entity Controls")]
        public static void Validate()
        {
            // ★ 修复（防Bug回归）：与其他回归入口一致，避免进入播放的域重载丢失检查回调。
            Check(Application.isPlaying,"请先进入播放模式并等待星球生成完成");
            Run();
        }
        private static DarwinEntity Spawn(Vector3 position,bool moving)
        {
            var before=EntityRegistry.All.ToArray(); var genome=new Genome();
            genome.Add(GeneInstance.Create("gene_growth",10,0));
            if(moving)genome.Add(GeneInstance.Create("gene_locomotion",75,0));
            Check(EntityPlacementUtility.Spawn(genome,position),"生成测试实体失败");
            var entity=EntityRegistry.All.First(e=>!before.Contains(e));
            entity.GetComponent<EntityGeneController>().enabled=false;
            entity.GetComponent<EntityVitals>().enabled=false;
            return entity;
        }
        private static float Nutrition(DarwinEntity e)
        {
            var stats=e.GetComponent<EntityStats>(); stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float stored);return food+stored;
        }
        private static void Run()
        {
            var report=new StringBuilder(); var view=UnityEngine.Object.FindFirstObjectByType<EntityViewController>();
            Check(view!=null,"场景未挂载 EntityViewController");
            var builder=UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var clock=ServiceLocator.Get<SimulationClock>(); bool running=clock.IsRunning; float scale=clock.TimeScale;
            var camera=Camera.main; var orbit=camera.GetComponent<OrbitCamera>(); var oldPosition=camera.transform.position;
            var oldRotation=camera.transform.rotation;
            Vector3 up=(camera.transform.position-builder.transform.position).normalized;
            // ★ 适配：陆地跳跃回归必须选择真实陆地，不能把海底跳跃当作通过条件。
            for(int i=0;i<512;i++)
            {
                float lat=1-2f*(i+.5f)/512;float angle=i*2.39996323f;
                var local=new Vector3(Mathf.Sqrt(1-lat*lat)*Mathf.Cos(angle),lat,Mathf.Sqrt(1-lat*lat)*Mathf.Sin(angle));
                if(builder.radius+PlanetTerrainSampler.SampleHeight(builder.Data,local)*builder.heightScale>builder.WaterRadius+4)
                {up=builder.transform.TransformDirection(local);break;}
            }
            Vector3 tangent=Vector3.ProjectOnPlane(Vector3.right,up).normalized;
            var hunter=Spawn(builder.transform.position+up*(builder.WaterRadius+25),true);
            var prey=Spawn(hunter.transform.position+tangent*1.2f,false);
            var genes=hunter.GetComponent<EntityGeneController>();var stats=hunter.GetComponent<EntityStats>();
            var inspector=UnityEngine.Object.FindFirstObjectByType<GeneInspectorUI>();
            try
            {
                clock.Play();clock.SetTimeScale(1);Physics.SyncTransforms();
                // ★ 新增：明确验证先补饱食度，饱食度没满时捕食收入不得流入储存。
                stats.Modify("nutrient",-10);stats.Feed(3);
                stats.TryGet("nutrient",out float fed);stats.TryGet("storedNutrient",out float banked);
                Check(Mathf.Abs(fed-3)<.001f && banked==0,"未优先恢复饱食度");
                stats.Feed(9);stats.TryGet("nutrient",out fed);stats.TryGet("storedNutrient",out banked);
                Check(Mathf.Abs(fed-10)<.001f && Mathf.Abs(banked-2)<.001f,"饱食度填满后的储存顺序错误");
                stats.Modify("storedNutrient",-banked);
                report.AppendLine("PASS: feeding fills satiety first (0+3=3, storage 0); 3+9 fills 10 then stores 2");
                Check(builder.subdivisions>=320 && builder.Data.Vertices.Length>600000,"地表网格密度未提升");
                var climate=builder.GetComponent<PlanetClimateMap>();
                Check(climate.IsReady && climate.ClimateTexture.width>=1024,"气候图未提升到 1024");
                Check(!ShaderUtil.ShaderHasError(builder.TerrainMaterial.shader),"近景细节 Shader 编译失败");
                report.AppendLine("PASS: 320 face subdivisions, >600k welded vertices; climate 1024; surface shader error-free");
                inspector.ShowEntityInfo(hunter);
                var panel=(GameObject)typeof(GeneInspectorUI).GetField("_panel",Private).GetValue(inspector);
                var title=panel.transform.Find("TitleBg");var input=title.Find("NameInput").GetComponent<InputField>();
                Check(title.GetComponent<RectTransform>().rect.height<=38 && title.Find("Title")==null,"标题没有压缩/仍存在信息标签");
                input.onEndEdit.Invoke("  测试·捕食者  ");Check(hunter.DisplayName=="测试·捕食者" && input.text==hunter.DisplayName,"名称输入没有提交");
                input.onEndEdit.Invoke(" ");Check(hunter.DisplayName=="测试·捕食者","空名称未回退");
                var favorites=EntityFavoritesManager.Instance;favorites.ToggleFavorite(hunter);favorites.TogglePanel();
                input.onEndEdit.Invoke("测试·改名");
                var root=ServiceLocator.Get<UIRoot>();
                Check(root.MainCanvas.transform.Find("FavoritesPanel").GetComponentsInChildren<Text>().Any(t=>t.text.Contains(hunter.DisplayName)),"收藏条目未同步改名");
                favorites.TogglePanel();favorites.ToggleFavorite(hunter);
                report.AppendLine("PASS: compact editable title; blank name rejected; renamed favorite label synchronized");
                var control=UnityEngine.Object.FindFirstObjectByType<SimulationControlPanel>();
                typeof(SimulationControlPanel).GetMethod("ToggleParamPopup",Private).Invoke(control,new object[]{"btn_param_temp","温度"});
                var parameter=UnityEngine.Object.FindFirstObjectByType<PlanetParameterEditor>();
                var rect=parameter.GetComponent<RectTransform>().rect;
                Check(rect.width==400 && rect.height==102,"参数面板尺寸未压缩");
                Canvas.ForceUpdateCanvases();
                var fieldRect=RectTransformUtility.CalculateRelativeRectTransformBounds(parameter.transform,parameter.GetComponentInChildren<InputField>().transform);
                var sliderRect=RectTransformUtility.CalculateRelativeRectTransformBounds(parameter.transform,parameter.GetComponentInChildren<Slider>().transform);
                Check(fieldRect.max.x<sliderRect.min.x,"输入框与滑条重叠");
                report.AppendLine("PASS: parameter panel 400x102, input and full-width slider do not overlap");
                typeof(SimulationControlPanel).GetMethod("DestroyMenus",Private).Invoke(control,null);
                view.RegisterClick(hunter,10);Check(view.Target==null,"单击错误进入实体视角");
                view.RegisterClick(hunter,10.2f);Check(view.Target==hunter && orbit.EntityViewActive,"同一实体双击未进入视角");
                Check(!Cursor.visible && Cursor.lockState==CursorLockMode.Locked,"实体视角没有隐藏/锁定鼠标");
                var crosshair=root.MainCanvas.transform.Find("EntityCrosshair");
                Check(crosshair!=null && crosshair.GetComponentsInChildren<Image>().All(i=>!i.raycastTarget),"准星缺失或阻挡射线");
                var possession=view.GetComponent<EntityPossessionInput>();
                Check(possession.attackMouseButton==0 && possession.jumpKey==KeyCode.Space,"左键攻击/空格跳跃绑定错误");
                var manual=hunter.GetComponent<EntityManualControl>();Check(manual.IsControlled && manual.CanMove,"行动基因实体未接管");
                manual.SetInput(tangent,false);Call(manual,"FixedUpdate");
                var body=hunter.GetComponent<Rigidbody>();Check(Vector3.Dot(body.linearVelocity,tangent)>0,"手动移动无速度");
                Vector3 velocity=body.linearVelocity;var action=genes.Genome.Genes.First(g=>g.DefinitionId=="gene_locomotion");
                genes.GetLogic(action).OnUpdate(.1f);Check(Vector3.Distance(velocity,body.linearVelocity)<.001f,"AI 仍在抢写手动速度");
                Call(view,"LateUpdate");Check(Vector3.Dot(camera.transform.forward,(hunter.transform.position-camera.transform.position).normalized)>.95f,"跟随相机没有看向实体");
                var jump=hunter.GetComponent<EntityJumpMotor>();var originalSpawn=body.position;
                Check(builder.GetComponent<MeshCollider>().Raycast(new Ray(builder.transform.position+up*(builder.WaterRadius+100),-up),out var ground,200),"跳跃测试未找到真实地表");
                float support=Vector3.Dot(hunter.GetComponent<Collider>().bounds.extents,new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z)));
                body.position=ground.point+up*(support+.03f);body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                body.position-=up*.3f;Physics.SyncTransforms();
                Check(jump.ResolveGroundContact(),"穿地兜底未修正接触");Physics.SyncTransforms();
                Check(jump.TryJump(manual.CanMove),"着地后无法跳跃");Call(manual,"FixedUpdate");
                Check(Vector3.Dot(body.linearVelocity,up)>0 && !jump.TryJump(manual.CanMove),"跳跃上升被截断或可空中连跳");
                body.linearVelocity=-up;Call(manual,"FixedUpdate");body.position+=up*10;Physics.SyncTransforms();
                Check(!jump.TryJump(manual.CanMove),"未接地仍然可以跳跃");
                body.position=originalSpawn;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                report.AppendLine("PASS: hidden locked cursor; non-blocking crosshair; left attack / Space jump; grounded jump survives movement; no air jump");
                action.Enabled=false;genes.Recalculate();Check(!manual.CanMove,"行动基因失效后仍可操作");
                manual.SetInput(tangent,true);Call(manual,"FixedUpdate");Check(Vector3.ProjectOnPlane(body.linearVelocity,up).magnitude<.01f && !manual.AttackHeld,"失效基因仍可移动/攻击");
                action.Enabled=true;genes.Recalculate();view.ExitView();
                Check(Cursor.visible && Cursor.lockState==CursorLockMode.None && !crosshair.gameObject.activeSelf,"退出没有恢复鼠标/隐藏准星");
                Check(!manual.IsControlled && !orbit.EntityViewActive && Vector3.Dot(camera.transform.forward,(hunter.transform.position-camera.transform.position).normalized)>.99f,"退出没有对准当前实体/释放输入");
                Check(view.EnterView(prey),"不能观察无行动基因实体");
                var observer=prey.GetComponent<EntityManualControl>();Check(!observer.CanMove,"无行动基因实体获得控制");view.ExitView();
                report.AppendLine("PASS: same-entity double-click; manual motion; AI yields; gene loss gates input; observation-only entity; exit restores orbit");
                view.EnterView(hunter);manual=hunter.GetComponent<EntityManualControl>();
                stats.Modify("nutrient",-10);float beforeNutrition=Nutrition(hunter);
                // ★ 适配：手动攻击现在必须瞄准目标，不能依赖附近实体的自动选择。
                manual.SetAim(new Ray(hunter.transform.position-tangent*5,tangent));
                manual.SetInput(Vector3.zero,true);genes.GetLogic(action).OnUpdate(.1f);
                prey.GetComponent<EntityStats>().TryGet("health",out float hp);Check(hp<100,"手动攻击没有造成伤害");
                stats.TryGet("nutrient",out fed);Check(Mathf.Abs(fed-10)<.001f,"真实捕食未先填满饱食度");
                var vitals=hunter.GetComponent<EntityVitals>();
                body.linearVelocity=tangent*genes.GetAttribute("moveSpeed");
                for(int i=0;i<10;i++)typeof(EntityVitals).GetMethod("AdvanceVitals",Private).Invoke(vitals,new object[]{.1f});
                float net=Nutrition(hunter)-beforeNutrition;Check(net>0,"一次正常攻击后的营养收益不足以覆盖 1 秒代谢");
                float beforeKill=Nutrition(hunter);prey.GetComponent<EntityStats>().Modify("health",.5f-hp,false);
                genes.GetLogic(action).OnUpdate(1.1f);
                var settings=LocomotionSettings.Load();
                Check(Mathf.Abs(Nutrition(hunter)-beforeKill-(.5f*settings.biteNutrition+settings.killNutrition))<.001f,"过量伤害刷营养/溢出收益丢失");
                stats.TryGet("storedNutrient",out float stored);Check(stored>0,"进食溢出没有进入储存");
                report.AppendLine($"PASS: manual attack and cooldown; bite minus 1s running metabolism net +{net:F2}; overkill rewards actual damage; overflow stored");
                clock.Pause();body.linearVelocity=Vector3.zero;manual.SetInput(tangent,true);Call(manual,"FixedUpdate");Check(body.linearVelocity==Vector3.zero,"暂停时仍可手动移动");
                clock.Play();stats.Modify("health",-100,false);Call(view,"Update");Check(view.Target==null && !orbit.EntityViewActive && !manual.IsControlled,"实体死亡未退出控制");
                report.AppendLine("PASS: pause blocks movement; death exits entity view and releases control");
            }
            finally
            {
                view.ExitView();camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                hunter.gameObject.SetActive(false);prey.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(hunter.gameObject);UnityEngine.Object.Destroy(prey.gameObject);
                clock.SetTimeScale(scale>0?scale:1);if(running)clock.Play();else clock.Pause();
            }
            Directory.CreateDirectory("Artifacts/EntityControlReview");
            File.WriteAllText("Artifacts/EntityControlReview/runtime-checks.txt",report.ToString());
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/EntityControlReview/compact-ui.png"));
            Debug.Log("[EntityControlReview] "+report);
        }
    }
}
