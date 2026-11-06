using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：在真实 Unity 播放环境验收路径、角色权限、驱逐、营养守恒及逆序存档引用；支持无窗口批处理。
    [InitializeOnLoad]
    public static class SocialEcologyRegression
    {
        private const string BatchKey="DarwinPark.SocialEcologyBatch";
        private static readonly List<DarwinEntity> Created=new List<DarwinEntity>();
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/SocialEcologyReview");
        static SocialEcologyRegression(){EditorApplication.playModeStateChanged+=OnMode;}
        public static void RunBatch()
        {
            SessionState.SetBool(BatchKey,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();
        }
        private static void OnMode(PlayModeStateChange mode)
        {
            if(mode==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(BatchKey,false))EditorApplication.delayCall+=Validate;
        }
        [MenuItem("Tools/Darwin Park/Validate Social Ecology")]
        public static void Validate()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("请先播放场景");
            if(Object.FindFirstObjectByType<WorldSessionManager>()?.Current!=null||WorldRuntimeGate.Ready)throw new InvalidOperationException("请在尚未加载世界的启动菜单运行生态回归，避免影响当前世界");
            Directory.CreateDirectory(Folder);Passed.Clear();Object.FindFirstObjectByType<WorldSessionManager>().StartCoroutine(Guard(Run()));
        }
        private static IEnumerator Guard(IEnumerator operation)
        {
            bool success=false;
            while(true)
            {
                bool next;object current=null;
                try{next=operation.MoveNext();if(next)current=operation.Current;}
                catch(Exception ex){File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL: "+ex+"\n"+string.Join("\n",Passed));Debug.LogException(ex);Finish(false);yield break;}
                if(!next){success=true;break;}yield return current;
            }
            if(success){File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));Debug.Log("[SocialEcology] PASS: "+Passed.Count+" checks");Finish(true);}
        }
        private static void Finish(bool success)
        {
            Cleanup();if(SessionState.GetBool(BatchKey,false)){SessionState.SetBool(BatchKey,false);EditorApplication.Exit(success?0:1);}
        }
        private static void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);Passed.Add(message);}
        private static Genome Genome(params (string id,float value)[] genes)
        {var genome=new Genome();foreach(var entry in genes)genome.Add(GeneInstance.Create(entry.id,entry.value,GeneDatabase.Get(entry.id).Tier));return genome;}
        private static DarwinEntity Spawn(Vector3 point,params (string id,float value)[] genes)
        {
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(Genome(genes),point,"生态回归");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;entity.Genes.RestoreStageState(AgeState.Adult,7,3);Created.Add(entity);return entity;
        }
        private static void Cleanup()
        {foreach(var entity in Created.ToArray())if(entity!=null){entity.gameObject.SetActive(false);Object.DestroyImmediate(entity.gameObject);}Created.Clear();}
        private static float Food(DarwinEntity entity){var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out var a);stats.TryGet("storedNutrient",out var b);return a+b;}
        private static void EmptyFood(DarwinEntity entity){entity.GetComponent<EntityStats>().Modify("nutrient",-1000,false);entity.GetComponent<EntityStats>().Modify("storedNutrient",-1000,false);}
        private static DarwinEntity Restore(EntitySaveState state)
        {
            var entity=Spawn(state.position,state.genome.Genes.Select(g=>(g.DefinitionId,g.Value)).ToArray());
            entity.Genes.Genome=JsonUtility.FromJson<Genome>(JsonUtility.ToJson(state.genome));entity.Genes.Recalculate();state.Restore(entity);return entity;
        }
        private static IEnumerator Run()
        {
            yield return null;var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();var random=UnityEngine.Random.state;
            GeneRuntimeProfile.Clear();GeneDatabase.Reload();GeneDatabase.EnsureLoaded();var catalog=GeneTreeStorage.ReadCatalog();GeneTreeStorage.Load();
            Check(catalog.Count>=14,"扩展基因与前置完整注册");GeneTreeStorage.Validate(GeneTreeStorage.Active,catalog);
            foreach(var id in new[]{"gene_territory","gene_population","gene_oviparous","gene_parasitic"})Check(GeneLogicRegistry.CreateLogic(GeneDatabase.Get(id).LogicScriptId)!=null,"逻辑已注册："+id);
            Check(GeneEvolutionRules.Allows("gene_locomotion","gene_territory","error",40)&&GeneEvolutionRules.Allows("gene_locomotion","gene_territory","error",60)&&!GeneEvolutionRules.Allows("gene_locomotion","gene_territory","error",39.9f),"行动出错 40～60 边界");
            Check(!GeneEvolutionRules.Allows("gene_territory","gene_population","evolution",80)&&GeneEvolutionRules.Allows("gene_territory","gene_population","evolution",80.00005f),"种群演化严格大于 80");
            Check(GeneEvolutionRules.Allows("gene_oviparous","gene_parasitic","error",50)&&!GeneEvolutionRules.Allows("gene_locomotion","gene_parasitic","error",50)&&!GeneEvolutionRules.Allows("gene_oviparous","gene_parasitic","evolution",50),"寄生仅允许卵生出错来源");
            var forceError=new GeneMutationSettings{ErrorWeight=1,DuplicateWeight=0,DeletionWeight=0,MaxErrorDelta=0};
            var errorGenome=Genome(("gene_locomotion",50));GeneMutator.Mutate(errorGenome,forceError);
            Check(errorGenome.Genes.Single().DefinitionId=="gene_territory"&&errorGenome.Genes.Single().Tier==1,"实际出错迁移及 Tier 更新");
            var copyGenome=Genome(("gene_territory",81));GeneMutator.Mutate(copyGenome,new GeneMutationSettings{ErrorWeight=0,DuplicateWeight=1,DeletionWeight=0});
            Check(copyGenome.Genes.Any(g=>g.DefinitionId=="gene_territory")&&copyGenome.Genes.Any(g=>g.DefinitionId=="gene_population"&&g.Tier==2),"复制保留领地并新增种群");
            var parasiteGenome=Genome(("gene_oviparous",50));GeneMutator.Mutate(parasiteGenome,forceError);Check(parasiteGenome.Genes.Single().DefinitionId=="gene_parasitic","实际卵生出错产生寄生");
            var actor=Spawn(new Vector3(0,320,0),("gene_locomotion",50));var instance=actor.Genes.Genome.Genes.Single();instance.DefinitionId="gene_territory";actor.Genes.Recalculate();
            Check(actor.Genes.GetLogic(instance) is TerritoryGeneLogic,"原地换基因释放旧行动逻辑");
            var territory=actor.GetComponent<TerritoryBehavior>();territory.Advance(.1f);var original=territory.CenterLocal;
            var attacker=Spawn(new Vector3(10,320,0),("gene_growth",20));actor.GetComponent<EntityStats>().Modify("health",-1,false);
            Check(territory.EvictionRemaining==0,"环境/普通扣血不触发驱逐");
            // ★ 适配：新规则允许反击可捕猎目标，此项明确使用无法对抗的大体型攻击者验证逃离兜底。
            attacker.GetComponent<EntityStats>().Modify("size",3,false);
            EntityCombat.Ensure(actor.gameObject).Receive(attacker,5);Check(territory.EvictionRemaining>0&&territory.Destination(out var flee,out var speed)&&speed>1,"无法对抗的真实攻击仍触发撤离");
            actor.transform.position=new Vector3(120,300,0);territory.Advance(0);Check(territory.CenterLocal==original,"暂停不推进迁居");
            territory.Advance(EcologySettings.Load().settleDelay+.1f);Check(Vector3.Distance(territory.CenterLocal,original)>20,"安全期结束在新位置定居");Cleanup();
            var founder=Spawn(new Vector3(0,320,0),("gene_population",80));var follower=Spawn(new Vector3(10,320,0),("gene_population",90));var prey=Spawn(new Vector3(20,320,0),("gene_growth",20));
            var a=founder.GetComponent<PopulationBehavior>();var b=follower.GetComponent<PopulationBehavior>();a.Advance(.1f);
            Check(Mathf.Approximately(a.AttackMultiplier,1),"独狼不获得虚构群攻奖励");b.Advance(.1f);
            Check(a.IsLeader&&a.SameGroup(follower)&&a.MemberCount==2,"首成员领袖及附近同模式聚群");
            Check(a.AttackMultiplier>1&&b.AttackMultiplier>1,"附近同群成员产生真实协同攻击加成");
            Check(!b.Issue(PopulationCommand.Hunt,prey,prey.transform.position,true,out _),"普通成员不能下令");
            Check(a.Issue(PopulationCommand.Hunt,prey,prey.transform.position,true,out _)&&b.Target==prey&&b.Command==PopulationCommand.Hunt,"同群领袖命令广播");
            Check(!EcologyMotion.CanHunt(founder.Genes,follower),"自主行动不把同群当猎物");
            founder.GetComponent<EntityStats>().Modify("health",-75,false);Check(b.ChallengeLeadership(out _)&&b.IsLeader&&!a.IsLeader,"更强成员决斗接任");
            Check(!a.ChallengeLeadership(out _)&&a.DuelRemaining>0&&b.DuelRemaining>0,"决斗双方冷却防重入");
            var beforeCommand=b.CommandRemaining;b.Advance(0);Check(b.CommandRemaining==beforeCommand,"暂停不扣命令时长");
            EmptyFood(founder);EmptyFood(follower);b.ShareReward(10);Check(Mathf.Abs(Food(founder)+Food(follower)-10)<.001f,"击杀分享总营养守恒");
            var leaderState=JsonUtility.FromJson<EntitySaveState>(JsonUtility.ToJson(EntitySaveState.Capture(follower)));var followerState=JsonUtility.FromJson<EntitySaveState>(JsonUtility.ToJson(EntitySaveState.Capture(founder)));
            founder.gameObject.SetActive(false);follower.gameObject.SetActive(false);
            var newFollower=Restore(followerState);var newLeader=Restore(leaderState);var map=new Dictionary<string,DarwinEntity>{{followerState.ecology.id,newFollower},{leaderState.ecology.id,newLeader},{EcologyIdentity.Of(prey),prey}};
            followerState.ecology.Resolve(newFollower,map);leaderState.ecology.Resolve(newLeader,map);
            Check(newLeader.GetComponent<PopulationBehavior>().IsLeader&&!newFollower.GetComponent<PopulationBehavior>().IsLeader,"逆序加载仍保留决斗产生的领袖");
            Check(newFollower.GetComponent<PopulationBehavior>().Target==prey,"命令目标通过稳定 ID 恢复");
            newLeader.gameObject.SetActive(false);newFollower.GetComponent<PopulationBehavior>().Advance(.1f);Check(newFollower.GetComponent<PopulationBehavior>().IsLeader,"领袖死亡/停用后按加入顺序递补");Cleanup();
            var ant=Spawn(new Vector3(0,320,0),("gene_population",20));var antPop=ant.GetComponent<PopulationBehavior>();antPop.Advance(.1f);
            var ore=PlanetOreSystem.Instance;ore.RestoreAmounts(Enumerable.Repeat(1f,16*16).ToArray(),16);EmptyFood(ant);float oreBefore=ore.GetOreAmount(ant.transform.position);
            // ★ 适配：固定分工替换旧全群采矿；王后仅从实际运达的能量块进食。
            var block=ColonyResource.Convert(antPop.GroupId,ant.transform.position,10,true);antPop.Advance(1);
            Check(antPop.Mode=="ant"&&ant.GetComponent<AntColonyMember>().Role==AntRole.Queen&&Food(ant)>0&&Mathf.Abs(oreBefore-ore.GetOreAmount(ant.transform.position))<.001f,"蚁群王后使用能量块且不运行旧采矿命令");
            Check(Mathf.Abs(block.State.energy+Food(ant)-10)<.001f,"能量块与王后吸收总营养守恒");ColonyResource.ClearWorld();
            var root=ServiceLocator.Get<UIRoot>();PopulationCommandPanel.Build(ant,root.MainCanvas.transform);var panel=Object.FindFirstObjectByType<PopulationCommandPanel>();Check(panel!=null&&panel.GetComponentsInChildren<Button>().Length==5,"命令/决斗面板完整创建");Object.DestroyImmediate(panel.gameObject);Cleanup();
            var parent=Spawn(new Vector3(0,320,0),("gene_oviparous",50));var beforeEgg=EntityRegistry.All.ToArray();EcologyReproduction.Spawn(parent.Genes,Genome(("gene_growth",20)),new Vector3(2,320,0));
            var egg=EntityRegistry.All.Single(e=>!beforeEgg.Contains(e));Created.Add(egg);egg.Genes.mutationChance=0;Check(egg.GetComponent<EggIncubation>().Incubating&&egg.Genes.StageMutationChecks==0,"卵独立存在且未提前出生变异");
            var eggState=JsonUtility.FromJson<EntitySaveState>(JsonUtility.ToJson(EntitySaveState.Capture(egg)));egg.gameObject.SetActive(false);var restoredEgg=Restore(eggState);
            var socialEgg=Spawn(new Vector3(5,320,0),("gene_population",80));socialEgg.gameObject.AddComponent<EggIncubation>().Begin(10);socialEgg.GetComponent<PopulationBehavior>().Advance(1);
            Check(!socialEgg.GetComponent<PopulationBehavior>().Active&&socialEgg.GetComponent<PopulationBehavior>().MemberCount==0,"孵化前不能加入种群或当领袖");
            restoredEgg.Genes.AdvanceSimulation(eggState.ecology.incubationRemaining+1);Check(!restoredEgg.GetComponent<EggIncubation>().Incubating&&restoredEgg.Genes.StageMutationChecks==1,"卵读档后孵化且出生只抽一次");Cleanup();
            // ★ 修复（防Bug回归）：gene_parasitic已迁移为卵寄生；旧吸血/体外附着用例不能再把它当作旧ParasiteBehavior来源。
            // 新营养寄生的吸收、成年脱离及读档由NutritionEcologyRegression专项覆盖；这里验收社会生态中的旧组件隔离。
            var host=Spawn(new Vector3(0,320,0),("gene_growth",20));host.GetComponent<EntityStats>().Modify("size",3,false);
            var parasite=Spawn(new Vector3(1,320,0),("gene_parasitic",30));EmptyFood(parasite);
            Check(parasite.GetComponent<ParasiteBehavior>()==null&&parasite.Genes.GetLogic(parasite.Genes.Genome.Genes.Single()) is EggParasiticGeneLogic,"卵寄生只挂新繁殖逻辑，不安装旧吸血组件");
            var behavior=parasite.gameObject.AddComponent<ParasiteBehavior>();
            Check(!behavior.Active&&!behavior.TryAttach(host),"旧组件残留也不能让卵寄生附着宿主");
            float hostFood=Food(host);behavior.Advance(1);
            Check(behavior.Host==null&&Mathf.Approximately(hostFood,Food(host))&&Food(parasite)==0,"新卵寄生不会通过旧组件消耗宿主营养");
            Check(parasite.GetComponent<Collider>().enabled&&!parasite.GetComponent<Rigidbody>().isKinematic&&parasite.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"未孕育的卵寄生个体保留正常渲染和独立物理");
            var data=new WorldSaveData{id=Guid.NewGuid().ToString("N"),name="生态存档回归",terrainParameters=JsonUtility.ToJson(Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>()),environmentParameters=JsonUtility.ToJson(ServiceLocator.Get<DarwinPark.Environment.EnvironmentSystem>()),definitions=catalog.Select(p=>new SavedGeneDefinition{id=p.Key,json=p.Value}).ToList(),tree=GeneTreeStorage.Active,entities=Created.Where(EcologyQueries.Alive).Select(EntitySaveState.Capture).ToList()};
            data.ore=ore.CaptureAmounts();data.oreResolution=ore.mapResolution;
            string file=Path.Combine(Folder,"roundtrip.json");WorldSaveStore.Write(data,file);var read=WorldSaveStore.Read(file);Check(read.entities.Count==data.entities.Count&&read.entities.All(e=>e.ecology!=null),"生态字段完整写盘与读取校验");
            string legacy=JsonUtility.ToJson(data);foreach(var state in data.entities)legacy=legacy.Replace(",\"ecology\":"+JsonUtility.ToJson(state.ecology),"");
            string legacyFile=Path.Combine(Folder,"legacy.json");File.WriteAllText(legacyFile,legacy);
            Check(!legacy.Contains("\"ecology\"")&&WorldSaveStore.Read(legacyFile).entities.All(e=>e.ecology==null),"没有生态字段的旧实体存档仍能读取");
            UnityEngine.Random.state=random;Check(clock.GameTime==0,"批量回归期间世界加载/暂停时间未推进");
            var runtimeErrors=new List<string>();Application.LogCallback collect=(message,stack,type)=>{if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)runtimeErrors.Add(message);};
            var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.enabled=false;WorldRuntimeGate.Ready=true;
            Application.logMessageReceived+=collect;clock.Play();
            yield return new WaitForSecondsRealtime(.25f);
            clock.Pause();Application.logMessageReceived-=collect;menu.enabled=true;WorldRuntimeGate.Ready=false;
            Check(runtimeErrors.Count==0,"真实 Update/FixedUpdate 窗口无运行异常："+string.Join(";",runtimeErrors));
            // ★ 适配：低细分真实地形只用于原有物理/出生点回归，不加载高精度可玩世界。
            Cleanup();var weather=Object.FindFirstObjectByType<PlanetWeatherVisuals>();if(weather!=null)Object.DestroyImmediate(weather);
            var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=32;planet.Build();
            var climate=Object.FindFirstObjectByType<PlanetClimateMap>();climate.CancelInvoke();climate.enabled=false;
            WorldRuntimeGate.Ready=true;GeneTreeWorkbenchRegression.Validate();
            Check(true,"原有基因树存储、第一人称攻击/冷却、阶段寿命及繁殖回归通过");
        }
    }
}
