using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Core
{
    /// <summary>
    /// 泛型 JSON 资源库加载基类。
    /// ★ 新增：统一 Resources.LoadAll + 解析 + 校验 + 缓存，消除 4 份重复逻辑（规则1）。
    /// 子类只需提供：资源目录、ID 提取、JSON→对象转换、加载后处理。
    /// </summary>
    public abstract class JsonResourceDatabase<T> where T : class
    {
        private Dictionary<string, T> _cache;
        private readonly Log.Tag _tag;

        protected JsonResourceDatabase(Log.Tag tag) { _tag = tag; }

        protected abstract string ResourceFolder { get; }          // 如 "Genes"
        protected abstract string GetId(T item);                   // 从已转换对象提取 id
        protected abstract T ConvertFromJson(string jsonText, string assetName);
        protected virtual void OnPostLoad(IReadOnlyDictionary<string, T> all) { }

        public void EnsureLoaded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, T>();

            var assets = Resources.LoadAll<TextAsset>(ResourceFolder);
            if (assets == null || assets.Length == 0)
            {
                Log.Error(_tag, $"{ResourceFolder} 下未找到任何配置");
                OnPostLoad(_cache);
                return;
            }

            foreach (var a in assets)
            {
                try
                {
                    T item = ConvertFromJson(a.text, a.name);
                    if (item == null) { Log.Error(_tag, $"解析为空：{a.name}"); continue; }

                    string id = GetId(item);
                    if (string.IsNullOrEmpty(id))
                    {
                        Log.Error(_tag, $"缺少 id：{a.name}");
                        continue;
                    }
                    if (id != a.name)
                        Log.Error(_tag, $"id≠文件名：{a.name} vs {id}");

                    if (_cache.ContainsKey(id))
                    {
                        Log.Warn(_tag, $"重复 id：{id}（{a.name}）");
                        _cache[id] = item;
                    }
                    else _cache[id] = item;
                }
                catch (Exception e)
                {
                    Log.Exception(_tag, $"加载 {a.name} 异常", e);
                }
            }

            OnPostLoad(_cache);
        }

        public T Get(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) { Log.Error(_tag, "Get(null)"); return null; }
            if (_cache.TryGetValue(id, out var v)) return v;
            Log.Warn(_tag, $"未找到定义：{id}");
            return null;
        }

        public IEnumerable<T> All { get { EnsureLoaded(); return _cache.Values; } }

        public void Reload() => _cache = null;

        public void Seed(T item)
        {
            EnsureLoaded();
            var id = GetId(item);
            if (!string.IsNullOrEmpty(id)) _cache[id] = item;
        }
    }
}