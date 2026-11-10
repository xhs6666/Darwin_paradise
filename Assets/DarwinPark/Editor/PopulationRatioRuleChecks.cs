using System;
using System.Collections;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.UI;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：真实规则卡、磁盘、模拟及旧入口共同验证比例调节不会再生成实体。
    public static class PopulationRatioRuleChecks
    {
        public static IEnumerator Verify(PopulationMaintenanceRule rule,RandomPlanetSpawner spawner,SimulationClock clock,Action<bool,string> check)
        {
            check(!new GameplayTuning().populationRatioControlEnabled&&!GameplayTuning.Parse("{\"version\":1,\"automaticRefillEnabled\":true}").populationRatioControlEnabled,"新配置及带旧补员字段的配置均默认关闭比例调节");
            check(!GameplayTuning.Current.populationRatioControlEnabled,"默认外部配置关闭比例调节");
            int before=EntityRegistry.All.Count;yield return RunFor(clock,2);check(EntityRegistry.All.Count==before&&!spawner.IsBusy&&PopulationMaintenance.Needed(rule)>0,"明显缺额且有旧比例规则，默认运行也不直接补员");
            var excess=new PopulationMaintenanceRule{id=Guid.NewGuid().ToString("N"),ratio=.3f,source=new RandomSpawnRecipe{method=RandomSpawnMethod.Genes,name="超额营养实体",included=new(){"gene_nutrition"}}};check(PopulationMaintenance.Set(excess),"建立真实超额类别作为繁殖调节样本");var parent=EntityRegistry.All.First();
            check(PopulationMaintenance.BreedingScale(parent.Genes)==1,"总规则关闭时不干预超额类别繁殖");
            var panel=Object.FindObjectsByType<SimulationRulesPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();var extension=panel.GetComponent<RuleCardExtensions>();
            check(extension.Rules.Select("population"),"规则列表可选择种群比例调节");check(panel.Card.sizeDelta==new Vector2(260,164)&&panel.Card.Find("MutationEnabled").GetComponentInChildren<Text>().text.Contains("关"),"比例调节默认显示关闭并保留原卡尺寸");
            var toggle=panel.Card.Find("MutationEnabled").GetComponent<Button>();toggle.onClick.Invoke();check(!GameplayTuning.Current.populationRatioControlEnabled,"修改草稿不立即改变模拟");panel.Apply();
            check(GameplayTuning.Current.populationRatioControlEnabled&&GameplayTuning.Parse(File.ReadAllText(GameplayTuning.FilePath)).populationRatioControlEnabled,"应用后比例规则同时写盘并生效");check(Mathf.Abs(PopulationMaintenance.BreedingScale(parent.Genes)-.3f)<.0001f,"启用后超额类别的真实繁殖速率按比例降低");
            yield return RunFor(clock,2);check(EntityRegistry.All.Count==before&&!spawner.IsBusy,"比例调节开启后依然不直接生成不足类别");
            bool retired=(bool)typeof(RandomPlanetSpawner).GetMethod("StartInternal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(spawner,new object[]{rule.source,3,true,rule});check(!retired&&!spawner.IsBusy,"退休的自动补员入口无法被旧调用重新启动");
            panel.Reload();check(panel.Card.Find("MutationEnabled").GetComponentInChildren<Text>().text.Contains("开"),"重新载入后保留比例调节开关");
            toggle.onClick.Invoke();panel.Apply();check(PopulationMaintenance.BreedingScale(parent.Genes)==1&&PopulationMaintenance.Rules.Any(r=>r.enabled),"关闭总规则恢复正常繁殖并保留世界的比例配置");
            toggle.onClick.Invoke();panel.Apply();PopulationMaintenance.Remove(excess.id);
            var hundred=JsonUtility.FromJson<PopulationMaintenanceRule>(JsonUtility.ToJson(rule));hundred.ratio=1;check(PopulationMaintenance.Set(hundred),"移除直接补员后允许100%目标");PopulationMaintenance.Set(rule);
            extension.Rules.Select("mutation");check(panel.Card.Find("Fraction").gameObject.activeSelf,"切回变异页恢复原有输入框");
            float day=LifeCycleSettings.Load().secondsPerDay,oldMutation=GameplayTuning.Current.mutationRoundSeconds,oldCleanup=GameplayTuning.Current.corpseCleanupSeconds;
            check(panel.Card.Find("PeriodUnit").GetComponent<Text>().text=="游戏天"&&float.Parse(panel.Card.Find("Seconds").GetComponent<InputField>().text,System.Globalization.CultureInfo.InvariantCulture)==oldMutation/day,"原模拟秒配置按游戏天显示，旧值不被迁移或重置");
            panel.Card.Find("Seconds").GetComponent<InputField>().text="2.5";panel.Apply();check(GameplayTuning.Current.mutationRoundSeconds==2.5f*day,"游戏天输入准确换算为内部模拟秒并保存");
            extension.Rules.Select("corpseCleanup");panel.Card.Find("Fraction").GetComponent<InputField>().text="1.5";panel.Apply();check(GameplayTuning.Current.corpseCleanupSeconds==1.5f*day&&GameplayTuning.Current.mutationRoundSeconds==2.5f*day,"清尸游戏天独立保存，不覆盖其他规则");
            bool rejected=false;try{SimulationTimeHelp.ToSeconds(.001f);}catch(FormatException error){rejected=error.Message.Contains("游戏天");}check(rejected,"无效周期提示使用界面当前的游戏天单位");
            panel.Card.Find("Fraction").GetComponent<InputField>().text=(oldCleanup/day).ToString("R",System.Globalization.CultureInfo.InvariantCulture);panel.Apply();extension.Rules.Select("mutation");panel.Card.Find("Seconds").GetComponent<InputField>().text=(oldMutation/day).ToString("R",System.Globalization.CultureInfo.InvariantCulture);panel.Apply();
            check(File.ReadAllText(GameplayTuning.FilePath).Contains("//"),"规则保存保留原外部配置中文注释");
        }
        private static IEnumerator RunFor(SimulationClock clock,float seconds)
        {float start=clock.GameTime;clock.Play();double timeout=Time.realtimeSinceStartupAsDouble+15;while(clock.GameTime<start+seconds){if(Time.realtimeSinceStartupAsDouble>timeout)throw new TimeoutException("比例规则模拟验证");yield return null;}clock.Pause();}
        // ★ 新增：正式大小的规则卡实际显示并截图，避免只检查隐藏控件的尺寸。
        public static IEnumerator Capture(SimulationRulesPanel panel,string path)
        {
            var extension=panel.GetComponent<RuleCardExtensions>();extension.Rules.Select("population");
            if(!panel.Card.gameObject.activeInHierarchy)panel.GetComponentsInChildren<Button>(true).Single(b=>b.name=="OpenRules").onClick.Invoke();
            yield return null;yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path);yield return null;
            extension.Rules.Select("mutation");
        }
    }
}
