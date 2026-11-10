using DarwinPark.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：比例调节是独立可选规则；默认关闭，开启后只影响超额类别后续繁殖。
    public sealed class PopulationRatioRulePage
    {
        private readonly RectTransform _card;
        private readonly InputField _fraction,_period;
        private readonly Button _toggle;
        private readonly Text _hint;
        private bool _enabled,_dirty;
        public bool Selected {get;set;}
        public PopulationRatioRulePage(RectTransform card,InputField fraction,InputField period,Button toggle,Text hint)
        {_card=card;_fraction=fraction;_period=period;_toggle=toggle;_hint=hint;}
        public void Sync(GameplayTuning tuning){_enabled=tuning.populationRatioControlEnabled;_dirty=false;}
        public void Toggle(){_enabled=!_enabled;_dirty=true;Render();}
        public string Edit(string text)=>_dirty?PopulationRatioRuleEdits.Edit(text,_enabled):text;
        public string Status=>_enabled?"只抑制超额繁殖；不直接生成":"关闭：不干预繁殖比例";
        public void Render()
        {
            var rect=(RectTransform)_toggle.transform;rect.anchoredPosition=new Vector2(10,-25);rect.sizeDelta=new Vector2(240,23);
            _toggle.GetComponentInChildren<Text>().text="种群比例调节："+(_enabled?"开":"关（默认）");
            Text("FractionLabel","不足");_fraction.gameObject.SetActive(false);Text("FractionUnit","自然繁殖补足");
            Text("PeriodLabel","超额");_period.gameObject.SetActive(false);_card.Find("PeriodUnit").gameObject.SetActive(false);
            _hint.gameObject.SetActive(true);_hint.text=_enabled?"延长对应类别的繁殖间隔":"不调节（开启后仅抑制超额）";
        }
        private void Text(string name,string value)=>_card.Find(name).GetComponent<Text>().text=value;
    }
}
