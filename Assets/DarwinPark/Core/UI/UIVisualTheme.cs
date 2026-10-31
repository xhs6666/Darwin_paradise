using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.Core
{
    // ★ 新增：两套 UI 共用资源与颜色；仅设置 Graphic，不触碰已有布局、锚点或射线规则。
    public static class UIVisualTheme
    {
        [Serializable] public sealed class IconEntry { public string id, icon; }
        [Serializable] public sealed class StatEntry { public string id, label, format;public bool bar; }
        [Serializable] public sealed class LayerEntry { public string id, label; }
        [Serializable] public sealed class ThemeData
        {
            public string[] fontNames;
            public string accent, text, muted, ink, border, panel, surface, danger, favorite;
            public IconEntry[] geneIcons;
            public StatEntry[] summaryStats;
            public LayerEntry[] layerButtons;
        }
        private static ThemeData _data;
        private static readonly Dictionary<string, Sprite> Surfaces = new Dictionary<string, Sprite>();
        public static ThemeData Data
        {
            get
            {
                if (_data != null) return _data;
                var asset = Resources.Load<TextAsset>("UI/theme");
                if (asset == null) throw new InvalidOperationException("缺少 UI/theme.json 主题配置");
                _data = JsonUtility.FromJson<ThemeData>(asset.text);
                return _data;
            }
        }
        private static Color Parse(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        public static Color Accent => Parse(Data.accent);
        public static Color Text => Parse(Data.text);
        public static Color Muted => Parse(Data.muted);
        public static Color Ink => Parse(Data.ink);
        public static Color Border => Parse(Data.border);
        public static Color Panel => Parse(Data.panel);
        public static Color Surface => Parse(Data.surface);
        public static Color Danger => Parse(Data.danger);
        public static Color Favorite => Parse(Data.favorite);
        public static Sprite SurfaceSprite(string name)
        {
            if (Surfaces.TryGetValue(name, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("UI/Surfaces/" + name);
            if (sprite == null) Debug.LogWarning("[UI主题] 缺少表面资源：" + name);
            Surfaces[name] = sprite;
            return sprite;
        }
        public static string GeneIcon(string id)
        {
            foreach (var entry in Data.geneIcons) if (entry.id == id) return entry.icon;
            return "dna";
        }
        public static void Paint(Image image, string surface = "panel")
        {
            if (image == null) return;
            image.sprite = SurfaceSprite(surface);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            // ★ 适配：九宫格边角按 UI 像素显示，保留所有原始点击范围。
            image.pixelsPerUnitMultiplier = 1;
        }
        public static void StyleButton(Button button, bool primary = false)
        {
            Paint(button.image, primary ? "primary" : "button");
            button.transition = Selectable.Transition.SpriteSwap;
            var states = button.spriteState;
            states.highlightedSprite = SurfaceSprite(primary ? "primary_hover" : "hover");
            states.pressedSprite = SurfaceSprite(primary ? "primary_pressed" : "pressed");
            states.selectedSprite = states.highlightedSprite;
            states.disabledSprite = SurfaceSprite("button");
            button.spriteState = states;
            foreach (var label in button.GetComponentsInChildren<Text>(true)) label.color = primary ? Ink : Text;
        }
        public static Image AddIcon(Transform parent, string id, Vector2 size, Vector2 position, Vector2 anchor)
        {
            var go = UIFactory.NewRect("ThemeIcon", parent, typeof(Image), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.sizeDelta = size; rt.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.sprite = UIIconRegistry.Get(id);
            image.color = Accent; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }
    }
}
