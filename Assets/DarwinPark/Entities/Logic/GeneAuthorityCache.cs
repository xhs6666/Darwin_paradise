using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Logic
{
    // ★ 新增：主行为只随表达或逻辑挂载变化；子步重复查询不再遍历整个基因集合，弱引用避免持有已销毁实体。
    public static class GeneAuthorityCache
    {
        private sealed class Selection {public GenomeEvaluation evaluation;public int revision;public GeneInstance gene;}
        private static readonly ConditionalWeakTable<EntityGeneController,Dictionary<Type,Selection>> Cache=new ConditionalWeakTable<EntityGeneController,Dictionary<Type,Selection>>();
        public static bool IsPrimary<T>(EntityGeneController controller,GeneInstance gene) where T:class,IGeneLogic
        {
            var values=Cache.GetValue(controller,_=>new Dictionary<Type,Selection>());var key=typeof(T);
            if(!values.TryGetValue(key,out var value)){value=new Selection{revision=-1};values.Add(key,value);}
            if(!ReferenceEquals(value.evaluation,controller.Evaluation)||value.revision!=controller.LogicRevision)
            {
                GeneEvaluation selected=null;
                if(controller.Evaluation!=null)foreach(var candidate in controller.Evaluation.Genes)
                    if(candidate.IsActive&&controller.GetLogic(candidate.Gene) is T&&(selected==null||candidate.Definition.Tier>selected.Definition.Tier||candidate.Definition.Tier==selected.Definition.Tier&&string.CompareOrdinal(candidate.Gene.InstanceId,selected.Gene.InstanceId)<0))selected=candidate;
                value.evaluation=controller.Evaluation;value.revision=controller.LogicRevision;value.gene=selected?.Gene;
            }
            return value.gene==gene;
        }
    }
}
