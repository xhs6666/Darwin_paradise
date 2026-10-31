using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 修复（防Bug回归）：旧世界快照只有旧基因，增量补库使新增基因可用，同时保留已有基因数值与自定义关系。
    public static class GeneCatalogExpansion
    {
        public static int Expand(Dictionary<string,string> catalog,GeneTreeDocument tree)
        {
            var available=GeneTreeStorage.ReadCatalog();
            var missing=new HashSet<string>(available.Keys.Where(id=>!catalog.ContainsKey(id)));
            if(missing.Count==0)return 0;
            var merged=new Dictionary<string,string>(catalog);
            foreach(var id in missing)merged.Add(id,available[id]);
            var result=JsonUtility.FromJson<GeneTreeDocument>(JsonUtility.ToJson(tree));
            var builtin=Resources.Load<TextAsset>("Genetics/default_tree");
            if(builtin==null)throw new System.IO.InvalidDataException("缺少 Genetics/default_tree，无法补齐基因关系");
            var defaults=JsonUtility.FromJson<GeneTreeDocument>(builtin.text);
            var nodes=new HashSet<string>(result.nodes.Select(n=>n.id));
            var newEdges=defaults.edges.Where(e=>(missing.Contains(e.from)||missing.Contains(e.to))&&merged.ContainsKey(e.from)&&merged.ContainsKey(e.to)).ToList();
            var required=new HashSet<string>(missing.Concat(newEdges.SelectMany(e=>new[]{e.from,e.to})));
            foreach(var node in defaults.nodes)if(required.Contains(node.id)&&nodes.Add(node.id))result.nodes.Add(node);
            foreach(var edge in newEdges)if(!result.edges.Any(e=>e.from==edge.from&&e.to==edge.to&&e.kind==edge.kind))result.edges.Add(edge);
            GeneTreeStorage.Validate(result,merged);
            foreach(var id in missing)catalog.Add(id,merged[id]);
            tree.nodes=result.nodes;tree.edges=result.edges;
            return missing.Count;
        }
    }
}
