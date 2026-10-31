using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;

using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：真实基因、收支、孕育、出生和死亡逻辑的多代隔离实验；运动仅作球面切向积分，不修改玩家世界。
    [InitializeOnLoad]
    public static class EcologyBalanceRegression
    {
        private const string BatchKey="DarwinPark.EcologyBalanceBatch";
        private const string QuickKey="DarwinPark.EcologyBalanceQuickBatch";
        private static readonly List<string> Passed=new List<string>();
        private static readonly MethodInfo Vitals=typeof(EntityVitals).GetMethod("AdvanceVitals",BindingFlags.NonPublic|BindingFlags.Instance);
        private static string Folder=>Path.GetFullPath(SessionState.GetBool(QuickKey,false)?"Artifacts/EcologyBalanceQuickReview":"Artifacts/EcologyBalanceReview");
        private static int _births;private static bool _countBirths;
        static EcologyBalanceRegression(){EditorApplication.playModeStateChanged+=mode=>{if(mode==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(BatchKey,false))EditorApplication.delayCall+=Validate;};}
        public static void RunBatch(){SessionState.SetBool(QuickKey,false);Boot();}
        public static void RunQuickBatch(){SessionState.SetBool(QuickKey,true);Boot();}
        private static void Boot(){SessionState.SetBool(BatchKey,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        [MenuItem("Tools/Darwin Park/Validate Ecology Balance")]
        public static void Validate()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();
            if(!Application.isPlaying||session==null||session.Current!=null)throw new InvalidOperationException("请从未加载世界的启动菜单执行");
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");Passed.Clear();session.StartCoroutine(Guard(Run()));
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);Passed.Add(message);}
        private static void Cleanup()
        {foreach(var entity in EntityRegistry.All.ToArray()){entity.gameObject.SetActive(false);Object.DestroyImmediate(entity.gameObject);}}
        private static IEnumerator Guard(IEnumerator run)
        {
            var priorSpawner=DarwinEntity.Spawner;bool logging=Debug.unityLogger.logEnabled;var simulation=Physics.simulationMode;float captureDelta=Time.captureDeltaTime;
            try
            {
                // ★ 修复（防Bug回归）：行动器的接近限速读取帧时长，固定为积分步长才能避免性能优化或机器负载改变实验轨迹。
                Time.captureDeltaTime=.1f;
                DarwinEntity.Spawner=(genome,point,name)=>{var before=EntityRegistry.All.ToArray();priorSpawner(genome,point,name);Setup(EntityRegistry.All.Single(e=>!before.Contains(e)));if(_countBirths)_births++;};
                while(true)
                {
                    bool next;object value=null;
                    try{next=run.MoveNext();if(next)value=run.Current;}
                    catch(Exception ex){File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL: "+ex+"\n"+string.Join("\n",Passed));Debug.unityLogger.logEnabled=true;Debug.LogException(ex);Finish(false);yield break;}
                    if(!next)break;yield return value;
                }
                File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));Debug.unityLogger.logEnabled=true;Debug.Log("[EcologyBalance] PASS: "+Passed.Count+" checks");Finish(true);
            }
            finally
            {
                Cleanup();DarwinEntity.Spawner=priorSpawner;Debug.unityLogger.logEnabled=logging;Physics.simulationMode=simulation;Time.captureDeltaTime=captureDelta;
                Object.FindFirstObjectByType<GameMainMenu>().enabled=true;ServiceLocator.Get<SimulationClock>().Pause();WorldRuntimeGate.Ready=false;GameInputGate.Blocked=true;
            }
        }
        private static void Finish(bool success)
        {if(SessionState.GetBool(BatchKey,false)){SessionState.SetBool(BatchKey,false);EditorApplication.Exit(success?0:1);}}
        private static void Setup(DarwinEntity entity)
        {
            entity.Genes.mutationChance=0;entity.Genes.enabled=false;
            foreach(var script in entity.GetComponents<MonoBehaviour>())if(script is EntityVitals||script is EntityEnvironmentHazard||script is PlanetGravity||script is EntitySurfaceAnchor||script.GetType().Name=="EntityAIController")script.enabled=false;
            var rb=entity.GetComponent<Rigidbody>();rb.useGravity=false;rb.linearDamping=0;rb.interpolation=RigidbodyInterpolation.None;
            entity.GetComponent<EntityLifeCycle>().InitializeBirth();
        }
        private static DarwinEntity Spawn(Vector3 position,bool moving=false,bool hunter=false)
        {
            var genome=new Genome();foreach(var pair in new[]{("gene_growth",50f),("gene_reproduction",50f),("gene_nutrition",moving?85f:50f)})genome.Add(GeneInstance.Create(pair.Item1,pair.Item2,0));
            if(moving)genome.Add(GeneInstance.Create("gene_locomotion",hunter?75:25,0));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,position,moving?(hunter?"实验捕猎者":"实验食草者"):"实验植物");return EntityRegistry.All.Single(e=>!before.Contains(e));
        }
        private static void Adult(DarwinEntity entity)
        {entity.Genes.CurrentAge=AgeState.Adult;entity.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);entity.Genes.Recalculate();}
        private static IEnumerator Run()
        {
            yield return null;ServiceLocator.Get<SimulationClock>().Pause();GeneRuntimeProfile.Clear();GeneDatabase.Reload();GeneDatabase.EnsureLoaded();
            var settings=EcologyBalanceSettings.Current;
            Check(!PredationSizePolicy.AllowsValues(.9f,1)&&PredationSizePolicy.AllowsValues(1,1)&&!PredationSizePolicy.AllowsValues(1,1.0001f)&&PredationSizePolicy.AllowsValues(1.2f,1),"实际数值体型边界：小不能吃大、同型允许、无模糊容差");
            Check(!PredationSizePolicy.AllowsValues(0,1)&&!PredationSizePolicy.AllowsValues(float.NaN,1),"无效或零体型拒绝捕食");
            var plant=Spawn(new Vector3(0,400,0));Adult(plant);var rep=plant.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_reproduction");
            var reproduction=(ReproductionGeneLogic)plant.Genes.GetLogic(rep);float original=reproduction.CalculateGestationTime(1);
            var nutrition=plant.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_nutrition");nutrition.Value=30;plant.Genes.Recalculate();float inhibited=reproduction.CalculateGestationTime(1);
            Check(inhibited>original,"营养基因对繁殖基因的促进改变有效孵化速度和实际孕育时长");nutrition.Value=50;plant.Genes.Recalculate();
            Check(reproduction.CalculateGestationTime(.01f)==70&&reproduction.CalculateGestationTime(100)==300,"孕育仍遵守 7～30 游戏日边界");
            var stats=plant.GetComponent<EntityStats>();stats.TryGet("storedNutrient",out float beforeReserve);
            var nutritionLogic=plant.Genes.GetLogic(nutrition);nutritionLogic.OnUpdate(1);stats.TryGet("storedNutrient",out float afterReserve);
            Check(afterReserve>beforeReserve,"植物营养生产溢出进入储存，能够积累繁殖预算");
            var duplicate=GeneInstance.Create("gene_nutrition",50,0);plant.Genes.Genome.Add(duplicate);plant.Genes.Recalculate();stats.Modify("storedNutrient",-1000,false);
            float expected=plant.Genes.GetAttribute(settings.absorptionAttribute)*settings.photoProductionRate;
            plant.Genes.GetLogic(nutrition).OnUpdate(1);plant.Genes.GetLogic(duplicate).OnUpdate(1);stats.TryGet("storedNutrient",out float produced);
            Check(Mathf.Abs(produced-expected)<.001f,"复制营养基因仅表达叠加，不重复执行生产");
            Cleanup();var actor=Spawn(new Vector3(0,400,0),true,true);var prey=Spawn(new Vector3(1.2f,400,0));
            actor.GetComponent<EntityStats>().Modify("size",-.2f,false);actor.Genes.Recalculate();
            Check(!EcologyMotion.CanHunt(actor.Genes,prey),"自主选取过滤更大体型");
            Check(EcologyReproductionTiming.FoodPressure(actor.Genes)>1,"周边食物不足、体型不可食时产生消费者繁殖压力");
            var clock=ServiceLocator.Get<SimulationClock>();clock.Play();var manual=actor.gameObject.AddComponent<EntityManualControl>();manual.BeginControl(EcologyQueries.Planet.transform);manual.SetAim(new Ray(actor.transform.position,Vector3.right));manual.QueueAttack();Physics.SyncTransforms();
            var action=actor.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_locomotion");actor.Genes.GetLogic(action).OnUpdate(.1f);prey.GetComponent<EntityStats>().TryGet("health",out float health);
            Check(health==100,"手动最终咬合也拒绝更大目标");manual.Release();
            clock.SetTimeScale(1);actor.GetComponent<Rigidbody>().linearVelocity=Vector3.right*2;float cost=EcologyEnergyBudget.Consumption(actor.Genes,1,.06f);
            clock.SetTimeScale(16);actor.GetComponent<Rigidbody>().linearVelocity=Vector3.right*32;float accelerated=EcologyEnergyBudget.Consumption(actor.Genes,1,.06f);
            Check(Mathf.Abs(cost-accelerated)<.00001f,"16 倍物理速度不会造成重复倍率的行动开销");
            Check(cost<.2f,"默认行动与基础代谢预算已降低到可持续量级");clock.SetTimeScale(1);clock.Pause();
            var actorStats=actor.GetComponent<EntityStats>();actorStats.Modify(settings.nutrientAttribute,-2,false);
            Check(EcologyEnergyBudget.WantsFood(actor.Genes,actorStats,LocomotionSettings.Load().hungerThreshold),"有繁殖基因且储备不足时提前觅食");
            actorStats.Feed(100);
            actorStats.Modify(settings.nutrientAttribute,-2,false);
            Check(!EcologyEnergyBudget.WantsFood(actor.Genes,actorStats,LocomotionSettings.Load().hungerThreshold),"饱食且储备充足时停止额外捕食");Cleanup();
            var legacy=JsonUtility.FromJson<GeneDefinitionJson>(GeneTreeStorage.ReadCatalog()["gene_growth"]);legacy.ecologyBalanceVersion=0;legacy.suppressionRules.Clear();legacy.attributeCurves[0].maxValue=123;
            var migrated=JsonUtility.FromJson<GeneDefinitionJson>(EcologyGeneDefaults.Upgrade(JsonUtility.ToJson(legacy)));
            Check(migrated.suppressionRules.Any(r=>r.targetGeneId=="gene_reproduction")&&migrated.attributeCurves[0].maxValue==123,"旧默认抑制矩阵迁移且保留自定义曲线");legacy.suppressionRules.Add(new GeneSuppressionJson{targetGeneId="gene_reproduction",coefficient=321});
            Check(JsonUtility.FromJson<GeneDefinitionJson>(EcologyGeneDefaults.Upgrade(JsonUtility.ToJson(legacy))).suppressionRules.Single().coefficient==321,"自定义抑制矩阵不被平衡补丁覆盖");
            // ★ 新增：实际写盘后恢复规则快照，避免只验证 DTO 而漏掉持久化链路。
            string priorBalance=EcologyBalanceSettings.CurrentJson;var customBalance=EcologyBalanceSettings.Parse(priorBalance);customBalance.baseMetabolismScale=.123f;
            var catalog=GeneTreeStorage.ReadCatalog();
            var save=new WorldSaveData{id=Guid.NewGuid().ToString("N"),name="生态规则往返实验",oreResolution=1,ore=new float[1],terrainParameters=JsonUtility.ToJson(Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>()),environmentParameters=JsonUtility.ToJson(ServiceLocator.Get<DarwinPark.Environment.EnvironmentSystem>()),definitions=catalog.Select(p=>new SavedGeneDefinition{id=p.Key,json=p.Value}).ToList(),tree=GeneTreeStorage.Active,ecologyBalanceSettings=JsonUtility.ToJson(customBalance)};
            string roundtrip=Path.Combine(Folder,"roundtrip.json");WorldSaveStore.Write(save,roundtrip);var read=WorldSaveStore.Read(roundtrip);
            try
            {
                EcologyBalanceSettings.UseSnapshot(read.ecologyBalanceSettings);Check(EcologyBalanceSettings.Current.baseMetabolismScale==.123f,"生态规则独立世界写盘、读取并恢复快照");
                customBalance.maximumPreySizeRatio=2;bool rejected=false;
                try{EcologyBalanceSettings.UseSnapshot(JsonUtility.ToJson(customBalance));}catch(InvalidDataException){rejected=true;}
                Check(rejected&&EcologyBalanceSettings.CurrentJson==read.ecologyBalanceSettings,"损坏生态快照被拒绝且现有规则不变");
            }
            finally{EcologyBalanceSettings.UseSnapshot(priorBalance);}
            var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var weather=Object.FindFirstObjectByType<PlanetWeatherVisuals>();if(weather!=null)Object.DestroyImmediate(weather);
            builder.subdivisions=32;builder.Build();PlanetClimateMap.Instance.CancelInvoke();PlanetClimateMap.Instance.enabled=false;
            Vector3 direction=Vector3.up;float best=float.MinValue;
            for(int i=0;i<512;i++){float y=1-2f*(i+.5f)/512,angle=i*2.39996323f;var d=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));float height=PlanetTerrainSampler.SampleHeight(builder.Data,d);if(height>best){best=height;direction=d;}}
            Vector3 center=EcologyQueries.Surface(builder.transform.position+direction*(builder.radius+builder.heightScale));
            var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.enabled=false;WorldRuntimeGate.Ready=true;GameInputGate.Blocked=false;clock.Play();Physics.simulationMode=SimulationMode.Script;Debug.unityLogger.logEnabled=false;
            // ★ 新增：模拟真实旧档中约 272 秒剩余孕育和 150 秒休养，验证不因恢复生长而在生产前老死。
            var legacyParent=Spawn(center);Adult(legacyParent);legacyParent.Genes.IsGestating=true;legacyParent.Genes.Recalculate();
            var legacyGene=legacyParent.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_reproduction");var legacyLogic=(ReproductionGeneLogic)legacyParent.Genes.GetLogic(legacyGene);
            legacyLogic.RestoreState(new ReproductionSaveState{gene=legacyGene.InstanceId,gestation=272});
            var restingParent=Spawn(center+Vector3.right*3);Adult(restingParent);
            var restingGene=restingParent.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_reproduction");var restingLogic=(ReproductionGeneLogic)restingParent.Genes.GetLogic(restingGene);
            restingLogic.RestoreState(new ReproductionSaveState{gene=restingGene.InstanceId,cooldown=150});
            var oldWorld=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(read));oldWorld.ecologyBalanceSettings=null;
            oldWorld.entities=new List<EntitySaveState>{EntitySaveState.Capture(legacyParent),EntitySaveState.Capture(restingParent)};
            Check(EcologyLegacySaveUpgrade.Apply(oldWorld,EntityRegistry.All)==2&&legacyLogic.CaptureState().gestation<100&&restingLogic.CaptureState().cooldown==5,"旧孕育/休养收纳到新周期，不重置生命周期或重复扣储备");
            float remaining=legacyLogic.CaptureState().gestation;legacyLogic.OnUpdate(1);
            Check(EcologyLegacySaveUpgrade.Apply(oldWorld,EntityRegistry.All)==0&&Mathf.Abs(legacyLogic.CaptureState().gestation-(remaining-1))<.001f,"已迁移世界再次检查不重置剩余孕育");
            string upgradedFile=Path.Combine(Folder,"legacy-upgraded.json");WorldSaveStore.Write(oldWorld,upgradedFile);
            var upgradedWorld=WorldSaveStore.Read(upgradedFile);
            Check(upgradedWorld.ecologyBalanceSettings==EcologyBalanceSettings.CurrentJson&&upgradedWorld.entities[0].reproduction.Single().gestation<100,"旧计时迁移和规则标记同步写盘读取");
            for(int step=0;step<1000&&EntityRegistry.All.Count==2;step++){legacyParent.Genes.AdvanceSimulation(.1f);Vitals.Invoke(legacyParent.GetComponent<EntityVitals>(),new object[]{.1f});}
            Check(EcologyQueries.Alive(legacyParent)&&EntityRegistry.All.Count>2,"旧长孕育实体在新规则下实际存活并产下后代");Cleanup();
            if(SessionState.GetBool(QuickKey,false))yield break;
            foreach(int seed in new[]{17,41,83})
            {
                Cleanup();UnityEngine.Random.InitState(seed);_births=0;_countBirths=false;
                var up=EcologyQueries.Up(center);var a=Vector3.Cross(up,Vector3.right).normalized;var b=Vector3.Cross(up,a);
                for(int i=0;i<24;i++){var point=UnityEngine.Random.insideUnitCircle*22;var e=Spawn(EcologyQueries.Surface(center+a*point.x+b*point.y));Adult(e);}
                for(int i=0;i<2;i++){var e=Spawn(EcologyQueries.Surface(center+a*(i*3)),true);Adult(e);}
                _countBirths=true;var csv=new StringBuilder("seconds,plants,consumers,births,consumer_details\n");int minPlants=int.MaxValue,minConsumers=int.MaxValue,maxTotal=0,completedSteps=0;
                for(int step=0;step<30000;step++)
                {
                    foreach(var entity in EntityRegistry.All.ToArray())if(EcologyQueries.Alive(entity)){entity.Genes.AdvanceSimulation(.1f);if(entity!=null&&entity.isActiveAndEnabled)Vitals.Invoke(entity.GetComponent<EntityVitals>(),new object[]{.1f});}
                    foreach(var entity in EntityRegistry.All.ToArray())
                    {
                        if(!EcologyQueries.Alive(entity)){entity.gameObject.SetActive(false);Object.DestroyImmediate(entity.gameObject);continue;}
                        if(entity.Genes.GetAttribute(settings.movementAttribute)>0){var body=entity.GetComponent<Rigidbody>();entity.transform.position=EcologyQueries.Surface(entity.transform.position+body.linearVelocity*.1f);}
                    }
                    Physics.SyncTransforms();completedSteps=step+1;
                    if(step%500==0||step==29999)
                    {
                        int plants=EntityRegistry.All.Count(e=>EcologyEnergyBudget.IsProducer(e.Genes)),consumers=EntityRegistry.All.Count-plants;maxTotal=Mathf.Max(maxTotal,EntityRegistry.All.Count);
                        if(step>=5000){minPlants=Mathf.Min(minPlants,plants);minConsumers=Mathf.Min(minConsumers,consumers);}
                        var details=string.Join(";",EntityRegistry.All.Where(e=>!EcologyEnergyBudget.IsProducer(e.Genes)).Select(e=>{var values=e.GetComponent<EntityStats>();values.TryGet("nutrient",out float food);values.TryGet("storedNutrient",out float reserve);return e.GetComponent<EntityLifeCycle>().Progress.ToString("F1")+"/"+food.ToString("F1")+"/"+reserve.ToString("F1")+"/"+e.Genes.IsGestating+"/"+EntityRegistry.All.Where(p=>EcologyEnergyBudget.IsProducer(p.Genes)).Select(p=>Vector3.Distance(p.transform.position,e.transform.position)).DefaultIfEmpty(-1).Min().ToString("F1");}));
                        csv.AppendLine((step*.1f).ToString("F0")+","+plants+","+consumers+","+_births+","+details);
                        File.WriteAllText(Path.Combine(Folder,"cycle-"+seed+".csv"),csv.ToString());
                        if(EntityRegistry.All.Count>400||plants==0||consumers==0)break;
                        yield return null;
                    }
                }
                Check(completedSteps==30000&&minPlants>0&&minConsumers>0&&maxTotal<=400&&_births>24,"种子 "+seed+"：3000 模拟秒多代生产/捕食/繁殖循环，数量有界；出生="+_births+"，最高数量="+maxTotal+"，后期植物/消费者最低="+minPlants+"/"+minConsumers);
                _countBirths=false;
            }
        }
    }
}
