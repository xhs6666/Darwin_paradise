using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：阶段变异统一限额，同一近邻基因组只抽部分代表；变异结果不复制给其他身份。
    public static class GlobalMutationManager
    {
        private static int _frame=-1,_attempts;
        public static long Allowed {get;private set;}
        public static long MergedSkipped {get;private set;}
        public static long BudgetSkipped {get;private set;}
        public static bool Permit(EntityGeneController genes)
        {
            if(_frame!=Time.frameCount){_frame=Time.frameCount;_attempts=0;}
            var tuning=GameplayTuning.Current;
            if(EntityWorkScheduler.TryCohortOrdinal(genes,out int ordinal,out int count)&&count>1&&ordinal>0)
            {
                int stride=tuning.mergedMutationFraction<=0?int.MaxValue:Mathf.Max(1,Mathf.CeilToInt(1/tuning.mergedMutationFraction));
                if(ordinal%stride!=0){MergedSkipped++;return false;}
            }
            if(_attempts>=tuning.mutationAttemptsPerFrame){BudgetSkipped++;return false;}
            _attempts++;Allowed++;return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset(){_frame=-1;_attempts=0;Allowed=MergedSkipped=BudgetSkipped=0;}
    }
}
