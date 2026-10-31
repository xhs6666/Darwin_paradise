using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：独立的旧配置适配，只补新标签字段，不覆盖旧世界已有基因值、曲线或食性分支。
    public static class GeneEntityStateTags
    {
        public static string Upgrade(string raw)
        {
            var gene=JsonUtility.FromJson<GeneDefinitionJson>(raw);
            if(gene==null)throw new InvalidDataException("基因配置为空");
            if(gene.entityStateTagVersion>=1)return raw;
            var asset=Resources.Load<TextAsset>("Genes/"+gene.id);
            var builtin=asset!=null?JsonUtility.FromJson<GeneDefinitionJson>(asset.text):null;
            gene.entityStateTags=builtin?.entityStateTags!=null?new List<string>(builtin.entityStateTags):new List<string>();
            gene.entityStateTagVersion=1;
            return JsonUtility.ToJson(gene);
        }
        public static void UpgradeCatalog(Dictionary<string,string> catalog)
        {foreach(var id in catalog.Keys.ToArray())catalog[id]=Upgrade(catalog[id]);}
        public static void Validate(GeneDefinitionJson gene,EntityStateDocument registry)
        {
            if(gene.entityStateTagVersion<0||gene.entityStateTagVersion>1||gene.entityStateTags==null||gene.entityStateTags.Count!=gene.entityStateTags.Distinct().Count())throw new InvalidDataException("实体状态标签格式或版本无效："+gene.id);
            EntityStateRegistry.ExpandTags(gene.entityStateTags,registry);
        }
        public static List<string> Read(string raw)
        {
            var gene=JsonUtility.FromJson<GeneDefinitionJson>(Upgrade(raw));Validate(gene,EntityStateRegistry.Current);return new List<string>(gene.entityStateTags);
        }
    }
}
