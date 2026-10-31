using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 修复（防Bug回归）：规则改成储存出错后，旧世界含上一版演化边会在加载前校验失败；只迁移已知旧入口并保留自定义数值。
    public static class NutrientUtilizationEvolutionUpgrade
    {
        [Serializable] private sealed class Rule
        {public string targetGeneId="",previousParentGeneId="",previousKind="",parentGeneId="",kind="";}
        private static Rule _rule;
        public static void Apply(GeneTreeDocument tree,Dictionary<string,string> catalog)
        {
            if(tree?.nodes==null||tree.edges==null||catalog==null)return;
            if(_rule==null)
            {
                var asset=Resources.Load<TextAsset>("Genetics/nutrient_utilization_upgrade");if(asset==null)throw new InvalidDataException("缺少营养利用演化迁移配置");
                _rule=JsonUtility.FromJson<Rule>(asset.text);
                if(_rule==null||new[]{_rule.targetGeneId,_rule.previousParentGeneId,_rule.previousKind,_rule.parentGeneId,_rule.kind}.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("营养利用演化迁移配置无效");
            }
            if(!catalog.TryGetValue(_rule.targetGeneId,out var raw))return;
            var definition=JsonUtility.FromJson<GeneDefinitionJson>(raw);
            if(definition?.parentGeneId==_rule.previousParentGeneId){definition.parentGeneId=_rule.parentGeneId;catalog[_rule.targetGeneId]=JsonUtility.ToJson(definition);}
            var prior=tree.edges.Where(e=>e.from==_rule.previousParentGeneId&&e.to==_rule.targetGeneId&&e.kind==_rule.previousKind).ToArray();
            foreach(var edge in prior)
            {
                tree.edges.Remove(edge);
                // 旧树未必包含储存节点；补齐注册节点后再连接，不删除世界中的原营养节点或已有基因。
                if(!catalog.ContainsKey(_rule.parentGeneId))throw new InvalidDataException("迁移营养利用关系缺少储存基因定义");
                if(tree.nodes.All(n=>n.id!=_rule.parentGeneId))
                {
                    var defaults=Resources.Load<TextAsset>("Genetics/default_tree");
                    var node=defaults!=null?JsonUtility.FromJson<GeneTreeDocument>(defaults.text).nodes.Find(n=>n.id==_rule.parentGeneId):null;
                    if(node==null)throw new InvalidDataException("缺少储存基因默认节点");tree.nodes.Add(node);
                }
                if(!tree.edges.Any(e=>e.from==_rule.parentGeneId&&e.to==_rule.targetGeneId&&e.kind==_rule.kind))tree.edges.Add(new GeneTreeEdge{from=_rule.parentGeneId,to=_rule.targetGeneId,kind=_rule.kind,value=edge.value});
            }
            var target=tree.nodes.Find(n=>n.id==_rule.targetGeneId);if(target!=null)target.error=true;
        }
    }
}
