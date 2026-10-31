using System;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    [Serializable] public sealed class EvolutionRequirement
    {
        public string source,target,kind; public float minValue,maxValue=100;
        public bool minExclusive;
        public string[] requiredGenes=Array.Empty<string>();
    }
    [Serializable] public sealed class GeneEvolutionRules
    {
        public EvolutionRequirement[] requirements=Array.Empty<EvolutionRequirement>();
        private static GeneEvolutionRules _cached;
        // ★ 新增：特殊演化条件独立配置；工具连线与自然变异使用相同约束，不能把寄生连到任意基因。
        public static GeneEvolutionRules Load()
        {
            if(_cached!=null)return _cached;
            var asset=Resources.Load<TextAsset>("Genetics/expansion_rules");
            if(asset==null)throw new InvalidOperationException("缺少 Genetics/expansion_rules");
            return _cached=JsonUtility.FromJson<GeneEvolutionRules>(asset.text);
        }
        public static bool Allows(string source,string target,string kind,float value,Genome genome=null)
        {
            var rules=Load().requirements.Where(r=>r.target==target).ToArray();
            if(rules.Length==0)return true;
            return rules.Any(r=>r.source==source&&r.kind==kind&&(float.IsNaN(value)||((r.minExclusive?value>r.minValue:value>=r.minValue)&&value<=r.maxValue))&&
                (genome==null||r.requiredGenes.All(id=>genome.Genes.Any(g=>g.Enabled&&g.DefinitionId==id))));
        }
    }
}
