using UnityEngine;
namespace DarwinPark.UI
{
    // ★ 新增：主界面与工具统一字体和点击高度，适配高 DPI 预览。
    public static class RuntimeMenuTheme
    {
        private static GUISkin _skin;
        public static void Apply()
        {
            if(_skin==null)
            {
                _skin=Object.Instantiate(GUI.skin);
                foreach(var style in new[]{_skin.label,_skin.button,_skin.toggle,_skin.box,_skin.textField})style.fontSize=16;
                _skin.button.fixedHeight=0;_skin.button.padding.top=6;_skin.button.padding.bottom=6;_skin.textField.fixedHeight=26;_skin.textArea.fontSize=14;
            }
            GUI.skin=_skin;
        }
    }
}
