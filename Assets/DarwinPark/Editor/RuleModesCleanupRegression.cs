using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Simulation;
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
    // ★ 新增：实际生命周期、受保护货物、分帧清理磁盘读档及规则卡回调联合回归。
    [InitializeOnLoad]public static class RuleModesCleanupRegression
    {
        private const string Key="DarwinPark.RuleModesCleanup";private static bool _started;private static string _config;private static readonly List<string> Passed=new();private static string Folder=>Path.GetFullPath("Artifacts/RuleModesCleanupReview");
        static RuleModesCleanupRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));EditorApplication.EnterPlaymode();}
        private static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            if(_config!=null){File.WriteAllText(GameplayTuning.FilePath,_config);GameplayTuning.Reload();}MutationRoundController.Restore(null);CorpseCleanupController.Restore(null);
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Wait(Func<bool> ready){double start=EditorApplication.timeSinceStartup;while(!ready()){if(EditorApplication.timeSinceStartup-start>120)throw new TimeoutException("等待世界或规则卡");yield return null;}}
        private static DarwinEntity Spawn(params (string,float)[] values)
        {
            var genome=new Genome();foreach(var pair in values)genome.Add(GeneInstance.Create(pair.Item1,pair.Item2,GeneDatabase.Get(pair.Item1).Tier));DarwinEntity actor=null;void Added(DarwinEntity value)=>actor=value;EntityRegistry.OnRegistered+=Added;try{DarwinEntity.Spawner(genome,new Vector3(0,350,0),"规则模式回归");}finally{EntityRegistry.OnRegistered-=Added;}if(actor==null)throw new InvalidOperationException("实体未生成");actor.GetComponent<Rigidbody>().isKinematic=true;return actor;
        }
        private static ColonyResource Resource(ColonyResourceKind kind)=>ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),kind=kind,group=kind==ColonyResourceKind.Corpse?null:Guid.NewGuid().ToString("N"),energy=20,scale=Vector3.one,local=new Vector3(0,350,0)});
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=16;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=32;PlanetOreSystem.Instance.mapResolution=16;
            session.Create("变异模式清尸回归",88441,.5f,20,44);yield return Wait(()=>!session.Busy);var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);foreach(var actor in EntityRegistry.All.ToArray())Object.DestroyImmediate(actor.gameObject);ColonyResource.ClearWorld();_config=File.ReadAllText(GameplayTuning.FilePath);
            var tuning=GameplayTuning.Current;tuning.mutationRoundsEnabled=false;var entity=Spawn(("gene_storage",50),("gene_environment",50),("gene_repair",50));entity.Genes.RestoreStageState(AgeState.Infant,0,0);tuning.mutationRoundsEnabled=true;tuning.mutationMode=1;tuning.stageMutationProbability=1;tuning.stageEnvironmentBoost=0;long attempts=StageMutationPolicy.Attempts;
            entity.Genes.TryStageMutation(AgeState.Infant);entity.Genes.TryStageMutation(AgeState.Infant);Check(StageMutationPolicy.Attempts==attempts+1&&entity.Genes.StageMutationChecks==1,"阶段100%出生仅抽签一次，重复阶段去重");
            entity.Genes.TryStageMutation(AgeState.Adult);entity.Genes.TryStageMutation(AgeState.Elder);Check(StageMutationPolicy.Attempts==attempts+3,"成年、老年分别触发一次");
            MutationRoundController.Restore(new MutationRoundSave{elapsed=1,population=1,quota=1,selected=1,pending=new List<string>{EcologyIdentity.Of(entity)}});string timed=JsonUtility.ToJson(MutationRoundController.Capture());MutationRoundController.Advance(100);Check(JsonUtility.ToJson(MutationRoundController.Capture())==timed,"阶段模式冻结定时轮次及待变异身份");
            entity.Genes.RestoreStageState(AgeState.Adult,0,0);tuning.stageMutationProbability=0;entity.Genes.TryStageMutation(AgeState.Adult);Check(StageMutationPolicy.Attempts==attempts+3,"阶段0概率且环境加成0不变异");
            entity.Genes.RestoreStageState(AgeState.Elder,0,0);tuning.mutationMode=0;entity.Genes.TryStageMutation(AgeState.Adult);tuning.mutationMode=1;tuning.stageMutationProbability=1;entity.Genes.TryStageMutation(AgeState.Adult);Check(StageMutationPolicy.Attempts==attempts+3,"切换模式不补抽已经登记的阶段");
            entity.Genes.RestoreStageState(AgeState.Elder,0,0);WorldAuthority.IsReplica=true;entity.Genes.TryStageMutation(AgeState.Elder);WorldAuthority.IsReplica=false;Check(StageMutationPolicy.Attempts==attempts+3,"联机副本不执行阶段变异");
            var worker=Spawn(("gene_population",20),("gene_locomotion",50));worker.GetComponent<AntColonyMember>().Assign(AntRole.Worker,Guid.NewGuid().ToString("N"));worker.Genes.RestoreStageState(AgeState.Adult,0,0);worker.Genes.TryStageMutation(AgeState.Adult);Check(StageMutationPolicy.Attempts==attempts+3,"阶段变异保留工匠基因冻结限制");
            tuning.mutationRoundsEnabled=false;MutationRoundController.Restore(null);CorpseCleanupController.Restore(null);tuning.corpseCleanupEnabled=true;tuning.corpseCleanupSeconds=10;tuning.corpseCleanupPerFrame=1;
            Check(Object.FindFirstObjectByType<CorpseCleanupController>()!=null,"清理调度器实际安装到游戏根节点");var energy=Resource(ColonyResourceKind.Energy);var protectedCorpse=Resource(ColonyResourceKind.Corpse);var member=worker.GetComponent<AntColonyMember>();member.State.resourceTarget=protectedCorpse.State.id;Check(protectedCorpse.Claim(member),"工匠实际领取尸体");var first=Resource(ColonyResourceKind.Corpse);var second=Resource(ColonyResourceKind.Corpse);string firstId=first.State.id,secondId=second.State.id,protectedId=protectedCorpse.State.id,energyId=energy.State.id;
            CorpseCleanupController.Advance(9);Check(ColonyResource.Find(firstId)!=null&&CorpseCleanupController.Removed==0,"到达设定模拟周期前不清尸");CorpseCleanupController.Advance(1);Check(ColonyResource.Find(firstId)==null&&ColonyResource.Find(secondId)!=null&&CorpseCleanupController.Queued==1,"清理严格按每帧预算，不一帧销毁所有尸体");Check(ColonyResource.Find(protectedId)!=null&&ColonyResource.Find(energyId)!=null,"已领取尸体和能量块均保留");
            var saved=CorpseCleanupController.Capture();session.SaveNow(false);var disk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(disk.corpseCleanup.pending.SequenceEqual(saved.pending)&&disk.corpseCleanup.elapsed==saved.elapsed,"清理时间、计数及剩余身份实际写入磁盘");session.Open(disk);yield return Wait(()=>!session.Busy);clock.Pause();Check(CorpseCleanupController.Queued==1&&CorpseCleanupController.Removed==1,"真正读档恢复清理进度，不重新收集名单");
            tuning=GameplayTuning.Current;tuning.corpseCleanupEnabled=true;tuning.corpseCleanupSeconds=10;tuning.corpseCleanupPerFrame=1;string progress=JsonUtility.ToJson(CorpseCleanupController.Capture());CorpseCleanupController.Advance(0);WorldAuthority.IsReplica=true;CorpseCleanupController.Advance(100);WorldAuthority.IsReplica=false;tuning.corpseCleanupEnabled=false;CorpseCleanupController.Advance(100);Check(JsonUtility.ToJson(CorpseCleanupController.Capture())==progress,"暂停、关闭和联机副本均冻结清理进度");tuning.corpseCleanupEnabled=true;
            var restoredWorker=EntityRegistry.All.First(e=>e.GetComponent<AntColonyMember>()?.Role==AntRole.Worker).GetComponent<AntColonyMember>();restoredWorker.State.resourceTarget=secondId;Check(ColonyResource.Find(secondId).Claim(restoredWorker),"待清理快照中的尸体可在下一帧被领取");CorpseCleanupController.Advance(.1f);Check(ColonyResource.Find(secondId)!=null&&CorpseCleanupController.Queued==0&&CorpseCleanupController.Removed==1,"删除前重新检查领取，不吞工匠货物");
            saved=CorpseCleanupController.Capture();saved.pending.Add(secondId);saved.pending.Add(secondId);bool rejected=false;try{CorpseCleanupController.Validate(saved);}catch(InvalidDataException){rejected=true;}Check(rejected,"重复清理身份在加载清场前拒绝");CorpseCleanupController.Validate(null);CorpseCleanupController.Validate(new CorpseCleanupSave{version=0});Check(true,"旧档缺少清理快照仍可载入");
            var mesh=planet.transform.Find("CloudVolume").GetComponent<MeshFilter>().sharedMesh;Check(mesh.triangles.Length/3==12*SimulationOptimization.Current.weatherProxySubdivisions*SimulationOptimization.Current.weatherProxySubdivisions,"天气代理三角形数与配置一致，独立于地形精度");var vertices=mesh.vertices;bool outward=true;var indices=mesh.triangles;float radius=planet.GetComponent<PlanetWeatherVisuals>().cloudClearance+planet.GetComponent<PlanetWeatherVisuals>().cloudThickness+planet.WaterRadius;for(int i=0;i<indices.Length;i+=3){var a=vertices[indices[i]];var n=Vector3.Cross(vertices[indices[i+1]]-a,vertices[indices[i+2]]-a).normalized;outward&=Vector3.Dot(n,a)>=radius-.001f;}Check(outward,"所有天气代理平面外接解析球，不漏天空边缘");
            SimulationRulesPanel panel=null;yield return Wait(()=>{panel=Object.FindFirstObjectByType<SimulationRulesPanel>();return panel?.Card!=null;});panel.Reload();Check(panel.Card.sizeDelta==new Vector2(260,164),"规则卡保持原尺寸");panel.Card.Find("MutationMode").GetComponent<Button>().onClick.Invoke();panel.Card.Find("Fraction").GetComponent<InputField>().text="100";if(!GameplayTuning.Current.mutationRoundsEnabled)panel.Card.Find("MutationEnabled").GetComponent<Button>().onClick.Invoke();panel.Apply();Check(GameplayTuning.Current.mutationMode==1&&GameplayTuning.Current.stageMutationProbability==1,"真实按钮切换并保存阶段模式与概率");yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"stage-rules.png"));yield return null;
            panel.Card.Find("RuleList").GetComponent<Dropdown>().value=1;panel.Card.Find("Fraction").GetComponent<InputField>().text="12";if(!GameplayTuning.Current.corpseCleanupEnabled)panel.Card.Find("MutationEnabled").GetComponent<Button>().onClick.Invoke();panel.Apply();Check(GameplayTuning.Current.corpseCleanupEnabled&&GameplayTuning.Current.corpseCleanupSeconds==12&&File.ReadAllText(GameplayTuning.FilePath).Contains("//"),"清尸页输入不被开关覆盖，真实按钮保存周期且保留注释");panel.Card.Find("Fraction").GetComponent<InputField>().text="15";File.WriteAllText(GameplayTuning.FilePath,RuleSelectionEdits.MutationModes(File.ReadAllText(GameplayTuning.FilePath),true,1,.1f,.42f,30));panel.Apply();Check(GameplayTuning.Current.stageMutationProbability==.42f&&GameplayTuning.Current.corpseCleanupSeconds==15,"清尸页保存保留编辑期间外部更新的变异参数");string valid=File.ReadAllText(GameplayTuning.FilePath);panel.Card.Find("Fraction").GetComponent<InputField>().text="0";panel.Apply();Check(File.ReadAllText(GameplayTuning.FilePath)==valid,"无效清理周期不破坏配置");panel.Reload();WorldAuthority.CanEdit=false;panel.Card.Find("Fraction").GetComponent<InputField>().text="99";panel.Apply();WorldAuthority.CanEdit=true;Check(File.ReadAllText(GameplayTuning.FilePath)==valid,"只读成员不能绕过按钮直接修改规则");panel.Reload();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"cleanup-rules.png"));yield return null;
        }
    }
}
