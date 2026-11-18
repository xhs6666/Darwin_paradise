using DarwinPark.Core;
using DarwinPark.Environment;
using DarwinPark.Planet;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ✦ 新增：独立维护Parameters视图；保持现有组件序列化与调用接口。
    public partial class SimulationControlPanel
    {
        private void BuildParamSection(GameObject parent)
        {
            var grid = parent.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(80, 80);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleCenter;

            CreateParamButton("btn_param_temp", "温度", parent.transform);
            CreateParamButton("btn_param_hum", "湿度", parent.transform);
            CreateParamButton("btn_param_terrain", "地形", parent.transform);
            CreateParamButton("btn_param_seed", "种子", parent.transform);
        }
        private void CreateParamButton(string iconId, string fallbackName, Transform parent)
        {
            var btnGO = UIFactory.NewRect(iconId, parent, typeof(Image), typeof(Button), typeof(RightClickHandler));
            var bg = btnGO.GetComponent<Image>();
            UIVisualTheme.Paint(bg,"button");

            var sprite = UIIconRegistry.Get(iconId);
            if (sprite != null)
            {
                var iconGO = UIFactory.NewRect("Icon", btnGO.transform, typeof(Image));
                var irt = iconGO.GetComponent<RectTransform>();
                UIFactory.Stretch(irt, 20, 20);
                var iconImg = iconGO.GetComponent<Image>();
                iconImg.sprite = sprite;
                iconImg.preserveAspect = true;
                iconImg.color=UIVisualTheme.Muted;
                iconImg.raycastTarget = false;
            }

            var txt = UIFactory.NewText("Name", btnGO.transform, fallbackName, 12, TextAnchor.LowerCenter);
            UIFactory.Stretch(txt.rectTransform, 2, 5);
            txt.raycastTarget = false;

            // ★ 修复（防Bug回归）：参数左键打开，右键只取消，不再把滑条释放冒泡到覆盖层的关闭按钮。
            btnGO.GetComponent<Button>().onClick.AddListener(() => ToggleParamPopup(iconId, fallbackName));
            btnGO.GetComponent<RightClickHandler>().OnRightClick += DestroyMenus;
        }
        private void ToggleParamPopup(string paramId, string paramName)
        {
            if(!WorldAuthority.CanEdit)return;
            if (_activeParamMenu != null && _activeParamId==paramId) { DestroyMenus(); return; }
            DestroyMenus();

            var root = ServiceLocator.Get<UIRoot>("需要 UIRoot").MainCanvas.transform;

            _activeParamId=paramId;
            _activeParamMenu = UIFactory.NewRect("ParamMenuOverlay", root, typeof(Image), typeof(RightClickHandler));
            var oRt = _activeParamMenu.GetComponent<RectTransform>();
            oRt.anchorMin = Vector2.zero; oRt.anchorMax = Vector2.one; oRt.offsetMin = Vector2.zero; oRt.offsetMax = Vector2.zero;
            _activeParamMenu.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            // ★ 修复（防Bug回归）：透明根节点不挡住原按钮，允许再次点击关闭。
            _activeParamMenu.GetComponent<Image>().raycastTarget=false;
            _activeParamMenu.GetComponent<RightClickHandler>().OnRightClick += DestroyMenus;

            var menu = UIFactory.NewRect("ParamMenuContent", _activeParamMenu.transform, typeof(Image));
            var mrt = menu.GetComponent<RectTransform>();
            UIVisualTheme.Paint(menu.GetComponent<Image>());
            menu.GetComponent<Image>().raycastTarget = true;

            mrt.anchorMin = new Vector2(0.5f, 0);
            mrt.anchorMax = new Vector2(0.5f, 0);
            mrt.pivot = new Vector2(0.5f, 0);
            mrt.anchoredPosition = new Vector2(0, 225f);
            mrt.sizeDelta = new Vector2(420, 90);

            var title = UIFactory.NewText("Title", menu.transform, "设定" + paramName + " (全年平均)", 16, TextAnchor.MiddleCenter);
            title.color = UIVisualTheme.Text;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            trt.anchoredPosition = new Vector2(0, -8); trt.sizeDelta = new Vector2(0, 20);

            // ★ 适配：参数草稿、文本输入与 Shift 应用由独立编辑器管理，种子不再落入空分支。
            var editor=menu.AddComponent<PlanetParameterEditor>();
            editor.Initialize(paramId, paramName);
            editor.CancelRequested=DestroyMenus;
        }
    }
}
