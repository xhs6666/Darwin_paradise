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

using System;
namespace DarwinPark.UI
{
    // ✦ 新增：布局工厂返回控件引用；回调由控制器提供，不读取或修改世界。
    internal static class GeneInspectorLayout
    {
        internal sealed class View
        {
            public GameObject panel;
            public Button favBtn;
            public Text favBtnText;
            public Image favIcon;
            public InputField nameInput;
            public Text titleText;
            public Transform contentParent;
            public ScrollRect scroll;
            public GameObject tooltipPanel;
            public Text tooltipText;
            public GameObject treePanel;
            public Text treeText;
            public GameObject addGenePanel;
            public Transform addGeneListParent;
        }
        public static View Build(Transform canvas,Vector2 infoCornerOffset,Vector2 infoPanelSize,Vector2 addCornerOffset,UnityEngine.Events.UnityAction favorite,UnityEngine.Events.UnityAction<string> rename,Action hide,Func<DarwinEntity> currentEntity)
        {
            var view=new View();

            view.panel = UIFactory.NewRect("GenePanel", canvas, typeof(Image));
            var rt = view.panel.GetComponent<RectTransform>();
            // ★ 适配：信息面板放在屏幕左上，添加面板放在左下并避开底部控制栏。
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0,1);
            // ★ 修复（防Bug回归）：信息面板改为更宽、更矮，尺寸集中配置，避免下一帧又被旧固定尺寸覆盖。
            rt.anchoredPosition = FrameRateDisplay.ReserveTop(infoCornerOffset,canvas.GetComponent<Canvas>()); rt.sizeDelta = infoPanelSize;
            UIVisualTheme.Paint(view.panel.GetComponent<Image>());
            // ★ 修复（防Bug回归）：面板背景必须挡住游戏射线与右键，避免编辑时拾取身后的星球。
            view.panel.GetComponent<Image>().raycastTarget = true;

            var titleBg = UIFactory.NewRect("TitleBg", view.panel.transform, typeof(Image));
            var trt = titleBg.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            // ★ 适配：标题只保留可编辑名称，压缩高度并给各按钮独立区域。
            trt.sizeDelta = new Vector2(0, 38);
            UIVisualTheme.Paint(titleBg.GetComponent<Image>(),"button");

            view.favBtn = UIFactory.NewRect("FavBtn", titleBg.transform, typeof(Image), typeof(Button)).GetComponent<Button>();
            var frt = view.favBtn.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0, 0.5f); frt.anchorMax = new Vector2(0, 0.5f); frt.pivot = new Vector2(0, 0.5f);
            frt.anchoredPosition = new Vector2(8, 0); frt.sizeDelta = new Vector2(28, 28);
            UIVisualTheme.StyleButton(view.favBtn);
            view.favBtnText = UIFactory.NewText("Star", view.favBtn.transform, "☆", 20, TextAnchor.MiddleCenter);
            UIFactory.Stretch(view.favBtnText.rectTransform);
            view.favBtnText.enabled=false;
            view.favIcon=UIVisualTheme.AddIcon(view.favBtn.transform,"star",new Vector2(22,22),Vector2.zero,new Vector2(.5f,.5f));
            view.favBtn.onClick.AddListener(favorite);

            var nameGO = UIFactory.NewRect("NameInput", titleBg.transform, typeof(Image), typeof(InputField));
            var nrt = nameGO.GetComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(44, 5); nrt.offsetMax = new Vector2(-126, -5);
            UIVisualTheme.Paint(nameGO.GetComponent<Image>(),"field");
            view.nameInput = nameGO.GetComponent<InputField>(); view.nameInput.characterLimit = 24;
            view.titleText = UIFactory.NewText("Text", nameGO.transform, "", 16);
            UIFactory.Stretch(view.titleText.rectTransform, 6, 1); view.nameInput.textComponent = view.titleText;
            view.nameInput.onEndEdit.AddListener(rename);

            var saveTplGO = UIFactory.NewRect("SaveTplBtn", titleBg.transform, typeof(Image), typeof(Button));
            var strt = saveTplGO.GetComponent<RectTransform>();
            strt.anchorMin = new Vector2(1, 0.5f); strt.anchorMax = new Vector2(1, 0.5f); strt.pivot = new Vector2(1, 0.5f);
            strt.anchoredPosition = new Vector2(-40, 0); strt.sizeDelta = new Vector2(74, 28);
            var saveBtn = saveTplGO.GetComponent<Button>();
            var saveTxt = UIFactory.NewText("Txt", saveTplGO.transform, "存为模板", 14, TextAnchor.MiddleCenter);
            UIFactory.Stretch(saveTxt.rectTransform);
            UIVisualTheme.StyleButton(saveBtn,true);
            saveBtn.onClick.AddListener(() => { if (currentEntity() != null) EntityTemplateManager.SaveTemplate(currentEntity()); });

            // ★ 修复（防Bug回归）：面板挡住世界输入后提供显式关闭入口。
            var closePanel = UIFactory.NewButton("ClosePanel", titleBg.transform, "×", () =>
            {
                hide();
            }, new Vector2(26, 28));
            var closeRect = closePanel.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, .5f);
            closeRect.pivot = new Vector2(1, .5f); closeRect.anchoredPosition = new Vector2(-8, 0);

            var scrollGO = UIFactory.NewRect("ScrollView", view.panel.transform, typeof(ScrollRect));
            var srt = scrollGO.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(10, 10); srt.offsetMax = new Vector2(-10, -46);

            var viewport = UIFactory.NewRect("Viewport", scrollGO.transform, typeof(Image), typeof(RectMask2D));
            UIFactory.Stretch(viewport.GetComponent<RectTransform>());
            // ★ 修复（防Bug回归）：列表只需矩形裁剪，不使用首次打开时可能遮住整块内容的模板遮罩材质。
            viewport.GetComponent<Image>().color = Color.clear;

            var contentGO = UIFactory.NewRect("Content", viewport.transform);
            var crt = contentGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, 0);
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(15, 15, 10, 10); vlg.spacing = 8;
            vlg.childControlHeight = true; vlg.childControlWidth = true; vlg.childForceExpandHeight = false;
            var fitter = contentGO.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.contentParent = contentGO.transform;

            var scroll = scrollGO.GetComponent<ScrollRect>();
            view.scroll=scroll;scroll.scrollSensitivity=25;
            scroll.content = crt; scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.horizontal = false; scroll.vertical = true;

            view.tooltipPanel = UIFactory.NewRect("Tooltip", canvas, typeof(Image));
            var ttrt = view.tooltipPanel.GetComponent<RectTransform>();
            ttrt.sizeDelta = new Vector2(200, 100); ttrt.pivot = new Vector2(0, 1);
            UIVisualTheme.Paint(view.tooltipPanel.GetComponent<Image>());
            view.tooltipPanel.GetComponent<Image>().raycastTarget = false;
            view.tooltipText = UIFactory.NewText("TText", view.tooltipPanel.transform, "", 13, TextAnchor.UpperLeft);
            UIFactory.Stretch(view.tooltipText.rectTransform, 10, 10);
            view.tooltipPanel.SetActive(false);

            view.treePanel = UIFactory.NewRect("TreePanel", canvas, typeof(Image));
            var trtrt = view.treePanel.GetComponent<RectTransform>();
            trtrt.anchorMin = new Vector2(0.5f, 0.5f); trtrt.anchorMax = new Vector2(0.5f, 0.5f); trtrt.pivot = new Vector2(0.5f, 0.5f);
            trtrt.sizeDelta = new Vector2(400, 300);
            UIVisualTheme.Paint(view.treePanel.GetComponent<Image>());
            view.treeText = UIFactory.NewText("TreeText", view.treePanel.transform, "", 16, TextAnchor.MiddleCenter);
            UIFactory.Stretch(view.treeText.rectTransform);
            view.treePanel.SetActive(false);
            view.treePanel.AddComponent<Button>().onClick.AddListener(() => view.treePanel.SetActive(false));

            view.addGenePanel = UIFactory.NewRect("AddGenePanel", canvas, typeof(Image));
            var agprt = view.addGenePanel.GetComponent<RectTransform>();
            agprt.anchorMin=agprt.anchorMax=agprt.pivot=Vector2.zero;
            agprt.anchoredPosition=addCornerOffset;
            agprt.sizeDelta = new Vector2(350, 450);
            UIVisualTheme.Paint(view.addGenePanel.GetComponent<Image>());
            var agTitle = UIFactory.NewText("Title", view.addGenePanel.transform, "选择要添加/复制的基因", 18, TextAnchor.MiddleCenter);
            agTitle.rectTransform.anchorMin = new Vector2(0, 1); agTitle.rectTransform.anchorMax = new Vector2(1, 1); agTitle.rectTransform.sizeDelta = new Vector2(0, 40);
            // ★ 修复（防Bug回归）：标题置于面板内并避开关闭按钮；新增基因数量增多后明确提示滚动。
            agTitle.rectTransform.pivot=new Vector2(.5f,1);agTitle.rectTransform.offsetMin=new Vector2(8,-40);agTitle.rectTransform.offsetMax=new Vector2(-45,0);
            var agHint=UIFactory.NewText("Hint",view.addGenePanel.transform,"滚轮向下查看更多基因",13,TextAnchor.MiddleCenter);
            agHint.rectTransform.anchorMin=Vector2.zero;agHint.rectTransform.anchorMax=new Vector2(1,0);agHint.rectTransform.pivot=new Vector2(.5f,0);agHint.rectTransform.sizeDelta=new Vector2(0,24);
            var closeBtn = UIFactory.NewButton("Close", view.addGenePanel.transform, "X", () => view.addGenePanel.SetActive(false), new Vector2(30, 30));
            var cbrt = closeBtn.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1, 1); cbrt.anchorMax = new Vector2(1, 1); cbrt.pivot = new Vector2(1, 1); cbrt.anchoredPosition = new Vector2(-10, -5);

            var agScroll = UIFactory.NewRect("Scroll", view.addGenePanel.transform, typeof(ScrollRect));
            var agsrt = agScroll.GetComponent<RectTransform>();
            // ★ 适配：分类筛选独占标题下方一行，不与滚动列表重叠。
            agsrt.anchorMin = Vector2.zero; agsrt.anchorMax = Vector2.one; agsrt.offsetMin = new Vector2(10, 28); agsrt.offsetMax = new Vector2(-10, -84);
            var agView = UIFactory.NewRect("View", agScroll.transform, typeof(Image), typeof(RectMask2D));
            UIFactory.Stretch(agView.GetComponent<RectTransform>());
            agView.GetComponent<RectTransform>().offsetMax=new Vector2(-20,0);
            agView.GetComponent<Image>().color = Color.clear;
            var agContent = UIFactory.NewRect("Content", agView.transform);
            var agcrt = agContent.GetComponent<RectTransform>();
            agcrt.anchorMin = new Vector2(0, 1); agcrt.anchorMax = new Vector2(1, 1); agcrt.pivot = new Vector2(0.5f, 1); agcrt.sizeDelta = new Vector2(0, 0);
            var agVlg = agContent.AddComponent<VerticalLayoutGroup>();
            agVlg.spacing = 5; agVlg.childControlHeight = false; agVlg.childControlWidth = true;
            var agFitter = agContent.AddComponent<ContentSizeFitter>(); agFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.addGeneListParent = agContent.transform;
            var agScrollRect = agScroll.GetComponent<ScrollRect>();
            agScrollRect.content = agcrt; agScrollRect.viewport = agView.GetComponent<RectTransform>(); agScrollRect.horizontal = false; agScrollRect.vertical = true;
            // ★ 新增：持续显示滚动条，避免新增条目在列表下方却无法被发现。
            var track=UIFactory.NewRect("Scrollbar",agScroll.transform,typeof(Image),typeof(Scrollbar));
            var trackRect=track.GetComponent<RectTransform>();trackRect.anchorMin=new Vector2(1,0);trackRect.anchorMax=Vector2.one;trackRect.pivot=Vector2.one;trackRect.sizeDelta=new Vector2(14,0);
            UIVisualTheme.Paint(track.GetComponent<Image>(),"track");
            var handle=UIFactory.NewRect("Handle",track.transform,typeof(Image));UIFactory.Stretch(handle.GetComponent<RectTransform>());
            UIVisualTheme.Paint(handle.GetComponent<Image>(),"fill");
            var bar=track.GetComponent<Scrollbar>();bar.handleRect=handle.GetComponent<RectTransform>();bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
            agScrollRect.verticalScrollbar=bar;agScrollRect.scrollSensitivity=30;agScrollRect.movementType=ScrollRect.MovementType.Clamped;
            view.addGenePanel.SetActive(false);
        
            return view;
        }
    }
}
