using System;
using System.Linq;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Logic
{
    // ★ 新增：复制基因保留属性叠加，但每个实体只有一个移动/繁殖行为写入者，避免重复攻击、孕育和抢方向。
    public static class GeneBehaviorAuthority
    {
        public static bool IsPrimary<T>(EntityGeneController controller,GeneInstance gene) where T:class,IGeneLogic
        {
            // ★ 修复（防Bug回归）：每个模拟步只需找最大优先级，不能为四类行为反复创建和排序相同的基因集合。
            GeneEvaluation selected=null;
            if(controller.Evaluation!=null)foreach(var candidate in controller.Evaluation.Genes)
                if(candidate.IsActive&&controller.GetLogic(candidate.Gene) is T&&(selected==null||candidate.Definition.Tier>selected.Definition.Tier||candidate.Definition.Tier==selected.Definition.Tier&&string.CompareOrdinal(candidate.Gene.InstanceId,selected.Gene.InstanceId)<0))selected=candidate;
            return selected?.Gene==gene;
        }
    }
}
