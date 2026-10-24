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
            t.color = Color.white;
            t.raycastTarget = false; // 规则附录：文本默认不参与射线
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Button NewButton(string name, Transform parent, string label,
            Action onClick, Vector2 size)
        {
            var go = NewRect(name, parent, typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.3f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            if (!string.IsNullOrEmpty(label))
            {
                var txt = NewText("Label", go.transform, label, 18, TextAnchor.MiddleCenter);
                Stretch(txt.rectTransform);
            }
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
            Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);

            var fillArea = NewRect("FillArea", parent);
            Stretch(fillArea.GetComponent<RectTransform>(), 0, 10);

            var fill = NewRect("Fill", fillArea.transform, typeof(Image));
            Stretch(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = new Color(0.3f, 0.75f, 0.4f);

            var handle = NewRect("Handle", parent, typeof(Image));
            handle.GetComponent<Image>().color = Color.white;
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