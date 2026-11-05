using DarwinPark.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 修复（防Bug回归）：DefaultControls没有内置精灵时，勾选图与滚动条会变成纯绿/纯白块；完整处理模板子控件。
    public static class RuntimeDropdownSkin
    {
        public static void Apply(Dropdown dropdown)
        {
            // ★ 适配：所有运行时下拉共用初始化保护、Esc优先关闭和模板清理，避免各入口再次漏修。
            if(dropdown.GetComponent<RuntimeDropdownDismissal>()==null)dropdown.gameObject.AddComponent<RuntimeDropdownDismissal>();
            UIVisualTheme.Paint(dropdown.template.GetComponent<Image>());
            foreach(var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                UIVisualTheme.Paint(toggle.targetGraphic as Image,"field");
                if(toggle.graphic!=null){toggle.graphic.enabled=false;toggle.graphic.raycastTarget=false;}
                var colors=toggle.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.8f,.95f,.9f,1);colors.selectedColor=Color.white;toggle.colors=colors;
            }
            foreach(var scrollbar in dropdown.template.GetComponentsInChildren<Scrollbar>(true))
            {
                UIVisualTheme.Paint(scrollbar.GetComponent<Image>(),"field");UIVisualTheme.Paint(scrollbar.handleRect.GetComponent<Image>(),"hover");
                var colors=scrollbar.colors;colors.normalColor=colors.highlightedColor=colors.selectedColor=Color.white;scrollbar.colors=colors;
            }
        }
    }
}
