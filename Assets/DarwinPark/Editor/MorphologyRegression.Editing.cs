using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Simulation;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：真实工厂、编辑入口及保存链路回归，覆盖大脑职责、批生产和交互状态隔离。
    public static partial class MorphologyRegression
    {
        private static IEnumerator FeatureChecks(PlanetTerrainMeshBuilder planet,Vector3 point)
        {
            Check(GeneDatabase.Get("gene_nutrition").States.Select(s=>s.StateId).SequenceEqual(new[]{"chemo","photo","hetero"}),"基础营养恢复矿石、阳光、异养方向，不注册器官或重复生产");
            var plantGenome=GeneMutator.CreateBaseGenome();Check(!plantGenome.HasDefinition("gene_nervous_system")&&!EcologyDefaults.Template("producer").CreateGenome().HasDefinition("gene_nervous_system"),"默认非行动实体和植物模板均不附带中枢");
            foreach(var template in EcologyDefaults.Current.templates)
            {var ids=new HashSet<string>(template.genes.Select(g=>g.id));Check(template.genes.All(g=>GeneDatabase.Get(g.id).RequiresGenes.All(ids.Contains)),"模板前置完整："+template.id);}
            // 位置随当前星球姿态选日照面，不能用此前物理测试推进前的局部法线冒充实时阳光。
            var sunny=planet.Data.Vertices.Where(v=>v.magnitude>planet.WaterRadius+4).OrderByDescending(v=>Vector3.Dot(planet.transform.TransformDirection(v.normalized),SunSystem.LightDirection)).First();point=planet.transform.TransformPoint(sunny)+planet.transform.TransformDirection(sunny.normalized);
            var plant=EntitySpawnCapture.Create(plantGenome,point,"植物与任务编辑测试");plant.GetComponent<Rigidbody>().isKinematic=true;yield return null;Refresh(plant);
            Check(StationaryCohortKernel.Supported(plant.Genes),"拆出的阳光自养重新进入静止批量结算");
            var env=plant.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>();var vitals=plant.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>();env.enabled=vitals.enabled=false;plant.Genes.ApplyEnvironmentPressure(0,0,"gene_environment",50);Refresh(plant);
            EntityStateEditor.SetStat(plant,"nutrient",1);EntityStateEditor.SetStat(plant,"storedNutrient",0);var stats=plant.GetComponent<EntityStats>();var produced=StationaryCohortKernel.Sample(plant.Genes,.25f);
            Check(produced.y>0,"代表批结算实际执行阳光生产，植物无需中枢仍积累营养 delta="+produced+" dot="+Vector3.Dot(EcologyQueries.Up(point),SunSystem.LightDirection)+" absorb="+plant.Genes.GetAttribute("absorbEfficiency"));stats.TryGet("nutrient",out float before);SingletonSettlement.Advance(plant.Genes,.25f);stats.TryGet("nutrient",out float after);Check(after>before,"孤立植物的批结算同样保留实际营养收入");
            var inertBrain=Add(plantGenome,"gene_nervous_system");Refresh(plant);Check(StationaryCohortKernel.Supported(plant.Genes),"没有行动表达的空中枢不阻断植物批结算");plantGenome.Remove(inertBrain.InstanceId);Refresh(plant);
            var mineral=Add(plantGenome,"gene_mineral_autotrophy");Refresh(plant);Check(!StationaryCohortKernel.Supported(plant.Genes),"具有有限矿物消耗的实体仍禁止共享生产");plantGenome.Remove(mineral.InstanceId);Refresh(plant);
            var twin=EntitySpawnCapture.Create(Genome.Copy(plantGenome),point+Vector3.up*2,"共享模型对照");twin.GetComponent<Rigidbody>().isKinematic=true;yield return null;twin.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;twin.Genes.ApplyEnvironmentPressure(0,0,"gene_environment",50);Refresh(twin);Refresh(plant);
            var meshField=typeof(EntityBodyView).GetField("_mesh",Private);Check(meshField.GetValue(plant.GetComponent<EntityBodyView>())==meshField.GetValue(twin.GetComponent<EntityBodyView>()),"两个相同表达实体共享合并网格");
            var popup=EntityEditPopup.Ensure();popup.Open(plant);var part=new PartOverride{slotId="body",scale=new Vector3(1.3f,.7f,1),position=new Vector3(.1f,0,0),rotation=new Vector3(0,20,0)};
            Check(popup.ApplyPart(part),"原实体弹窗通过权威入口修改部位形状、位置与旋转");Refresh(plant);Check(meshField.GetValue(plant.GetComponent<EntityBodyView>())!=meshField.GetValue(twin.GetComponent<EntityBodyView>())&&twin.Genes.Genome.BodyOverrides.Count==0,"编辑个体单独分配网格，对照实体的形态保持不变");
            var bad=new PartOverride{slotId="body",scale=new Vector3(float.NaN,1,1)};string stable=JsonUtility.ToJson(plantGenome);Check(!popup.ApplyPart(bad)&&JsonUtility.ToJson(plantGenome)==stable,"非法部位编辑整项拒绝，不留下部分参数");
            EntityTemplateManager.SaveTemplate(plant);var copied=EntityTemplateManager.Templates.First(t=>t.templateName==plant.DisplayName).ToGenome();Check(copied.BodyOverrides.Single().position==part.position&&copied.BodyOverrides.Single().rotation==part.rotation,"模板往返保留新增部位偏移与朝向");
            var tasks=new EntityActivityPlanSave{tasks=new List<EntityActivityTask>{new(){kind=EntityActivityKind.Feed},new(){kind=EntityActivityKind.Rest}}};Check(popup.ApplyTasks(tasks),"原实体弹窗通过权威入口应用补给和休息顺序计划");var plan=plant.GetComponent<EntityActivityPlan>();Check(!StationaryCohortKernel.Supported(plant.Genes),"具有独立任务的植物不能共享其他实体的任务进度");
            Check(EntityStateEditor.SetStat(plant,"nutrient",10),"小数饱食度仍允许填写恰好上限");plan.Advance(.1f);Check(plan.State.tasks.Count==1&&plan.State.tasks[0].kind==EntityActivityKind.Rest,"补给满足真实饱食条件后继续下一项休息");plan.Advance(.5f);float remaining=plan.State.tasks[0].remaining;plan.Advance(0);Check(plan.State.tasks[0].remaining==remaining,"暂停时休息任务不消耗计时");
            var restored=EcologySaveState.Capture(plant);EntityActivityPlan.Ensure(twin).Restore(restored.activityPlan);Check(twin.GetComponent<EntityActivityPlan>().State.tasks[0].remaining==remaining,"生态保存恢复保留任务顺序和执行进度");
            Check(!popup.ApplyTasks(new EntityActivityPlanSave{tasks=new(){new(){kind=(EntityActivityKind)99}}}),"伪造任务类型被权威入口拒绝");WorldAuthority.CanEdit=false;Check(!popup.ApplyPart(part)&&!popup.ApplyTasks(tasks),"只读成员无法更改部位或任务");WorldAuthority.CanEdit=true;
            // 显示完整编辑区域和有内容的队列，截图包含实际IMGUI而非临时拼图。
            var world=Object.FindFirstObjectByType<WorldSessionManager>();world.SaveNow(false);var disk=WorldSaveStore.Read(WorldSaveStore.FileFor(world.Current.id)).entities.First(e=>e.name==plant.DisplayName);
            Check(disk.ecology.activityPlan.tasks.Single().remaining==remaining&&disk.genome.BodyOverrides.Single().position==part.position,"实际世界写盘读取保留任务进度及部位偏移");
            plan.Restore(tasks);popup.Close();popup.Open(plant);yield return null;if(!Application.isBatchMode){yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();System.IO.File.WriteAllBytes(System.IO.Path.Combine(Folder,"body-task-editor.png"),shot.EncodeToPNG());Object.Destroy(shot);typeof(EntityEditPopup).GetField("_editTab",Private).SetValue(popup,1);yield return new WaitForEndOfFrame();shot=ScreenCapture.CaptureScreenshotAsTexture();System.IO.File.WriteAllBytes(System.IO.Path.Combine(Folder,"task-list-editor.png"),shot.EncodeToPNG());Object.Destroy(shot);}popup.Close();
            plan.Restore(new EntityActivityPlanSave{tasks=new(){new(){kind=EntityActivityKind.Rest,started=true,remaining=.1f}}});var scheduler=Object.FindFirstObjectByType<EntityWorkScheduler>();EntityWorkScheduler.Wake(plant.Genes);scheduler.Advance(.001f);plan.Advance(1);scheduler.Advance(.001f);
            var entries=(System.Collections.IDictionary)typeof(EntityWorkScheduler).GetField("_entries",Private).GetValue(scheduler);var entry=entries[plant.Genes];Check((bool)entry.GetType().GetField("supported").GetValue(entry),"最后任务完成后调度重新识别植物批结算资格");
            plan.Restore(new EntityActivityPlanSave());var motion=Add(plantGenome,"gene_locomotion","swimming");Refresh(plant);Check(plant.Genes.GetAttribute("moveSpeed")>0&&plant.Genes.GetAttribute("jumpAbility")>0,"游泳演化方向不屏蔽陆地速度及跳跃属性");
            float swim=plant.Genes.GetAttribute("swimSpeed");motion.StateId="amphibious";Refresh(plant);Check(Mathf.Abs(plant.Genes.GetAttribute("swimSpeed")-swim)<.001f,"两栖演化方向不会抹除同强度下的实际游泳能力");
            var orbit=Camera.main.GetComponent<OrbitCamera>();Vector3 savedPoint=orbit.transform.position;Quaternion savedRotation=orbit.transform.rotation;float savedDistance=orbit.distance;orbit.EntityViewActive=false;var neutral=Quaternion.LookRotation((orbit.target.position-savedPoint).normalized,Vector3.ProjectOnPlane(savedRotation*Vector3.up,(orbit.target.position-savedPoint).normalized));orbit.RestoreView(savedPoint,neutral,savedDistance);
            Check(orbit.RotateLook(35,-20)&&Vector3.Distance(savedPoint,orbit.transform.position)<.0001f&&Quaternion.Angle(neutral,orbit.transform.rotation)>10,"Alt观察接口原地改变方向，不移动轨道位置");orbit.distance=Mathf.Max(orbit.minDistance,orbit.minimumSurfaceRadius)+orbit.autoStraightenAltitude+50;typeof(OrbitCamera).GetField("targetDistance",Private).SetValue(orbit,orbit.distance);for(int i=0;i<180;i++)typeof(OrbitCamera).GetMethod("LateUpdate",Private).Invoke(orbit,null);Check(!orbit.HasLookOffset&&Quaternion.Angle(neutral,orbit.transform.rotation)<.1f,"拉远归正恢复原轨道观察方向");orbit.RestoreView(savedPoint,savedRotation,savedDistance);
            var globe=Object.FindFirstObjectByType<PlanetGlobeMinimap>();globe.RefreshNow();long uploads=globe.AtlasUploads;for(int i=0;i<30;i++)globe.RefreshNow();Check(globe.AtlasUploads==uploads,"静止导航图连续刷新不再上传CPU投影纹理");
            // ★ 新增：运输任务必须真实拾取并交付一次，不能仅测试列表写入或显示文字。
            var founder=EcologyDefaults.Template("ant_founder").CreateGenome();var queenEntity=EntitySpawnCapture.Create(founder,point,"运输回归王后");var queen=AntColonyMember.Ensure(queenEntity.GetComponent<PopulationBehavior>());queen.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));EntityStateEditor.SetStage(queenEntity,AgeState.Adult);
            var workerEntity=EntitySpawnCapture.Create(EcologyDefaults.Template("ant_founder").CreateGenome(),point+Vector3.right,"运输回归工匠");var worker=AntColonyMember.Ensure(workerEntity.GetComponent<PopulationBehavior>());worker.Assign(AntRole.Worker,queen.GroupId);EntityStateEditor.SetStage(workerEntity,AgeState.Adult);workerEntity.GetComponent<EntityStats>().Feed(100);
            var cargo=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),kind=ColonyResourceKind.Corpse,energy=2,scale=Vector3.one,local=EcologyQueries.ToLocal(workerEntity.transform.position)});string cargoId=cargo.State.id;
            EntityActivityPlan.Apply(workerEntity,new EntityActivityPlanSave{tasks=new(){new(){kind=EntityActivityKind.Transport},new(){kind=EntityActivityKind.Rest}}});worker.State.scan=0;AntColonyWorker.Advance(worker,.1f);var transport=workerEntity.GetComponent<EntityActivityPlan>();transport.Advance(.1f);
            Check(worker.State.carriedResource==cargoId&&transport.State.tasks[0].started,"运输任务实际领取尸体，记录运输阶段");workerEntity.transform.position=EcologyQueries.ToWorld(worker.State.deliveryPoint);AntColonyWorker.Advance(worker,.1f);transport.Advance(.1f);
            Check(ColonyResource.Find(cargoId)==null&&transport.State.tasks.Count==1&&transport.State.tasks[0].kind==EntityActivityKind.Rest,"实际交付后只完成一次运输并继续休息");
            foreach(var resource in ColonyResource.All.Where(r=>r.State.group==queen.GroupId).ToArray())resource.Remove();Object.Destroy(workerEntity.gameObject);Object.Destroy(queenEntity.gameObject);
            if(plant!=null)Object.Destroy(plant.gameObject);if(twin!=null)Object.Destroy(twin.gameObject);yield return null;
        }
    }
}
