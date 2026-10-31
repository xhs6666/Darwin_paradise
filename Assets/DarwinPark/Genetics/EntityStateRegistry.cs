using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：注册的是行为类型接口，具体状态 ID、名称、阈值和关系全部来自 JSON。
    public enum EntityStateBehavior { None,TerritoryPatrol,Hunt }
    [Serializable] public sealed class EntityStateCondition
    {
        public string kind,attributeId,comparison,geneId,geneStateId,rangeAttributeId;
        public float value;
    }
    [Serializable] public sealed class EntityStateRule {public List<EntityStateCondition> all=new List<EntityStateCondition>();}
    [Serializable] public sealed class EntityStateTransition {public string to;public List<EntityStateRule> any=new List<EntityStateRule>();}
    [Serializable] public sealed class EntityStateDefinition
    {
        public string id,displayName,behavior;
        public string targetRangeAttributeId;
        public int priority;
        public List<EntityStateRule> entryAny=new List<EntityStateRule>();
        public List<EntityStateTransition> transitions=new List<EntityStateTransition>();
        public EntityStateBehavior Behavior=> (EntityStateBehavior)Enum.Parse(typeof(EntityStateBehavior),behavior);
    }
    [Serializable] public sealed class EntityStateDocument
    {
        public int version=1;
        public string defaultStateId;
        public List<EntityStateDefinition> states=new List<EntityStateDefinition>();
        public EntityStateDefinition Get(string id)=>states.FirstOrDefault(s=>s.id==id);
    }
    public static class EntityStateRegistry
    {
        private static string _snapshot,_json;
        private static EntityStateDocument _current;
        public static string ConfiguredPath=>Path.Combine(GeneTreeStorage.Folder,"entity_states.json");
        public static EntityStateDocument Current
        {
            get {if(_current==null){_json=_snapshot??ReadConfiguredJson();_current=Parse(_json);}return _current;}
        }
        public static string CurrentJson {get {_=Current;return _json;}}
        public static string ReadConfiguredJson()
        {
            if(File.Exists(ConfiguredPath))return File.ReadAllText(ConfiguredPath);
            var asset=Resources.Load<TextAsset>("Genetics/entity_states");
            if(asset==null)throw new InvalidDataException("缺少实体状态注册表 Genetics/entity_states");
            return asset.text;
        }
        // ★ 适配：存档使用自己的注册表快照；缺少该字段的旧档使用当前配置，异常必须明确阻止加载。
        public static void UseSnapshot(string json)
        {_snapshot=string.IsNullOrWhiteSpace(json)?null:json;_current=null;_json=null;}
        public static void ReloadConfigured(){if(_snapshot==null){_current=null;_json=null;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()=>UseSnapshot(null);
        public static EntityStateDocument Parse(string json)
        {
            var doc=JsonUtility.FromJson<EntityStateDocument>(json);
            if(doc==null||doc.version!=1||doc.states==null||doc.states.Count==0)throw new InvalidDataException("实体状态注册表版本或内容无效");
            var ids=new HashSet<string>();
            foreach(var state in doc.states)
            {
                if(state==null||!System.Text.RegularExpressions.Regex.IsMatch(state.id??"","^[A-Za-z0-9_-]+$")||!ids.Add(state.id)||string.IsNullOrWhiteSpace(state.displayName)||!Enum.TryParse<EntityStateBehavior>(state.behavior,out var behavior)||!Enum.IsDefined(typeof(EntityStateBehavior),behavior)||state.transitions==null)
                    throw new InvalidDataException("实体状态缺失、重复或行为未注册："+state?.id);
                ValidateRules(state.entryAny);
                if(behavior==EntityStateBehavior.Hunt&&string.IsNullOrEmpty(state.targetRangeAttributeId))throw new InvalidDataException("捕猎状态缺少目标感知属性："+state.id);
                foreach(var transition in state.transitions){if(transition==null)throw new InvalidDataException("实体状态迁移为空");ValidateRules(transition.any);}
            }
            if(!ids.Contains(doc.defaultStateId))throw new InvalidDataException("实体默认状态未注册");
            foreach(var state in doc.states)foreach(var transition in state.transitions)if(!ids.Contains(transition.to))throw new InvalidDataException("实体迁移目标未注册："+transition.to);
            return doc;
        }
        private static void ValidateRules(List<EntityStateRule> rules)
        {
            if(rules==null)throw new InvalidDataException("实体状态条件列表为空");
            foreach(var rule in rules)
            {
                if(rule?.all==null||rule.all.Count==0)throw new InvalidDataException("实体状态条件组不能为空");
                foreach(var condition in rule.all)
                {
                    if(condition==null)throw new InvalidDataException("实体状态条件为空");
                    bool valid=condition.kind=="stat"&&!string.IsNullOrEmpty(condition.attributeId)&&new[]{"gt","lte"}.Contains(condition.comparison)&&!float.IsNaN(condition.value)&&!float.IsInfinity(condition.value)||
                        condition.kind=="nearby_gene_state"&&!string.IsNullOrEmpty(condition.geneId)&&!string.IsNullOrEmpty(condition.geneStateId)&&!string.IsNullOrEmpty(condition.rangeAttributeId)&&new[]{"present","absent"}.Contains(condition.comparison);
                    if(!valid)throw new InvalidDataException("实体状态条件类型、比较方式或参数无效："+condition.kind);
                }
            }
        }
        public static HashSet<string> ExpandTags(IEnumerable<string> tags,EntityStateDocument doc)
        {
            var result=new HashSet<string>();var pending=new Queue<string>(tags);
            while(pending.Count>0)
            {
                string id=pending.Dequeue();var definition=doc.Get(id);
                if(definition==null)throw new InvalidDataException("实体状态标签未注册："+id);
                if(!result.Add(id))continue;
                foreach(var transition in definition.transitions)pending.Enqueue(transition.to);
            }
            result.Add(doc.defaultStateId);return result;
        }
    }
}
