using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：真实世界中的分工、击杀、运输、能量守恒、王后繁殖和逆序读档回归。
    [InitializeOnLoad]
    public static class AntColonyRegression
    {
        private const string Key="DarwinPark.AntColonyReview";
        private static readonly List<string> Passed=new List<string>();
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string Folder=>Path.GetFullPath("Artifacts/AntColonyReview");
        private static string _owned;private static Vector3 _home;
        static AntColonyRegression(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))EditorApplication.delayCall+=Validate;};}
        public static void RunBatch(){SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        [MenuItem("Tools/Darwin Park/Validate Ant Colony")]
        public static void Validate()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();if(!Application.isPlaying||session==null||session.Current!=null)throw new InvalidOperationException("请从启动菜单运行");
            Directory.CreateDirectory(Folder);Passed.Clear();File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");session.StartCoroutine(Guard(Run()));
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);Passed.Add(message);}
        private static IEnumerator Guard(IEnumerator run)
        {
            while(true)
            {
                bool next;object value=null;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception ex){Debug.LogException(ex);Finish(false,ex.ToString());yield break;}
                if(!next)break;yield return value;
            }
            Finish(true,"");
        }
        private static void Finish(bool passed,string error)
        {
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(passed?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));
            // 只清理本测试使用的独立 GUID 目录内的已知文件。
            if(!string.IsNullOrEmpty(_owned)&&_owned.StartsWith(Path.GetFullPath(GamePaths.Saves)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Guid.TryParseExact(Path.GetFileName(_owned),"N",out _))
            {
                foreach(string file in new[]{"world.json","world.json.bak"}){var path=Path.Combine(_owned,file);if(File.Exists(path))File.Delete(path);}
                if(Directory.Exists(_owned)&&!Directory.EnumerateFileSystemEntries(_owned).Any())Directory.Delete(_owned);
            }
            Debug.Log("[AntColonyReview] "+(passed?"PASS":"FAIL")+": "+Passed.Count+" checks");
            if(SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);EditorApplication.Exit(passed?0:1);}
        }
        private static Vector3 At(float x,float z=0)
        {var up=EcologyQueries.Up(_home);var axis=Vector3.Cross(up,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.Cross(up,Vector3.right).normalized;return EcologyQueries.Surface(_home+axis*x+Vector3.Cross(up,axis)*z);}
        private static DarwinEntity Spawn(Vector3 point,params (string,float)[] definitions)
        {
            var genome=new Genome();foreach(var def in definitions)genome.Add(GeneInstance.Create(def.Item1,def.Item2,GeneDatabase.Get(def.Item1).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"蚁群回归");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;entity.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);entity.Genes.RestoreStageState(AgeState.Adult,3,2);entity.Genes.Recalculate();
            foreach(var component in entity.GetComponents<MonoBehaviour>())
                if(component is EntityGeneController||component is DarwinPark.Entities.Vitals.EntityVitals||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component is TerritoryBehavior||component.GetType().Name=="EntityAIController")component.enabled=false;
            entity.GetComponent<Rigidbody>().isKinematic=true;return entity;
        }
        private static AntColonyMember Member(AntRole role,string group,Vector3 point,bool sexual=false)
        {
            var entity=Spawn(point,("gene_growth",50),("gene_reproduction",sexual?100:20),("gene_nutrition",50),("gene_population",20));
            var member=entity.GetComponent<AntColonyMember>();member.Assign(role,group);entity.DisplayName=AntColonySettings.Current.Rule(role).label;return member;
        }
        private static float Food(DarwinEntity entity){var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out var food);stats.TryGet("storedNutrient",out var reserve);return food+reserve;}
        private static void Fill(DarwinEntity entity,float food,float reserve=0)
        {var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out var a);stats.TryGet("storedNutrient",out var b);stats.Modify("nutrient",food-a,false);stats.Modify("storedNutrient",reserve-b,false);}
        private static void Bite(AntColonyMember soldier,DarwinEntity target)
        {
            var gene=soldier.Genes.Genome.Genes.First(g=>soldier.Genes.GetLogic(g) is LocomotionGeneLogic&&GeneBehaviorAuthority.IsPrimary<LocomotionGeneLogic>(soldier.Genes,g));
            var logic=soldier.Genes.GetLogic(gene);typeof(LocomotionGeneLogic).GetField("_target",Private).SetValue(logic,target);typeof(LocomotionGeneLogic).GetField("_actionCooldown",Private).SetValue(logic,0f);
            Physics.SyncTransforms();typeof(LocomotionGeneLogic).GetMethod("TryBite",Private).Invoke(logic,new object[]{soldier.GetComponent<EntityStats>(),false});
        }
        private static IEnumerator Run()
        {
            yield return null;var session=Object.FindFirstObjectByType<WorldSessionManager>();var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var clock=ServiceLocator.Get<SimulationClock>();
            builder.subdivisions=32;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(builder));PlanetClimateMap.Instance.mapResolution=128;PlanetOreSystem.Instance.mapResolution=64;
            session.Create("蚁群专项回归",78031,.5f,20,44);double start=EditorApplication.timeSinceStartup;
            while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("生成世界超时");yield return null;}
            Check(session.Current!=null,"真实测试世界生成成功");_owned=Path.GetDirectoryName(WorldSaveStore.FileFor(session.Current.id));clock.Pause();
            _home=EcologyQueries.Surface(builder.transform.position+Vector3.up*(builder.radius+builder.heightScale));
            for(int i=0;i<192;i++)
            {float y=1-2*(i+.5f)/192;float angle=i*2.399963f;var dir=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));var point=EcologyQueries.Surface(builder.transform.position+dir*(builder.radius+builder.heightScale));if((point-builder.transform.position).magnitude>builder.WaterRadius+3){_home=point;break;}}
            Check(AntColonySettings.Current.roles.Length==5,"五种蚁群分工从配置加载");
            var normal=Spawn(At(-110),("gene_territory",50));var territory=normal.GetComponent<TerritoryBehavior>();territory.Advance(.1f);
            var rhythm=PatrolRhythm.Ensure(normal.gameObject);rhythm.Restore(new PatrolRhythmSave{active=2});var state=normal.GetComponent<EntityStateController>();state.Advance(.1f);
            Check(state.Current.Behavior==EntityStateBehavior.TerritoryPatrol,"巡逻窗口内产生巡逻状态");territory.Advance(3);state.Advance(.1f);
            Check(!rhythm.Patrolling&&state.Current.Behavior!=EntityStateBehavior.TerritoryPatrol,"巡逻结束进入休息，不能永久刷新巡逻");
            // ★ 新增：附近捕猎者和低饱食都不能绕过普通巡逻的受击反击限制。
            var enemy=Spawn(normal.transform.position+Vector3.right,("gene_locomotion",75));state.Advance(.1f);
            Check(state.Current.Behavior!=EntityStateBehavior.Hunt&&EcologyMotion.Directive(normal.Genes,out _,out var unsolicited,out _)&&unsolicited==null,"普通巡逻发现捕猎者不会主动攻击");
            Fill(normal,0);state.Advance(.1f);Check(EcologyMotion.Directive(normal.Genes,out _,out unsolicited,out _)&&unsolicited==null,"普通巡逻饥饿时也不绕过受击策略主动攻击");
            EntityCombat.Ensure(normal.gameObject).Receive(enemy,1);
            Check(CombatIntent.Destination(normal.Genes,out _,out var retaliate)&&retaliate==enemy&&territory.EvictionRemaining==0,"受击反击符合捕猎条件的攻击者");
            normal.GetComponent<CombatIntent>().Advance(AntColonySettings.Current.retaliationSeconds+1);Check(!CombatIntent.Destination(normal.Genes,out _,out _),"反击记忆按模拟时间过期");normal.gameObject.SetActive(false);enemy.gameObject.SetActive(false);
            string group=Guid.NewGuid().ToString("N");var queen=Member(AntRole.Queen,group,At(0));var soldier=Member(AntRole.Soldier,group,At(5));var worker=Member(AntRole.Worker,group,At(8));
            Check(queen.Population.IsLeader&&queen.Population.SameGroup(worker.Entity),"王后与成员使用同一持久种群标记");
            Check(queen.Genes.GetAttribute("attack")==0&&worker.Genes.GetAttribute("attack")==0&&queen.Genes.GetAttribute("moveSpeed")<worker.Genes.GetAttribute("moveSpeed"),"王后无攻击且减速，工匠战斗属性被抑制");
            foreach(var member in new[]{worker,soldier})
            {
                Check(member.Genes.Evaluation.Genes.Where(g=>g.Definition.LogicScriptId.Contains("Reproduction")).All(g=>!g.IsActive),member.Role+" 繁殖表达被抑制");
                string frozen=JsonUtility.ToJson(member.Genes.Genome);member.Genes.Genome.Genes[0].Value=1;member.Genes.Recalculate();Check(JsonUtility.ToJson(member.Genes.Genome)==frozen,member.Role+" 基因编辑不会改写出生快照");
                member.Genes.mutationChance=1;member.Genes.TryStageMutation(AgeState.Elder);Check(JsonUtility.ToJson(member.Genes.Genome)==frozen,member.Role+" 生命周期变异被禁止");
            }
            Check(soldier.Genes.Evaluation.Genes.Where(g=>g.Definition.Id=="gene_nutrition").All(g=>!g.IsActive),"士兵营养基因被抑制");
            Check(EntityCombat.Ensure(queen.gameObject).Receive(soldier.Entity,20)==0,"最终伤害入口拒绝同群误伤");
            var victim=Spawn(soldier.transform.position+EcologyQueries.Up(soldier.transform.position)*.2f,("gene_growth",50));victim.GetComponent<EntityStats>().Modify("health",-99,false);
            Check(EntityCombat.Ensure(victim.gameObject).Receive(worker.Entity,10)==0,"工匠无法绕过属性抑制直接攻击");
            soldier.Advance(.1f);soldier.Destination(out _,out var acquired,out _);
            Check(acquired==victim&&soldier.Activity==ColonyActivity.Hunt,"士兵自主扫描并追击外群猎物");
            float beforeFood=Food(soldier.Entity);Bite(soldier,victim);var corpse=ColonyResource.All.Single(r=>r.State.kind==ColonyResourceKind.Corpse);
            Check(Food(soldier.Entity)==beforeFood&&corpse.State.energy>0,"真实击杀留下尸体且士兵没有进食奖励");Check(soldier.State.patrolRemaining>0,"击杀后启动围绕王后的圆周巡逻");
            float energy=corpse.State.energy;worker.transform.position=corpse.transform.position;worker.Advance(.1f);
            Check(worker.State.carriedResource==corpse.State.id&&corpse.State.claim==worker.Id,"工匠独占并拾取真实尸体");
            var other=Member(AntRole.Worker,group,worker.transform.position);Check(!corpse.Claim(other),"其他工匠不能重复领取同一尸体");
            worker.transform.position=EcologyQueries.ToWorld(worker.State.deliveryPoint);worker.Advance(.1f);
            var nursery=ColonyResource.All.Single(r=>r.State.kind==ColonyResourceKind.Nursery);
            Check(!ColonyResource.All.Any(r=>r.State.kind==ColonyResourceKind.Corpse)&&Mathf.Abs(nursery.State.energy-energy)<.001f,"运达王后区后尸体只转换一次且能量守恒");
            Check(nursery.GetComponent<Light>()!=null&&nursery.GetComponent<Renderer>()!=null,"王后能量块有黄色发光外观");
            Fill(soldier.Entity,1);ColonyResource.Feed(soldier,1);Check(Food(soldier.Entity)==1,"成年士兵不能吸收育幼区能量块");
            Fill(queen.Entity,0);ColonyResource.Feed(queen,1);Check(Food(queen.Entity)>0,"饥饿王后自动吸收附近能量块");
            var young=Member(AntRole.Soldier,group,queen.transform.position);young.Genes.CurrentAge=AgeState.Infant;Fill(young.Entity,0);ColonyResource.Feed(young,.1f);Check(Food(young.Entity)>0,"幼年实体与王后共享育幼补给");
            var plant=Spawn(worker.transform.position,("gene_growth",50));float plantEnergy=ColonyResource.Biomass(plant);Fill(worker.Entity,10,100);
            worker.State.livingTarget=EcologyIdentity.Of(plant);worker.State.scan=1;worker.Advance(.1f);Check(worker.State.carriedLiving==EcologyIdentity.Of(plant)&&!plant.GetComponent<Rigidbody>().detectCollisions,"工匠可搬运无法移动的外群活实体");
            worker.State.deliveryNursery=false;worker.State.deliveryPoint=queen.State.supplyPoints[0];worker.transform.position=EcologyQueries.ToWorld(worker.State.deliveryPoint);worker.Advance(.1f);
            var supply=ColonyResource.All.Single(r=>r.State.kind==ColonyResourceKind.Supply);Check(Mathf.Abs(supply.State.energy-plantEnergy)<.001f,"群内补给点转换活实体并保留能量");
            soldier.transform.position=At(-4);Fill(soldier.Entity,0);soldier.Destination(out var feedingPoint,out var feedingTarget,out _);
            Check(feedingTarget==null&&Vector3.Distance(feedingPoint,supply.transform.position)<.001f&&soldier.Activity==ColonyActivity.Feed,"饥饿士兵会实际前往共享补给点");
            soldier.transform.position=supply.transform.position;Fill(soldier.Entity,0);ColonyResource.Feed(soldier,1);Check(Food(soldier.Entity)>0,"普通成年成员可吸收群内补给");
            // ★ 适配：覆盖繁忙运输、追猎、王后离开旧位置及模板实际落盘，避免只用瞬时 Feed 调用验收。
            ColonyFeedingTemplateRegression.Verify(queen,worker,soldier,young,nursery,supply);
            var cargo=Spawn(worker.transform.position,("gene_growth",50));worker.State.livingTarget=EcologyIdentity.Of(cargo);worker.State.scan=1;worker.Advance(.1f);
            Check(worker.State.carriedLiving==EcologyIdentity.Of(cargo),"搬运中断前建立独占活体任务");worker.Suspend();
            Check(cargo.GetComponent<Rigidbody>().detectCollisions&&string.IsNullOrEmpty(worker.State.carriedLiving),"搬运中断恢复活体碰撞并释放任务");cargo.gameObject.SetActive(false);
            var successor=Member(AntRole.Reproductive,group,At(2));successor.Advance(.1f);Check(successor.Role==AntRole.Queen&&successor.GroupId!=group&&successor.State.founding,"繁殖个体成年后成为独立种群王后");
            Check(EcologyQueries.Distance(queen.transform.position,EcologyQueries.ToWorld(successor.State.foundingPoint))>queen.Radius,"新王后离开原种群范围建群");
            successor.transform.position=EcologyQueries.ToWorld(successor.State.foundingPoint);successor.Advance(.1f);Check(!successor.State.founding&&successor.State.supplyPoints.Count==AntColonySettings.Current.supplyPointCount,"新王后到达后生成自己的随机补给点");
            for(int i=0;i<AntColonySettings.Current.maximumReproductives;i++){var reproductive=Member(AntRole.Reproductive,group,At(3+i));reproductive.Genes.CurrentAge=AgeState.Infant;}
            Check(Enumerable.Range(0,100).All(_=>queen.ChooseBirthRole()!=AntRole.Reproductive),"繁殖个体达到配置上限后不再随机增加");
            Fill(queen.Entity,10,100);AntColonyReproduction.Advance(queen,.1f);Check(queen.Genes.IsGestating&&queen.State.reproductionTimer>0,"王后有营养时开始持续繁殖流程");
            int beforeBirth=EntityRegistry.All.Count;AntColonyReproduction.Advance(queen,queen.State.reproductionTimer+1);
            Check(EntityRegistry.All.Count==beforeBirth+1,"王后实际生产后代");var child=EntityRegistry.All.Last();Check(child.GetComponent<AntColonyMember>()?.Role!=AntRole.None&&child.GetComponent<PopulationBehavior>().GroupId==group,"新生后代立即获得固定分工和同群标记");
            AntColonyReproduction.Advance(queen,AntColonySettings.Current.queenCooldownSeconds+1);queen.State.reproductionCooldown=0;AntColonyReproduction.Advance(queen,.1f);Check(queen.Genes.IsGestating,"王后生产后自动进入下一轮孕育");
            var sexual=Member(AntRole.Queen,Guid.NewGuid().ToString("N"),At(100),true);Fill(sexual.Entity,10,100);AntColonyReproduction.Advance(sexual,.1f);AntColonyReproduction.Advance(sexual,.1f);
            var mates=sexual.Group.Where(m=>m.Role==AntRole.Mate).ToArray();Check(mates.Length==1,"有性王后仅产生一名专用同群配偶");
            Check(mates[0].Genes.Evaluation.Genes.Where(g=>g.Definition.LogicScriptId.Contains("Reproduction")).All(g=>!g.IsActive)&&sexual.Genes.IsGestating&&sexual.State.partnerGenome!=null,"配偶无法自主繁殖，王后保存有性孕育的配偶遗传快照");
            // ★ 新增：卵的孵化暂停行动，但不能清除血缘标记或导致同群误伤。
            var eggMember=Member(AntRole.Worker,group,queen.transform.position);eggMember.Genes.CurrentAge=AgeState.Infant;
            eggMember.gameObject.AddComponent<EggIncubation>().Begin(30);eggMember.Population.Advance(1);
            Check(eggMember.GroupId==group&&queen.Population.SameGroup(eggMember.Entity)&&!AntColonyCombat.CanAttack(soldier.Entity,eggMember.Entity),"孵化期保留固定分工和同群免伤身份");
            var pending=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=group,kind=ColonyResourceKind.Corpse,energy=13,local=EcologyQueries.ToLocal(At(12)),scale=Vector3.one});
            worker.State.resourceTarget=pending.State.id;worker.State.scan=1;worker.transform.position=pending.transform.position;worker.Advance(.1f);Check(worker.State.carriedResource==pending.State.id,"读档前保持正在搬运的任务");
            string workerId=worker.Id,queenId=queen.Id,resourceId=pending.State.id;float timer=queen.State.reproductionTimer;
            // ★ 新增：索引优化必须覆盖正在搬运活体的逆序读档，不能只验证尸体资源 ID。
            var liveCargo=Spawn(other.transform.position,("gene_growth",50));string carrierId=other.Id,liveCargoId=EcologyIdentity.Of(liveCargo);
            other.State.livingTarget=liveCargoId;other.State.scan=1;other.Advance(.1f);
            Check(AntColonyWorker.IsCarried(liveCargo),"读档前登记活体运输索引");
            session.SaveNow(false);var data=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));
            Check(data.colony.resources.Any(r=>r.id==resourceId)&&data.entities.Single(e=>e.ecology.id==workerId).ecology.ant.carriedResource==resourceId,"世界存档实际写入资源、分工与运输关系");
            var invalid=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(data));invalid.colony.resources[0].energy=float.NaN;bool rejected=false;try{WorldSaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"非法能量快照在清场前拒绝");
            data.entities.Reverse();session.Open(data);start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("蚁群读档超时");yield return null;}
            clock.Pause();var loadedWorker=AntColonyMember.All.Single(m=>m.Id==workerId);var loadedQueen=AntColonyMember.All.Single(m=>m.Id==queenId);var loadedResource=ColonyResource.Find(resourceId);
            Check(loadedWorker.Role==AntRole.Worker&&loadedWorker.State.carriedResource==resourceId&&loadedResource.State.claim==workerId,"逆序读档恢复固定分工及尸体独占运输");
            Check(loadedWorker.Queen==loadedQueen&&Mathf.Abs(loadedQueen.State.reproductionTimer-timer)<.001f&&loadedQueen.Genes.IsGestating,"逆序读档保留王后关系和孕育计时");
            Check(loadedWorker.Genes.Evaluation.Genes.Where(g=>g.Definition.LogicScriptId.Contains("Reproduction")).All(g=>!g.IsActive),"读档后工匠仍不可繁殖");
            var loadedCarrier=AntColonyMember.All.Single(m=>m.Id==carrierId);var loadedCargo=EcologyIdentity.Find(liveCargoId);
            Check(loadedCargo!=null&&AntColonyWorker.IsCarried(loadedCargo)&&loadedCargo.GetComponent<Rigidbody>().isKinematic&&!loadedCargo.GetComponent<Rigidbody>().detectCollisions,"逆序读档重建活体运输索引与物理关系");
            AntColonyWorker.Release(loadedCarrier);Check(!AntColonyWorker.IsCarried(loadedCargo)&&loadedCargo.GetComponent<Rigidbody>().detectCollisions,"读档后的活体运输可正常释放并清理索引");
            var bottom=ServiceLocator.Get<UIRoot>().MainCanvas.transform.Find("BottomPanel/ContentArea/Page1/LayerSec");var button=bottom.Find("layer_population").GetComponent<Button>();button.onClick.Invoke();yield return null;yield return null;
            Check(PopulationRangeLayer.Visible&&Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Count(r=>r.name.StartsWith("范围 "))>=2,"实际图层按钮显示不同种群的球面范围");
            var range=Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).First(r=>r.name=="范围 "+group);var tint=new MaterialPropertyBlock();range.GetPropertyBlock(tint);
            Check(tint.GetColor("_BaseColor")==ColonyAppearance.ColorFor(group),"范围颜色实际写入 URP 材质属性而非只设置顶点色");
            var orbit=Camera.main.GetComponent<OrbitCamera>();var focus=loadedQueen.transform.position;var cameraPoint=focus+EcologyQueries.Up(focus)*160;
            orbit.RestoreView(cameraPoint,Quaternion.LookRotation(focus-cameraPoint,Vector3.Cross(EcologyQueries.Up(focus),Vector3.right)),Vector3.Distance(cameraPoint,builder.transform.position));
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"population-ranges.png"));yield return null;yield return null;
            Check(UIIconRegistry.Get("layer_population")!=null,"种群图层图标可加载");
            var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.ShowPause();menu.ReturnToMainMenu();yield return null;
            Check(session.Current==null&&!ColonyResource.All.Any()&&!PopulationRangeLayer.Visible,"返回主界面清理种群资源与图层");
        }
    }
}
