using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Core
{
    /// <summary>
    /// 轻量级服务定位器。
    /// ★ 新增：统一管理 EnvironmentSystem / SimulationClock / UIRoot 等单例，
    /// 场景缺组件时一次报错而不是各处 NRE（规则3.6）。
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) { Log.Error(Log.Tag.Bootstrap, $"注册 null 服务：{typeof(T).Name}"); return; }
            if (_services.ContainsKey(typeof(T)))
                Log.Warn(Log.Tag.Bootstrap, $"重复注册服务：{typeof(T).Name}（覆盖）");
            _services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class => _services.Remove(typeof(T));

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var o)) { service = o as T; return service != null; }
            service = null;
            return false;
        }

        public static T Get<T>(string reason = null) where T : class
        {
            if (TryGet<T>(out var s)) return s;
            Log.Error(Log.Tag.Bootstrap,
                $"服务缺失：{typeof(T).Name}" + (string.IsNullOrEmpty(reason) ? "" : $"（{reason}）"));
            return null;
        }

        public static void Clear() => _services.Clear();
    }
}