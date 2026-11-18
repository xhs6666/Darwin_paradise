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
using DarwinPark.Entities.Simulation;
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：读取用户世界的隔离副本，对照暂停/运行开销，不覆盖用户存档或降低画质。
    [InitializeOnLoad] public static class EntityOptimizationRegression
    {
        private const string Key="DarwinPark.EntityOptimization";
        private static bool started;
        private static string Folder=>Path.GetFullPath("Artifacts/EntityOptimizationReview");
        static EntityOptimizationRegression(){EditorApplication.update+=()=>{if(!started&&SessionState.GetBool(Key,false)&&Application.isPlaying&&LanSession.Instance!=null){started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch()
        {
            string project=Application.dataPath.Replace('\\','/');
            if(!project.Contains("/Artifacts/StandaloneServerValidation/Project/Assets")&&!project.Contains("/Artifacts/GeneRiverReview/Project/Assets"))throw new InvalidOperationException("只允许隔离验证项目");
            Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            ConfigureView();EditorApplication.EnterPlaymode();
        }
        internal static void ConfigureView()
        {
            // ★ 修复（防Bug回归）：没有Game视口的batchmode不提交相机渲染，不可拿其无DrawCall结果声称恢复游戏帧率。
            var assembly=typeof(UnityEditor.Editor).Assembly;var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
            var get=sizesType.GetMethod("GetGroup");var group=get.Invoke(sizes,new[]{Enum.Parse(get.GetParameters()[0].ParameterType,"Standalone")});var sizeType=assembly.GetType("UnityEditor.GameViewSize");var mode=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{Enum.Parse(mode,"FixedResolution"),(object)1920,1080,"EntityOptimization1080p"},null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,count-1);view.Show();view.Focus();
        }
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error);SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Sample(string name)
        {
            yield return new WaitForSecondsRealtime(1);double began=EditorApplication.timeSinceStartup,previous=began;using var sample=new PerformanceSampleSession();
            while(EditorApplication.timeSinceStartup-began<5){yield return null;double now=EditorApplication.timeSinceStartup;sample.RecordFrame((now-previous)*1000);previous=now;}
            var report=sample.Finish(name+" entities="+EntityRegistry.All.Count+" screen="+Screen.width+"x"+Screen.height+" meshBuilds="+EntityBodyView.MeshBuilds+" meshReuses="+EntityBodyView.MeshReuses+" dormant="+Object.FindFirstObjectByType<EntityWorkScheduler>().DormantMembers+" atlasUploads="+Object.FindFirstObjectByType<PlanetGlobeMinimap>().AtlasUploads);
            File.WriteAllText(Path.Combine(Folder,name+".json"),JsonUtility.ToJson(report,true));
        }
        private static IEnumerator Run()
        {
            var data=WorldSaveStore.Read(Path.Combine(Folder,"input-world.json"));var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;
            session.Open(data);while(session.Busy)yield return null;var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();FramePacingSettings.SetLimit(0);
            var camera=Camera.main;camera.GetComponent<OrbitCamera>().RestoreView(data.cameraPosition,data.cameraRotation,data.cameraDistance);
            string label=System.Environment.GetCommandLineArgs().Contains("-baseline")?"baseline":"optimized";
            if (!System.Environment.GetCommandLineArgs().Contains("-mobile-stress"))
            {
            yield return Sample(label+"-paused");UserFrameHierarchyAudit.Start();yield return new WaitForSecondsRealtime(2);UserFrameHierarchyAudit.Finish(Folder);
            clock.Play();yield return Sample(label+"-running");clock.Pause();
            }
            if(System.Environment.GetCommandLineArgs().Contains("-mobile-stress"))
            {
                yield return SampleActiveAnimals(camera,clock);
                yield break;
            }
            if(System.Environment.GetCommandLineArgs().Contains("-stress"))
            {
                // ★ 新增：同一批1000个活体、同一画质与镜头，对照关闭/打开静止批处理；仅测试副本禁用新增出生以稳定样本规模。
                Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);GameplayTuning.Current.mutationRoundsEnabled=false;
                foreach(var actor in EntityRegistry.All.ToArray())Object.Destroy(actor.gameObject);yield return null;
                var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var land=planet.Data.Vertices.Where(p=>p.magnitude>planet.WaterRadius+4).OrderByDescending(p=>Vector3.Dot(p.normalized,SunSystem.LightDirection)).First();Vector3 origin=planet.transform.TransformPoint(land);var radial=(origin-planet.transform.position).normalized;var right=Vector3.Cross(radial,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;var forward=Vector3.Cross(right,radial).normalized;
                var source=GeneMutator.CreateBaseGenome();camera.GetComponent<OrbitCamera>().FocusOn(origin);camera.GetComponent<OrbitCamera>().enabled=false;camera.transform.SetPositionAndRotation(origin+radial*90,Quaternion.LookRotation(-radial,forward));
                for(int i=0;i<1000;i++)
                {
                    var location=EcologyQueries.Surface(origin+right*((i%32)-16)*1.6f+forward*((i/32)-16)*1.6f)+radial;
                    var actor=EntitySpawnCapture.Create(DarwinPark.Genetics.Genome.Copy(source),location,"千实体植物样本");actor.GetComponent<Rigidbody>().isKinematic=true;actor.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;actor.Genes.ApplyEnvironmentPressure(0,0,"gene_environment",50);
                    foreach(var gene in actor.Genes.Genome.Genes)if(actor.Genes.GetLogic(gene) is ReproductionGeneLogic logic)typeof(ReproductionGeneLogic).GetField("_cooldownTimer",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(logic,100000f);
                    if(i%50==49)yield return null;
                }
                bool merge=GameplayTuning.Current.mergeStationaryEntities;GameplayTuning.Current.mergeStationaryEntities=false;clock.Play();yield return Sample("stress-1000-unmerged");
                GameplayTuning.Current.mergeStationaryEntities=true;yield return new WaitForSecondsRealtime(GameplayTuning.Current.mergeQuietSeconds+2);yield return Sample("stress-1000-merged");clock.Pause();GameplayTuning.Current.mergeStationaryEntities=merge;
            }
        }

        // ★ 新增：主动移动样本与静止批处理分开，测试实际刚体、寻路、感知和画面开销。
        private static IEnumerator SampleActiveAnimals(Camera camera,SimulationClock clock)
        {
            var tuning=GameplayTuning.Current;
            tuning.mutationRoundsEnabled=false;
            PopulationMaintenance.Restore(null);
            Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;
            foreach(var actor in EntityRegistry.All.ToArray()) Object.Destroy(actor.gameObject);
            yield return null;
            var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var nav=planet.Data.Navigation;
            var region=nav.Regions.Where(id=>id>0).GroupBy(id=>id).OrderByDescending(group=>group.Count()).First().Key;
            int center=Enumerable.Range(0,nav.Regions.Length).Where(i=>nav.Regions[i]==region)
                .OrderByDescending(i=>nav.Points[i].magnitude).First();
            Vector3 local=nav.Points[center], origin=planet.transform.TransformPoint(local);
            var radial=(origin-planet.transform.position).normalized;
            var right=Vector3.Cross(radial,Vector3.up).normalized;
            if(right.sqrMagnitude<.1f) right=Vector3.right;
            var forward=Vector3.Cross(right,radial).normalized;
            camera.GetComponent<OrbitCamera>().enabled=false;
            camera.transform.SetPositionAndRotation(origin+radial*110,Quaternion.LookRotation(-radial,forward));
            var source=GeneMutator.CreateBaseGenome();
            foreach(string id in new[]{"gene_locomotion","gene_nervous_system","gene_observation"})
                if(!source.HasDefinition(id)) source.Add(DarwinPark.Genetics.GeneInstance.Create(id,65,0));
            for(int i=0;i<1000;i++)
            {
                var point=EcologyQueries.Surface(origin+right*((i%32)-16)*2.1f+forward*((i/32)-16)*2.1f)+radial;
                var actor=EntitySpawnCapture.Create(DarwinPark.Genetics.Genome.Copy(source),point,"千实体陆行动物样本");
                foreach(var gene in actor.Genes.Genome.Genes)
                    if(actor.Genes.GetLogic(gene) is ReproductionGeneLogic logic)
                        typeof(ReproductionGeneLogic).GetField("_cooldownTimer",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(logic,100000f);
                if(i%50==49) yield return null;
            }
            yield return Sample("mobile-1000-paused");
            clock.Play();
            yield return Sample("mobile-1000-running");
            UserFrameHierarchyAudit.Start();
            yield return new WaitForSecondsRealtime(2);
            UserFrameHierarchyAudit.Finish(Folder);
            clock.Pause();
        }
    }
}
