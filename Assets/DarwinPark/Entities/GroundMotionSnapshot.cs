using System.Runtime.CompilerServices;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 修复（防Bug回归）：一次画面内补跑多个物理步不需要反复解析相同移动表达；表达变更立即重读。
    public static class GroundMotionSnapshot
    {
        private sealed class Entry { public int frame = -1, revision = -1; public float speed; }
        private static ConditionalWeakTable<EntityGeneController, Entry> _entries = new();
        public static long Resolved { get; private set; }
        public static float Speed(EntityGeneController genes)
        {
            if (genes == null || genes.Evaluation == null || !genes.Evaluation.Attributes.TryGetValue("moveSpeed", out float basis) || basis <= 0) return 0;
            var entry = _entries.GetOrCreateValue(genes);
            if (entry.frame != Time.frameCount || entry.revision != genes.LogicRevision)
            {
                entry.frame = Time.frameCount; entry.revision = genes.LogicRevision;
                entry.speed = genes.GetAttribute("moveSpeed"); Resolved++;
            }
            return entry.speed;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _entries = new(); Resolved = 0; }
    }
}
