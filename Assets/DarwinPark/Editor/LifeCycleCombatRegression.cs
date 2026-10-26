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
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：真实跟随镜头瞄准、短点击消费、阶段变异/寿终、静止无性繁殖与面板开关的播放验收。</summary>
    public static class LifeCycleCombatRegression
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private static void Call(object o,string method)=>o.GetType().GetMethod(method,Private).Invoke(o,null);
        private static string Key(Genome genome)=>string.Join("|",genome.Genes.Select(g=>g.InstanceId+":"+g.Value));
        private static DarwinEntity Spawn(Vector3 position,float action=-1,bool reproduction=false)
        {
            var before=EntityRegistry.All.ToArray();var genome=new Genome();genome.Add(GeneInstance.Create("gene_growth",50,0));
            if(action>=0)genome.Add(GeneInstance.Create("gene_locomotion",action,0));
            if(reproduction)genome.Add(GeneInstance.Create("gene_reproduction",20,0));
            Check(EntityPlacementUtility.Spawn(genome,position),"测试实体无法生成");var entity=EntityRegistry.All.First(x=>!before.Contains(x));
            entity.Genes.mutationChance=0;entity.Genes.enabled=false;
            entity.GetComponent<EntityVitals>().enabled=false;entity.GetComponent<EntityEnvironmentHazard>().enabled=false;
            entity.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.None;
            entity.GetComponent<EntityLifeCycle>().InitializeBirth();return entity;
        }
        [MenuItem("Tools/Darwin Park/Validate Lifecycle and Combat")]
        public static void Validate()
        {
            Check(Application.isPlaying,"先播放并等待星球生成");
            var report=new StringBuilder();var clock=ServiceLocator.Get<SimulationClock>();var view=EntityViewController.Instance;
            bool running=clock.IsRunning;float scale=clock.TimeScale;var initial=EntityRegistry.All.ToArray();var random=UnityEngine.Random.state;
            var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();var oldPos=camera.transform.position;var oldRot=camera.transform.rotation;float oldDistance=orbit.distance;
            var orbitState=new System.Collections.Generic.Dictionary<string,object>();
            foreach(string field in new[]{"x","y","targetDistance","_isFocusing","targetPosition","targetRotation"})orbitState[field]=typeof(OrbitCamera).GetField(field,Private).GetValue(orbit);
            var control=UnityEngine.Object.FindFirstObjectByType<SimulationControlPanel>();var inspector=UnityEngine.Object.FindFirstObjectByType<GeneInspectorUI>();
            try
            {
                clock.Play();clock.SetTimeScale(1);UnityEngine.Random.InitState(42626);
                var settings=GeneMutationSettings.CreateDefault();Check(settings.ErrorWeight==.9f && settings.DuplicateWeight==.05f && settings.DeletionWeight==.05f,"变异配置不是90/5/5");
                int error=0,duplicate=0,deletion=0;
                settings.AdaptationGeneId="gene_environment";settings.AdaptationMutationChance=1;settings.AdaptationTargetValue=80;
                for(int i=0;i<20000;i++)
                {
                    var genome=new Genome();genome.Add(GeneInstance.Create("gene_repair",50,0));
                    var result=GeneMutator.Mutate(genome,settings);
                    if(result.Type==MutationType.Error)error++;else if(result.Type==MutationType.Duplicate)duplicate++;else if(result.Type==MutationType.Deletion)deletion++;else Check(false,"压力产生了未允许的插入类型");
                }
                Check(Mathf.Abs(error/20000f-.9f)<.01f && Mathf.Abs(duplicate/20000f-.05f)<.005f && Mathf.Abs(deletion/20000f-.05f)<.005f,"变异类型采样偏离概率");
                report.AppendLine($"PASS: 20000 mutation samples including adaptation pressure: error {error}, duplicate {duplicate}, deletion {deletion}; no insertions");
                var builder=UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();Vector3 up=Vector3.up;float ground=builder.WaterRadius+5;
                for(int i=0;i<512;i++)
                {
                    float y=1-2f*(i+.5f)/512,angle=i*2.39996323f;var d=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));
                    float radius=builder.radius+PlanetTerrainSampler.SampleHeight(builder.Data,d)*builder.heightScale;
                    if(radius>builder.WaterRadius+3){up=builder.transform.TransformDirection(d);ground=radius;break;}
                }
                var pos=builder.transform.position+up*(ground+4);var tangent=Vector3.Cross(up,Vector3.right).normalized;
                var actor=Spawn(pos,50);Check(view.EnterView(actor),"无法进入真实实体视角");Call(view,"LateUpdate");
                Check(Vector3.Distance(camera.transform.position,actor.transform.position)<1,"第一人称镜头仍在实体后方");
                Check(actor.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"第一人称自身模型没有隐藏");
                var forward=(Vector3)typeof(EntityViewController).GetField("_forward",Private).GetValue(view);
                var target=Spawn(pos+forward*1.3f);Physics.SyncTransforms();Call(view,"LateUpdate");
                var manual=actor.GetComponent<EntityManualControl>();
                Check(manual.GetAttackTarget()==target,"真实第一人称镜头的准星无法选取近身目标");
                manual.QueueAttack();manual.SetInput(Vector3.zero,false);
                var action=actor.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_locomotion");
                actor.Genes.GetLogic(action).OnUpdate(.1f);target.GetComponent<EntityStats>().TryGet("health",out float hitHp);
                Check(hitHp<100,"非捕食状态/短促点击无法主动攻击");
                actor.Genes.GetLogic(action).OnUpdate(.1f);target.GetComponent<EntityStats>().TryGet("health",out float afterCooldown);Check(afterCooldown==hitHp,"单次点击重复攻击或忽略冷却");
                // ★ 修复（防Bug回归）：冷却中点击须保留到冷却结束，失焦/退出则不能保留。
                manual.QueueAttack();actor.Genes.GetLogic(action).OnUpdate(.01f);
                target.GetComponent<EntityStats>().TryGet("health",out float duringCooldown);
                Check(duringCooldown==hitHp,"冷却期间造成了额外伤害");
                actor.Genes.GetLogic(action).OnUpdate(LocomotionSettings.Load().attackInterval);
                target.GetComponent<EntityStats>().TryGet("health",out float queuedHp);
                Check(queuedHp<hitHp,"冷却中的短点击没有在冷却结束后执行");
                manual.QueueAttack();view.ExitView();Check(!manual.ConsumeAttack(),"退出仍残留攻击请求");
                report.AppendLine("PASS: locked first-person camera centre aim acquires nearby target; locomotion value 50 attacks via queued short click after release; cooldown and exit clear pending input");
                var aged=Spawn(pos+tangent*20);var genes=aged.Genes;var life=aged.GetComponent<EntityLifeCycle>();
                // 已出生的测试体明确保证成年/老年必抽变异，普通时间推进应没有任何抽签。
                genes.mutationChance=1;string before=Key(genes.Genome);int checks=genes.StageMutationChecks;
                genes.AdvanceSimulation(10);Check(genes.StageMutationChecks==checks && Key(genes.Genome)==before,"阶段内定时推进仍触发变异");
                typeof(EntityLifeCycle).GetField("<Progress>k__BackingField",Private).SetValue(life,29.99f);genes.AdvanceSimulation(1);
                Check(genes.CurrentAge==AgeState.Adult && genes.StageMutationChecks==checks+1,"成年未抽一次变异");
                before=Key(genes.Genome);genes.TryStageMutation(AgeState.Adult);Check(genes.StageMutationChecks==checks+1 && Key(genes.Genome)==before,"同一阶段重复抽变异");
                typeof(EntityLifeCycle).GetField("<Progress>k__BackingField",Private).SetValue(life,79.99f);genes.AdvanceSimulation(1);
                Check(genes.CurrentAge==AgeState.Elder && genes.StageMutationChecks==checks+2,"老年未抽一次变异");
                foreach(var gene in genes.Genome.Genes)gene.Enabled=false;genes.Recalculate();
                typeof(EntityLifeCycle).GetField("<Progress>k__BackingField",Private).SetValue(life,99.99f);genes.AdvanceSimulation(1);aged.GetComponent<EntityStats>().TryGet("health",out float deadHp);
                Check(life.HasDied && deadHp==0 && !aged.gameObject.activeSelf,"生长失效后的老年寿命耗尽仍不死亡");
                report.AppendLine("PASS: no mutation within stage; one check at birth/adult/elder; repeated stage call ignored; disabled growth cannot prevent terminal death (health zero/inactive)");
                var parent=Spawn(pos+tangent*35,-1,true);parent.Genes.CurrentAge=AgeState.Adult;parent.Genes.Recalculate();
                var repGene=parent.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_reproduction");var rep=(ReproductionGeneLogic)parent.Genes.GetLogic(repGene);
                Check(Mathf.Abs(rep.CalculateGestationTime(.01f)-70)<.01f && Mathf.Abs(rep.CalculateGestationTime(100)-300)<.01f,"孕育没有限制7～30天");
                var parentStats=parent.GetComponent<EntityStats>();parentStats.Modify("storedNutrient",100);int oldChecks=parent.Genes.StageMutationChecks;
                rep.OnUpdate(.1f);Check(parent.Genes.IsGestating,"无性繁殖未开始");
                float duration=(float)typeof(ReproductionGeneLogic).GetField("_gestationTimer",Private).GetValue(rep);
                var preBirth=EntityRegistry.All.ToArray();float inherited=parent.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_growth").Value;
                rep.OnUpdate(duration-.1f);Check(EntityRegistry.All.Count==preBirth.Length,"孕育结束前提前出生");rep.OnUpdate(.2f);
                var child=EntityRegistry.All.Single(e=>!preBirth.Contains(e));child.Genes.mutationChance=0;
                var delta=Vector3.ProjectOnPlane(child.transform.position-parent.transform.position,up);
                Check(delta.magnitude>1.7f && delta.magnitude<8.5f,"无行动亲本幼体没有周围散布");
                Check(child.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_growth").Value==inherited && parent.Genes.StageMutationChecks==oldChecks,"出生重写遗传值或生产触发阶段外变异");
                var radial=(child.transform.position-builder.transform.position).normalized;float outside=builder.GetComponent<MeshCollider>().bounds.extents.magnitude+10;
                Check(builder.GetComponent<MeshCollider>().Raycast(new Ray(builder.transform.position+radial*outside,-radial),out var surface,outside),"幼体脚下没有真实地表");
                Check(Vector3.Dot(child.transform.position-surface.point,radial)>.5f,"幼体出生在地形内部");
                report.AppendLine($"PASS: gestation clamps 70..300 simulated seconds (7..30 days); stationary asexual parent births after full timer; child scattered {delta.magnitude:F2}m and above actual terrain; inherited gene retained; no parental mutation on delivery");
                typeof(SimulationControlPanel).GetMethod("ToggleParamPopup",Private).Invoke(control,new object[]{"btn_param_temp","温度"});
                var editor=UnityEngine.Object.FindFirstObjectByType<PlanetParameterEditor>();Check(editor!=null,"参数未打开");
                var button=control.GetComponentsInChildren<Button>(true).First(b=>b.name=="btn_param_temp");button.onClick.Invoke();
                Check(typeof(SimulationControlPanel).GetField("_activeParamMenu",Private).GetValue(control)==null,"再次点参数未关闭");
                inspector.ShowEntityInfo(parent);Call(inspector,"ShowAddGeneMenu");Call(inspector,"ShowAddGeneMenu");
                var add=ServiceLocator.Get<UIRoot>().MainCanvas.transform.Find("AddGenePanel");Check(!add.gameObject.activeSelf,"再次点添加未关闭");
                var prefab=control.EntityPrefab;var tool=EntityPlacementTool.Instance;tool.BeginPlacement(prefab,null);tool.BeginPlacement(prefab,null);Check(!tool.IsPlacing,"再次点生成未关闭");
                report.AppendLine("PASS: parameter / add-gene / placement buttons toggle closed on second activation; shared cancel paths release state");
                Directory.CreateDirectory("Artifacts/LifecycleCombatReview");File.WriteAllText("Artifacts/LifecycleCombatReview/runtime-checks.txt",report.ToString());Debug.Log("[LifecycleCombatReview] "+report);
            }
            finally
            {
                view.ExitView();EntityPlacementTool.Instance?.CancelPlacement();inspector.HideEntityInfo();Call(control,"DestroyMenus");
                foreach(var entity in EntityRegistry.All.ToArray())if(!initial.Contains(entity)){entity.gameObject.SetActive(false);UnityEngine.Object.Destroy(entity.gameObject);}
                camera.transform.SetPositionAndRotation(oldPos,oldRot);orbit.distance=oldDistance;
                foreach(var pair in orbitState)typeof(OrbitCamera).GetField(pair.Key,Private).SetValue(orbit,pair.Value);
                UnityEngine.Random.state=random;clock.SetTimeScale(scale>0?scale:1);if(running)clock.Play();else clock.Pause();
            }
        }
    }
}
