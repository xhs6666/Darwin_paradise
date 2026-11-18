using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DarwinPark.Genetics;
namespace DarwinPark.Entities.Logic
{
    // ★ 新增：主表达按版本缓存，探测、中枢、行动和营养不再相互冒充。
    public static class GeneCapabilities
    {
        private sealed class Entry {public int revision=-1;public GenomeEvaluation evaluation;public readonly Dictionary<string,GeneEvaluation> primary=new();}
        private static readonly ConditionalWeakTable<EntityGeneController,Entry> Cache=new();
        public static GeneEvaluation Active(EntityGeneController genes,string id)
        {
            var value=Cache.GetValue(genes,_=>new Entry());if(value.revision!=genes.LogicRevision||!ReferenceEquals(value.evaluation,genes.Evaluation))
            {value.revision=genes.LogicRevision;value.evaluation=genes.Evaluation;value.primary.Clear();if(genes.Evaluation!=null)foreach(var gene in genes.Evaluation.Genes)if(gene.IsActive&&(!value.primary.TryGetValue(gene.Definition.Id,out var old)||gene.EffectiveStrength>old.EffectiveStrength||gene.EffectiveStrength==old.EffectiveStrength&&string.CompareOrdinal(gene.Gene.InstanceId,old.Gene.InstanceId)<0))value.primary[gene.Definition.Id]=gene;}
            return value.primary.TryGetValue(id,out var result)?result:null;
        }
        public static string Diet(EntityGeneController genes)=>Active(genes,BiologySettings.Current.feedingGene)?.State?.StateId;
    }
}
