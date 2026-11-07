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
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：真实场景验证锁帧持久化、原面板布局、阶段编辑、失效解释、尸体档案及大资源量查询复杂度。
    [InitializeOnLoad]public static class EntityInspectionFramePacingRegression
    {
        private const string Key="DarwinPark.InspectionFrameReview";private static bool _started,_backed,_hadLimit;private static int _savedLimit,_target,_vsync;
        private static readonly List<string> Passed=new();private static string Folder=>Path.GetFullPath("Artifacts/InspectionFrameReview");
        static EntityInspectionFramePacingRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Passed.Add(text);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            if(_backed){if(_hadLimit)PlayerPrefs.SetInt(FramePacingSettings.PreferenceKey,_savedLimit);else PlayerPrefs.DeleteKey(FramePacingSettings.PreferenceKey);PlayerPrefs.Save();FramePacingSettings.Reload();Application.targetFrameRate=_target;QualitySettings.vSyncCount=_vsync;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(Genome genome,Vector3 point)
        {
            DarwinEntity child=null;void Registered(DarwinEntity value)=>child=value;EntityRegistry.OnRegistered+=Registered;try{DarwinEntity.Spawner(RandomSpawnRecipe.Clone(genome),point,"详情回归");}finally{EntityRegistry.OnRegistered-=Registered;}
            child.Genes.mutationChance=0;child.GetComponent<EntityLifeCycle>().RestoreProgress(40);child.Genes.RestoreStageState(AgeState.Adult,3,2);child.Genes.Recalculate();child.GetComponent<Rigidbody>().isKinematic=true;return child;
        }
        private static IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;}
        private static IEnumerator Run()
        {
            _hadLimit=PlayerPrefs.HasKey(FramePacingSettings.PreferenceKey);_savedLimit=PlayerPrefs.GetInt(FramePacingSettings.PreferenceKey);_target=Application.targetFrameRate;_vsync=QualitySettings.vSyncCount;_backed=true;
            Check(FramePacingSettings.Choices.Contains(0)&&FramePacingSettings.Choices.Contains(30)&&FramePacingSettings.Choices.Contains(60),"锁帧选项包含不限制、30和60，来自资源配置");
            Check(FramePacingSettings.SetLimit(30)&&Application.targetFrameRate==30&&QualitySettings.vSyncCount==0,"30帧设置实际交给引擎，VSync不覆盖");
            FramePacingSettings.Save();FramePacingSettings.SetLimit(45);FramePacingSettings.Reload();Check(FramePacingSettings.Limit==30&&Application.targetFrameRate==30,"保存并重新读取恢复锁帧目标");
            QualitySettings.vSyncCount=1;FramePacingSettings.Apply();Check(QualitySettings.vSyncCount==0&&Application.targetFrameRate==30,"重新应用画质后可恢复锁帧");
            Check(!FramePacingSettings.SetLimit(-2)&&!FramePacingSettings.SetLimit(123)&&FramePacingSettings.Limit==30,"无效帧率不破坏有效设置");
            FramePacingSettings.SetLimit(0);Check(Application.targetFrameRate==-1&&QualitySettings.vSyncCount==0,"不限制模式恢复引擎无限制标记");
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=24;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("实体详情与锁帧",31277,.5f,20,44);double start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>90)throw new TimeoutException("创建世界");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);ColonyResource.ClearWorld();
            var genome=EcologyDefaults.Template("producer").CreateGenome();var origin=new Vector3(0,350,0);var entity=Spawn(genome,origin);var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(entity);yield return null;
            var root=ServiceLocator.Get<UIRoot>();var panel=root.MainCanvas.transform.Find("GenePanel").GetComponent<RectTransform>();var size=panel.sizeDelta;var position=panel.anchoredPosition;
            Check(panel.Find("ScrollView/Viewport/Content/BaseInfo/EntityEdit")!=null,"原摘要标题内提供独立编辑入口");int mutations=entity.Genes.StageMutationChecks;string original=JsonUtility.ToJson(entity.Genes.Genome);
            var popup=EntityEditPopup.Ensure();popup.Open(entity);yield return null;Object.FindFirstObjectByType<WorldHudInputLifecycle>().Recover();Check(popup.Visible&&GameInputGate.Blocked&&!clock.IsRunning,"实体弹窗合法门控与暂停不被输入恢复器清除");
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=new Vector2(Screen.width/2,10)},hits);Check(hits.Count>0&&hits[0].gameObject.name=="EntityEditOverlay","弹窗遮罩真正阻止底部栏点击穿透");
            foreach(var age in new[]{AgeState.Infant,AgeState.Adult,AgeState.Elder}){Check(popup.ApplyStage(age)&&entity.Genes.CurrentAge==age,"实体阶段实际改为 "+age);float expected=age==AgeState.Infant?0:age==AgeState.Adult?LifeCycleSettings.Load().adultProgress:LifeCycleSettings.Load().elderProgress;Check(entity.GetComponent<EntityLifeCycle>().Progress==expected,"阶段和生命周期边界一致 "+age);}
            Check(entity.Genes.StageMutationChecks==mutations&&JsonUtility.ToJson(entity.Genes.Genome)==original,"阶段编辑不额外变异或改写遗传数据");
            Check(popup.ApplyStat("health",65)&&entity.GetComponent<EntityStats>().TryGet("health",out var hp)&&hp==65,"生命值通过同一权威入口应用");
            Check(!popup.ApplyStat("health",101)&&!popup.ApplyStat("size",2)&&!popup.ApplyStat("nutrient",float.NaN),"数值越界、未开放字段和非有限值被拒绝");
            WorldAuthority.CanEdit=false;Check(!popup.ApplyStage(AgeState.Infant)&&!popup.ApplyStat("health",50),"只读成员不能更改阶段或数值");WorldAuthority.CanEdit=true;
            Check(!LanEditActions.Submit(new LanCommand{kind="entityStage",entity=EcologyIdentity.Of(entity),count=99}),"伪造生命阶段被执行入口拒绝");
            yield return Capture("entity-edit");popup.Close();yield return null;Check(!GameInputGate.Blocked&&panel.sizeDelta==size&&panel.anchoredPosition==position,"关闭编辑恢复输入且原详情布局不变");
            var growth=entity.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_growth");growth.Value=5;entity.Genes.Recalculate();Check(GeneFailureReasons.Explain(entity.Genes.Genome,entity.Genes.GetEvaluation(growth)).Any(x=>x.Contains("阈值")),"低于阈值的失效原因有实际数值");
            var reproduction=entity.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_reproduction");reproduction.Enabled=false;entity.Genes.Recalculate();inspector.ShowEntityInfo(entity);yield return null;Check(panel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("已禁用")),"禁用基因仍在检查列表中并明确原因");
            var suppressed=GeneValueResolver.Resolve(entity.Genes.Genome,false,20,additionalSuppression:new Dictionary<string,float>{{growth.DefinitionId,30}}).Genes.First(g=>g.Gene==growth);var reasons=GeneFailureReasons.Explain(entity.Genes.Genome,suppressed);Check(reasons.Any(x=>x.Contains("环境"))&&reasons.Any(x=>x.Contains("角色"))&&reasons.Any(x=>x.Contains("有效值")),"环境、角色和有效值计算分别列出");
            var id=EcologyIdentity.Of(entity);session.SaveNow(false);var saved=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(saved.entities.Single(e=>e.ecology.id==id).age==AgeState.Elder&&saved.entities.Single(e=>e.ecology.id==id).lifeProgress==LifeCycleSettings.Load().elderProgress,"修改阶段真实落盘并通过存档校验");
            int living=EntityRegistry.All.Count;Check(EntityStateEditor.SetStat(entity,"health",0),"生命值归零沿真实死亡结算");yield return null;var corpse=ColonyResource.All.First(r=>r.State.kind==ColonyResourceKind.Corpse);Check(corpse.State.source?.genome.Genes.Count==genome.Genes.Count&&corpse.State.source.age==AgeState.Elder,"新尸体保留真实生前基因和生命阶段");
            WorldResourceInspector.Ensure().Show(corpse);yield return null;Check(inspector.CurrentCorpse==corpse&&EntityRegistry.All.Count==living-1,"尸体使用原检查器且不注册伪活体");
            Check(panel.sizeDelta==size&&panel.anchoredPosition==position&&panel.Find("ScrollView/Viewport/Content/BaseInfo/Stat_health")!=null,"尸体与正常实体使用同一面板尺寸、位置及摘要列");
            Check(panel.GetComponentsInChildren<InputField>().All(f=>f.readOnly||!f.interactable)&&panel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("已死亡")),"尸体明确已死亡，基因和名称为只读");
            WorldResourceInspector.Instance.Refresh();Check(inspector.CurrentCorpse==corpse,"原资源公共刷新入口安全委托统一尸体面板");
            WorldResourceInspector.Instance.Hide();Check(inspector.CurrentCorpse==null&&!panel.gameObject.activeSelf,"原资源关闭入口同样关闭统一尸体面板");WorldResourceInspector.Instance.Show(corpse);yield return null;
            Check(panel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("已禁用")),"尸体也能查看生前禁用基因");yield return Capture("corpse-panel");
            var captured=corpse.Capture();string file=Path.Combine(Folder,"corpse-roundtrip.json");File.WriteAllText(file,JsonUtility.ToJson(captured));var read=JsonUtility.FromJson<ColonyResourceSave>(File.ReadAllText(file));CorpseEntityRecord.Validate(read.source);Check(read.source.age==AgeState.Elder&&read.source.genome.Genes.Any(g=>!g.Enabled),"尸体完整档案实际写盘读取");
            Check(CorpseEntityRecord.Normalize(new CorpseEntityRecord())==null,"旧档全零内联档案按未记录兼容");bool rejected=false;try{CorpseEntityRecord.Validate(new CorpseEntityRecord{version=1});}catch(InvalidDataException){rejected=true;}Check(rejected,"损坏的新尸体档案不会被当作旧档静默忽略");
            inspector.HideEntityInfo();var capture=RuntimePerformanceCapture.Instance;Check(capture!=null,"实际菜单安装按需性能采样组件");capture.sampleSeconds=.3f;capture.Request();start=EditorApplication.timeSinceStartup;while(string.IsNullOrEmpty(capture.ReportPath)){if(EditorApplication.timeSinceStartup-start>8)throw new TimeoutException(capture.Status);yield return null;}var report=JsonUtility.FromJson<PerformanceReport>(File.ReadAllText(capture.ReportPath));Check(report.frames>0&&report.metrics.Any(m=>m.name=="Scheduler")&&!PerformanceTimings.Capturing,"按需采样完成实际报告并释放采样状态");
            var founder=EcologyDefaults.Template("ant_founder").CreateGenome();founder.Genes.RemoveAll(g=>g.DefinitionId=="gene_reproduction");var queenEntity=Spawn(founder,origin);var queen=AntColonyMember.Ensure(queenEntity.GetComponent<PopulationBehavior>());queen.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));var workerEntity=Spawn(founder,origin+Vector3.right*.5f);var worker=AntColonyMember.Ensure(workerEntity.GetComponent<PopulationBehavior>());worker.Assign(AntRole.Worker,queen.GroupId);
            workerEntity.GetComponent<EntityStats>().Feed(100);for(int i=0;i<1500;i++){ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),kind=ColonyResourceKind.Corpse,energy=2,scale=Vector3.one,local=EcologyQueries.ToLocal(origin+Vector3.right*(150+i))});if(i%100==99)yield return null;}
            var near=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),kind=ColonyResourceKind.Corpse,energy=2,scale=Vector3.one,local=EcologyQueries.ToLocal(workerEntity.transform.position+Vector3.forward*.25f)});worker.State.scan=0;
            var raised=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),kind=ColonyResourceKind.Corpse,energy=2,scale=Vector3.one,local=EcologyQueries.ToLocal(workerEntity.transform.position+EcologyQueries.Up(workerEntity.transform.position)*AntColonySettings.Current.transportSearchRadius*2)});
            long old=ColonyActivityRange.EnumerationCount;var expectedResource=ColonyResource.All.Where(r=>ColonyCorpseAccess.Available(r,worker)&&ColonyActivityRange.Contains(worker.Population,r.transform.position)&&EcologyQueries.Distance(worker.transform.position,r.transform.position)<=AntColonySettings.Current.transportSearchRadius).OrderBy(r=>(r.transform.position-worker.transform.position).sqrMagnitude).FirstOrDefault();long oldBuilds=ColonyActivityRange.EnumerationCount-old;
            old=ColonyActivityRange.EnumerationCount;AntColonyWorker.Advance(worker,.1f);long newBuilds=ColonyActivityRange.EnumerationCount-old;
            Check(expectedResource==near&&worker.State.resourceTarget==near.State.id,"1500远处尸体下目标与旧球面资格和最近距离排序一致");Check(oldBuilds>=1500&&newBuilds<=2,"一次工匠搜索的活动圆构建从逐尸体降为至多两次");
            AntColonyWorker.Release(worker);foreach(var resource in ColonyResource.All.ToArray())if(resource!=raised)resource.Remove();worker.State.scan=0;AntColonyWorker.Advance(worker,.1f);
            Check(worker.State.carriedResource==raised.State.id,"仅有不同高度的合法尸体时仍按原球面距离领取，未用弦长误排除");
            Check(EntityStateEditor.SetStage(workerEntity,AgeState.Infant)&&string.IsNullOrEmpty(worker.State.carriedResource)&&string.IsNullOrEmpty(worker.State.resourceTarget)&&!raised.HasLiveClaim,"运输成员改回幼年时释放货物和领取，不遗留无法完成的任务");
        }
    }
}
