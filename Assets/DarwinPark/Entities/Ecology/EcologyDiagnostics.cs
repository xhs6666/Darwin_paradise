using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：普通繁殖条件不足不是错误；诊断按真实时间合并，不再为每个幼体生成字符串和Console堆栈。
    public static class EcologyDiagnostics
    {
        public enum ReproductionBlock { Age, Reserve, Mate }
        private static readonly int[] Counts=new int[3];private static float _next;
        public static void Blocked(ReproductionBlock reason)
        {
            var settings=EcologyPerformanceSettings.Current;if(!settings.reproductionDiagnostics)return;
            Counts[(int)reason]++;if(Time.realtimeSinceStartup<_next)return;
            _next=Time.realtimeSinceStartup+Mathf.Max(1,settings.summaryInterval);
            Debug.Log($"[生态诊断] 繁殖条件检查：年龄未满足 {Counts[0]}，储粮不足 {Counts[1]}，无配偶 {Counts[2]}（合并计数，非个体数）");
            System.Array.Clear(Counts,0,Counts.Length);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){_next=0;System.Array.Clear(Counts,0,Counts.Length);}
    }
}
