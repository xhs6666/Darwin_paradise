using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：只迁移已知旧默认抑制矩阵；用户自定义矩阵保持原值，版本字段可显式拒绝自动迁移。
    public static class EcologyGeneDefaults
    {
        [Serializable] private sealed class LegacyEntry {public string geneId="";public List<GeneSuppressionJson> suppressionRules=new List<GeneSuppressionJson>();}
        [Serializable] private sealed class LegacyDocument {public List<LegacyEntry> genes=new List<LegacyEntry>();}
        private static LegacyDocument _legacy;
        public static string Upgrade(string raw)
        {
            var gene=JsonUtility.FromJson<GeneDefinitionJson>(raw);if(gene==null||gene.ecologyBalanceVersion>=1)return raw;
            if(_legacy==null)
            {
                var asset=Resources.Load<TextAsset>("Genetics/ecology_legacy_defaults");
                if(asset==null)throw new System.IO.InvalidDataException("缺少 Genetics/ecology_legacy_defaults");
                _legacy=JsonUtility.FromJson<LegacyDocument>(asset.text);
            }
            var prior=_legacy.genes.Find(e=>e.geneId==gene.id);if(prior==null)return raw;
            var builtin=Resources.Load<TextAsset>("Genes/"+gene.id);if(builtin==null)throw new System.IO.InvalidDataException("缺少生态基因默认配置："+gene.id);
            var current=JsonUtility.FromJson<GeneDefinitionJson>(builtin.text);
            string Key(GeneSuppressionJson rule)=>rule.targetGeneId+":"+rule.coefficient.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            if(gene.suppressionRules!=null&&gene.suppressionRules.Select(Key).OrderBy(x=>x).SequenceEqual(prior.suppressionRules.Select(Key).OrderBy(x=>x)))gene.suppressionRules=current.suppressionRules;
            gene.ecologyBalanceVersion=1;return JsonUtility.ToJson(gene);
        }
        public static void UpgradeCatalog(Dictionary<string,string> catalog)
        {foreach(var id in catalog.Keys.ToArray())catalog[id]=Upgrade(catalog[id]);}
    }
}
