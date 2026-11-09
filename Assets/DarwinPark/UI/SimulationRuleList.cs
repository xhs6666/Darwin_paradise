using System;
using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;
using UnityEngine.UI;
namespace DarwinPark.UI
{
    // ★ 新增：规则入口统一为可注册列表，增加规则只追加条目，不再向固定卡片标题挤按钮。
    public sealed class SimulationRuleList
    {
        private readonly List<(string id,Action selected)> _rules=new();private readonly Dropdown _dropdown;
        public Dropdown Dropdown=>_dropdown;
        public SimulationRuleList(Transform parent)
        {
            var go=DefaultControls.CreateDropdown(new DefaultControls.Resources());go.name="RuleList";go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(105,-2);rect.sizeDelta=new Vector2(145,21);
            _dropdown=go.GetComponent<Dropdown>();_dropdown.ClearOptions();UIVisualTheme.Paint(go.GetComponent<Image>(),"field");
            foreach(var text in go.GetComponentsInChildren<Text>(true)){text.font=UIFactory.DefaultFont;text.fontSize=11;text.color=UIVisualTheme.Text;}
            // ★ 修复（防Bug回归）：默认下拉为30高，原上下内边距会把21高标题的文字裁空。
            _dropdown.captionText.rectTransform.offsetMin=new Vector2(8,1);_dropdown.captionText.rectTransform.offsetMax=new Vector2(-20,-1);
            var arrow=go.transform.Find("Arrow").GetComponent<Image>();arrow.sprite=UIIconRegistry.Get("ui_icon_next");arrow.color=UIVisualTheme.Accent;arrow.preserveAspect=true;arrow.rectTransform.sizeDelta=new Vector2(12,12);arrow.rectTransform.localRotation=Quaternion.Euler(0,0,-90);
            RuntimeDropdownSkin.Apply(_dropdown);_dropdown.onValueChanged.AddListener(index=>{if(index>=0&&index<_rules.Count)_rules[index].selected();});
        }
        public void Register(string id,string name,Action selected)
        {if(_rules.Exists(r=>r.id==id))throw new ArgumentException("规则编号重复："+id);_rules.Add((id,selected));_dropdown.options.Add(new Dropdown.OptionData(name));_dropdown.RefreshShownValue();}
        public bool Select(string id)
        {int index=_rules.FindIndex(r=>r.id==id);if(index<0)return false;_dropdown.SetValueWithoutNotify(index);_rules[index].selected();return true;}
    }
}
