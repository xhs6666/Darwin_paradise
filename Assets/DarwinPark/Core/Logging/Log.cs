using UnityEngine;

namespace DarwinPark.Core
{
    /// <summary>
    /// 统一日志出口。
    /// ★ 新增：给所有模块打标签前缀，方便 Console 过滤；禁用时可按 tag 关闭。
    /// 规则1：加载失败/引用缺失必须走这里，不允许空 catch 吞错误。
    /// </summary>
    public static class Log
    {
        [System.Flags]
        public enum Tag
        {
            None = 0,
            Data = 1 << 0,
            Genetics = 1 << 1,
            Mutation = 1 << 2,
            Reproduction = 1 << 3,
            Entity = 1 << 4,
            AI = 1 << 5,
            Planet = 1 << 6,
            Environment = 1 << 7,
            Stats = 1 << 8,
            UI = 1 << 9,
            Bootstrap = 1 << 10
        }

        public static Tag Mask = Tag.None; // 设为 None 表示全部输出

        public static void Info(Tag tag, string msg)
        {
            if (Mask != Tag.None && (Mask & tag) == 0) return;
            Debug.Log($"[{tag}] {msg}");
        }

        public static void Warn(Tag tag, string msg)
        {
            if (Mask != Tag.None && (Mask & tag) == 0) return;
            Debug.LogWarning($"[{tag}] {msg}");
        }

        public static void Error(Tag tag, string msg)
        {
            Debug.LogError($"[{tag}] {msg}"); // 错误永远输出
        }

        public static void Exception(Tag tag, string msg, System.Exception e)
        {
            Debug.LogError($"[{tag}] {msg}：{e}");
        }
    }
}