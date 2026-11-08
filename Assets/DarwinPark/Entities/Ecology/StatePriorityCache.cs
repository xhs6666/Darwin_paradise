using System.Collections.Generic;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：状态优先级排序按授权集合和注册表缓存，避免每个子步排序分配。
    public sealed class StatePriorityCache
    {
        private readonly List<DarwinPark.Genetics.EntityStateDefinition> _states=new();private HashSet<string> _grants;private DarwinPark.Genetics.EntityStateDocument _registry;
        public IReadOnlyList<DarwinPark.Genetics.EntityStateDefinition> Read(HashSet<string> grants,DarwinPark.Genetics.EntityStateDocument registry)
        {if(ReferenceEquals(grants,_grants)&&ReferenceEquals(registry,_registry))return _states;_grants=grants;_registry=registry;_states.Clear();foreach(var state in registry.states)if(grants.Contains(state.id)&&state.id!=registry.defaultStateId)_states.Add(state);_states.Sort((a,b)=>{int priority=b.priority.CompareTo(a.priority);return priority!=0?priority:string.CompareOrdinal(a.id,b.id);});return _states;}
    }
}
