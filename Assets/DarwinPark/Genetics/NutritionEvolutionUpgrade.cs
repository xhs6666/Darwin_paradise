using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：旧存档增量补齐两条营养分支；原寄生ID保留，升级为卵寄生而非把旧实体变成新营养寄生体。
    public static class NutritionEvolutionUpgrade
    {
        public static void Apply(GeneTreeDocument tree,Dictionary<string,string> catalog)
        {
            if(tree?.nodes==null||tree.edges==null||catalog==null)return;
            var settings=NutritionEcologySettings.Current;
            var builtin=JsonUtility.FromJson<GeneTreeDocument>(Resources.Load<TextAsset>("Genetics/default_tree").text);
            if(catalog.ContainsKey(settings.nutritionGene)&&tree.nutritionVersion<1)
            {
                // ★ 修复（防Bug回归）：默认资源库可能已含新定义，不能据此跳过旧自定义树的节点和连线迁移。
                foreach(string id in new[]{settings.heterotrophyGene,settings.scavengerGene})
                {
                    if(!catalog.ContainsKey(id))catalog[id]=Resources.Load<TextAsset>("Genes/"+id)?.text??throw new System.IO.InvalidDataException("缺少营养分支定义："+id);
                    foreach(var edge in builtin.edges.Where(e=>e.to==id||e.from==id))
                    {
                        foreach(string endpoint in new[]{edge.from,edge.to})if(tree.nodes.All(n=>n.id!=endpoint))tree.nodes.Add(builtin.nodes.First(n=>n.id==endpoint));
                        if(!tree.edges.Any(e=>e.from==edge.from&&e.to==edge.to&&e.kind==edge.kind))tree.edges.Add(edge);
                    }
                }
                tree.nutritionVersion=1;
            }
            if(!catalog.TryGetValue(settings.eggGene,out var raw))return;
            var old=JsonUtility.FromJson<GeneDefinitionJson>(raw);if(old.logicScriptId!=settings.legacyEggLogic)return;
            var current=JsonUtility.FromJson<GeneDefinitionJson>(Resources.Load<TextAsset>("Genes/"+settings.eggGene).text);
            old.displayName=current.displayName;old.logicScriptId=current.logicScriptId;old.states=current.states;
            old.attributeCurves=old.attributeCurves.Where(c=>c.attributeId!=settings.parasiteDrainAttribute&&c.attributeId!="hide_from_host").ToList();
            foreach(var curve in current.attributeCurves)if(old.attributeCurves.All(c=>c.attributeId!=curve.attributeId))old.attributeCurves.Add(curve);
            old.attributeLinkages=old.attributeLinkages.Where(l=>l.sourceAttributeId!=settings.eggGene).ToList();catalog[settings.eggGene]=JsonUtility.ToJson(old);
        }
    }
}
