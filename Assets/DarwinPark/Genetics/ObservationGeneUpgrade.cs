using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace DarwinPark.Genetics
{
    // ★ 新增：旧世界增量补观察定义与独立根节点；只迁移一次，保留已有自定义数值与后续主动删除的节点。
    public static class ObservationGeneUpgrade
    {
        public static void Apply(GeneTreeDocument tree,Dictionary<string,string> catalog)
        {
            if(tree?.nodes==null||catalog==null||tree.observationVersion>=1||!catalog.ContainsKey(ObservationSettings.Current.migrationSource))return;string id=ObservationSettings.Current.geneId;
            var definition=Resources.Load<TextAsset>("Genes/"+id);var defaults=Resources.Load<TextAsset>("Genetics/default_tree");if(definition==null||defaults==null)throw new InvalidDataException("缺少观察基因默认资源");
            if(!catalog.ContainsKey(id))catalog[id]=definition.text;
            if(tree.nodes.All(n=>n.id!=id)){var node=JsonUtility.FromJson<GeneTreeDocument>(defaults.text).nodes.First(n=>n.id==id);tree.nodes.Add(node);}
            tree.observationVersion=1;
        }
    }
}
