using System;
using System.Collections;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：Q 必须有独立有效基因且真实扣账；包含旧世界补库、无UI、Alt按住语义、自动寻路和能量守恒。
    public static class NutrientUtilizationRegression
    {
        public static string ActorId {get;private set;}
        private static float Food(DarwinEntity entity){var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);return food+reserve;}
        private static void Fill(DarwinEntity entity,float food,float reserve)
        {var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out float current);stats.Modify("nutrient",food-current,false);stats.TryGet("storedNutrient",out current);stats.Modify("storedNutrient",reserve-current,false);}
        private static float Energy()=>ColonyResource.All.Where(r=>r.State.kind==ColonyResourceKind.Energy).Sum(r=>r.State.energy);
        public static IEnumerator Verify(Action<bool,string> check)
        {
            var view=EntityViewController.Instance;var input=view.GetComponent<EntityPossessionInput>();var keys=view.GetComponent<EnergyBlockInput>();var clock=ServiceLocator.Get<SimulationClock>();
            var settings=NutrientUtilizationSettings.Current;var canvas=ServiceLocator.Get<UIRoot>().MainCanvas.transform;
            check(canvas.Find("EntityViewBar")==null&&canvas.Find("BottomPanel/TabBar/AddColonyEnergy")==null,"操控提示栏和免费添加能量按钮均已移除");
            input.SetCursorReleased(true);check(input.CursorReleased&&Cursor.lockState==CursorLockMode.None,"按住Alt释放鼠标");
            input.SetCursorReleased(true);check(input.CursorReleased,"Alt持续按住不会反复切换");input.SetCursorReleased(false);check(!input.CursorReleased,"松开Alt恢复操控");
            clock.Play();check(!keys.HandleKey(KeyCode.E),"旧E键免费补给入口已取消");check(!keys.HandleKey(KeyCode.Q),"已有营养但缺少独立营养利用基因时Q无效");clock.Pause();
            var definition=GeneDatabase.Get(settings.geneId);check(definition!=null&&definition.LogicScriptId=="EnergyBlockGeneLogic"&&UIIconRegistry.Get(UIVisualTheme.GeneIcon(definition.Id))!=null,"旧存档补齐独立营养利用基因及图标");
            var up=EcologyQueries.Up(view.Target.transform.position);var axis=Vector3.Cross(up,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.Cross(up,Vector3.right).normalized;
            var point=EcologyQueries.Surface(view.Target.transform.position+axis*24);
            var genome=new Genome();foreach(var id in new[]{"gene_growth","gene_locomotion",settings.geneId})genome.Add(GeneInstance.Create(id,50,GeneDatabase.Get(id).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"营养利用验证");var actor=EntityRegistry.All.Single(e=>!before.Contains(e));ActorId=EcologyIdentity.Of(actor);
            actor.Genes.mutationChance=0;actor.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);actor.Genes.RestoreStageState(AgeState.Adult,3,2);actor.Genes.Recalculate();
            actor.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>().enabled=false;actor.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;
            var metabolism=actor.GetComponent<EnergyBlockMetabolism>();var gene=actor.Genes.Genome.Genes.Single(g=>g.DefinitionId==settings.geneId);
            check(metabolism!=null&&metabolism.Active&&actor.Genes.Genome.Genes.All(g=>g.DefinitionId!="gene_nutrition"),"新基因可独立生效，不依赖营养生产基因或蚁群身份");
            view.EnterView(actor);yield return null;Fill(actor,10,7);clock.Play();float initial=Food(actor),energy=Energy(),cost=actor.Genes.GetAttribute(settings.releaseAttribute);
            check(keys.HandleKey(KeyCode.Q),"Q通过实际操控入口吐出能量块");
            var stats=actor.GetComponent<EntityStats>();stats.TryGet("nutrient",out float nutrient);stats.TryGet("storedNutrient",out float reserve);
            check(Mathf.Abs(Food(actor)+Energy()-initial-energy)<.001f&&Mathf.Abs(nutrient-10)<.001f&&Mathf.Abs(reserve-(7-cost))<.001f,"吐出优先扣储存营养，实体与块总能量守恒");
            check(keys.HandleKey(KeyCode.Q),"储备不足时允许补扣饱食度");stats.TryGet("storedNutrient",out reserve);
            check(reserve==0&&Mathf.Abs(Food(actor)+Energy()-initial-energy)<.001f,"混合扣账不重复或凭空生成能量");
            float foodBefore=Food(actor);metabolism.Advance(.1f);check(Mathf.Abs(Food(actor)-foodBefore)<.001f,"吐出距离超过吸收半径，避免同帧自动吸回");
            gene.Enabled=false;actor.Genes.Recalculate();check(!keys.HandleKey(KeyCode.Q),"禁用基因后不能吐出");gene.Enabled=true;gene.Value=0;actor.Genes.Recalculate();check(!keys.HandleKey(KeyCode.Q),"基因失效后不能吐出");gene.Value=50;actor.Genes.Recalculate();
            actor.Genes.EnvironmentGeneSuppression=10000;actor.Genes.Recalculate();check(!keys.HandleKey(KeyCode.Q),"基因受环境抑制后不能绕过表达限制");actor.Genes.EnvironmentGeneSuppression=0;actor.Genes.Recalculate();
            Fill(actor,0,0);energy=Energy();check(!keys.HandleKey(KeyCode.Q)&&Mathf.Abs(Energy()-energy)<.001f,"营养不足不生成任何块");
            Fill(actor,10,7);clock.Pause();check(!keys.HandleKey(KeyCode.Q),"暂停时Q不改变能量");clock.Play();
            GameInputGate.Blocked=true;check(!keys.HandleKey(KeyCode.Q),"覆盖菜单屏蔽Q");GameInputGate.Blocked=false;input.SetCursorReleased(true);check(!keys.HandleKey(KeyCode.Q),"按住Alt操作鼠标时Q不吐出");input.SetCursorReleased(false);
            Fill(actor,3,0);view.ExitView();var origin=actor.transform.position;float worldEnergy=Energy();clock.SetTimeScale(4);clock.Play();double start=EditorApplication.timeSinceStartup;float began=clock.GameTime;
            while(Food(actor)<=3||Energy()>=worldEnergy)
            {if(actor==null||clock.GameTime-began>25||EditorApplication.timeSinceStartup-start>30)throw new TimeoutException("普通实体没有实际移动并吸收通用能量块");yield return null;}
            clock.Pause();check(EcologyQueries.Distance(origin,actor.transform.position)>.2f&&Food(actor)>3&&Energy()<worldEnergy,"普通实体在实际物理步中自动靠近并吸收能量块");
            check(Mathf.Abs(Food(actor)-3+Energy()-worldEnergy)<.02f,"普通实体自动吸收保持能量守恒");
            check(view.EnterView(actor)&&canvas.Find("EntityViewBar")==null,"重新进入操控仍不创建功能UI");yield return null;
        }
    }
}
