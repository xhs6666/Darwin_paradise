using System;
using System.Linq;
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.UI;
namespace DarwinPark.UI
{
    // ★ 新增：添加基因按职责分类，筛选只重建当前列表，不修改基因库或实体数据。
    public partial class GeneInspectorUI
    {
        private Dropdown _geneCategory;
        private List<GeneCategory?> _geneCategories;
        public static string CategoryName(GeneCategory category)=>category switch{GeneCategory.Reproduction=>"生殖",GeneCategory.Nutrition=>"营养",GeneCategory.Locomotion=>"行动",GeneCategory.Growth=>"生长与修复",GeneCategory.Adaptation=>"环境适应",GeneCategory.Perception=>"探测",GeneCategory.Control=>"中枢",GeneCategory.Social=>"社会",_=>"其他"};
        private void ShowAddGeneMenu()
        {
            if(_currentEntity==null||!WorldAuthority.CanEdit)return;
            if(_addGenePanel.activeSelf){_addGenePanel.SetActive(false);return;}
            _addGenePanel.SetActive(true);_addGenePanel.transform.SetAsLastSibling();
            if(_geneCategory==null)
            {
                var go=DefaultControls.CreateDropdown(new DefaultControls.Resources());go.name="Category";go.transform.SetParent(_addGenePanel.transform,false);_geneCategory=go.GetComponent<Dropdown>();
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(10,-77);rect.offsetMax=new Vector2(-10,-45);
                UIVisualTheme.Paint(go.GetComponent<Image>(),"field");RuntimeDropdownSkin.Apply(_geneCategory);foreach(var text in go.GetComponentsInChildren<Text>(true)){text.font=UIFactory.DefaultFont;text.fontSize=14;text.color=UIVisualTheme.Text;}
                _geneCategory.onValueChanged.AddListener(_=>RefreshAddGeneList());
            }
            var prior=_geneCategories!=null&&_geneCategory.value<_geneCategories.Count?_geneCategories[_geneCategory.value]:null;
            _geneCategories=new List<GeneCategory?>{null};_geneCategories.AddRange(GeneDatabase.All.Select(g=>g.Category).Distinct().OrderBy(g=>(int)g).Select(g=>(GeneCategory?)g));
            _geneCategory.ClearOptions();_geneCategory.AddOptions(_geneCategories.Select(c=>c.HasValue?CategoryName(c.Value):"全部分类").ToList());_geneCategory.SetValueWithoutNotify(Mathf.Max(0,_geneCategories.IndexOf(prior)));_geneCategory.RefreshShownValue();RefreshAddGeneList();
        }
        private void RefreshAddGeneList()
        {
            if(_currentEntity==null||_addGeneListParent==null)return;
            foreach(Transform child in _addGeneListParent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var category=_geneCategories[_geneCategory.value];var all=GeneDatabase.All.ToArray();var definitions=all.Where(d=>!category.HasValue||d.Category==category.Value).OrderBy(d=>d.Tier).ThenBy(d=>d.DisplayName).ToArray();
            _addGenePanel.transform.Find("Title").GetComponent<Text>().text=$"添加 / 复制基因（{definitions.Length}/{all.Length}）";_addGeneListParent.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;
            foreach(var definition in definitions)
            {
                var gene=definition;var button=UIFactory.NewButton("Btn_"+gene.Id,_addGeneListParent,gene.DisplayName+" · T"+gene.Tier,()=>{if(_currentEntity==null||!WorldAuthority.CanEdit)return;LanEditActions.AddGene(_currentEntity,gene.Id);_addGenePanel.SetActive(false);ShowEntityInfo(_currentEntity);},new Vector2(300,32));
                var label=button.GetComponentInChildren<Text>();label.alignment=TextAnchor.MiddleLeft;label.rectTransform.offsetMin=new Vector2(38,0);label.rectTransform.offsetMax=new Vector2(-24,0);
                UIVisualTheme.AddIcon(button.transform,UIVisualTheme.GeneIcon(gene.Id),new Vector2(24,24),new Vector2(19,0),new Vector2(0,.5f));var plus=UIFactory.NewText("Plus",button.transform,"+",20,TextAnchor.MiddleCenter);plus.rectTransform.anchorMin=plus.rectTransform.anchorMax=new Vector2(1,.5f);plus.rectTransform.anchoredPosition=new Vector2(-16,0);plus.rectTransform.sizeDelta=new Vector2(22,26);
            }
        }
    }
}
