using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 修复（防Bug回归）：行动谱系的共有能力只表达一次；进化基因保留专属属性，单独存在时仍继承基础行动能力。
    public static class GeneAttributeInheritance
    {
        private static readonly Dictionary<string,List<GeneDefinition>> Chains=new Dictionary<string,List<GeneDefinition>>();
        private static readonly Dictionary<string,List<AttributeCurve>> Curves=new Dictionary<string,List<AttributeCurve>>();
        public static bool Applies(GeneDefinition definition)=>definition.Category==GeneCategory.Locomotion;
        public static IEnumerable<GeneDefinition> Lineage(GeneDefinition definition)=>Chains.TryGetValue(definition.Id,out var chain)?chain:new[]{definition}.AsEnumerable();
        public static void Prepare(IReadOnlyDictionary<string,GeneDefinition> definitions)
        {
            Chains.Clear();Curves.Clear();
            foreach(var definition in definitions.Values.Where(Applies))
            {
                var chain=new List<GeneDefinition>();var seen=new HashSet<string>();var current=definition;
                while(current!=null&&seen.Add(current.Id))
                {
                    chain.Add(current);string parent=!string.IsNullOrEmpty(current.ParentGeneId)?current.ParentGeneId:current.BaseGeneId;
                    current=!string.IsNullOrEmpty(parent)&&definitions.TryGetValue(parent,out var ancestor)&&Applies(ancestor)?ancestor:null;
                }
                Chains[definition.Id]=chain;
            }
            // 兼容旧世界的重复曲线，仅规范运行定义，不覆盖存档和用户的原始配置文件。
            foreach(var pair in Chains.OrderBy(p=>p.Value.Count))
            {
                var definition=pair.Value[0];var inherited=new Dictionary<string,AttributeCurve>();
                foreach(var ancestor in pair.Value.Skip(1).Reverse())foreach(var curve in ancestor.AttributeCurves)inherited[curve.attributeId]=curve;
                definition.AttributeCurves.RemoveAll(c=>inherited.ContainsKey(c.attributeId));
                foreach(var curve in definition.AttributeCurves)inherited[curve.attributeId]=curve;
                Curves[definition.Id]=inherited.Values.ToList();
            }
        }
        private static IEnumerable<AttributeCurve> Effective(GeneDefinition definition)=>Curves.TryGetValue(definition.Id,out var curves)?curves:definition.AttributeCurves;
        public static Dictionary<string,float> Accumulate(List<GeneEvaluation> genes)
        {
            var result=new Dictionary<string,float>();
            void Add(GeneEvaluation gene,AttributeCurve curve)
            {
                float value=Mathf.Lerp(curve.minValue,curve.maxValue,Mathf.Clamp01(gene.EffectiveStrength/100f))*gene.Gene.CopyStrength;
                gene.AttributeContributions[curve.attributeId]=value;
                result.TryGetValue(curve.attributeId,out float prior);result[curve.attributeId]=prior+value;
            }
            foreach(var gene in genes.Where(g=>g.IsActive&&!Applies(g.Definition)))foreach(var curve in gene.Definition.AttributeCurves)Add(gene,curve);
            foreach(var family in genes.Where(g=>g.IsActive&&Applies(g.Definition)).GroupBy(g=>Lineage(g.Definition).Last().Id))
            {
                foreach(string attribute in family.SelectMany(g=>Effective(g.Definition)).Select(c=>c.attributeId).Distinct())
                {
                    var owner=family.Where(g=>g.Definition.AttributeCurves.Any(c=>c.attributeId==attribute)).OrderBy(g=>g.Definition.Tier).ThenBy(g=>g.Definition.Id,StringComparer.Ordinal).FirstOrDefault()
                        ??family.Where(g=>Effective(g.Definition).Any(c=>c.attributeId==attribute)).OrderByDescending(g=>g.Definition.Tier).ThenBy(g=>g.Definition.Id,StringComparer.Ordinal).First();
                    // 同一种基因的复制仍保留 CopyStrength；不同进化类型不重复叠加共有属性。
                    foreach(var gene in family.Where(g=>g.Definition.Id==owner.Definition.Id))Add(gene,Effective(gene.Definition).First(c=>c.attributeId==attribute));
                }
            }
            return result;
        }
    }
}
