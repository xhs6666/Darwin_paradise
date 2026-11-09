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
        private SimulationRulesPanel _owner;private RectTransform _card;private InputField _fraction,_seconds;private Button _enabled,_mode,_mutationTab,_cleanupTab;private Text _stageHint;
        private bool _cleanupPage,_mutationEnabled,_cleanupEnabled,_mutationDirty,_cleanupDirty;private int _modeValue;private string _timedPercent,_stagePercent,_period,_cleanupPeriod;
        public bool SecondsEditable=>!_cleanupPage&&_modeValue==0;
        public void Bind(SimulationRulesPanel owner,InputField fraction,InputField seconds,Button enabled)
        {
            _owner=owner;_card=owner.Card;_fraction=fraction;_seconds=seconds;_enabled=enabled;
            _fraction.onValueChanged.AddListener(_=>InputChanged());_seconds.onValueChanged.AddListener(_=>InputChanged());
            _card.Find("Title").GetComponent<RectTransform>().sizeDelta=new Vector2(95,18);
            _mutationTab=AddButton("MutationTab","变异",()=>Page(false),112,2,64,21);
            _cleanupTab=AddButton("CleanupTab","清尸",()=>Page(true),182,2,68,21);
            _mode=AddButton("MutationMode","定时抽样",SwitchMode,10,25,158,23);
            _stageHint=UIFactory.NewText("StageTrigger",_card,"出生 / 成年 / 老年，各一次",11);Place(_stageHint.rectTransform,55,82,195,23);
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
        private Button AddButton(string name,string label,UnityEngine.Events.UnityAction action,float x,float y,float width,float height){var button=UIFactory.NewButton(name,_card,label,()=>action(),new Vector2(width,height)).GetComponent<Button>();Place((RectTransform)button.transform,x,y,width,height);button.GetComponentInChildren<Text>().fontSize=11;return button;}
        private static string Number(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        public void Sync(GameplayTuning tuning)
        {
            _mutationEnabled=tuning.mutationRoundsEnabled;_cleanupEnabled=tuning.corpseCleanupEnabled;_modeValue=tuning.mutationMode;
            _timedPercent=Number(tuning.mutationPopulationFraction*100);_stagePercent=Number(tuning.stageMutationProbability*100);_period=Number(tuning.mutationRoundSeconds);_cleanupPeriod=Number(tuning.corpseCleanupSeconds);_mutationDirty=_cleanupDirty=false;Render();
        }
        private void Stash(){if(_cleanupPage)_cleanupPeriod=_fraction.text;else{if(_modeValue==0)_timedPercent=_fraction.text;else _stagePercent=_fraction.text;if(_modeValue==0)_period=_seconds.text;}}
        private void Page(bool cleanup){Stash();_cleanupPage=cleanup;Render();}
        private void InputChanged(){if(_cleanupPage)_cleanupDirty=true;else _mutationDirty=true;}
        private void SwitchMode(){Stash();_modeValue=1-_modeValue;_mutationDirty=true;_owner.MarkRuleDirty();Render();}
        public void Toggle(){Stash();if(_cleanupPage){_cleanupEnabled=!_cleanupEnabled;_cleanupDirty=true;}else{_mutationEnabled=!_mutationEnabled;_mutationDirty=true;}_owner.MarkRuleDirty();Render();}
        private void Text(string name,string value)=>_card.Find(name).GetComponent<Text>().text=value;
        private void Render()
        {
            _mode.gameObject.SetActive(!_cleanupPage);_mode.GetComponentInChildren<Text>().text=_modeValue==0?"模式：定时抽样 ⇄":"模式：生命阶段 ⇄";
            Place((RectTransform)_enabled.transform,_cleanupPage?10:175,25,_cleanupPage?240:75,23);
            _enabled.GetComponentInChildren<Text>().text=_cleanupPage?"定时清理尸体："+(_cleanupEnabled?"开":"关"):"变异："+(_mutationEnabled?"开":"关");
            Text("FractionLabel",_cleanupPage?"周期":_modeValue==0?"抽样":"概率");Text("FractionUnit",_cleanupPage?"模拟秒":_modeValue==0?"% 活体":"% 每阶段");
            _fraction.SetTextWithoutNotify(_cleanupPage?_cleanupPeriod:_modeValue==0?_timedPercent:_stagePercent);_seconds.SetTextWithoutNotify(_period);
            _seconds.gameObject.SetActive(!_cleanupPage&&_modeValue==0);_card.Find("PeriodUnit").gameObject.SetActive(!_cleanupPage&&_modeValue==0);
            Text("PeriodLabel",_cleanupPage?"保护":_modeValue==0?"周期":"触发");_stageHint.gameObject.SetActive(_cleanupPage||_modeValue==1);_stageHint.text=_cleanupPage?"跳过已领取 / 运输中的尸体":"出生 / 成年 / 老年，各一次";
            UIVisualTheme.StyleButton(_mutationTab,!_cleanupPage);UIVisualTheme.StyleButton(_cleanupTab,_cleanupPage);
        }
        private static float Parse(string value){if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var result)||float.IsNaN(result)||float.IsInfinity(result))throw new FormatException("请输入有效数值");return result;}
        public void Save()
        {
            Stash();
            // ★ 修复（防Bug回归）：外部文件可在编辑期间更新，未编辑的另一页不能被旧草稿覆盖。
            RuleSelectionEdits.Save(text=>{if(_mutationDirty)text=RuleSelectionEdits.MutationModes(text,_mutationEnabled,_modeValue,Parse(_timedPercent)/100,Parse(_stagePercent)/100,Parse(_period));if(_cleanupDirty)text=RuleSelectionEdits.Cleanup(text,_cleanupEnabled,Parse(_cleanupPeriod));return text;});
        }
        public void SetEditable(bool editable){_mode.interactable=editable;}
        public string Status()=>_cleanupPage?"已清理 "+CorpseCleanupController.Removed+" · 待清理 "+CorpseCleanupController.Queued:_modeValue==1?"阶段去重触发 · 成年 / 老年仅一次":"第"+MutationRoundController.Rounds+"轮 · 抽取"+MutationRoundController.LastSelected+"/"+MutationRoundController.LastPopulation+" · 待"+MutationRoundController.Queued;
    }
}
