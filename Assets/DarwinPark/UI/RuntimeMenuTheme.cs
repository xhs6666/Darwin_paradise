using DarwinPark.Core;
using UnityEngine;
namespace DarwinPark.UI
{
    // ★ 适配：菜单/基因树共用九宫格主题，保留原窗口矩形和业务事件。
    public static class RuntimeMenuTheme
    {
        private static GUISkin _skin;
        private static GUIStyle _sliderInput,_sliderThumbInput;
        public static GUIStyle Primary { get; private set; }
        public static GUIStyle Danger { get; private set; }
        public static GUIStyle Selected { get; private set; }
        public static GUIStyle Muted { get; private set; }
        public static GUIStyle Title { get; private set; }
        public static GUIStyle Subtitle { get; private set; }
        private static Texture2D Texture(string name) => UIVisualTheme.SurfaceSprite(name)?.texture;
        private static void ClearScaledBackgrounds(GUIStyleState state)
        {
            // ★ 修复（防Bug回归）：scaledBackgrounds只有编辑器API，发布版不能引用；编辑器仍需清除旧高DPI皮肤。
#if UNITY_EDITOR
            state.scaledBackgrounds=System.Array.Empty<Texture2D>();
#endif
        }
        private static void States(GUIStyle style, string normal, string hover, string pressed, Color text)
        {
            // ★ 修复（防Bug回归）：系统高 DPI 会优先使用旧 scaledBackgrounds，必须与普通背景一起清除。
            foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused})
                ClearScaledBackgrounds(state);
            style.border = new RectOffset(16,16,16,16);
            style.normal.background=Texture(normal);style.normal.textColor=text;
            style.hover.background=Texture(hover);style.hover.textColor=text;
            style.active.background=Texture(pressed);style.active.textColor=text;
            style.focused.background=Texture(hover);style.focused.textColor=text;
            style.onNormal.background=Texture(hover);style.onNormal.textColor=text;
            style.onHover.background=Texture(hover);style.onHover.textColor=text;
            style.onActive.background=Texture(pressed);style.onActive.textColor=text;
            style.onFocused.background=Texture(hover);style.onFocused.textColor=text;
        }
        public static void Apply()
        {
            if(_skin==null)
            {
                _skin=Object.Instantiate(GUI.skin);
                _skin.font=UIFactory.DefaultFont;
                foreach(var style in new[]{_skin.label,_skin.button,_skin.toggle,_skin.box,_skin.textField})
                {style.fontSize=16;style.normal.textColor=UIVisualTheme.Text;}
                States(_skin.box,"panel","panel","panel",UIVisualTheme.Text);
                States(_skin.button,"button","hover","pressed",UIVisualTheme.Text);
                States(_skin.textField,"field","field","selected",UIVisualTheme.Text);
                States(_skin.textArea,"field","field","field",UIVisualTheme.Text);
                _skin.button.fixedHeight=0;_skin.button.padding=new RectOffset(12,12,6,6);
                _skin.textField.fixedHeight=26;_skin.textField.padding=new RectOffset(10,10,3,3);
                _skin.textArea.fontSize=14;_skin.textArea.padding=new RectOffset(10,10,6,6);
                _skin.settings.selectionColor=new Color(UIVisualTheme.Accent.r,UIVisualTheme.Accent.g,UIVisualTheme.Accent.b,.35f);
                _skin.settings.cursorColor=UIVisualTheme.Accent;
                States(_skin.horizontalSlider,"track","track","track",Color.white);
                _skin.horizontalSlider.fixedHeight=8;_skin.horizontalSlider.margin=new RectOffset(0,0,9,9);
                States(_skin.horizontalSliderThumb,"thumb","primary_hover","primary",Color.white);
                _skin.horizontalSliderThumb.fixedWidth=20;_skin.horizontalSliderThumb.fixedHeight=20;
                // ★ 修复（防Bug回归）：透明资源阻止 Unity 的平台默认轨道再次覆盖自绘填充。
                var clear=Resources.Load<Texture2D>("UI/Surfaces/clear");
                _sliderInput=new GUIStyle(){fixedHeight=20};
                _sliderThumbInput=new GUIStyle(){fixedHeight=20,fixedWidth=20};
                foreach(var style in new[]{_sliderInput,_sliderThumbInput})
                    foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused})
                    {state.background=clear;ClearScaledBackgrounds(state);}
                States(_skin.verticalScrollbar,"track","track","track",Color.white);
                States(_skin.verticalScrollbarThumb,"field","hover","pressed",Color.white);
                States(_skin.horizontalScrollbar,"track","track","track",Color.white);
                States(_skin.horizontalScrollbarThumb,"field","hover","pressed",Color.white);
                Primary=new GUIStyle(_skin.button);States(Primary,"primary","primary_hover","primary_pressed",UIVisualTheme.Ink);Primary.fontStyle=FontStyle.Bold;
                Danger=new GUIStyle(_skin.button);States(Danger,"danger","danger","pressed",UIVisualTheme.Danger);
                Selected=new GUIStyle(_skin.button);States(Selected,"selected","hover","pressed",UIVisualTheme.Accent);
                Muted=new GUIStyle(_skin.label);Muted.normal.textColor=UIVisualTheme.Muted;
                Title=new GUIStyle(_skin.label){fontSize=25,fontStyle=FontStyle.Bold};
                Subtitle=new GUIStyle(Muted){fontSize=13};
            }
            GUI.skin=_skin;
        }
        public static void Icon(Rect rect,string id,Color color)
        {
            var sprite=UIIconRegistry.Get(id);if(sprite==null)return;
            var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,sprite.texture,ScaleMode.ScaleToFit,true);GUI.color=old;
        }
        public static void Divider()
        {
            var rect=GUILayoutUtility.GetRect(1,1,GUILayout.ExpandWidth(true));
            var old=GUI.color;GUI.color=UIVisualTheme.Border;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;
        }
        public static bool Button(string label,string icon=null,bool primary=false,float height=48,bool danger=false)
        {
            var style=new GUIStyle(danger?Danger:primary?Primary:GUI.skin.button);
            if(icon!=null)style.padding.left+=26;
            bool pressed=GUILayout.Button(label,style,GUILayout.Height(height));
            var rect=GUILayoutUtility.GetLastRect();
            if(icon!=null)Icon(new Rect(rect.x+18,rect.center.y-12,24,24),icon,primary?UIVisualTheme.Ink:UIVisualTheme.Text);
            return pressed;
        }
        public static bool MenuButton(string label,string icon,bool primary=false,string badge=null)
        {
            var style=new GUIStyle(primary?Primary:GUI.skin.button){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(64,38,6,6)};
            // ★ 修复（防Bug回归）：配合缩短主菜单宽度，按钮增加高度，避免又长又扁。
            bool pressed=GUILayout.Button(label,style,GUILayout.Height(58));var rect=GUILayoutUtility.GetLastRect();
            var color=primary?UIVisualTheme.Ink:UIVisualTheme.Text;
            Icon(new Rect(rect.x+18,rect.center.y-13,26,26),icon,color);
            Icon(new Rect(rect.xMax-30,rect.center.y-9,18,18),"ui_icon_next",color);
            if(badge!=null)
            {
                var badgeStyle=new GUIStyle(GUI.skin.box){fontSize=12,alignment=TextAnchor.MiddleCenter,padding=new RectOffset()};
                badgeStyle.normal.textColor=UIVisualTheme.Muted;
                GUI.Label(new Rect(rect.xMax-112,rect.center.y-12,72,24),badge,badgeStyle);
            }
            return pressed;
        }
        public static float Slider(float value,float min,float max)
        {
            // ★ 新增：填充只作装饰；透明原生滑条继续拥有拖动、键盘和焦点处理。
            var rect=GUILayoutUtility.GetRect(80,26,GUILayout.ExpandWidth(true));
            return Slider(rect,value,min,max);
        }
        public static float Slider(Rect rect,float value,float min,float max)
        {
            value=GUI.HorizontalSlider(new Rect(rect.x,rect.center.y-10,rect.width,20),value,min,max,_sliderInput,_sliderThumbInput);
            var bar=new Rect(rect.x,rect.center.y-4,rect.width,8);
            GUI.DrawTexture(bar,Texture("track"),ScaleMode.StretchToFill,true,0,Color.white,0,4);
            float fraction=Mathf.InverseLerp(min,max,value);
            if(fraction>0)GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*fraction,bar.height),Texture("fill"),ScaleMode.StretchToFill,true,0,Color.white,0,4);
            GUI.DrawTexture(new Rect(rect.x+(rect.width-20)*fraction,rect.center.y-10,20,20),Texture("thumb"));
            return value;
        }
    }
}
