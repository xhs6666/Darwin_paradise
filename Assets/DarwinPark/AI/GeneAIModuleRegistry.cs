using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.AI
{
    public static class GeneAIModuleRegistry
    {
        private static readonly Dictionary<string, Func<IGeneAIModule>> _factories = new();

        public static void Register(string moduleId, Func<IGeneAIModule> factory)
        {
            if (!string.IsNullOrEmpty(moduleId) && factory != null)
                _factories[moduleId] = factory;
        }

        public static IGeneAIModule Create(string moduleId)
        {
            if (_factories.TryGetValue(moduleId, out var f)) return f.Invoke();
            return null;
        }
    }

    // 自动注册（占位，稍后按需添加具体模块）
    internal static class BuiltinAIModuleBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // GeneAIModuleRegistry.Register("ai_sexual", () => new Modules.SexualAIModule());
        }
    }
}