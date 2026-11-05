using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：新出错分支只迁移一次；不覆盖旧世界的自定义定义、坐标和后续主动编辑。
    public static class ParthenogenesisEvolutionUpgrade
    {
        [Serializable] private sealed class Settings {public string sourceGene="",gene="";}
        private static Settings _settings;
        private static Settings Config
        {
            get
            {
                if(_settings!=null)return _settings;
                var asset=Resources.Load<TextAsset>("Genetics/parthenogenesis");
                if(asset==null)throw new InvalidDataException("缺少 Genetics/parthenogenesis");
                var settings=JsonUtility.FromJson<Settings>(asset.text);
                if(settings==null||string.IsNullOrWhiteSpace(settings.sourceGene)||string.IsNullOrWhiteSpace(settings.gene)||settings.sourceGene==settings.gene)throw new InvalidDataException("孤雌生殖演化配置无效");
                return _settings=settings;
            }
        }
        public static string GeneId=>Config.gene;
        public static string SourceGeneId=>Config.sourceGene;
        public static void Apply(GeneTreeDocument tree,Dictionary<string,string> catalog)
        {
            if(tree?.nodes==null||tree.edges==null||catalog==null||tree.parthenogenesisVersion>=1||!catalog.ContainsKey(Config.sourceGene))return;
            var builtin=Resources.Load<TextAsset>("Genetics/default_tree");var definition=Resources.Load<TextAsset>("Genes/"+GeneId);
            if(builtin==null||definition==null)throw new InvalidDataException("缺少孤雌生殖基因或默认树资源");
            if(!catalog.ContainsKey(GeneId))catalog[GeneId]=definition.text;
            var defaults=JsonUtility.FromJson<GeneTreeDocument>(builtin.text);
            foreach(var edge in defaults.edges.Where(e=>e.from==Config.sourceGene&&e.to==GeneId&&e.kind=="error"))
            {
                foreach(string id in new[]{edge.from,edge.to})if(tree.nodes.All(n=>n.id!=id))tree.nodes.Add(defaults.nodes.First(n=>n.id==id));
                if(!tree.edges.Any(e=>e.from==edge.from&&e.to==edge.to&&e.kind==edge.kind))tree.edges.Add(edge);
            }
            tree.parthenogenesisVersion=1;
        }
    }
}
