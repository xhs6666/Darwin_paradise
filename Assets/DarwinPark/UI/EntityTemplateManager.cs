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
        // ★ 修复（防Bug回归）：进程/播放会话重启后首次访问从磁盘读取，不能只保留静态内存列表。
        private static List<EntityTemplateData> _templates = new List<EntityTemplateData>();
        private static bool _loaded;
        private static void EnsureLoaded(){if(_loaded)return;_templates=EntityTemplateStore.Load();_loaded=true;}
        public static IReadOnlyList<EntityTemplateData> Templates {get{EnsureLoaded();return _templates;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){_templates=new List<EntityTemplateData>();_loaded=false;OnTemplatesChanged=null;}

        // 保存事件，通知 UI 刷新
        public static event System.Action OnTemplatesChanged;

        public static void SaveTemplate(DarwinEntity entity)
        {
            if(!DarwinPark.Core.WorldAuthority.CanEdit)return;
            EnsureLoaded();
            if(entity==null)return;
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

            // ★ 修复（防Bug回归）：实际写盘成功后才更新列表/通知界面，失败不误报“保存成功”或丢掉原模板。
            var next=new List<EntityTemplateData>(_templates);
            int index=next.FindIndex(t=>t.templateName==data.templateName);
            if(index>=0)next[index]=data;else next.Add(data);
            try{EntityTemplateStore.Save(next);}
            catch(System.Exception ex) when(ex is System.IO.IOException||ex is System.UnauthorizedAccessException||ex is System.ArgumentException)
            {Debug.LogError("[Template] 模板保存失败，原模板保留："+ex.Message);return;}
            _templates=next;
            Debug.Log($"[Template] 已保存模板：{data.templateName}");

            OnTemplatesChanged?.Invoke();
        }
    }
}
