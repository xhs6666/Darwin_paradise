using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
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
    // ★ 新增：真实新世界、自动70/30、原生界面、读档防重领及物理运输/繁衍，不能仅凭修改权重宣称种群可延续。
    [InitializeOnLoad] public static class ColonyContinuityRegression
    {
        private const string Key="DarwinPark.ColonyContinuity";private static bool _started;
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static readonly List<string> Passed=new();private static string Folder=>Path.GetFullPath("Artifacts/ColonyContinuityReview");
        private static string _externalBackup;private static bool _externalExisted;
        static ColonyContinuityRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);Passed.Add("PASS: "+text);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);Passed.Clear();File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            if(_externalExisted)File.WriteAllText(EcologyDefaults.ConfigurationPath,_externalBackup);else if(File.Exists(EcologyDefaults.ConfigurationPath))File.Delete(EcologyDefaults.ConfigurationPath);
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        public static void RunShortBatch(){SessionState.SetBool(Key+".Short",true);RunBatch();}
        private static IEnumerator Wait(Func<bool> done,string name,float timeout=45)
        {double start=EditorApplication.timeSinceStartup;while(!done()){if(EditorApplication.timeSinceStartup-start>timeout)throw new TimeoutException(name);yield return null;}}
        private static void Clear(){foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);ColonyResource.ClearWorld();}
        private static DarwinEntity Spawn(RandomSpawnRecipe source,Vector3 position,bool adult=true)
        {
            DarwinEntity created=null;void Capture(DarwinEntity entity)=>created=entity;EntityRegistry.OnRegistered+=Capture;
            try{DarwinEntity.Spawner(source.CreateGenome(),position,source.name);}finally{EntityRegistry.OnRegistered-=Capture;}
            if(created==null)throw new InvalidOperationException("测试实体未生成");created.Genes.mutationChance=0;
            created.GetComponent<EntityLifeCycle>().RestoreProgress(adult?LifeCycleSettings.Load().adultProgress:0);created.Genes.RestoreStageState(adult?AgeState.Adult:AgeState.Infant,adult?3:1,adult?2:1);created.Genes.Recalculate();return created;
        }
        private static Vector3 Home(PlanetTerrainMeshBuilder planet)
        {
            for(int i=0;i<512;i++){float y=1-2*(i+.5f)/512,angle=i*2.399963f;var direction=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));var point=EcologyQueries.Surface(planet.transform.position+direction*(planet.radius+planet.heightScale));if((point-planet.transform.position).magnitude>planet.WaterRadius+3)return point;}throw new InvalidOperationException("测试世界没有合适陆地");
        }
        private static IEnumerator Capture(string name){yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;}
        private static IEnumerator Run()
        {
            _externalExisted=File.Exists(EcologyDefaults.ConfigurationPath);if(_externalExisted)_externalBackup=File.ReadAllText(EcologyDefaults.ConfigurationPath);
            string defaults=Resources.Load<TextAsset>("Entities/ecology_defaults").text;Directory.CreateDirectory(Path.GetDirectoryName(EcologyDefaults.ConfigurationPath));File.WriteAllText(EcologyDefaults.ConfigurationPath,defaults);EcologyDefaults.UseSnapshot(defaults);
            var config=EcologyDefaults.Current;Check(config.producerRatio==.7f&&config.mobileRatio==.3f&&config.minimumPopulation==100,"默认70/30和100个体保底来自可外调JSON");
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=24;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("新种群延续验证",742091,.5f,20,44);yield return Wait(()=>!session.Busy,"创建真实世界");var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();GameInputGate.Blocked=false;
            Check(session.Current!=null&&PopulationMaintenance.Rules.Count==2&&PopulationMaintenance.Rules.All(r=>r.enabled),"新世界自动启用两条生态规则");
            var producer=PopulationMaintenance.Rules.Single(r=>r.category==PopulationRuleCategory.Producers);var mobile=PopulationMaintenance.Rules.Single(r=>r.category==PopulationRuleCategory.Mobile);
            clock.Play();yield return Wait(()=>PopulationMaintenance.Count(producer)>=70&&PopulationMaintenance.Count(mobile)>=30&&!Object.FindFirstObjectByType<RandomPlanetSpawner>().IsBusy,"自动补到70/30",90);clock.Pause();
            Check(PopulationMaintenance.Count(producer)==70&&PopulationMaintenance.Count(mobile)==30,"真实自动生成70个静止自养生产者和30个移动消化者");
            var altered=EntityRegistry.All.First(e=>EcologyEnergyBudget.IsProducer(e.Genes));altered.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_repair").Value=61;altered.Genes.Recalculate();yield return null;
            Check(!producer.source.Matches(altered.Genes.Genome)&&PopulationMaintenance.Count(producer)==70,"变异后的生产者仍计入实际能力比例，不依赖精确模板");
            var home=Home(planet);var inert=new RandomSpawnRecipe{template=new Genome()};inert.template.Add(GeneInstance.Create("gene_growth",20,0));var outside=Spawn(inert,home+Vector3.up*2);
            yield return null;Check(PopulationMaintenance.Denominator(producer)==100&&PopulationMaintenance.Total==101&&!PopulationMaintenance.RequiresRefill(producer)&&!PopulationMaintenance.RequiresRefill(mobile),"分区外静止个体不造成70/30无解或无限增生");Object.DestroyImmediate(outside.gameObject);
            // ★ 新增：默认规则持续补充时不能退出玩家模式，使用真正的自动补充和实体视角/放置预览验证。
            var controlled=EntityRegistry.All.First(e=>e.Genes.GetAttribute("moveSpeed")>0);var view=EntityViewController.Instance;Check(view.EnterView(controlled),"可进入实际实体操控用于补充回归");
            Object.DestroyImmediate(EntityRegistry.All.First(e=>EcologyEnergyBudget.IsProducer(e.Genes)).gameObject);clock.Play();
            yield return Wait(()=>PopulationMaintenance.Count(producer)>=70&&!Object.FindFirstObjectByType<RandomPlanetSpawner>().IsBusy,"操控期间自动补充");clock.Pause();
            Check(view.Target==controlled&&controlled.GetComponent<EntityManualControl>().IsControlled,"实际自动补充不会退出实体操控");view.ExitView();
            var placement=EntityPlacementTool.Instance;placement.BeginPlacement(Object.FindFirstObjectByType<SimulationControlPanel>().EntityPrefab,null);Check(placement.IsPlacing,"可进入真实放置预览用于补充回归");
            Object.DestroyImmediate(EntityRegistry.All.First(e=>EcologyEnergyBudget.IsProducer(e.Genes)).gameObject);clock.Play();
            yield return Wait(()=>PopulationMaintenance.Count(producer)>=70&&!Object.FindFirstObjectByType<RandomPlanetSpawner>().IsBusy,"放置期间自动补充");clock.Pause();
            Check(placement.IsPlacing,"实际自动补充不会取消放置预览");placement.CancelPlacement();
            var control=Object.FindFirstObjectByType<SimulationControlPanel>();typeof(SimulationControlPanel).GetMethod("SwitchPage",Private).Invoke(control,new object[]{1});yield return null;var panel=Object.FindFirstObjectByType<RandomSpawnPanel>();var work=RandomSpawnWorkbench.Ensure();work.Open(panel,true);yield return null;
            var root=ServiceLocator.Get<UIRoot>().MainCanvas.transform.Find("RandomSpawnWorkbenchBlocker/RandomSpawnWorkbench");Check(work.MaintenanceSources().Any(s=>s.name.Contains("创始王后"))&&work.MaintenanceSources().Any(s=>s.name.Contains("清道夫")),"可繁衍的生产者、移动者、王后和食腐模板自动列出");
            var extra=new PopulationMaintenanceRule{id=Guid.NewGuid().ToString("N"),ratio=.1f,source=new RandomSpawnRecipe{method=RandomSpawnMethod.Genes,name="保留自定义",included=new(){"gene_growth"}}};Check(PopulationMaintenance.Set(extra),"可保留自定义重叠规则");
            root.GetComponentsInChildren<Button>().Single(b=>b.name=="EcologyDefaults").onClick.Invoke();yield return null;
            Check(PopulationMaintenance.Rules.Count==3&&PopulationMaintenance.Rules.Any(r=>r.id==extra.id),"实际按钮原子应用默认项且保留自定义规则，不重复添加默认行");yield return Capture("default-maintenance");work.Close();
            session.SaveNow(false);var disk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(disk.maintenance.rules.Single(r=>r.id==EcologyDefaults.ProducerRuleId).minimumCount==70&&!string.IsNullOrEmpty(disk.colony.ecologyDefaults),"比例分类、保底和生态配置实际写盘");Check(LanWorldSnapshot.Frame(1).maintenance.rules.Any(r=>r.category==PopulationRuleCategory.Mobile),"联机帧携带生态分类规则");
            var legacy=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(disk));legacy.entities.Clear();legacy.maintenance=null;legacy.colony.resources.Clear();legacy.colony.ecologyDefaults=null;session.Open(legacy);yield return Wait(()=>!session.Busy,"加载旧世界");clock.Pause();
            Check(PopulationMaintenance.Rules.Count==0&&EntityRegistry.All.Count==0,"旧世界没有规则时不偷偷覆盖或启动默认生成");EcologyDefaults.UseSnapshot(defaults);PopulationMaintenance.Restore(EcologyDefaults.CreateRules());Clear();
            var founder=Spawn(EcologyDefaults.Template("ant_founder"),home);var queen=AntColonyMember.Ensure(founder.GetComponent<PopulationBehavior>());queen.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));var stats=founder.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);stats.Modify("nutrient",-food,false);stats.Modify("storedNutrient",-reserve,false);
            queen.Advance(.1f);Check(queen.State.startup?.version==1&&queen.State.startup.foodIssued==config.foundingFood&&ColonyResource.All.Sum(r=>r.State.energy)==config.foundingFood,"只有一只王后的新群可领一次有限育幼储粮");
            for(int i=0;i<200&&!founder.Genes.IsGestating;i++)queen.Advance(.1f);
            Check(founder.Genes.IsGestating&&queen.State.reproductionTimer>0,"创业储粮实际进入储备并支付首次孕育费用");Check(queen.ChooseBirthRole()==AntRole.Worker,"首胎强制补齐工匠而非随机士兵");
            string id=EcologyIdentity.Of(founder);session.SaveNow(false);disk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));float savedResource=disk.colony.resources.Sum(r=>r.energy);var edited=JsonUtility.FromJson<EcologyDefaultsSettings>(defaults);edited.foundingFood=128;File.WriteAllText(EcologyDefaults.ConfigurationPath,JsonUtility.ToJson(edited));
            session.Open(disk);yield return Wait(()=>!session.Busy,"创业进度读档");clock.Pause();queen=EcologyIdentity.Find(id).GetComponent<AntColonyMember>();queen.Advance(.1f);
            Check(EcologyDefaults.Current.foundingFood==64&&queen.State.startup.foodIssued==64&&ColonyResource.All.Sum(r=>r.State.energy)<=savedResource,"读档保留世界配置和已领标志，外部修改不重复发粮");
            var bad=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(disk));bad.entities[0].ecology.ant.startup.version=99;bool rejected=false;try{WorldSaveStore.Validate(bad);}catch(InvalidDataException){rejected=true;}Check(rejected&&EcologyQueries.Alive(queen.Entity),"错误创业状态在清场前拒绝");
            WorldAuthority.IsReplica=true;float before=queen.State.reproductionTimer;queen.Advance(1);WorldAuthority.IsReplica=false;Check(queen.State.reproductionTimer==before,"联机副本不独立领取创业补给或繁殖");
            Check(LanWorldSnapshot.Frame(2,true).ecologyDefaults==EcologyDefaults.CurrentJson,"低频联机帧携带世界实际使用的生态默认配置");
            if(!SessionState.GetBool(Key+".Short",false))yield return Sustain(queen,clock,planet);
            SessionState.SetBool(Key+".Short",false);
        }
        private static void Freeze(DarwinEntity entity)
        {
            entity.Genes.mutationChance=0;entity.Genes.enabled=false;entity.GetComponent<EntityVitals>().enabled=false;entity.GetComponent<EntityEnvironmentHazard>().enabled=false;entity.GetComponent<PlanetGravity>().enabled=false;
            entity.GetComponent<EntityLifeCycle>().InitializeBirth();var pop=entity.GetComponent<PopulationBehavior>();if(pop!=null){pop.enabled=false;pop.EnsureMembership();}
            entity.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.None;
        }
        // ★ 适配：长期验证记录真实生命/粮食/任务，区分被捕食、断粮与上限等待。
        private static string StationaryValues(AntColonyMember member)
        {var stats=member.GetComponent<EntityStats>();stats.TryGet("health",out var hp);stats.TryGet("nutrient",out var food);stats.TryGet("storedNutrient",out var reserve);return "hp="+hp+",food="+food+",reserve="+reserve+",task="+member.Activity;}
        private static IEnumerator Sustain(AntColonyMember queen,SimulationClock clock,PlanetTerrainMeshBuilder planet)
        {
            // ★ 新增：实际地形碰撞、移动、生命周期、代谢、生产、采集和默认自动补充；仅隔离环境伤害与随机变异，便于定位补给链路。
            float dt=.05f;const int steps=14000;var prepared=new HashSet<DarwinEntity>();var seenAnts=new HashSet<string>{queen.Id};var firstRoles=new List<AntRole>();var trace=new List<string>();bool queenCollected=false,workerCollected=false;int peak=1;
            var spawner=Object.FindFirstObjectByType<RandomPlanetSpawner>();spawner.enabled=false;clock.enabled=false;ServiceLocator.Register(clock);float startTime=clock.GameTime;clock.RestoreState(startTime,1);clock.Play();GameInputGate.Blocked=false;EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=180});
            var simulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;var gameTime=typeof(SimulationClock).GetField("<GameTime>k__BackingField",Private);var delta=typeof(SimulationClock).GetField("<GameDeltaTime>k__BackingField",Private);var budget=typeof(SimulationClock).GetField("_budgetedFrame",Private);
            var vitals=typeof(EntityVitals).GetMethod("AdvanceVitals",Private);var gravity=typeof(PlanetGravity).GetMethod("FixedUpdate",Private);var refill=typeof(RandomPlanetSpawner).GetMethod("Update",Private);
            try
            {
                for(int step=0;step<steps;step++)
                {
                    float time=step*dt;gameTime.SetValue(clock,startTime+time);delta.SetValue(clock,dt);budget.SetValue(clock,false);
                    foreach(var entity in EntityRegistry.All.ToArray())if(EcologyQueries.Alive(entity)&&prepared.Add(entity))Freeze(entity);
                    foreach(var entity in EntityRegistry.All.ToArray())if(EcologyQueries.Alive(entity)){entity.GetComponent<PopulationBehavior>()?.Advance(dt);entity.Genes.AdvanceSimulation(dt);if(EcologyQueries.Alive(entity))vitals.Invoke(entity.GetComponent<EntityVitals>(),new object[]{dt});if(EcologyQueries.Alive(entity))gravity.Invoke(entity.GetComponent<PlanetGravity>(),null);}
                    foreach(var member in AntColonyMember.All.ToArray())
                    {
                        if(seenAnts.Add(member.Id))firstRoles.Add(member.Role);
                        if(!string.IsNullOrEmpty(member.State.carriedLiving)||!string.IsNullOrEmpty(member.State.carriedResource)){if(member.Role==AntRole.Queen)queenCollected=true;if(member.Role==AntRole.Worker)workerCollected=true;}
                    }
                    peak=Mathf.Max(peak,AntColonyMember.All.Count());Physics.SyncTransforms();Physics.Simulate(dt);
                    if(step%20==0){refill.Invoke(spawner,null);yield return null;}
                    if(step%1000==0){trace.Add("t="+time+" alive="+EntityRegistry.All.Count+" ants="+AntColonyMember.All.Count()+" workers="+AntColonyMember.All.Count(m=>m.Role==AntRole.Worker&&!m.Young)+" resources="+ColonyResource.All.Sum(r=>r.State.energy)+" queenCollected="+queenCollected+" workerCollected="+workerCollected+" queen="+string.Join(";",AntColonyMember.All.Where(m=>m.Role==AntRole.Queen).Select(m=>m.Id.Substring(0,4)+"/"+m.Genes.IsGestating+"/"+m.State.reproductionTimer+"/"+m.GetComponent<EntityLifeCycle>().Progress+"/"+StationaryValues(m))));File.WriteAllLines(Path.Combine(Folder,"continuity-trace.txt"),trace);}
                }
                clock.Pause();
                Check(firstRoles.Count>=2&&firstRoles.Take(2).All(role=>role==AntRole.Worker),"实际前两胎均为工匠，未靠预生成成年工匠跳过成长");
                Check(queenCollected,"实际物理步中缺工匠王后完成过采集取货");Check(workerCollected,"工匠成年后实际接手采集取货");
                Check(peak>=6&&AntColonyMember.All.Any(m=>m.Role==AntRole.Queen),"700模拟秒后仍有王后与存活蚁群，超过首批王后的常规寿命窗口");
                Check(AntColonyMember.All.Where(m=>m.Role==AntRole.Queen).All(m=>m.State.startup==null||m.State.startup.foodIssued<=EcologyDefaults.Current.foundingFood),"长期模拟没有单只王后重复发放创业储粮");
                var session=Object.FindFirstObjectByType<WorldSessionManager>();session.SaveNow(false);var saved=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(saved.entities.Any(e=>e.ecology?.ant?.role==AntRole.Queen.ToString()),"延续后的种群实际写盘且通过完整存档校验");
                var living=AntColonyMember.All.First(m=>m.Role==AntRole.Queen);Camera.main.GetComponent<OrbitCamera>().FocusOn(living.transform.position);yield return Capture("surviving-colony");
            }
            finally{clock.Pause();clock.enabled=true;Physics.simulationMode=simulation;}
        }
    }
}
