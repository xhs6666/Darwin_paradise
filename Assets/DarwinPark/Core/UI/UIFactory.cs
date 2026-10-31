using System;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.Core
{
    /// <summary>
    /// UI 元素工厂。
    /// ★ 新增：统一字体/样式，消除各 UI 脚本重复建 Rect/Text/Button/Slider 代码。
    /// 字体带 fallback（LegacyRuntime.ttf → Arial.ttf），兼容不同 Unity 版本。
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;
        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                // ★ 适配：中文界面优先使用系统无衬线字体，避免默认字体缺字和图标字符兜底。
                var installed=Font.GetOSInstalledFontNames();
                foreach(var name in UIVisualTheme.Data.fontNames)
                    if(Array.Exists(installed,f=>string.Equals(f,name,StringComparison.OrdinalIgnoreCase)))
                    {_font=Font.CreateDynamicFontFromOSFont(name,16);break;}
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null) Log.Error(Log.Tag.UI, "找不到内置字体，UI 文本将不可见");
                return _font;
            }
        }

        public static GameObject NewRect(string name, Transform parent, params Type[] extra)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (extra != null && extra.Length > 0)
            {
                var all = new Type[1 + extra.Length];
                all[0] = typeof(RectTransform);
                Array.Copy(extra, 0, all, 1, extra.Length);
                DestroySafely(go); // 先建好再补组件，避免重复 RectTransform
                go = new GameObject(name, all);
            }
            go.transform.SetParent(parent, false);
            // ★ 适配：默认按钮与输入背景统一主题，业务脚本仍拥有原有位置和大小。
            if(go.TryGetComponent<Button>(out var button)) UIVisualTheme.StyleButton(button);
            else if(go.GetComponent<InputField>()!=null) UIVisualTheme.Paint(go.GetComponent<Image>(),"field");
            return go;
        }

        private static void DestroySafely(GameObject go)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        public static Text NewText(string name, Transform parent, string content,
            int fontSize = 16, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = NewRect(name, parent, typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.text = content;
            t.alignment = align;
            t.color = UIVisualTheme.Text;
            t.raycastTarget = false; // 规则附录：文本默认不参与射线
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Button NewButton(string name, Transform parent, string label,
            Action onClick, Vector2 size)
        {
            var go = NewRect(name, parent, typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            if (!string.IsNullOrEmpty(label))
            {
                var txt = NewText("Label", go.transform, label, 18, TextAnchor.MiddleCenter);
                Stretch(txt.rectTransform);
            }
            UIVisualTheme.StyleButton(btn);
            return btn;
        }

        public static Slider NewSlider(string name, Transform parent, string label,
            float min, float max, float value, float width, out Text labelText)
        {
            var row = NewRect(name + "Row", parent);
            var rrt = row.GetComponent<RectTransform>();
            rrt.sizeDelta = new Vector2(width, 32);

            labelText = NewText(name + "Label", row.transform, label, 14);
            var lrt = labelText.rectTransform;
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(0, 1);
            lrt.pivot = new Vector2(0, 0.5f);
            lrt.sizeDelta = new Vector2(60, 0);
            lrt.anchoredPosition = Vector2.zero;

            var sliderGO = NewRect(name + "Slider", row.transform, typeof(Slider));
            var srt = sliderGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0);
            srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(0, 0.5f);
            srt.offsetMin = new Vector2(64, 4);
            srt.offsetMax = new Vector2(-4, -4);

            var slider = sliderGO.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            BuildSliderVisuals(sliderGO.transform, slider);
            return slider;
        }

        private static void BuildSliderVisuals(Transform parent, Slider slider)
        {
            var bg = NewRect("Background", parent, typeof(Image));
            var bgRect=bg.GetComponent<RectTransform>();
            bgRect.anchorMin=new Vector2(0,.5f);bgRect.anchorMax=new Vector2(1,.5f);bgRect.sizeDelta=new Vector2(0,8);
            UIVisualTheme.Paint(bg.GetComponent<Image>(),"track");
            bg.GetComponent<Image>().raycastTarget=false;

            var fillArea = NewRect("FillArea", parent);
            var areaRect=fillArea.GetComponent<RectTransform>();
            areaRect.anchorMin=new Vector2(0,.5f);areaRect.anchorMax=new Vector2(1,.5f);areaRect.sizeDelta=new Vector2(-16,8);

            var fill = NewRect("Fill", fillArea.transform, typeof(Image));
            Stretch(fill.GetComponent<RectTransform>());
            UIVisualTheme.Paint(fill.GetComponent<Image>(),"fill");fill.GetComponent<Image>().raycastTarget=false;

            var handle = NewRect("Handle", parent, typeof(Image));
            UIVisualTheme.Paint(handle.GetComponent<Image>(),"thumb");
            handle.GetComponent<RectTransform>().anchorMin=handle.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,.5f);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(16, 16);

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
        }

        public static void Stretch(RectTransform rt, float padX = 0, float padY = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
