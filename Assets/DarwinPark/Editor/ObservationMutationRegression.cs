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
    // ★ 新增：真实实体、磁盘读档、坐标边界和规则卡实际回调共同验收，避免只测独立抽样函数。
    [InitializeOnLoad]public static class ObservationMutationRegression
    {
        private const string Key="DarwinPark.ObservationMutation";private static bool _started;private static readonly List<string> Passed=new();private static string _config;
        private static string Folder=>Path.GetFullPath("Artifacts/ObservationMutationReview");
        static ObservationMutationRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(20,20,1920,1080);EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            if(_config!=null){File.WriteAllText(GameplayTuning.FilePath,_config);GameplayTuning.Reload();}MutationRoundController.Restore(null);
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Wait(Func<bool> ready){double start=EditorApplication.timeSinceStartup;while(!ready()){if(EditorApplication.timeSinceStartup-start>120)throw new TimeoutException("等待世界或界面");yield return null;}}
        private static void Clear(){foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);ColonyResource.ClearWorld();EntityPerceptionHub.Clear();MutationRoundController.Restore(null);}
        private static DarwinEntity Spawn(Vector3 point,params (string,float)[] genes)
        {
            var genome=new Genome();foreach(var gene in genes)genome.Add(GeneInstance.Create(gene.Item1,gene.Item2,GeneDatabase.Get(gene.Item1).Tier));DarwinEntity child=null;void Added(DarwinEntity value)=>child=value;EntityRegistry.OnRegistered+=Added;
            try{DarwinEntity.Spawner(genome,point,"观察轮次回归");}finally{EntityRegistry.OnRegistered-=Added;}if(child==null)throw new InvalidOperationException("生成失败");
            child.Genes.mutationChance=0;child.GetComponent<EntityLifeCycle>().RestoreProgress(40);child.Genes.RestoreStageState(AgeState.Adult,3,2);child.Genes.Recalculate();
            foreach(var component in child.GetComponents<MonoBehaviour>())if(component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component is TerritoryBehavior||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component.GetType().Name=="EntityAIController")component.enabled=false;
            child.GetComponent<Rigidbody>().isKinematic=true;return child;
        }
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=16;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=32;PlanetOreSystem.Instance.mapResolution=16;
            session.Create("观察与全局轮次回归",73161,.5f,20,44);yield return Wait(()=>!session.Busy);var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);Clear();_config=File.ReadAllText(GameplayTuning.FilePath);var tuning=GameplayTuning.Current;
            Check(Object.FindFirstObjectByType<MutationRoundController>()!=null,"全局轮次控制器实际挂在游戏根节点");
            typeof(MutationRoundController).GetField("_instance",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);MutationRoundController.Ensure(clock.gameObject);Check(Object.FindObjectsByType<MutationRoundController>(FindObjectsSortMode.None).Length==1,"静态引用重置后认领原计时器，不重复安装");
            Check(GeneDatabase.Get("gene_observation")?.States.Count==2&&GeneTreeStorage.Active.nodes.Any(n=>n.id=="gene_observation"),"观察基因两状态与默认树节点可用");
            var actors=new List<DarwinEntity>();for(int i=0;i<100;i++)actors.Add(Spawn(new Vector3(i*3,350,0),("gene_storage",50),("gene_repair",50),("gene_environment",50)));
            tuning.mutationRoundsEnabled=true;tuning.mutationPopulationFraction=.1f;tuning.mutationRoundSeconds=2;tuning.mutationAttemptsPerFrame=1000;UnityEngine.Random.InitState(8317);
            var genomes=actors.Select(e=>JsonUtility.ToJson(e.Genes.Genome)).ToArray();actors[0].Genes.TryStageMutation(AgeState.Elder);
            Check(JsonUtility.ToJson(actors[0].Genes.Genome)==genomes[0],"生命阶段只登记不再额外变异");
            GlobalMutationManager.Advance(1);Check(MutationRoundController.Rounds==0,"未到周期不抽样");GlobalMutationManager.Advance(1);
            Check(MutationRoundController.LastPopulation==100&&MutationRoundController.LastSelected==10&&MutationRoundController.Attempts==10,"100活体一轮仅随机选择10个身份");
            Check(actors.Where((e,i)=>JsonUtility.ToJson(e.Genes.Genome)!=genomes[i]).Count()==10,"实际10个不同实体基因改变，旧个体概率0不抵消全局名额");
            Check(actors.Skip(10).Where((e,i)=>JsonUtility.ToJson(e.Genes.Genome)!=genomes[i+10]).Any(),"随机选择不固定落在注册表前10个");
            long rounds=MutationRoundController.Rounds;GlobalMutationManager.Advance(0);WorldAuthority.IsReplica=true;GlobalMutationManager.Advance(5);WorldAuthority.IsReplica=false;Check(MutationRoundController.Rounds==rounds,"暂停和联机副本不推进轮次");
            MutationRoundController.Restore(null);tuning.mutationPopulationFraction=0;GlobalMutationManager.Advance(2);Check(MutationRoundController.LastSelected==0&&MutationRoundController.Attempts==0,"0比例不强行变异一个实体");
            MutationRoundController.Restore(null);tuning.mutationPopulationFraction=.5f;tuning.mutationAttemptsPerFrame=3;GlobalMutationManager.Advance(2);
            Check(MutationRoundController.LastSelected==50&&MutationRoundController.Attempts==3&&MutationRoundController.Queued==47,"每帧预算保留剩余47人而不丢名单");var state=MutationRoundController.Capture();
            Check(state.pending.Distinct().Count()==47,"持久化待执行名单不重复");session.SaveNow(false);var disk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(disk.mutationRounds.pending.SequenceEqual(state.pending),"待变异身份、计时和计数实际落盘");
            var bad=JsonUtility.FromJson<MutationRoundSave>(JsonUtility.ToJson(state));bad.pending.Add(bad.pending[0]);bool rejected=false;try{MutationRoundController.Validate(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"重复待变异名单在清场前拒绝");
            session.Open(disk);yield return Wait(()=>!session.Busy);clock.Pause();Check(MutationRoundController.Queued==47&&MutationRoundController.Attempts==3,"真正读档恢复剩余名单，不重新抽50人");
            tuning=GameplayTuning.Current;tuning.mutationRoundsEnabled=true;tuning.mutationAttemptsPerFrame=3;
            for(int i=0;i<16;i++)GlobalMutationManager.Advance(.01f);Check(MutationRoundController.Queued==0&&MutationRoundController.Attempts==50,"后续帧恰好完成原50个名额");
            var locked=Spawn(new Vector3(0,350,5),("gene_population",20),("gene_locomotion",50));var worker=locked.GetComponent<AntColonyMember>();worker.Assign(AntRole.Worker,Guid.NewGuid().ToString("N"));string lockedGenome=JsonUtility.ToJson(locked.Genes.Genome);MutationRoundController.Restore(null);tuning.mutationPopulationFraction=1;tuning.mutationAttemptsPerFrame=1000;GlobalMutationManager.Advance(2);
            Check(JsonUtility.ToJson(locked.Genes.Genome)==lockedGenome&&MutationRoundController.LastSelected<MutationRoundController.LastPopulation,"全局100%名额也不绕过工匠基因锁定");
            Clear();tuning.mutationRoundsEnabled=false;
            var here=new Vector3(0,350,0);var wide=Spawn(here,("gene_locomotion",50),("gene_observation",25),("gene_heterotrophy",100));var attention=Spawn(here+Vector3.forward,("gene_locomotion",50),("gene_observation",75),("gene_heterotrophy",100));var target=Spawn(here+Vector3.right*10,("gene_storage",50));var far=Spawn(here+Vector3.right*500,("gene_storage",50));
            Check(EntityPerceptionHub.Range(wide.Genes)>EntityPerceptionHub.Range(attention.Genes),"视野范围状态比注意力状态接收更大范围");
            Check(EntityPerceptionHub.Read(wide.Genes).Any(s=>s.entity==target)&&!EntityPerceptionHub.Read(wide.Genes).Any(s=>s.entity==far),"只向观察者投递视野内实体坐标");long searches=EntityPerceptionHub.Searches;for(int i=0;i<100;i++)EntityPerceptionHub.Read(wide.Genes);Check(EntityPerceptionHub.Searches==searches,"同帧100次行动子步复用一次收件箱");
            EntityPerceptionHub.TryLocate(wide.Genes,target,out var sampled);GlobalCoordinateRegression.ForceScan(attention.Genes);EntityPerceptionHub.TryLocate(attention.Genes,target,out var attentionSample);target.transform.position+=Vector3.right*3;EntityNeighborhood.ObservePosition(target.Genes);
            Check(EntityPerceptionHub.TryLocate(wide.Genes,target,out var retained)&&retained==sampled,"范围状态保持上次收到的采样坐标");Check(EntityPerceptionHub.TryLocate(attention.Genes,target,out var tracked)&&tracked==attentionSample,"Attention tracks the last delivered snapshot between scans");
            EntityPerceptionHub.Notice(attention.Genes,target);Check(EntityPerceptionHub.TryLocate(attention.Genes,target,out var noticed)&&noticed==target.transform.position,"Visible notices update attention without scanning the world");
            target.transform.position=here+Vector3.right*100;EntityNeighborhood.ObservePosition(target.Genes);GlobalCoordinateRegression.ForceScan(wide.Genes);GlobalCoordinateRegression.ForceScan(attention.Genes);Check(!EntityPerceptionHub.TryLocate(wide.Genes,target,out _)&&!EntityPerceptionHub.TryLocate(attention.Genes,target,out _),"目标离开视野后不能继续获得追踪坐标");
            target.transform.position=here+Vector3.right*10;EntityNeighborhood.ObservePosition(target.Genes);wide.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_observation").Enabled=false;wide.Genes.Recalculate();Check(EntityPerceptionHub.Range(wide.Genes)==0&&!EntityPerceptionHub.TryLocate(wide.Genes,target,out _),"禁用观察基因立即撤销视野信息");
            wide.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_observation").Enabled=true;wide.Genes.Recalculate();var newborn=Spawn(here+Vector3.right*5,("gene_storage",50));Check(EntityPerceptionHub.Read(wide.Genes).Any(s=>s.entity==newborn),"同帧新注册实体可进入坐标收件箱");newborn.gameObject.SetActive(false);Check(!EntityPerceptionHub.TryLocate(wide.Genes,newborn,out _),"实体停用后立即撤销检索资格");
            var legacy=Spawn(here+Vector3.forward*2,("gene_locomotion",50));Check(Mathf.Abs(EntityPerceptionHub.Range(legacy.Genes)-legacy.Genes.GetAttribute("senseRange"))<.001f,"旧实体无观察基因时保持原感知范围");
            for(int i=0;i<300;i++)Spawn(here+Vector3.right*(1000+i*3),("gene_storage",50));EntityPerceptionHub.Clear();long visits=EntityPerceptionHub.CandidateVisits;EntityPerceptionHub.Read(wide.Genes);Check(EntityPerceptionHub.CandidateVisits-visits<15&&EntityCoordinateGrid.CandidateVisits<30,"300个远处实体不进入行动候选或粗格距离判断");
            var found=PerceivedTargets.Nearest(wide.Genes,false,null,out var received);Check(found!=null&&received!=default,"行动选择实际收到的目标和坐标");
            int builds=EntityCoordinateGrid.Builds;yield return null;EntityCoordinateGrid.Query(here,50,new List<EntityCoordinate>());Check(EntityCoordinateGrid.Builds==builds,"跨帧坐标未变化时复用视野粗格，不因营养快照刷新重建");
            var raw=File.ReadAllText(GameplayTuning.FilePath);var edited=MutationRuleEdits.Edit(raw,true,.2f,12);Check(edited.Contains("//")&&GameplayTuning.Parse(edited).mutationPopulationFraction==.2f,"修改规则保留中文注释且数值正确");
            string missing="{\n//保留注释\n\"version\":1\n}";Check(GameplayTuning.Parse(MutationRuleEdits.Edit(missing,false,.3f,9)).mutationRoundSeconds==9,"旧外部文件缺字段时可补齐规则且保留注释");
            SimulationRulesPanel panel=null;yield return Wait(()=>{panel=Object.FindFirstObjectByType<SimulationRulesPanel>();return panel!=null&&panel.Card!=null;});yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(Folder,"layout.txt"),$"parent={panel.transform.parent.name} size={panel.Card.sizeDelta} configured={panel.cardWidth},{panel.cardHeight} root={panel.transform.parent.GetComponent<RectTransform>().rect} active={panel.isActiveAndEnabled}");
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"rules-layout.png"));yield return null;Check(panel.transform.parent.name=="Page1"&&panel.Card.sizeDelta==new Vector2(260,164),"规则控制卡位于第一页且与原卡片同尺寸："+panel.transform.parent.name+" "+panel.Card.sizeDelta);
            panel.Card.Find("Fraction").GetComponent<InputField>().text="20";panel.Card.Find("Seconds").GetComponent<InputField>().text="12";panel.Card.Find("ApplyRules").GetComponent<Button>().onClick.Invoke();Check(GameplayTuning.Current.mutationPopulationFraction==.2f&&GameplayTuning.Current.mutationRoundSeconds==12,"规则卡真实按钮保存并应用比例与周期");
            string saved=File.ReadAllText(GameplayTuning.FilePath);WorldAuthority.CanEdit=false;panel.Card.Find("Fraction").GetComponent<InputField>().text="99";panel.Apply();Check(File.ReadAllText(GameplayTuning.FilePath)==saved,"只读成员绕过按钮回调也不能写规则");WorldAuthority.CanEdit=true;panel.Reload();panel.Card.Find("Seconds").GetComponent<InputField>().text="0";panel.Apply();Check(File.ReadAllText(GameplayTuning.FilePath)==saved,"无效周期不破坏原配置");panel.Reload();
            var root=ServiceLocator.Get<UIRoot>().MainCanvas.transform;root.Find("BottomPanel/TabBar/ui_icon_next").GetComponent<Button>().onClick.Invoke();Check(!panel.gameObject.activeInHierarchy,"第二页不显示第一页规则卡");root.Find("BottomPanel/TabBar/ui_icon_prev").GetComponent<Button>().onClick.Invoke();Check(panel.gameObject.activeInHierarchy,"返回第一页规则卡可继续操作");
            MutationRuleEdits.Save(true,.1f,30);panel.Reload();Check(panel.Card.Find("Status").GetComponent<Text>().text.Contains("有效规则"),"重载有效规则后清除上一条输入错误提示");Object.FindFirstObjectByType<MapEntityStatisticsUI>().RefreshNow();Object.FindFirstObjectByType<MapEntityGeneFilterUI>().RefreshNow();clock.Pause();Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"rules-first-page.png"));yield return null;
        }
    }
}
