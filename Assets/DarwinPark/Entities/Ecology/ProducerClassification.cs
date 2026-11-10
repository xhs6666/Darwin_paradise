using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：生产者的遗传资格仅随表达版本更新；孵化和动态移动限制仍实时检查。
    public static class ProducerClassification
    {
        private sealed class Entry {public int revision=-1;public DarwinPark.Genetics.GenomeEvaluation evaluation;public EcologyBalanceSettings settings;public DarwinPark.Genetics.NutritionEcologySettings nutrition;public bool candidate;}
        private static readonly Dictionary<EntityGeneController,Entry> Entries=new();
        public static bool IsProducer(EntityGeneController genes)
        {
            if(genes==null)return false;if(!Entries.TryGetValue(genes,out var entry)){entry=new();Entries.Add(genes,entry);}
            var settings=EcologyBalanceSettings.Current;var nutrition=DarwinPark.Genetics.NutritionEcologySettings.Current;
            // ★ 修复（防Bug回归）：表达事件先于逻辑版本递增，新评估引用同样撤销旧分类。
            if(!ReferenceEquals(entry.evaluation,genes.Evaluation)||entry.revision!=genes.LogicRevision||!ReferenceEquals(entry.settings,settings)||!ReferenceEquals(entry.nutrition,nutrition))
            {
                entry.evaluation=genes.Evaluation;entry.revision=genes.LogicRevision;entry.settings=settings;entry.nutrition=nutrition;entry.candidate=false;DarwinPark.Genetics.GeneEvaluation primary=null;bool heterotroph=false;
                if(genes.Evaluation!=null)foreach(var gene in genes.Evaluation.Genes)if(gene.IsActive){if(gene.Definition.Id==nutrition.heterotrophyGene||gene.Definition.Id==nutrition.scavengerGene)heterotroph=true;if(gene.Definition.Id==settings.nutritionGene&&(primary==null||gene.EffectiveStrength>primary.EffectiveStrength))primary=gene;}
                entry.candidate=!heterotroph&&primary?.State!=null&&System.Array.IndexOf(settings.producerStates,primary.State.StateId)>=0;
            }
            if(!entry.candidate||genes.GetComponent<EggIncubation>()?.Incubating==true)return false;
            // 基础速度本来为0时，角色/光环的乘法不可能产生行动，无需再次查询整条种群链。
            return !genes.Evaluation.Attributes.TryGetValue(settings.movementAttribute,out var move)||move<=0||genes.GetAttribute(settings.movementAttribute)<=0;
        }
        public static void Remove(EntityGeneController genes){if(!ReferenceEquals(genes,null))Entries.Remove(genes);}
        public static void Clear()=>Entries.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
