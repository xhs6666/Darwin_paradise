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
    // ★ 新增：验证活跃优化数值等价、缓存失效、未知扩展回退及真实列表/整卡悬浮交互。
    [InitializeOnLoad]public static class ActiveControlsRegression
    {
        private const string Key="DarwinPark.ActiveControls";private static bool _started;private static readonly List<string> Passed=new();private static string Folder=>Path.GetFullPath("Artifacts/ActiveControlsRegression");
        static ActiveControlsRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));EditorApplication.EnterPlaymode();}
        private static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(params (string,float)[] values)
        {
            var genome=new Genome();foreach(var pair in values)genome.Add(GeneInstance.Create(pair.Item1,pair.Item2,GeneDatabase.Get(pair.Item1).Tier));DarwinEntity actor=null;void Added(DarwinEntity value)=>actor=value;EntityRegistry.OnRegistered+=Added;try{DarwinEntity.Spawner(genome,new Vector3(0,350,0),"活跃优化回归");}finally{EntityRegistry.OnRegistered-=Added;}actor.GetComponent<Rigidbody>().isKinematic=true;actor.GetComponent<EntityLifeCycle>().RestoreProgress(40);actor.Genes.CurrentAge=AgeState.Adult;actor.Genes.Recalculate();return actor;
        }
        private sealed class Callback:IGeneLogic
        {public Action action;public int calls;public void Initialize(EntityGeneController owner,GeneInstance gene){}public void OnUpdate(float dt){calls++;action?.Invoke();}public string GetTooltipInfo()=>"";}
        private static List<IGeneLogic> Logics(EntityGeneController owner)=>((Dictionary<GeneInstance,IGeneLogic>)typeof(EntityGeneController).GetField("_activeLogics",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner)).Values.ToList();
        private static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(instance);
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=16;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=32;PlanetOreSystem.Instance.mapResolution=16;session.Create("活跃与交互验证",88641,.5f,20,44);while(session.Busy)yield return null;
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);GameplayTuning.Current.mutationRoundsEnabled=false;foreach(var actor in EntityRegistry.All.ToArray())Object.DestroyImmediate(actor.gameObject);ColonyResource.ClearWorld();
            var entity=Spawn(("gene_growth",50),("gene_repair",50),("gene_storage",50),("gene_nutrition",50),("gene_nutrition",45),("gene_reproduction",45));yield return null;
            var genes=entity.Genes;var optimization=SimulationOptimization.Current;var initial=EntitySaveState.Capture(entity);var stats=entity.GetComponent<EntityStats>();stats.Modify("nutrient",-8,false);var depleted=stats.CaptureValues();var oldStage=genes.CurrentAge;
            optimization.activeExecutionPlan=false;optimization.activeComponentReuse=false;for(int i=0;i<5;i++)genes.AdvanceSimulation(.5f);var expected=stats.CaptureValues();string expectedReproduction=JsonUtility.ToJson(new ReproductionList{values=EntitySaveState.Capture(entity).reproduction});float progress=entity.GetComponent<EntityLifeCycle>().Progress;
            initial.Restore(entity);stats.RestoreValues(depleted);genes.CurrentAge=oldStage;optimization.activeExecutionPlan=true;optimization.activeComponentReuse=true;long skipped=ActiveSimulationPlan.Skipped,hits=ActiveSimulationPlan.ComponentHits;for(int i=0;i<5;i++)genes.AdvanceSimulation(.5f);
            Check(JsonUtility.ToJson(new StatList{values=stats.CaptureValues()})==JsonUtility.ToJson(new StatList{values=expected}),"优化开关前后生命、营养、储存及所有属性逐值一致");Check(JsonUtility.ToJson(new ReproductionList{values=EntitySaveState.Capture(entity).reproduction})==expectedReproduction,"孕育、冷却、配偶等待计时与原入口一致");Check(expected.First(v=>v.id=="nutrient").value>depleted.First(v=>v.id=="nutrient").value,"等价样本确实产生了营养收入");Check(Mathf.Abs(entity.GetComponent<EntityLifeCycle>().Progress-progress)<.00001f,"成长时间逐小步等价");Check(ActiveSimulationPlan.Skipped>skipped,"真实推进跳过空行为与重复生产调用");Check(ActiveSimulationPlan.ComponentHits>hits,"真实推进命中批内组件复用");
            optimization.activeExecutionPlan=false;optimization.activeComponentReuse=false;genes.Recalculate();long built=ActiveSimulationPlan.Builds;genes.AdvanceSimulation(.05f);Check(ActiveSimulationPlan.Builds==built,"两项关闭后直接回退原快照，不维护执行计划");long reads=ActiveSimulationPlan.ComponentReads;Check(ActiveSimulationBatch.Read<EntityStats>(genes)==stats&&ActiveSimulationPlan.ComponentReads==reads,"批次外原生查询不增加优化计数");optimization.activeExecutionPlan=true;optimization.activeComponentReuse=true;
            foreach(var recipe in new[]{new[]{("gene_nutrition",50f)},new[]{("gene_nutrition",20f)},new[]{("gene_nutrition",80f)},new[]{("gene_nutrition",50f),("gene_locomotion",70f)},new[]{("gene_nutrition",50f),("gene_heterotrophy",75f)},new[]{("gene_nutrition",50f),("gene_scavenging",70f)}})
            {
                var classified=Spawn(recipe);Check(EcologyEnergyBudget.IsProducer(classified.Genes)==OriginalProducer(classified.Genes),"生产资格与完整规则等价："+string.Join("/",recipe.Select(x=>x.Item1+":"+x.Item2)));Check(EcologyMobility.CanMove(classified.Genes)==(classified.Genes.GetAttribute(EcologyBalanceSettings.Current.movementAttribute)>0),"移动资格未改变："+string.Join("/",recipe.Select(x=>x.Item1)));
                classified.Genes.OnEvaluationChanged+=_=>Check(EcologyEnergyBudget.IsProducer(classified.Genes)==OriginalProducer(classified.Genes),"重算事件期间评估引用失效，与原资格一致");classified.Genes.Genome.Genes[0].Value=80;classified.Genes.Recalculate();
                var cached=(System.Collections.IDictionary)typeof(ProducerClassification).GetField("Entries",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);classified.gameObject.SetActive(false);Check(!cached.Contains(classified.Genes),"注销立即清除规则分类，即使空间快照尚未收录");bool flashError=false;void Error(string text,string stack,LogType type){if(type==LogType.Error&&text.Contains("Coroutine"))flashError=true;}Application.logMessageReceived+=Error;try{classified.GetComponent<EntityStats>().Modify("health",-1);}finally{Application.logMessageReceived-=Error;}Check(!flashError,"同帧停用实体扣血不启动闪烁协程");Object.DestroyImmediate(classified.gameObject);
            }
            var plan=new ActiveSimulationPlan();plan.Prepare(genes,Logics(genes));Check(plan.KnownOnly,"内置行为计划允许批内复用");ActiveSimulationBatch.Begin(genes,plan);try{long count=ActiveSimulationPlan.ComponentHits;Check(ActiveSimulationBatch.Read<EntityStats>(genes)==stats&&ActiveSimulationBatch.Read<EntityStats>(genes)==stats&&ActiveSimulationPlan.ComponentHits==count+1,"同批组件仅首次真实查询");Check(ActiveSimulationBatch.Read<EntityManualControl>(genes)==null,"不存在的可选组件可查询");var manual=entity.gameObject.AddComponent<EntityManualControl>();ActiveSimulationBatch.Invalidate(genes);Check(ActiveSimulationBatch.Read<EntityManualControl>(genes)==manual,"安装组件后显式失效不保留旧空值");Object.DestroyImmediate(manual);Check(ActiveSimulationBatch.Read<EntityManualControl>(genes)==null,"已销毁组件不作为有效缓存返回");}finally{ActiveSimulationBatch.End(genes);}
            var other=Spawn(("gene_storage",50));var otherPlan=new ActiveSimulationPlan();otherPlan.Prepare(other.Genes,Logics(other.Genes));ActiveSimulationBatch.Begin(genes,plan);try{ActiveSimulationBatch.Read<EntityStats>(genes);ActiveSimulationBatch.Begin(other.Genes,otherPlan);try{Check(ActiveSimulationBatch.Read<EntityStats>(other.Genes)==other.GetComponent<EntityStats>(),"嵌套其他实体读取自身组件");}finally{ActiveSimulationBatch.End(other.Genes);}long count=ActiveSimulationPlan.ComponentHits;ActiveSimulationBatch.Read<EntityStats>(genes);Check(ActiveSimulationPlan.ComponentHits==count+1,"嵌套批次结束恢复父缓存");}finally{ActiveSimulationBatch.End(genes);}
            var callback=new Callback();var unknownPlan=new ActiveSimulationPlan();var unknown=new List<IGeneLogic>{callback};unknownPlan.Run(genes,unknown,.1f);Check(callback.calls==1&&!unknownPlan.KnownOnly,"未知扩展始终执行并回退原组件查询");
            var passive=new GrowthGeneLogic();passive.Initialize(genes,genes.Genome.Genes.First(g=>g.DefinitionId=="gene_growth"));var tail=new Callback();callback.action=()=>genes.Recalculate();var changed=new List<IGeneLogic>{callback,passive,tail};unknownPlan.Invalidate();unknownPlan.Run(genes,changed,.1f);Check(tail.calls==1,"回调重算后继续原快照剩余行为，下一步重建计划");
            bool blocked=ActiveNutritionContext.Blocked(genes);optimization.activeComponentReuse=false;Check(blocked==ActiveNutritionContext.Blocked(genes),"生产资格缓存与原始查询一致");optimization.activeComponentReuse=true;var nutrition=genes.Genome.Genes.First(g=>g.DefinitionId=="gene_nutrition");nutrition.Enabled=false;genes.Recalculate();Check(ActiveNutritionContext.Blocked(genes)==(EcologyQueries.Active(genes,NutritionEcologySettings.Current.heterotrophyGene)!=null||EcologyQueries.Active(genes,NutritionEcologySettings.Current.scavengerGene)!=null),"表达重算后生产资格重新解析");
            var panel=Object.FindFirstObjectByType<SimulationRulesPanel>();while(panel?.Card==null){yield return null;panel=Object.FindFirstObjectByType<SimulationRulesPanel>();}var extension=panel.GetComponent<RuleCardExtensions>();if(extension==null)extension=panel.Card.GetComponent<RuleCardExtensions>();var dropdown=panel.Card.Find("RuleList").GetComponent<Dropdown>();Check(panel.Card.Find("CleanupTab")==null&&dropdown.options.Count==3&&panel.Card.sizeDelta==new Vector2(260,164),"同尺寸规则卡使用列表，不保留旧页签按钮");
            panel.Card.Find("Fraction").GetComponent<InputField>().text="23";dropdown.value=1;panel.Card.Find("Fraction").GetComponent<InputField>().text="67";dropdown.value=0;Check(panel.Card.Find("Fraction").GetComponent<InputField>().text=="23","列表切换保留变异页草稿");dropdown.value=1;Check(panel.Card.Find("Fraction").GetComponent<InputField>().text=="67","列表切回保留清尸页草稿");
            // ★ 修复（测试夹具）：窄屏规则卡默认隐藏，先经原入口展开并等待下拉初始化；不能直接对停用对象Show。
            if(!panel.Card.gameObject.activeInHierarchy)panel.GetComponentsInChildren<Button>(true).Single(b=>b.name=="OpenRules").onClick.Invoke();yield return null;yield return null;
            Check(dropdown.captionText.text.Length>0&&dropdown.captionText.rectTransform.rect.height>=dropdown.captionText.preferredHeight,"紧凑列表标题文字有足够空间，不被默认内边距裁空");dropdown.Show();yield return null;Check(dropdown.transform.Find("Dropdown List")!=null,"规则列表真实展开");dropdown.Hide();yield return new WaitForSecondsRealtime(.3f);Check(dropdown.transform.Find("Dropdown List")==null,"规则列表关闭不残留遮罩");
            bool third=false;extension.Rules.Register("audit","测试第三规则",()=>third=true);Check(extension.Rules.Select("audit")&&third&&dropdown.options.Count==4,"追加规则可登记并选择，无需扩展标题按钮");extension.Rules.Select("mutation");
            var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(entity);yield return null;var inspectorPanel=Field<GameObject>(inspector,"_panel");var size=inspectorPanel.GetComponent<RectTransform>().sizeDelta;var entry=inspectorPanel.transform.Find("ScrollView/Viewport/Content/BaseInfo/EntityEdit");Check(entry!=null&&entry.GetComponentInChildren<Text>().text=="编辑","原位置入口仅显示编辑");var hover=inspectorPanel.GetComponentsInChildren<GeneHoverSurface>().First(s=>s.name.StartsWith("Block_gene_nutrition_"));var pointer=new PointerEventData(EventSystem.current);ExecuteEvents.Execute(hover.gameObject,pointer,ExecuteEvents.pointerEnterHandler);var tooltip=Field<GameObject>(inspector,"_tooltipPanel");Check(tooltip.activeSelf&&Field<Text>(inspector,"_tooltipText").text.Contains("禁用"),"整张基因卡悬浮显示实际失效原因");Check(tooltip.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"提示不拦截下方数值框或按钮");ExecuteEvents.Execute(hover.gameObject,pointer,ExecuteEvents.pointerExitHandler);Check(!tooltip.activeSelf,"离开基因卡关闭原因提示");
            entry.GetComponent<Button>().onClick.Invoke();Check(EntityEditPopup.Instance.Visible&&inspectorPanel.GetComponent<RectTransform>().sizeDelta==size,"编辑入口有效且原检查面板大小不变");EntityEditPopup.Instance.Close();Check(!GameInputGate.Blocked,"关闭编辑恢复游戏输入");
            Check(AtmosphereViewPolicy.OuterRadius(planet.Data,1,20)>=planet.Data.Vertices.Max(v=>v.magnitude)+19.99f,"大气外壳覆盖最高地形并保留厚度");var atmosphere=planet.transform.Find("AtmosphericScattering").GetComponent<Renderer>().sharedMaterial;AtmosphereViewPolicy.Apply(atmosphere,Camera.main);Check(atmosphere.GetFloat("_EntitySkyDensity")==1,"轨道模式使用原散射密度");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"rule-list-hover-edit.png"));yield return null;
            yield return PopulationRatioRuleChecks.Capture(panel,Path.Combine(Folder,"population-ratio-rule.png"));
        }
        private static bool OriginalProducer(EntityGeneController genes)
        {
            var settings=EcologyBalanceSettings.Current;if(genes==null||genes.GetComponent<EggIncubation>()?.Incubating==true)return false;if(EcologyQueries.Active(genes,NutritionEcologySettings.Current.heterotrophyGene)!=null||EcologyQueries.Active(genes,NutritionEcologySettings.Current.scavengerGene)!=null)return false;GeneEvaluation selected=null;foreach(var gene in genes.Evaluation.Genes)if(gene.IsActive&&gene.Definition.Id==settings.nutritionGene&&(selected==null||gene.EffectiveStrength>selected.EffectiveStrength))selected=gene;return selected?.State!=null&&Array.IndexOf(settings.producerStates,selected.State.StateId)>=0&&genes.GetAttribute(settings.movementAttribute)<=0;
        }
        [Serializable]private sealed class ReproductionList{public List<ReproductionSaveState> values;}
        [Serializable]private sealed class StatList{public List<StatSaveValue> values;}
    }
}
