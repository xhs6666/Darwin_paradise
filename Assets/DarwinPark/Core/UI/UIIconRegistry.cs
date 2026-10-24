using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Core
{
    /// <summary>
    /// UI 图标自动注册表。
    /// ★ 新增：扫描 Resources/UI/Icons 下的所有 Sprite，用文件名作为 ID 注册。
    /// 遵循数据驱动，新增图标无需修改代码。
    /// </summary>
    public static class UIIconRegistry
    {
        private static Dictionary<string, Sprite> _icons;
        private static bool _initialized = false;

        public static void Init()
        {
            if (_initialized) return;
            _icons = new Dictionary<string, Sprite>();

            // 扫描所有图标
            var sprites = Resources.LoadAll<Sprite>("UI/Icons");
            foreach (var sprite in sprites)
            {
                if (!_icons.ContainsKey(sprite.name))
                {
                    _icons.Add(sprite.name, sprite);
                }
                else
                {
                    Debug.LogWarning($"[UIIconRegistry] 发现重复图标 ID: {sprite.name}");
                }
            }

            _initialized = true;
        }

        public static Sprite Get(string id)
        {
            if (!_initialized) Init();
            if (string.IsNullOrEmpty(id)) return null;

            if (_icons.TryGetValue(id, out var sprite))
            {
                return sprite;
            }

            // 不输出报错，允许静默返回 null 以便 UI 显示纯色块兜底
            return null;
        }

        public static IEnumerable<string> GetAllIds()
        {
            if (!_initialized) Init();
            return _icons.Keys;
        }
    }
}