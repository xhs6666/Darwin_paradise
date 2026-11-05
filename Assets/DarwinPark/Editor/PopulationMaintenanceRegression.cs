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
    // ★ 新增：真实场景验证两列来源、自动补充/繁殖调节、磁盘/联机以及同群身体从王后上方向侧面疏散。
    [InitializeOnLoad] public static class PopulationMaintenanceRegression
    {
        private const string Key="DarwinPark.PopulationMaintenanceReview";private static bool _started;
        private static readonly List<string> Passed=new();private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static string Folder=>Path.GetFullPath("Artifacts/PopulationMaintenanceReview");
        static PopulationMaintenanceRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);Passed.Add(text);File.WriteAllText(Path.Combine(Folder,"progress.txt"),string.Join("\n",Passed));}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool more=false;object current=null;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!more){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);Debug.Log("[PopulationMaintenance] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static void Clear(){foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);ColonyResource.ClearWorld();}
        private static DarwinEntity Spawn(Vector3 point,params (string id,float value)[] definitions)
        {
            var genome=new Genome();foreach(var d in definitions)genome.Add(GeneInstance.Create(d.id,d.value,GeneDatabase.Get(d.id).Tier));DarwinEntity result=null;void Added(DarwinEntity e)=>result=e;EntityRegistry.OnRegistered+=Added;try{DarwinEntity.Spawner(genome,point,"维持验证实体");}finally{EntityRegistry.OnRegistered-=Added;}
            result.Genes.mutationChance=0;result.Genes.RestoreStageState(AgeState.Adult,3,2);result.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);result.Genes.Recalculate();
            foreach(var component in result.GetComponents<MonoBehaviour>())if(component is EntityGeneController||component is DarwinPark.Entities.Vitals.EntityVitals||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component is TerritoryBehavior||component.GetType().Name=="EntityAIController")component.enabled=false;
            result.GetComponent<Rigidbody>().isKinematic=true;var stats=result.GetComponent<EntityStats>();stats.Modify("storedNutrient",100,false);return result;
        }
        private static ReproductionGeneLogic Logic(DarwinEntity e)=>e.Genes.Genome.Genes.Select(e.Genes.GetLogic).OfType<ReproductionGeneLogic>().First();
        // ★ 适配：切页后等待布局与动态字体字形准备完毕，再截取真实稳定画面。
        private static IEnumerator Capture(string name){yield return new WaitForSecondsRealtime(.5f);yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;}
        private static IEnumerator Wait(Func<bool> done,string reason){double start=EditorApplication.timeSinceStartup;while(!done()){if(EditorApplication.timeSinceStartup-start>20)throw new TimeoutException(reason);yield return null;}}
        private static IEnumerator Run()
        {
            yield return null;var session=Object.FindFirstObjectByType<WorldSessionManager>();var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var clock=ServiceLocator.Get<SimulationClock>();builder.subdivisions=24;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(builder));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("队形与维持比例验证",20261105,.5f,20,44);yield return Wait(()=>!session.Busy,"生成世界");clock.Pause();Clear();PopulationMaintenance.Restore(null);EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=0});
            Vector3 home=EcologyQueries.Surface(builder.transform.position+Vector3.up*(builder.radius+builder.heightScale));for(int i=0;i<192;i++){float y=1-2*(i+.5f)/192,angle=i*2.399963f;var d=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));var p=EcologyQueries.Surface(builder.transform.position+d*(builder.radius+builder.heightScale));if((p-builder.transform.position).magnitude>builder.WaterRadius+3){home=p;break;}}
            var control=Object.FindFirstObjectByType<SimulationControlPanel>();typeof(SimulationControlPanel).GetMethod("SwitchPage",Private).Invoke(control,new object[]{1});yield return null;yield return null;var panel=Object.FindFirstObjectByType<RandomSpawnPanel>();panel.transform.Find("Source").GetComponent<Button>().onClick.Invoke();var work=RandomSpawnWorkbench.Instance;
            Check(work.Visible&&GameInputGate.Blocked&&!clock.IsRunning,"选择实体实际打开两列来源窗口并暂停单人模拟");yield return VerifySourceUI(work);work.SetGene("gene_growth",true,true);work.SetGene("gene_locomotion",false,true);Check(work.Draft.method==RandomSpawnMethod.Genes,"基因条件自动选择独立随机方式");yield return Capture("two-column-source");
            var source=work.Draft;var signatures=new HashSet<string>();for(int i=0;i<80;i++){var genome=source.CreateGenome();CheckSource(genome,source);signatures.Add(string.Join("|",genome.Genes.Select(g=>g.DefinitionId+g.Value)));}Check(signatures.Count>20,"必有必无以外的基因和数值逐个随机，而非整批复制");
            work.SelectTemplate(null);Check(work.Draft.method==RandomSpawnMethod.Template&&work.Draft.CreateGenome().Genes.Count==RandomSpawnRecipe.DefaultGenome().Genes.Count,"模板方式与基因随机方式互斥，不附加随机基因");work.ApplySelection();Check(!work.Visible&&!GameInputGate.Blocked,"应用来源后恢复输入");
            var spawner=RandomPlanetSpawner.Ensure(clock.gameObject);Check(spawner.StartRecipe(source,8,true),"实际启动条件随机生成");yield return Wait(()=>!spawner.IsBusy,"条件随机生成");Check(EntityRegistry.All.Count==8&&EntityRegistry.All.All(e=>source.Matches(e.Genes.Genome)),"真实批次满足必有必无条件");yield return null;Check(EntityRegistry.All.All(e=>source.Matches(e.Genes.Genome)),"出生初始化不破坏明确生成条件");
            work.Open(panel);work.SetGene("gene_growth",true,true);work.SetGene("gene_locomotion",false,true);Check(work.SaveRule(30)&&WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id)).maintenance.rules.Single().source.included.Contains("gene_growth"),"两列界面可新增维持规则并实际保存");work.Close();PopulationMaintenance.Restore(null);Clear();
            var tuning=PopulationMaintenanceSettings.Current;int oldMin=tuning.minimumAdditionalGenes,oldMax=tuning.maximumAdditionalGenes;tuning.minimumAdditionalGenes=tuning.maximumAdditionalGenes=0;
            try
            {
                source=new RandomSpawnRecipe{method=RandomSpawnMethod.Genes,name="有生长无行动",included=new(){"gene_growth"},excluded=new(){"gene_locomotion"}};
                for(int i=0;i<10;i++)Spawn(home+Vector3.right*i*3,("gene_nutrition",20));var rule=new PopulationMaintenanceRule{id=Guid.NewGuid().ToString("N"),source=source,ratio=.3f};Check(PopulationMaintenance.Set(rule)&&PopulationMaintenance.Needed(rule)==5,"30%目标按新增后分母计算：10个不匹配实体应补5个");
                int before=EntityRegistry.All.Count;yield return new WaitForSecondsRealtime(.2f);Check(EntityRegistry.All.Count==before,"暂停时不自动补充实体");clock.Play();yield return Wait(()=>PopulationMaintenance.Count(rule)>=5&&!spawner.IsBusy,"自动补充到目标比例");clock.Pause();Check(EntityRegistry.All.Count==15&&PopulationMaintenance.Count(rule)==5,"不足时实际自动生成到目标比例而非只改计数");
                Object.DestroyImmediate(EntityRegistry.All.First(e=>source.Matches(e.Genes.Genome)).gameObject);clock.Play();yield return Wait(()=>PopulationMaintenance.Count(rule)==5&&!spawner.IsBusy,"死亡后补足");clock.Pause();Check(EntityRegistry.All.Count==15,"匹配成员死亡后重新计算并补充");
                Object.DestroyImmediate(EntityRegistry.All.First(e=>source.Matches(e.Genes.Genome)).gameObject);EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=14});float resume=clock.GameTime;clock.Play();yield return Wait(()=>clock.GameTime>resume+1.2f,"数量上限检查");clock.Pause();Check(EntityRegistry.All.Count==14&&PopulationMaintenance.Count(rule)==4,"自动补充遵守世界实体上限");EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=0});clock.Play();yield return Wait(()=>PopulationMaintenance.Count(rule)==5&&!spawner.IsBusy,"释放数量上限后补充");clock.Pause();Check(EntityRegistry.All.Count==15,"数量空位恢复后自动继续维持");
                WorldAuthority.IsReplica=true;Check(!PopulationMaintenance.Set(new PopulationMaintenanceRule{id=Guid.NewGuid().ToString("N"),source=source}),"联机副本不能本地修改维持规则");WorldAuthority.IsReplica=false;WorldAuthority.CanEdit=false;Check(!spawner.StartRecipe(source,2,true)&&!PopulationMaintenance.Remove(rule.id),"普通成员不能绕过生成或维持规则编辑权限");WorldAuthority.CanEdit=true;
                session.SaveNow(false);var disk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(disk.maintenance.rules.Single().ratio==.3f&&disk.maintenance.rules.Single().source.excluded.Contains("gene_locomotion"),"维持来源、条件和比例实际写盘读取");var frame=LanWorldSnapshot.Frame(1);Check(frame.maintenance.rules.Single().source.method==RandomSpawnMethod.Genes,"联机快照携带维持规则");
                var invalid=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(disk));invalid.maintenance.rules[0].ratio=1;bool rejected=false;try{WorldSaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"无法有限补足的100%比例在清场前拒绝");invalid=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(disk));invalid.maintenance.rules[0].source.included.Add("missing-gene");rejected=false;try{WorldSaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"未知基因维持规则在清场前拒绝");
                session.Open(disk);yield return Wait(()=>!session.Busy,"维持规则重载");clock.Pause();Check(PopulationMaintenance.Rules.Count==1&&PopulationMaintenance.Count(PopulationMaintenance.Rules[0])==5,"真实重载恢复规则和目标计数");
                panel=Object.FindFirstObjectByType<RandomSpawnPanel>();panel.transform.Find("Maintain").GetComponent<Button>().onClick.Invoke();yield return Capture("maintenance-rules");RandomSpawnWorkbench.Instance.Close();Clear();
                var parent=Spawn(home,("gene_growth",50),("gene_reproduction",20));var logic=Logic(parent);float reserve=parent.GetComponent<EntityStats>().CaptureValues().Single(v=>v.id=="storedNutrient").value;logic.OnUpdate(.1f);Check(!parent.Genes.IsGestating&&logic.CaptureState().maintenanceWait>0&&parent.GetComponent<EntityStats>().CaptureValues().Single(v=>v.id=="storedNutrient").value==reserve,"超量时延迟新孕育，不扣储粮或删除亲本");
                session.SaveNow(false);Check(WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id)).entities.Single().reproduction.Single().maintenanceWait>0,"降低繁殖的等待进度成对保存");logic.OnUpdate(tuning.extraBreedingDelaySeconds*4);Check(parent.Genes.IsGestating,"超量降低繁殖频率而非永久禁止繁殖");rule=PopulationMaintenance.Rules.Single();var zero=JsonUtility.FromJson<PopulationMaintenanceRule>(JsonUtility.ToJson(rule));zero.ratio=0;PopulationMaintenance.Set(zero);before=EntityRegistry.All.Count;logic.OnUpdate(logic.CaptureState().gestation+1);Check(EntityRegistry.All.Count==before+1,"维持比例不取消已经开始的孕育");Clear();
                zero.ratio=.3f;PopulationMaintenance.Set(zero);var royal=Spawn(home,("gene_growth",50),("gene_population",20),("gene_reproduction",20));var queen=AntColonyMember.Ensure(royal.GetComponent<PopulationBehavior>());queen.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));AntColonyReproduction.Advance(queen,.1f);Check(!royal.Genes.IsGestating&&queen.State.maintenanceWait>0,"王后同样应用维持比例的繁殖间隔");AntColonyReproduction.Advance(queen,tuning.extraBreedingDelaySeconds*4);Check(royal.Genes.IsGestating,"王后超量时降低频率但仍可正常开始孕育");
                var legacy=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(disk));legacy.maintenance=null;session.Open(legacy);yield return Wait(()=>!session.Busy,"旧档无维持字段重载");clock.Pause();Check(PopulationMaintenance.Rules.Count==0,"旧档缺维持字段不继承上个世界的自动生成规则");
                Clear();for(int i=0;i<10;i++)Spawn(home+Vector3.right*i*3,("gene_nutrition",20));var partitionA=new PopulationMaintenanceRule{id=Guid.NewGuid().ToString("N"),source=source,ratio=.333f};var partitionB=new PopulationMaintenanceRule{id=Guid.NewGuid().ToString("N"),source=new RandomSpawnRecipe{method=RandomSpawnMethod.Genes,name="无生长有营养",included=new(){"gene_nutrition"},excluded=new(){"gene_growth"}},ratio=.667f};Check(PopulationMaintenance.Set(partitionA)&&PopulationMaintenance.Set(partitionB),"可同时保存互斥的33.3%和66.7%维持规则");clock.Play();yield return Wait(()=>PopulationMaintenance.Count(partitionA)>=5&&!spawner.IsBusy,"互斥比例分区补充");int stable=EntityRegistry.All.Count;float stableTime=clock.GameTime;yield return Wait(()=>clock.GameTime>stableTime+3,"分区稳定检查");clock.Pause();Check(EntityRegistry.All.Count==stable,"互斥完整比例按一个实体精度稳定，不因分母变化交替无限补充");var impossible=JsonUtility.FromJson<PopulationMaintenanceRule>(JsonUtility.ToJson(partitionB));impossible.ratio=.8f;Check(!PopulationMaintenance.Set(impossible)&&PopulationMaintenance.Rules.Single(r=>r.id==partitionB.id).ratio==.667f,"互斥比例总和超过100%被拒绝并保留原规则");PopulationMaintenance.Restore(null);
            }
            finally{tuning.minimumAdditionalGenes=oldMin;tuning.maximumAdditionalGenes=oldMax;WorldAuthority.CanEdit=true;WorldAuthority.IsReplica=false;}
            yield return VerifyFormation(home,clock);clock.Pause();PopulationMaintenance.Set(new PopulationMaintenanceRule{id=Guid.NewGuid().ToString("N"),source=new RandomSpawnRecipe{method=RandomSpawnMethod.Genes,name="清理验证",included=new(){"gene_growth"}}});Check(WorldEntityActions.ClearAll()&&PopulationMaintenance.Rules.Count==1&&PopulationMaintenance.Rules.All(r=>!r.enabled),"清除全部实体会停用维持规则，不会立即重新生成");
        }
        // ★ 新增：通过真实Canvas输入控件、按钮和JSON文件验证焦点/筛选/互斥/自动列出，而不是仅调用字符串筛选函数。
        private static IEnumerator VerifySourceUI(RandomSpawnWorkbench work)
        {
            Transform root=ServiceLocator.Get<UIRoot>().MainCanvas.transform.Find("RandomSpawnWorkbenchBlocker/RandomSpawnWorkbench");
            InputField Search()=>root.GetComponentsInChildren<InputField>().Single(f=>f.name=="GeneSearch");
            Button Named(string name)=>root.GetComponentsInChildren<Button>().Single(b=>b.name==name);
            var search=Search();search.Select();search.ActivateInputField();yield return null;
            foreach(char character in "生长")search.ProcessEvent(new Event{type=EventType.KeyDown,character=character});
            Check(search.text=="生长"&&search.isFocused,"原生搜索框连续输入中文并保持焦点");yield return null;
            Check(root.GetComponentsInChildren<Text>().Where(t=>t.name=="GeneLabel").All(t=>t.text.Contains("生长")),"搜索输入即时过滤真实基因行");
            search.ProcessEvent(Event.KeyboardEvent("backspace"));Check(search.text=="生","搜索退格可正常编辑而不丢失输入");
            search.text="GENE_GROWTH";yield return null;Check(root.GetComponentsInChildren<Text>().Count(t=>t.name=="GeneLabel")==1,"基因编号搜索不区分大小写");search.text="";yield return null;
            Named("Include_gene_growth").onClick.Invoke();Named("Exclude_gene_growth").onClick.Invoke();
            Check(work.Draft.excluded.Contains("gene_growth")&&!work.Draft.included.Contains("gene_growth"),"主题有无按钮真实点击互斥");
            Named("Include_gene_growth").onClick.Invoke();Named("Exclude_gene_locomotion").onClick.Invoke();
            Check(Named("Include_gene_growth").image.sprite==UIVisualTheme.SurfaceSprite("selected"),"选中按钮使用游戏统一主题表面");
            var name=root.GetComponentsInChildren<InputField>().Single(f=>f.name=="ConditionName");string conditionName="生长条件自测"+Guid.NewGuid().ToString("N").Substring(0,6);string externalName=conditionName+"外改";name.text=conditionName;Named("SaveCondition").onClick.Invoke();yield return null;
            var saved=RandomSpawnPresetStore.Load().Single(p=>p.source.name==conditionName);string path=Path.Combine(RandomSpawnPresetStore.DirectoryPath,saved.id+".json");
            Check(File.Exists(path)&&File.ReadAllText(path).Contains("gene_growth")&&saved.source.excluded.Contains("gene_locomotion"),"保存条件按钮实际生成独立可读JSON并成对加载");
            var bytes=File.ReadAllBytes(path);Check(!(bytes.Length>2&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191),"条件JSON为无BOM的UTF-8");
            var edited=JsonUtility.FromJson<RandomSpawnPresetStore.Preset>(File.ReadAllText(path));edited.source.name=externalName;File.WriteAllText(path,JsonUtility.ToJson(edited,true));
            work.Close();var owner=Object.FindFirstObjectByType<RandomSpawnPanel>();work.Open(owner,true);yield return null;root=ServiceLocator.Get<UIRoot>().MainCanvas.transform.Find("RandomSpawnWorkbenchBlocker/RandomSpawnWorkbench");
            var sources=work.MaintenanceSources();Check(sources.Any(r=>r.name=="默认实体")&&EntityTemplateManager.Templates.All(t=>sources.Any(r=>r.name==t.templateName))&&sources.Any(r=>r.name==externalName),"重新打开后自动列出默认模板、所有模板和外部编辑的条件");
            Check(PopulationMaintenance.Rules.Count==0&&root.GetComponentsInChildren<Text>().Any(t=>t.name=="Conditions"&&t.text.Contains("必有：")&&t.text.Contains("必无：")),"自动列出包含控制基因说明但不会自动启动全部规则");
            root.GetComponentsInChildren<ScrollRect>().Single(r=>r.name=="MaintenanceSources").verticalNormalizedPosition=0;yield return Capture("automatic-maintenance-sources");
            Transform ConditionRow()=>root.GetComponentsInChildren<Text>().Single(t=>t.name=="Name"&&t.text==externalName).transform.parent;
            var conditionRow=ConditionRow();conditionRow.GetComponentsInChildren<InputField>().Single(f=>f.name=="Ratio").text="25";conditionRow.GetComponentsInChildren<Button>().Single(b=>b.name=="ApplyRatio").onClick.Invoke();yield return null;
            Check(PopulationMaintenance.Rules.Count==1&&PopulationMaintenance.Rules.Single().ratio==.25f&&PopulationMaintenance.Rules.Single().source.included.Contains("gene_growth"),"自动列出条件可直接设定比例并启用真实规则");
            ConditionRow().GetComponentsInChildren<Button>().Single(b=>b.name=="RuleEnabled").onClick.Invoke();yield return null;Check(!PopulationMaintenance.Rules.Single().enabled,"自动列表原生按钮可停用规则");
            ConditionRow().GetComponentsInChildren<Button>().Single(b=>b.name=="DeleteRule").onClick.Invoke();yield return null;Check(PopulationMaintenance.Rules.Count==0&&work.MaintenanceSources().Any(r=>r.name==externalName),"移除规则保留JSON来源供再次启用");
            work.Close();work.Open(owner);yield return null;root=ServiceLocator.Get<UIRoot>().MainCanvas.transform.Find("RandomSpawnWorkbenchBlocker/RandomSpawnWorkbench");
            var dropdown=root.GetComponentsInChildren<Dropdown>().Single(d=>d.name=="SavedConditions");int index=dropdown.options.FindIndex(o=>o.text==externalName);dropdown.value=index;yield return null;
            Check(work.Draft.included.Contains("gene_growth")&&work.Draft.excluded.Contains("gene_locomotion"),"已存条件在生成来源中可选且恢复控制基因");
        }
        private static void CheckSource(Genome genome,RandomSpawnRecipe source)
        {if(!source.Matches(genome)||genome.Genes.Count==0)throw new InvalidOperationException("受约束随机基因组违反生成条件");}
        private static IEnumerator VerifyFormation(Vector3 home,SimulationClock clock)
        {
            Clear();string group=Guid.NewGuid().ToString("N");var queen=Spawn(home,("gene_growth",50),("gene_population",20));var q=AntColonyMember.Ensure(queen.GetComponent<PopulationBehavior>());q.Assign(AntRole.Queen,group);var workers=new List<AntColonyMember>();
            for(int i=0;i<8;i++){var e=Spawn(home+EcologyQueries.Up(home)*(i+1)*.8f,("gene_growth",50),("gene_population",20),("gene_locomotion",50));var m=AntColonyMember.Ensure(e.GetComponent<PopulationBehavior>());m.Assign(AntRole.Worker,group);workers.Add(m);}
            var destinations=workers.Select(m=>{m.Destination(out var p,out _,out _);return p;}).ToArray();Check(destinations.All(p=>EcologyQueries.Distance(p,home)>1)&&destinations.Distinct().Count()==8,"休息工匠获得不同地表站位，不以王后身体为终点");
            var initial=workers.Select(m=>m.transform.position).ToArray();foreach(var m in workers){var desired=ColonyFormation.Avoid(m.Genes,Vector3.zero,5);Check(Mathf.Abs(Vector3.Dot(desired,EcologyQueries.Up(m.transform.position)))<.001f&&desired.sqrMagnitude>0,"同轴堆叠成员产生切向避让而非向上吸附");}
            yield return null;var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;clock.Play();
            try
            {
                foreach(var e in EntityRegistry.All){e.GetComponent<Rigidbody>().isKinematic=false;e.GetComponent<PlanetGravity>().enabled=true;}yield return null;
                for(int step=0;step<300;step++)
                {
                    foreach(var m in workers){EntityNeighborhood.ObservePosition(m.Genes);m.Advance(.02f);var gene=m.Genes.Genome.Genes.First(g=>m.Genes.GetLogic(g) is LocomotionGeneLogic&&GeneBehaviorAuthority.IsPrimary<LocomotionGeneLogic>(m.Genes,g));m.Genes.GetLogic(gene).OnUpdate(.02f);}
                    foreach(var e in EntityRegistry.All)typeof(PlanetGravity).GetMethod("FixedUpdate",Private).Invoke(e.GetComponent<PlanetGravity>(),null);Physics.SyncTransforms();Physics.Simulate(.02f);if(step%5==0)yield return null;
                }
                Check(workers.Count(m=>EcologyQueries.Distance(m.transform.position,queen.transform.position)>1.2f)>=6,"真实刚体模拟后大多数堆叠成员走离王后头顶");
            }
            finally{Physics.simulationMode=mode;clock.Pause();}
            Check(ColonyFormation.TryBirthPosition(q,out var birth)&&EntityRegistry.All.All(e=>EcologyQueries.Distance(e.transform.position,birth)>=2),"新生位置避开现有成员身体占位");Camera.main.GetComponent<OrbitCamera>().FocusOn(queen.transform.position);yield return new WaitForSecondsRealtime(1.2f);File.WriteAllText(Path.Combine(Folder,"formation-positions.txt"),string.Join("\n",workers.Select(m=>m.Id+" distance="+EcologyQueries.Distance(m.transform.position,queen.transform.position)+" position="+m.transform.position)));yield return Capture("colony-spread");
        }
    }
}
