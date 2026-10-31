using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：覆盖单群和多群的属性/行为热路径，并记录耗时与分配，不能用少量手动角色验收代替规模验证。
    [InitializeOnLoad]
    public static class PopulationPerformanceRegression
    {
        private const string Key="DarwinPark.PopulationPerformance",BaselineKey=Key+".Baseline",SuiteKey=Key+".Suite";
        private static bool _started;
        private static readonly List<string> Results=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/PopulationPerformanceReview");
        static PopulationPerformanceRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&Object.FindFirstObjectByType<WorldSessionManager>()!=null){_started=true;Object.FindFirstObjectByType<WorldSessionManager>().StartCoroutine(Guard());}};}
        public static void RunBaseline(){SessionState.SetBool(BaselineKey,true);RunBatch();}
        // ★ 修复（防Bug回归）：独立验证窗口失焦后仍须推进实际模拟，否则后台等待会误以为种群逻辑卡死。
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        public static void RunAllBatch(){SessionState.SetBool(SuiteKey,true);RunBatch();}
        private static DarwinEntity Spawn(int index,int groups,bool wolf=false)
        {
            var genome=new Genome();foreach(var id in new[]{"gene_growth","gene_nutrition","gene_reproduction","gene_population"})genome.Add(GeneInstance.Create(id,id=="gene_population"?(wolf?80:20):50,GeneDatabase.Get(id).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,new Vector3(index%groups*120,340,index/groups*2),"性能验证");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;entity.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);entity.Genes.RestoreStageState(AgeState.Adult,3,2);
            string group=Groups[index%groups];if(wolf)entity.GetComponent<PopulationBehavior>().JoinColony(group,index<groups);else entity.GetComponent<AntColonyMember>().Assign(index<groups?AntRole.Queen:index%2==0?AntRole.Soldier:AntRole.Worker,group);
            entity.GetComponent<PlanetGravity>().enabled=false;entity.GetComponent<Rigidbody>().useGravity=false;
            entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;return entity;
        }
        private static readonly List<string> Groups=new List<string>();
        private static void Clean(){foreach(var entity in EntityRegistry.All.ToArray()){entity.gameObject.SetActive(false);Object.DestroyImmediate(entity.gameObject);}ColonyResource.ClearWorld();}
        private static IEnumerator Guard()
        {
            yield return null;Directory.CreateDirectory(Folder);Results.Clear();string error=null;
            ServiceLocator.Get<SimulationClock>().Pause();var operation=SessionState.GetBool(SuiteKey,false)?RunAll():Run();
            while(true)
            {
                bool next=false;object value=null;try{next=operation.MoveNext();if(next)value=operation.Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);}
                if(error!=null||!next)break;yield return value;
            }
            string file=SessionState.GetBool(BaselineKey,false)?"baseline.txt":"result.txt";
            File.WriteAllText(Path.Combine(Folder,file),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Results));Clean();
            SessionState.SetBool(Key,false);Debug.Log("[PopulationPerformance] "+(error==null?"PASS":"FAIL"));EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator RunAll()
        {
            var performance=Run();while(performance.MoveNext())yield return performance.Current;
            Clean();ServiceLocator.Get<SimulationClock>().RestoreState(0,1);WorldRuntimeGate.Ready=false;GameInputGate.Blocked=true;
            string path="Artifacts/EntityPanelMotionReview/result.txt";Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,"RUNNING");EntityPanelMotionRegression.ValidateSuite();double began=EditorApplication.timeSinceStartup;
            while(File.ReadAllText(path)=="RUNNING"){if(EditorApplication.timeSinceStartup-began>300)throw new TimeoutException(path);yield return null;}
            if(!File.ReadAllText(path).StartsWith("PASS"))throw new InvalidOperationException(File.ReadAllText(path));Results.Add("PASS: entity panel/motion, states, social ecology, ant transport/reproduction/save compatibility suite");
        }
        private static IEnumerator Run()
        {
            foreach(var scenario in new[]{(count:10,groups:1),(count:100,groups:1),(count:100,groups:10)})
            {
                Clean();Groups.Clear();for(int i=0;i<scenario.groups;i++)Groups.Add(Guid.NewGuid().ToString("N"));
                var entities=new List<DarwinEntity>();for(int i=0;i<scenario.count;i++)entities.Add(Spawn(i,scenario.groups));
                // 先记录单次调度，过慢的基线不再多次叠加，确保诊断本身也可结束。
                var timer=Stopwatch.StartNew();long allocated=GC.GetAllocatedBytesForCurrentThread();
                foreach(var entity in entities)
                {
                    entity.GetComponent<PopulationBehavior>().Advance(.016f);
                    for(int read=0;read<5;read++){entity.Genes.GetAttribute("moveSpeed");entity.Genes.GetAttribute("attack");entity.GetComponent<AntColonyMember>().Destination(out _,out _,out _);}
                }
                timer.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
                string report=$"{scenario.count} entities / {scenario.groups} colonies: {timer.Elapsed.TotalMilliseconds:F3} ms, {bytes} bytes per dispatch";
                Results.Add(report);File.WriteAllText(Path.Combine(Folder,"progress.txt"),string.Join("\n",Results));
                if(timer.Elapsed.TotalSeconds>5){if(!SessionState.GetBool(BaselineKey,false))throw new InvalidOperationException(report);yield break;}
                yield return null;
                if(!SessionState.GetBool(BaselineKey,false))
                {
                    foreach(var entity in entities){entity.Genes.GetAttribute("moveSpeed");if(entity.GetComponent<AntColonyMember>().Queen==null)throw new InvalidOperationException("王后缺失");}
                    long builds=PopulationGroupIndex.BuildCount;
                    foreach(var entity in entities)for(int read=0;read<20;read++){entity.Genes.GetAttribute("moveSpeed");if(entity.GetComponent<AntColonyMember>().Queen==null)throw new InvalidOperationException("王后缺失");}
                    if(PopulationGroupIndex.BuildCount!=builds)throw new InvalidOperationException("稳定成员的重复属性读取仍在重建群体索引");
                    Results.Add("PASS: repeated movement/queen reads reuse group index");
                    var originalQueen=entities[0].GetComponent<AntColonyMember>();var successor=entities[scenario.groups].GetComponent<AntColonyMember>();string group=originalQueen.GroupId;
                    successor.Assign(AntRole.Queen,group);originalQueen.gameObject.SetActive(false);
                    if(successor.Queen!=successor)throw new InvalidOperationException("停用旧王后后缓存仍引用旧领袖");
                    originalQueen.Assign(AntRole.Worker,group);originalQueen.gameObject.SetActive(true);originalQueen.Population.Advance(.01f);
                    if(originalQueen.Queen!=successor)throw new InvalidOperationException("角色/激活变化未更新群体索引");
                    Results.Add("PASS: queen disable, role change and re-enable invalidate membership index");
                    var populationGene=successor.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_population");populationGene.Value=80;successor.Genes.Recalculate();
                    if(originalQueen.Population.GroupMembers.Contains(successor.Population)||originalQueen.Queen!=null)throw new InvalidOperationException("同帧改变种群模式后仍使用旧缓存");
                    populationGene.Value=20;successor.Genes.Recalculate();
                    if(originalQueen.Queen!=successor)throw new InvalidOperationException("同帧恢复种群表达后王后缓存未更新");
                    Results.Add("PASS: same-frame gene mode changes invalidate group/queen lookup");
                    var worker=entities.Select(e=>e.GetComponent<AntColonyMember>()).First(m=>m.Role==AntRole.Worker);
                    var plantGenome=new Genome();plantGenome.Add(GeneInstance.Create("gene_growth",50,0));var previous=EntityRegistry.All.ToArray();DarwinEntity.Spawner(plantGenome,worker.transform.position,"运输索引验证");var plant=EntityRegistry.All.Single(e=>!previous.Contains(e));string plantId=EcologyIdentity.Of(plant);
                    worker.State.livingTarget=plantId;worker.State.scan=1;worker.Advance(.01f);
                    if(!AntColonyWorker.IsCarried(plant))throw new InvalidOperationException("拾取活体后搬运索引未登记");
                    AntColonyWorker.Release(worker);
                    if(AntColonyWorker.IsCarried(plant))throw new InvalidOperationException("释放活体后搬运索引未清理");plant.gameObject.SetActive(false);Object.DestroyImmediate(plant.gameObject);
                    if(EcologyIdentity.Find(plantId)!=null)throw new InvalidOperationException("停用实体仍出现在 ID 索引");
                    Results.Add("PASS: live cargo claim/release and identity cleanup");
                    var cargoIndex=(System.Collections.IDictionary)typeof(AntColonyWorker).GetField("Cargo",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);int cargoBefore=cargoIndex.Count;
                    previous=EntityRegistry.All.ToArray();DarwinEntity.Spawner(plantGenome,worker.transform.position,"运输中销毁验证");var doomed=EntityRegistry.All.Single(e=>!previous.Contains(e));worker.State.livingTarget=EcologyIdentity.Of(doomed);worker.State.scan=1;worker.Advance(.01f);Object.DestroyImmediate(doomed.gameObject);worker.Advance(.01f);
                    if(cargoIndex.Count!=cargoBefore)throw new InvalidOperationException("Unity 假空对象导致运载物销毁后索引泄漏");AntColonyWorker.Release(worker);Results.Add("PASS: destroyed cargo does not retain worker/entity index entries");
                    // 必须实际推进模拟时钟，单纯观察暂停菜单的渲染帧无法证明种群逻辑不再卡顿。
                    foreach(int scale in scenario.count==10?new[]{1,16}:new[]{1})
                    {
                        var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.enabled=false;WorldRuntimeGate.Ready=true;
                        var clock=ServiceLocator.Get<SimulationClock>();float gameBefore=clock.GameTime;clock.SetTimeScale(scale);clock.Play();int frames=0;double began=EditorApplication.timeSinceStartup;
                        while(EditorApplication.timeSinceStartup-began<1){frames++;yield return null;}
                        clock.Pause();WorldRuntimeGate.Ready=false;menu.enabled=true;Results.Add($"Runtime {scale}x heartbeat: {frames} frames / {EditorApplication.timeSinceStartup-began:F2} s, game advanced {clock.GameTime-gameBefore:F2} s");
                        if(clock.GameTime-gameBefore<.1f)throw new InvalidOperationException("实时验证未推进实际模拟时钟");
                        if(entities.Count(e=>e!=null&&e.GetComponent<PopulationBehavior>().Active)!=scenario.count)throw new InvalidOperationException("规模验证中的种群成员没有保持实际生效");
                        if(frames<5)throw new InvalidOperationException("实时帧推进不足："+report);
                    }
                    foreach(var entity in entities)if(entity!=null&&entity.GetComponent<AntColonyMember>().Queen==null)throw new InvalidOperationException("种群缓存丢失王后");
                }
            }
            if(!SessionState.GetBool(BaselineKey,false))
            {
                Clean();Groups.Clear();for(int i=0;i<10;i++)Groups.Add(Guid.NewGuid().ToString("N"));
                var wolves=new List<DarwinEntity>();for(int i=0;i<100;i++)wolves.Add(Spawn(i,10,true));
                var timer=Stopwatch.StartNew();foreach(var entity in wolves){entity.GetComponent<PopulationBehavior>().Advance(.016f);for(int read=0;read<5;read++){entity.Genes.GetAttribute("moveSpeed");entity.Genes.GetAttribute("attack");}}timer.Stop();
                Results.Add($"100 wolves / 10 groups: {timer.Elapsed.TotalMilliseconds:F3} ms per dispatch");
                var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.enabled=false;WorldRuntimeGate.Ready=true;var clock=ServiceLocator.Get<SimulationClock>();float before=clock.GameTime;clock.SetTimeScale(1);clock.Play();int frames=0;double began=EditorApplication.timeSinceStartup;
                while(EditorApplication.timeSinceStartup-began<1){frames++;yield return null;}
                clock.Pause();menu.enabled=true;WorldRuntimeGate.Ready=false;
                if(frames<5||clock.GameTime-before<.1f||wolves.Any(w=>!w.GetComponent<PopulationBehavior>().Active||w.GetComponent<PopulationBehavior>().Leader==null))throw new InvalidOperationException("狼群规模验证失败");
                Results.Add($"PASS: 100 wolves active, {frames} frames / {EditorApplication.timeSinceStartup-began:F2} s, game advanced {clock.GameTime-before:F2} s");
            }
        }
    }
}
