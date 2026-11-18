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
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：在实际存档副本及原生物理下同时检查变异保护和移动，不能以 READY 或速度属性代替实际位移。
    [InitializeOnLoad] public static class MutationMovementRegression
    {
        private const string Key="DarwinPark.MutationMovement";
        private static bool started;
        private static readonly List<string> passed=new();
        private static readonly StringBuilder diagnostics=new();
        private static string Folder=>Path.GetFullPath(SessionState.GetBool(Key+".Water",false)?"Artifacts/WaterHazardReview":"Artifacts/MutationMovementReview");
        static MutationMovementRegression(){EditorApplication.update+=()=>
        {
            // ★ 新增：仅在请求文件存在时只读采集正式编辑器现场；不改实体、暂停状态或存档。
            string request=Path.GetFullPath("Artifacts/WaterHazardReview/live.request");
            if(File.Exists(request)&&!EditorApplication.isCompiling){File.Delete(request);CaptureLiveWater();}
            if(!started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){started=true;LanSession.Instance.StartCoroutine(Guard());}
        };}
        private static void CaptureLiveWater()
        {
            var report=new StringBuilder();ServiceLocator.TryGet<SimulationClock>(out var clock);
            report.AppendLine("playing="+Application.isPlaying+" replica="+WorldAuthority.IsReplica+" running="+clock?.IsRunning+" dt="+clock?.GameDeltaTime+" loading="+WorldRuntimeGate.Loading);
            var selected=Object.FindFirstObjectByType<GeneInspectorUI>()?.CurrentEntity;
            int water=0;foreach(var entity in EntityRegistry.All.ToArray())
            {
                var hazard=entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>();if(hazard==null)continue;
                if(!hazard.IsSubmerged&&!hazard.IsInWater&&entity!=selected)continue;water++;
                entity.GetComponent<EntityStats>().TryGet("health",out float hp);
                var geography=hazard.Geography;
                report.AppendLine("entity="+EcologyIdentity.Of(entity)+" selected="+(entity==selected)+" name="+entity.DisplayName+" hp="+hp+" enabled="+hazard.enabled+" active="+hazard.isActiveAndEnabled+" immersion="+hazard.ImmersionDepth+" actualImmersion="+geography.Immersion+" waterDepth="+geography.WaterDepth+" drowning="+hazard.IsDrowning+" dormant="+DarwinPark.Entities.Simulation.EntityWorkScheduler.Dormant(entity.Genes)+" status="+hazard.GetStatusLine());
            }
            report.AppendLine("waterEntities="+water+" all="+EntityRegistry.All.Count);Directory.CreateDirectory("Artifacts/WaterHazardReview");File.WriteAllText("Artifacts/WaterHazardReview/live.txt",report.ToString());
        }
        // ★ 新增：通过实际存档校验和写入入口修复隔离副本，可用于历史基础基因缺失检查。
        public static void RunBaseGeneRepairBatch()
        {
            string folder=Path.GetFullPath("Artifacts/BaseGeneRepair");
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Artifacts/GeneRiverReview/Project/Assets"))
                    throw new InvalidOperationException("仅在隔离验证项目运行");
                string input=Path.Combine(folder,"input-world.json");
                var original=WorldSaveStore.Read(input);
                var repaired=WorldBaseGeneRepair.CreateCopy(original,out int entities,out int genes);
                var required=GeneMutationSettings.CreateDefault().ProtectedGeneIds;
                if(repaired.id==original.id||repaired.entities.Count!=original.entities.Count||
                    repaired.entities.Any(entity=>required.Any(id=>!entity.genome.HasDefinition(id))))
                    throw new InvalidOperationException("修复后基因组或世界身份无效");
                string output=Path.Combine(folder,"repaired",repaired.id,"world.json");
                WorldSaveStore.Write(repaired,output);
                WorldSaveStore.Read(output);
                File.WriteAllText(Path.Combine(folder,"result.txt"),$"PASS\n{repaired.id}\n{entities}\n{genes}");
                EditorApplication.Exit(0);
            }
            catch(Exception error)
            {
                File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL\n"+error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        public static void RunBatch(){SessionState.SetBool(Key+".Water",false);Begin();}
        public static void RunWaterBatch(){SessionState.SetBool(Key+".Water",true);Begin();}
        private static void Begin(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);passed.Add(text);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"diagnostics.txt"),diagnostics.ToString());File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static string Route(LocomotionGeneLogic logic)
        {
            var route=typeof(LocomotionGeneLogic).GetField("_landRoute",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(logic);
            return string.Join(" ",new[]{"_pending","_failed","_cursor","_goal"}.Select(name=>name+"="+route.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(route)));
        }
        private static IEnumerator Run()
        {
            if(SessionState.GetBool(Key+".Water",false)){yield return RunWater();yield break;}
            var world=Object.FindFirstObjectByType<WorldSessionManager>();var clock=ServiceLocator.Get<SimulationClock>();
            var input=Path.Combine(Folder,"world-input.json");world.Open(WorldSaveStore.Read(input));
            double deadline=EditorApplication.timeSinceStartup+180;while(world.Busy){if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("world load");yield return null;}clock.Pause();
            var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var graph=planet.Data.Navigation;
            diagnostics.AppendLine("walkable="+graph.Walkable.Count(v=>v)+" regions="+graph.Regions.Max()+" scheduler="+(LandPathScheduler.Instance!=null));
            int inspected=0;foreach(var entity in EntityRegistry.All)
            {
                var genes=entity.Genes;float speed=genes.GetAttribute("moveSpeed");if(speed<=0)continue;
                if(inspected++>=12)break;var direction=planet.transform.InverseTransformPoint(entity.transform.position).normalized;
                var body=entity.GetComponent<Rigidbody>();
                diagnostics.AppendLine("existing speed="+speed+" dry="+graph.Dry(direction)+" closest="+graph.Closest(direction)+" kinematic="+body.isKinematic+" immobilized="+EcologyMotion.Immobilized(genes)+" suppression="+genes.EnvironmentGeneSuppression);
            }
            var reproduction=new Genome();reproduction.Add(GeneInstance.Create("gene_reproduction",50,0));
            var result=GeneMutator.Mutate(reproduction,new GeneMutationSettings{ErrorWeight=1,DuplicateWeight=0,DeletionWeight=0,MaxErrorDelta=0});
            diagnostics.AppendLine("single base after error="+string.Join(",",reproduction.Genes.Select(g=>g.DefinitionId))+" result="+result.Success);
            Check(reproduction.HasDefinition("gene_reproduction"),"唯一基础繁殖基因不能通过出错变异丢失");
            foreach(var definition in GeneDatabase.All.Where(d=>GeneMutationSettings.IsProtected(d.Id)))
            {
                var one=new Genome();one.Add(GeneInstance.Create(definition.Id,50,definition.Tier));
                Check(!GeneMutator.Mutate(one,new GeneMutationSettings{ErrorWeight=0,DuplicateWeight=0,DeletionWeight=1}).Success&&one.HasDefinition(definition.Id),"唯一基础基因禁止删除："+definition.Id);
            }
            var copies=new Genome();copies.Add(GeneInstance.Create("gene_reproduction",50,0));copies.Add(GeneInstance.Create("gene_reproduction",50,0));
            Check(GeneMutator.Mutate(copies,new GeneMutationSettings{ErrorWeight=1,DuplicateWeight=0,DeletionWeight=0,MaxErrorDelta=0}).Success&&copies.Genes.Count(g=>g.DefinitionId=="gene_reproduction")==1&&copies.HasDefinition(MultipleBirthEvolutionUpgrade.GeneId),"多副本仍允许出错演化且保留最后基础副本");
            var fallback=GeneMutator.CreateBaseGenome();Check(GeneMutationSettings.CreateDefault().ProtectedGeneIds.All(fallback.HasDefinition),"默认兜底基因组包含全部必要基础基因");
            var action=new Genome();action.Add(GeneInstance.Create("gene_locomotion",50,0));GeneMutator.Mutate(action,new GeneMutationSettings{ErrorWeight=1,DuplicateWeight=0,DeletionWeight=0,MaxErrorDelta=0});
            Check(action.HasDefinition("gene_territory"),"行动根仍可按配置演化为领地，不误用树根标记禁止合法演化");
            var randomState=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(601116);var evolving=GeneMutator.CreateBaseGenome();var settings=GeneMutationSettings.CreateDefault();
                for(int i=0;i<3000;i++){GeneMutator.Mutate(evolving,settings);if(!settings.ProtectedGeneIds.All(evolving.HasDefinition))throw new InvalidOperationException("必要基础基因在随机变异轮次丢失："+i);}
                Check(true,"连续3000次90/5/5变异仍保留必要基础基因");
            }
            finally{UnityEngine.Random.state=randomState;}
            var probes=new List<DarwinEntity>();var origins=new List<Vector3>();
            foreach(string movement in new[]{"gene_locomotion","gene_territory","gene_population","autonomous"})
            {
                Vector3 point=default,destination=default;
                // Select separate, traversable interior corridors; physics separation and water must not decide the result.
                int offset=(probes.Count+1)*graph.Walkable.Length/5;
                for(int n=0;n<graph.Walkable.Length;n++)
                {int i=(n+offset)%graph.Walkable.Length;if(!graph.Walkable[i]||Mathf.Abs(graph.Directions[i].y)>.8f)continue;var p=graph.Points[i];var side=Vector3.Cross(p.normalized,Vector3.up).normalized;var across=Vector3.Cross(p.normalized,side);var target=(p+side*30).normalized;if(!graph.Segment(p.normalized,target)||!graph.Segment(p.normalized,(p-side*30).normalized)||!graph.Segment(p.normalized,(p+across*30).normalized)||!graph.Segment(p.normalized,(p-across*30).normalized))continue;point=planet.transform.TransformPoint(p);destination=planet.transform.TransformPoint(target*PlanetCoordinate.SurfaceRadius(planet.Data,target));break;}
                Check(point!=default,"存在可行走的独立内陆测试走廊："+movement);
                var genome=new Genome();foreach(string id in new[]{"gene_growth","gene_nutrition","gene_reproduction","gene_environment",movement=="autonomous"?"gene_locomotion":movement})genome.Add(GeneInstance.Create(id,50,GeneDatabase.Get(id).Tier));
                var entity=EntitySpawnCapture.Create(genome,point+EcologyQueries.Up(point),"Movement probe");
                entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;entity.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>().enabled=false;
                entity.Genes.EnvironmentGeneSuppression=0;entity.Genes.Recalculate();
                if(movement!="autonomous")Check(RtsOrderController.Ensure(entity).Issue(RtsOrderKind.Move,destination),"行动谱系接受合法陆地移动意图："+movement);
                else{var logic=entity.Genes.Genome.Genes.Select(entity.Genes.GetLogic).OfType<LocomotionGeneLogic>().First();typeof(LocomotionGeneLogic).GetField("_wanderDir",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(logic,(destination-point).normalized);typeof(LocomotionGeneLogic).GetField("_wanderTimer",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(logic,100f);}
                probes.Add(entity);origins.Add(entity.transform.position);
            }
            clock.SetTimeScale(1);clock.Play();double end=EditorApplication.timeSinceStartup+3;while(EditorApplication.timeSinceStartup<end)yield return null;clock.Pause();
            int moved=0;
            for(int i=0;i<probes.Count;i++)
            {
                var entity=probes[i];var genes=entity.Genes;var logic=genes.Genome.Genes.Select(genes.GetLogic).OfType<LocomotionGeneLogic>().First();
                float distance=Vector3.ProjectOnPlane(entity.transform.position-origins[i],EcologyQueries.Up(origins[i])).magnitude;
                diagnostics.AppendLine("probe "+i+" displacement="+distance+" speed="+genes.GetAttribute("moveSpeed")+" primary="+genes.Genome.Genes.Any(g=>genes.GetLogic(g)==logic&&GeneBehaviorAuthority.IsPrimary<LocomotionGeneLogic>(genes,g))+" velocity="+entity.GetComponent<Rigidbody>().linearVelocity+" "+Route(logic));
                if(distance>.5f)moved++;
            }
            Check(moved==4,"三种行动谱系与无输入自主行动均在真实地形和物理中实际产生切向位移");
            var original=WorldSaveStore.Read(input);string unchanged=JsonUtility.ToJson(original);
            var repaired=WorldBaseGeneRepair.CreateCopy(original,out int repairedEntities,out int addedGenes);
            Check(JsonUtility.ToJson(original)==unchanged&&repaired.id!=original.id,"历史修复只生成新世界副本，原始对象和身份不改写");
            Check(repaired.entities.Count==original.entities.Count&&repaired.gameTime==original.gameTime&&repaired.seed==original.seed,"修复副本保留实体总数、世界日期和地形种子");
            Check(original.entities.SelectMany(e=>e.genome.Genes.Select(g=>(e.ecology.id,g))).All(item=>JsonUtility.ToJson(repaired.entities.Find(e=>e.ecology.id==item.id).genome.Find(item.g.InstanceId))==JsonUtility.ToJson(item.g)),"修复保留全部既有基因实例和数值，只添加缺失项");
            repaired.running=false;WorldSaveStore.Write(repaired);
            world.Open(repaired);deadline=EditorApplication.timeSinceStartup+180;while(world.Busy){if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("repaired load");yield return null;}clock.Pause();
            Check(EntityRegistry.All.Count==original.entities.Count&&EntityRegistry.All.All(e=>GeneMutationSettings.CreateDefault().ProtectedGeneIds.All(e.Genes.Genome.HasDefinition)),"修复副本真实读档后全部必要基础基因保留，包括锁定分工实体");
            world.SaveNow(false);WorldSaveStore.Write(WorldSaveStore.Read(WorldSaveStore.FileFor(repaired.id)),Path.Combine(Folder,"repaired-world.json"));
            diagnostics.AppendLine("repair world="+repaired.id+" entities="+repairedEntities+" added="+addedGenes);
        }
        // ★ 新增：用当前世界海水及完整环境/代谢 Update 验证伤害和实际摘要文本，不只反射调用扣血函数。
        private static IEnumerator RunWater()
        {
            var world=Object.FindFirstObjectByType<WorldSessionManager>();var clock=ServiceLocator.Get<SimulationClock>();
            world.Open(WorldSaveStore.Read(Path.Combine(Folder,"world-input.json")));double deadline=EditorApplication.timeSinceStartup+180;
            while(world.Busy){if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("water world load");yield return null;}clock.Pause();
            var existing=EntityRegistry.All.Select(e=>new{entity=e,hazard=e.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>(),stats=e.GetComponent<EntityStats>()}).Where(e=>e.hazard!=null&&e.hazard.IsInWater).ToArray();
            var initialHealth=existing.Select(e=>{e.stats.TryGet("health",out float hp);return hp;}).ToArray();
            clock.SetTimeScale(1);clock.Play();double existingUntil=EditorApplication.timeSinceStartup+2;while(EditorApplication.timeSinceStartup<existingUntil)yield return null;clock.Pause();
            for(int i=0;i<existing.Length;i++){var e=existing[i];if(e.entity==null)continue;e.stats.TryGet("health",out float hp);diagnostics.AppendLine("existingWater id="+EcologyIdentity.Of(e.entity)+" before="+initialHealth[i]+" after="+hp+" immersion="+e.hazard.ImmersionDepth+" active="+e.hazard.isActiveAndEnabled+" dormant="+DarwinPark.Entities.Simulation.EntityWorkScheduler.Dormant(e.entity.Genes)+" status="+e.hazard.GetStatusLine());}
            diagnostics.AppendLine("existingWaterCount="+existing.Length);
            var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var terrain=planet.Data;
            Vector3 direction=default;float depth=0;
            foreach(var dir in terrain.Navigation.Directions)
            {var coordinate=PlanetCoordinate.AtLocal(terrain,dir*planet.WaterRadius);if(coordinate.WaterDepth>depth){depth=coordinate.WaterDepth;direction=dir;}}
            Check(depth>4,"当前世界存在真实深水海域");
            var point=planet.transform.TransformPoint(direction*(PlanetCoordinate.WaterSurfaceRadius(terrain,direction)-2));
            var genome=GeneMutator.CreateBaseGenome();genome.Add(GeneInstance.Create("gene_repair",50,GeneDatabase.Get("gene_repair").Tier));
            var entity=EntitySpawnCapture.Create(genome,point,"Water probe");var body=entity.GetComponent<Rigidbody>();body.isKinematic=true;Physics.SyncTransforms();
            var hazard=entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>();var stats=entity.GetComponent<EntityStats>();
            diagnostics.AppendLine("water depth="+depth+" immersion="+hazard.ImmersionDepth+" coordinate="+hazard.Geography.Immersion+" geographicWater="+hazard.Geography.IsWater+" transform="+entity.transform.position);
            Check(entity.Genes.GetAttribute("swimSpeed")<=0,"测试实体没有游泳能力");stats.TryGet("health",out float before);
            Check(hazard.GetStatusLine().Contains("溺水"),"尚未推进伤害也能立即显示当前入水风险");
            yield return null;stats.TryGet("health",out float pausedHealth);Check(pausedHealth==before,"暂停查看风险不会推进生命损耗");
            clock.SetTimeScale(1);clock.Play();double until=EditorApplication.timeSinceStartup+2;while(EditorApplication.timeSinceStartup<until)yield return null;clock.Pause();
            stats.TryGet("health",out float after);diagnostics.AppendLine("health before="+before+" after="+after+" inwater="+hazard.IsInWater+" drowning="+hazard.IsDrowning+" status="+hazard.GetStatusLine()+" enabled="+hazard.enabled+" dormant="+DarwinPark.Entities.Simulation.EntityWorkScheduler.Dormant(entity.Genes));
            Check(hazard.IsDrowning&&after<before,"非游泳实体由真实Update持续溺水扣血，修复不能抵消");
            Check(hazard.GetStatusLine().Contains("溺水"),"海中明确显示溺水状态");
            var summary=new GameObject("WaterSummary",typeof(RectTransform));var refresh=EntitySummaryView.Build(summary.transform,entity);refresh();
            Check(summary.transform.Find("Environment").GetComponent<UnityEngine.UI.Text>().text.Contains("溺水"),"实体摘要实际文本显示溺水");Object.Destroy(summary);
            // ★ 新增：中心露出水面的浅浸没也必须受伤；入水后不能继续使用陆地休眠代表的安全状态。
            entity.transform.position=planet.transform.TransformPoint(direction*(PlanetCoordinate.WaterSurfaceRadius(terrain,direction)+.2f));Physics.SyncTransforms();
            stats.TryGet("health",out before);clock.Play();until=EditorApplication.timeSinceStartup+2;while(EditorApplication.timeSinceStartup<until)yield return null;clock.Pause();stats.TryGet("health",out after);
            diagnostics.AppendLine("shallow immersion="+hazard.ImmersionDepth+" center="+hazard.Geography.Immersion+" before="+before+" after="+after+" status="+hazard.GetStatusLine());
            Check(hazard.IsDrowning&&after<before,"中心露出水面但身体浸水仍持续扣血并显示溺水");
            entity.transform.position=planet.transform.TransformPoint(direction*(PlanetCoordinate.WaterSurfaceRadius(terrain,direction)+2));Physics.SyncTransforms();
            Check(!hazard.GetStatusLine().Contains("溺水"),"离水后状态立即清除，不残留上次伤害的溺水提示");
        }
    }
}
