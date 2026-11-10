using System;
using System.Globalization;
using DarwinPark.Core;
using DarwinPark.Entities.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：在原尺寸规则卡内分页，模式切换及清尸配置不挤压底栏其他控件。
    public sealed class RuleCardExtensions:MonoBehaviour
    {
        private SimulationRulesPanel _owner;private RectTransform _card;private InputField _fraction,_seconds;private Button _enabled,_mode;private Text _stageHint;
        // ★ 适配：规则列表使用独立注册入口，原页草稿、保存和卡片尺寸保持不变。
        public SimulationRuleList Rules {get;private set;}
        private PopulationRatioRulePage _population;
        private bool _cleanupPage,_mutationEnabled,_cleanupEnabled,_mutationDirty,_cleanupDirty;private int _modeValue;private string _timedPercent,_stagePercent,_period,_cleanupPeriod;
        public bool SecondsEditable=>!_population.Selected&&!_cleanupPage&&_modeValue==0;
        public void Bind(SimulationRulesPanel owner,InputField fraction,InputField seconds,Button enabled)
        {
            _owner=owner;_card=owner.Card;_fraction=fraction;_seconds=seconds;_enabled=enabled;
            _fraction.onValueChanged.AddListener(_=>InputChanged());_seconds.onValueChanged.AddListener(_=>InputChanged());
            _card.Find("Title").GetComponent<RectTransform>().sizeDelta=new Vector2(95,18);
            // ★ 适配：存储仍用模拟秒保持旧配置兼容，界面改按游戏天输入并提供悬浮换算。
            foreach(var name in new[]{"FractionUnit","PeriodUnit"}){var unit=_card.Find(name);unit.GetComponent<Text>().raycastTarget=true;unit.gameObject.AddComponent<SimulationTimeHelp>();}Text("PeriodUnit","游戏天");
            Rules=new SimulationRuleList(_card);Rules.Register("mutation","变异规则",()=>Page(false));Rules.Register("corpseCleanup","定时清尸",()=>Page(true));Rules.Register("population","种群比例调节",PopulationPage);
            _mode=AddButton("MutationMode","定时抽样",SwitchMode,10,25,158,23);
            _stageHint=UIFactory.NewText("StageTrigger",_card,"出生 / 成年 / 老年，各一次",11);Place(_stageHint.rectTransform,55,82,195,23);
            _population=new PopulationRatioRulePage(_card,_fraction,_seconds,_enabled,_stageHint);
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
        private Button AddButton(string name,string label,UnityEngine.Events.UnityAction action,float x,float y,float width,float height){var button=UIFactory.NewButton(name,_card,label,()=>action(),new Vector2(width,height)).GetComponent<Button>();Place((RectTransform)button.transform,x,y,width,height);button.GetComponentInChildren<Text>().fontSize=11;return button;}
        private static string Number(float value)=>value.ToString("0.######",CultureInfo.InvariantCulture);
        public void Sync(GameplayTuning tuning)
        {
            _population.Sync(tuning);_mutationEnabled=tuning.mutationRoundsEnabled;_cleanupEnabled=tuning.corpseCleanupEnabled;_modeValue=tuning.mutationMode;
            _timedPercent=Number(tuning.mutationPopulationFraction*100);_stagePercent=Number(tuning.stageMutationProbability*100);_period=Number(tuning.mutationRoundSeconds/DarwinPark.Entities.LifeCycleSettings.Load().secondsPerDay);_cleanupPeriod=Number(tuning.corpseCleanupSeconds/DarwinPark.Entities.LifeCycleSettings.Load().secondsPerDay);_mutationDirty=_cleanupDirty=false;Render();
        }
        private void Stash(){if(_population.Selected)return;if(_cleanupPage)_cleanupPeriod=_fraction.text;else{if(_modeValue==0)_timedPercent=_fraction.text;else _stagePercent=_fraction.text;if(_modeValue==0)_period=_seconds.text;}}
        private void Page(bool cleanup){Stash();_population.Selected=false;_cleanupPage=cleanup;Render();}
        private void PopulationPage(){Stash();_population.Selected=true;_cleanupPage=false;Render();}
        private void InputChanged(){if(_population.Selected)return;if(_cleanupPage)_cleanupDirty=true;else _mutationDirty=true;}
        private void SwitchMode(){Stash();_modeValue=1-_modeValue;_mutationDirty=true;_owner.MarkRuleDirty();Render();}
        public void Toggle(){if(_population.Selected){_population.Toggle();_owner.MarkRuleDirty();Render();return;}Stash();if(_cleanupPage){_cleanupEnabled=!_cleanupEnabled;_cleanupDirty=true;}else{_mutationEnabled=!_mutationEnabled;_mutationDirty=true;}_owner.MarkRuleDirty();Render();}
        private void Text(string name,string value)=>_card.Find(name).GetComponent<Text>().text=value;
        private void Render()
        {
            foreach(var name in new[]{"FractionUnit","PeriodUnit"})_card.Find(name).GetComponent<SimulationTimeHelp>().enabled=!_population.Selected&&(name=="PeriodUnit"||_cleanupPage);
            if(_population.Selected)
            {
                _mode.gameObject.SetActive(false);_population.Render();return;
            }
            _fraction.gameObject.SetActive(true);
            _mode.gameObject.SetActive(!_cleanupPage);_mode.GetComponentInChildren<Text>().text=_modeValue==0?"模式：定时抽样 ⇄":"模式：生命阶段 ⇄";
            Place((RectTransform)_enabled.transform,_cleanupPage?10:175,25,_cleanupPage?240:75,23);
            _enabled.GetComponentInChildren<Text>().text=_cleanupPage?"定时清理尸体："+(_cleanupEnabled?"开":"关"):"变异："+(_mutationEnabled?"开":"关");
            Text("FractionLabel",_cleanupPage?"周期":_modeValue==0?"抽样":"概率");Text("FractionUnit",_cleanupPage?"游戏天":_modeValue==0?"% 活体":"% 每阶段");
            _fraction.SetTextWithoutNotify(_cleanupPage?_cleanupPeriod:_modeValue==0?_timedPercent:_stagePercent);_seconds.SetTextWithoutNotify(_period);
            _seconds.gameObject.SetActive(!_cleanupPage&&_modeValue==0);_card.Find("PeriodUnit").gameObject.SetActive(!_cleanupPage&&_modeValue==0);
            Text("PeriodLabel",_cleanupPage?"保护":_modeValue==0?"周期":"触发");_stageHint.gameObject.SetActive(_cleanupPage||_modeValue==1);_stageHint.text=_cleanupPage?"跳过已领取 / 运输中的尸体":"出生 / 成年 / 老年，各一次";
        }
        private static float Parse(string value){if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var result)||float.IsNaN(result)||float.IsInfinity(result))throw new FormatException("请输入有效数值");return result;}
        public void Save()
        {
            Stash();
            // ★ 修复（防Bug回归）：外部文件可在编辑期间更新，未编辑的另一页不能被旧草稿覆盖。
            RuleSelectionEdits.Save(text=>{text=_population.Edit(text);if(_mutationDirty)text=RuleSelectionEdits.MutationModes(text,_mutationEnabled,_modeValue,Parse(_timedPercent)/100,Parse(_stagePercent)/100,SimulationTimeHelp.ToSeconds(Parse(_period)));if(_cleanupDirty)text=RuleSelectionEdits.Cleanup(text,_cleanupEnabled,SimulationTimeHelp.ToSeconds(Parse(_cleanupPeriod)));return text;});
        }
        public void SetEditable(bool editable){_mode.interactable=editable;if(_population.Selected)_fraction.interactable=false;}
        public string Status()=>_population.Selected?_population.Status:_cleanupPage?"已清理 "+CorpseCleanupController.Removed+" · 待清理 "+CorpseCleanupController.Queued:_modeValue==1?"阶段去重触发 · 成年 / 老年仅一次":"第"+MutationRoundController.Rounds+"轮 · 抽取"+MutationRoundController.LastSelected+"/"+MutationRoundController.LastPopulation+" · 待"+MutationRoundController.Queued;
    }
}
