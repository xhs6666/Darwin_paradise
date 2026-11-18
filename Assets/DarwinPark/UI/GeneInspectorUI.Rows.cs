using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;

namespace DarwinPark.UI
{
    // ✦ 新增：基因列表控件与刷新绑定，单独维护列表表现。
    public partial class GeneInspectorUI
    {
        private void RefreshMutations()
        {
            if (_mutationContainer == null) return;
            foreach (Transform child in _mutationContainer.transform) Destroy(child.gameObject);

            if (_allMutations.Count == 0)
            {
                var noMut = UIFactory.NewText("NoMut", _mutationContainer.transform, "<color=#888888>暂无变异基因</color>", 14, TextAnchor.UpperLeft);
                noMut.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            // ★ 修复（防Bug回归）：一次列出所有变异基因，由现有 ScrollRect 滚轮浏览，不再按四条分页。
            foreach(var mutation in _allMutations)
                CreateGeneRow(mutation,_currentEntity.GetComponent<EntityGeneController>(),_mutationContainer.transform);
        }
        private void CreateGeneRow(GeneEvaluation eval, EntityGeneController controller, Transform parent)
        {
            if (eval == null || eval.Definition == null) return;

            // ★ 新增：行背景与图标只装饰原有基因行，不改变编辑、滚动或双击命中。
            var geneBlock = UIFactory.NewRect("Block_" + eval.Definition.Id + "_" + eval.Gene.InstanceId.Substring(0, 4), parent,typeof(Image));
            UIVisualTheme.Paint(geneBlock.GetComponent<Image>(),"button");geneBlock.GetComponent<Image>().raycastTarget=false;
            // ★ 适配：失效原因保留在原提示内容中，触发区域扩展为整个基因卡。
            GeneHoverSurface.Bind(geneBlock,()=>ShowGeneTooltip(eval),()=>{if(_tooltipPanel!=null)_tooltipPanel.SetActive(false);},()=>ShowGeneTree(eval.Definition.Id));
            var blockLE = geneBlock.AddComponent<LayoutElement>();
            // ★ 适配：带标签的基因增加独立一行，基因倾向与实体能力不会互相覆盖。
            blockLE.minHeight=blockLE.preferredHeight=eval.Definition.EntityStateTags.Count>0?94:70;
            if(eval.Definition.EntityStateTags.Count>0)
            {
                var tag=UIFactory.NewText("EntityStateTags",geneBlock.transform,EntityStatePresentation.GeneTags(eval.Definition),12,TextAnchor.MiddleLeft);
                tag.rectTransform.anchorMin=new Vector2(0,1);tag.rectTransform.anchorMax=Vector2.one;tag.rectTransform.pivot=new Vector2(.5f,1);
                tag.rectTransform.anchoredPosition=new Vector2(0,-66);tag.rectTransform.sizeDelta=new Vector2(-16,20);
            }

            var row1 = UIFactory.NewRect("Row1", geneBlock.transform);
            var r1rt = row1.GetComponent<RectTransform>();
            r1rt.anchorMin = new Vector2(0, 1); r1rt.anchorMax = new Vector2(1, 1); r1rt.pivot = new Vector2(0, 1);
            r1rt.anchoredPosition=new Vector2(8,-6);r1rt.sizeDelta = new Vector2(-16, 28);

            var colorGO = UIFactory.NewRect("Color", row1.transform, typeof(Image));
            var crt = colorGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0.5f); crt.anchorMax = new Vector2(0, 0.5f); crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(24, 24);
            var geneIcon=colorGO.GetComponent<Image>();geneIcon.sprite=UIIconRegistry.Get(UIVisualTheme.GeneIcon(eval.Definition.Id));
            geneIcon.color=eval.IsActive?UIVisualTheme.Accent:UIVisualTheme.Muted;geneIcon.preserveAspect=true;geneIcon.raycastTarget=false;

            string activeStr = eval.IsActive ? (controller==null?"<color=#4EE5B5>生前生效</color>":"<color=#4EE5B5>生效中</color>") : "<color=#FF6B6B>失效 · "+GeneFailureReasons.Summary(eval)+"</color>";
            var nameTxt = UIFactory.NewText("Name", row1.transform, $"<b>{eval.Definition.DisplayName}</b> {activeStr}", 14, TextAnchor.MiddleLeft);
            var ntrt = nameTxt.rectTransform;
            ntrt.anchorMin = new Vector2(0, 0); ntrt.anchorMax = new Vector2(1, 1);
            ntrt.offsetMin = new Vector2(34, 0); ntrt.offsetMax = new Vector2(0, 0);nameTxt.horizontalOverflow=HorizontalWrapMode.Overflow;

            // ★ 修复（防Bug回归）：标题双击、提示和滚轮统一交给卡片，不再每行额外挂 EventTrigger。
            var row2 = UIFactory.NewRect("Row2", geneBlock.transform);
            var r2rt = row2.GetComponent<RectTransform>();
            r2rt.anchorMin = new Vector2(0, 1); r2rt.anchorMax = new Vector2(1, 1); r2rt.pivot = new Vector2(0, 1);
            r2rt.sizeDelta = new Vector2(-16, 24); r2rt.anchoredPosition = new Vector2(8,-38);

            var valLabel = UIFactory.NewText("ValLabel", row2.transform, "值:", 14, TextAnchor.MiddleLeft);
            var vlrt = valLabel.rectTransform;
            vlrt.anchorMin = new Vector2(0, 0); vlrt.anchorMax = new Vector2(0, 1); vlrt.pivot = new Vector2(0, 0.5f);
            vlrt.sizeDelta = new Vector2(25, 0); vlrt.anchoredPosition = Vector2.zero;

            var inputGO = UIFactory.NewRect("Input", row2.transform, typeof(Image), typeof(InputField));
            var inrt = inputGO.GetComponent<RectTransform>();
            inrt.anchorMin = new Vector2(0, 0.5f); inrt.anchorMax = new Vector2(0, 0.5f); inrt.pivot = new Vector2(0, 0.5f);
            inrt.sizeDelta = new Vector2(50, 20); inrt.anchoredPosition = new Vector2(30, 0);
            UIVisualTheme.Paint(inputGO.GetComponent<Image>(),"field");

            var inputText = UIFactory.NewText("Text", inputGO.transform, eval.Gene.Value.ToString("F1"), 14, TextAnchor.MiddleCenter);
            UIFactory.Stretch(inputText.rectTransform, 2, 2);
            var inputField = inputGO.GetComponent<InputField>();
            inputField.textComponent = inputText; inputField.text = eval.Gene.Value.ToString("F1");
            inputField.contentType = InputField.ContentType.DecimalNumber;inputField.readOnly=controller==null;
            // ★ 适配：固定出生分工已锁定遗传值，界面不能再显示可编辑却立即回滚的输入框。
            inputField.interactable=controller!=null&&controller.GetComponent<DarwinPark.Entities.Ecology.AntColonyMember>()?.Frozen!=true;

            string stateName = eval.State != null ? eval.State.DisplayName : "无状态";
            var stateTxt = UIFactory.NewText("StateTxt", row2.transform, $" | 状态: {stateName}", 14, TextAnchor.MiddleLeft);
            var strt = stateTxt.rectTransform;
            strt.anchorMin = new Vector2(0, 0); strt.anchorMax = new Vector2(1, 1); strt.pivot = new Vector2(0, 0.5f);
            strt.offsetMin = new Vector2(90, 0); strt.offsetMax = new Vector2(0, 0);
            // ★ 新增：运动/食性模式独立选择，数值输入仅调强度；继续经过房主权限和状态合法性检查。
            Dropdown mode=null;
            if(eval.Definition.ExplicitState&&controller!=null)
            {
                stateTxt.gameObject.SetActive(false);var modeObject=DefaultControls.CreateDropdown(new DefaultControls.Resources());modeObject.name="GeneMode";modeObject.transform.SetParent(row2.transform,false);mode=modeObject.GetComponent<Dropdown>();
                var mr=modeObject.GetComponent<RectTransform>();mr.anchorMin=new Vector2(0,0);mr.anchorMax=Vector2.one;mr.offsetMin=new Vector2(90,0);mr.offsetMax=Vector2.zero;
                UIVisualTheme.Paint(modeObject.GetComponent<Image>(),"field");RuntimeDropdownSkin.Apply(mode);foreach(var text in modeObject.GetComponentsInChildren<Text>(true)){text.font=UIFactory.DefaultFont;text.fontSize=13;text.color=UIVisualTheme.Text;}
                var states=eval.Definition.States.Where(s=>!eval.Gene.MissingStateIds.Contains(s.StateId)).ToList();mode.ClearOptions();mode.AddOptions(states.Select(s=>(eval.Definition.Category==GeneCategory.Locomotion?"演化方向：":"")+s.DisplayName).ToList());mode.SetValueWithoutNotify(Mathf.Max(0,states.FindIndex(s=>s.StateId==eval.Gene.StateId)));mode.interactable=WorldAuthority.CanEdit&&controller.GetComponent<DarwinPark.Entities.Ecology.AntColonyMember>()?.Frozen!=true;
                mode.onValueChanged.AddListener(index=>{if(index>=0&&index<states.Count){LanEditActions.SetGeneState(_currentEntity,eval.Gene.InstanceId,states[index].StateId);ShowEntityInfo(_currentEntity);}});
            }

            inputField.onEndEdit.AddListener((string input) => {
                if(!WorldAuthority.CanEdit)return;
                if (float.TryParse(input, out float newValue))
                {
                    newValue = Mathf.Clamp(newValue, 0f, 100f);
                    if (Mathf.Abs(newValue - eval.Gene.Value) > 0.01f)
                    {
                        LanEditActions.SetGene(_currentEntity,eval.Gene.InstanceId,newValue);
                        ShowEntityInfo(_currentEntity);
                    }
                    else inputField.text = eval.Gene.Value.ToString("F1");
                }
                else inputField.text = eval.Gene.Value.ToString("F1");
            });
            // ★ 修复（防Bug回归）：就地更新显示节点，避免周期重建抢走输入焦点或重置滚动位置。
            _rowRefresh.Add(()=>{
                if(geneBlock==null || controller==null)return;
                // ★ 适配：表达索引随重算刷新，避免每个可见基因行重新扫描。
                var live=controller.GetEvaluation(eval.Gene)??GeneFailureReasons.InspectGene(eval.Gene,null);if(live==null)return;
                nameTxt.text=$"<b>{live.Definition.DisplayName}</b> "+(live.IsActive?"<color=#4EE5B5>生效中</color>":"<color=#FF6B6B>失效 · "+GeneFailureReasons.Summary(live)+"</color>");
                stateTxt.text=" | 状态: "+(live.State?.DisplayName??"无状态");
                if(!inputField.isFocused)inputField.SetTextWithoutNotify(live.Gene.Value.ToString("F1"));
                geneIcon.color=live.IsActive?UIVisualTheme.Accent:UIVisualTheme.Muted;
            });
        }
    }
}
