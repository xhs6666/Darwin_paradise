using DarwinPark.Entities;
using DarwinPark.Genetics;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.UI
{
    [System.Serializable]
    public class EntityTemplateData
    {
        public string templateName;
        public string iconId;
        public string baseEntityJsonId; // 保留字段，如果以后有更高级的json生成
        public List<GeneInstanceData> genes = new List<GeneInstanceData>();
    }

    public static class EntityTemplateManager
    {
        // 内存中保存模板
        private static List<EntityTemplateData> _templates = new List<EntityTemplateData>();
        public static IReadOnlyList<EntityTemplateData> Templates => _templates;

        // 保存事件，通知 UI 刷新
        public static event System.Action OnTemplatesChanged;

        public static void SaveTemplate(DarwinEntity entity)
        {
            var genes = entity.GetComponent<EntityGeneController>();
            if (genes == null || genes.Genome == null) return;

            var data = new EntityTemplateData
            {
                templateName = entity.DisplayName,
                iconId = "entity_template_default", // 默认图标ID，你可以根据基因判断赋予不同ID
                genes = new List<GeneInstanceData>()
            };

            foreach (var g in genes.Genome.Genes)
            {
                data.genes.Add(new GeneInstanceData
                {
                    definitionId = g.DefinitionId,
                    value = g.Value,
                    tier = g.Tier
                });
            }

            // 简单去重，同名覆盖
            var existing = _templates.Find(t => t.templateName == data.templateName);
            if (existing != null) _templates.Remove(existing);

            _templates.Add(data);
            Debug.Log($"[Template] 已保存模板：{data.templateName}");

            OnTemplatesChanged?.Invoke();
        }
    }
}