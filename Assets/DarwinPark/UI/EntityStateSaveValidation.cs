using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：清场前独立校验状态注册表和基因标签，坏配置不能先破坏玩家现场。
    public static class EntityStateSaveValidation
    {
        public static void Validate(WorldSaveData data)
        {
            var registry=EntityStateRegistry.Parse(string.IsNullOrWhiteSpace(data.entityStateRegistry)?EntityStateRegistry.ReadConfiguredJson():data.entityStateRegistry);
            var catalog=new Dictionary<string,GeneDefinitionJson>();
            foreach(var entry in data.definitions)
            {
                var gene=JsonUtility.FromJson<GeneDefinitionJson>(GeneEntityStateTags.Upgrade(entry.json));
                GeneEntityStateTags.Validate(gene,registry);catalog.Add(entry.id,gene);
            }
            foreach(var condition in registry.states.SelectMany(s=>s.entryAny.Concat(s.transitions.SelectMany(t=>t.any))).SelectMany(r=>r.all))
                if(condition.kind=="nearby_gene_state"&&(!catalog.TryGetValue(condition.geneId,out var gene)||!gene.states.Any(s=>s.stateId==condition.geneStateId)))
                    throw new InvalidDataException("实体状态条件引用缺失基因或基因状态："+condition.geneId+" / "+condition.geneStateId);
            foreach(var entity in data.entities)
                if(!string.IsNullOrEmpty(entity?.ecology?.entityStateId)&&registry.Get(entity.ecology.entityStateId)==null)throw new InvalidDataException("存档实体状态未注册："+entity.ecology.entityStateId);
        }
    }
}
