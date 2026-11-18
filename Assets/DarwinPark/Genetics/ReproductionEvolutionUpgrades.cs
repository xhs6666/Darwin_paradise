using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 适配：两条繁殖出错分支共用迁移实现，各自保留配置、版本和调用入口。
    public static class MultipleBirthEvolutionUpgrade
    {
        private static ReproductionEvolutionUpgrade _upgrade;
        private static ReproductionEvolutionUpgrade Upgrade => _upgrade ??= new ReproductionEvolutionUpgrade("Genetics/multiple_birth");
        public static string GeneId => Upgrade.GeneId;
        public static string SourceGeneId => Upgrade.SourceGeneId;

        public static void Apply(GeneTreeDocument tree, Dictionary<string, string> catalog)
        {
            if (tree?.nodes == null || tree.edges == null || catalog == null || tree.multipleBirthVersion >= 1) return;
            if (Upgrade.Apply(tree, catalog)) tree.multipleBirthVersion = 1;
        }
    }

    public static class ParthenogenesisEvolutionUpgrade
    {
        private static ReproductionEvolutionUpgrade _upgrade;
        private static ReproductionEvolutionUpgrade Upgrade => _upgrade ??= new ReproductionEvolutionUpgrade("Genetics/parthenogenesis");
        public static string GeneId => Upgrade.GeneId;
        public static string SourceGeneId => Upgrade.SourceGeneId;

        public static void Apply(GeneTreeDocument tree, Dictionary<string, string> catalog)
        {
            if (tree?.nodes == null || tree.edges == null || catalog == null || tree.parthenogenesisVersion >= 1) return;
            if (Upgrade.Apply(tree, catalog)) tree.parthenogenesisVersion = 1;
        }
    }

    internal sealed class ReproductionEvolutionUpgrade
    {
        [Serializable] private sealed class Settings { public string sourceGene = "", gene = ""; }
        private readonly Settings _settings;

        public ReproductionEvolutionUpgrade(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null) throw new InvalidDataException("缺少 " + resource);
            _settings = JsonUtility.FromJson<Settings>(asset.text);
            if (_settings == null || string.IsNullOrWhiteSpace(SourceGeneId) ||
                string.IsNullOrWhiteSpace(GeneId) || SourceGeneId == GeneId)
                throw new InvalidDataException("繁殖演化配置无效：" + resource);
        }

        public string GeneId => _settings.gene;
        public string SourceGeneId => _settings.sourceGene;

        public bool Apply(GeneTreeDocument tree, Dictionary<string, string> catalog)
        {
            if (!catalog.ContainsKey(SourceGeneId)) return false;
            var builtin = Resources.Load<TextAsset>("Genetics/default_tree");
            var definition = Resources.Load<TextAsset>("Genes/" + GeneId);
            if (builtin == null || definition == null) throw new InvalidDataException("缺少基因或默认树资源：" + GeneId);
            if (!catalog.ContainsKey(GeneId)) catalog.Add(GeneId, definition.text);

            var defaults = JsonUtility.FromJson<GeneTreeDocument>(builtin.text);
            foreach (var edge in defaults.edges)
            {
                if (edge.from != SourceGeneId || edge.to != GeneId || edge.kind != "error") continue;
                AddMissingNode(tree, defaults, edge.from);
                AddMissingNode(tree, defaults, edge.to);
                if (!tree.edges.Exists(item => item.from == edge.from && item.to == edge.to && item.kind == edge.kind))
                    tree.edges.Add(edge);
            }
            return true;
        }

        private static void AddMissingNode(GeneTreeDocument tree, GeneTreeDocument defaults, string id)
        {
            if (tree.nodes.Exists(item => item.id == id)) return;
            var node = defaults.nodes.Find(item => item.id == id);
            if (node == null) throw new InvalidDataException("默认基因树缺少节点：" + id);
            tree.nodes.Add(node);
        }
    }
}
