using System.Collections.Generic;
using System.Linq;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：实体状态是基因授予的独立能力；解释注册表的条件和迁移，不修改基因本身的状态。
    public sealed class EntityStateController : MonoBehaviour
    {
        private EntityGeneController _genes;
        private string _current;
        private HashSet<string> _available=new HashSet<string>();
        private GenomeEvaluation _grantsEvaluation;private EntityStateDocument _grantsRegistry;private bool _grantsEgg;
        public DarwinEntity DetectedTarget {get;private set;}
        public DarwinEntity HuntTarget {get;set;}
        public EntityStateDefinition Current=>EntityStateRegistry.Current.Get(_available.Contains(_current??"")?_current:EntityStateRegistry.Current.defaultStateId);
        public IEnumerable<EntityStateDefinition> Available=>EntityStateRegistry.Current.states.Where(s=>_available.Contains(s.id));
        private void Awake(){_genes=GetComponent<EntityGeneController>();}
        // ★ 适配：运行时基因逻辑使用的动态组件由有效标签安装；移除/抑制基因时同步撤销状态授权。
        public static void Rebind(EntityGeneController genes)
        {
            bool tagged=genes.Evaluation?.Genes.Any(e=>e.IsActive&&e.Definition.EntityStateTags.Count>0)==true;
            var component=genes.GetComponent<EntityStateController>();
            if(component==null&&tagged)component=genes.gameObject.AddComponent<EntityStateController>();
            if(component==null)return;
            component.RebuildGrants(true);
            if(!component._available.Contains(component._current??"")){component._current=EntityStateRegistry.Current.defaultStateId;component.HuntTarget=null;component.DetectedTarget=null;}
        }
        private void RebuildGrants(bool force=false)
        {
            bool egg=GetComponent<EggIncubation>()?.Incubating==true;
            // ★ 修复（防Bug回归）：基因授权只随表达、注册表或孵化变化，不应每个小步重新扩展图并分配 HashSet。
            if(!force&&ReferenceEquals(_grantsEvaluation,_genes.Evaluation)&&ReferenceEquals(_grantsRegistry,EntityStateRegistry.Current)&&_grantsEgg==egg)return;
            _grantsEvaluation=_genes.Evaluation;_grantsRegistry=EntityStateRegistry.Current;_grantsEgg=egg;
            var tags=_genes.Evaluation.Genes.Where(e=>e.IsActive).SelectMany(e=>e.Definition.EntityStateTags);
            if(egg)tags=Enumerable.Empty<string>();
            _available=EntityStateRegistry.ExpandTags(tags,EntityStateRegistry.Current);
        }
        public void Advance(float dt)
        {
            if(dt<=0)return;
            RebuildGrants();DetectedTarget=null;
            // ★ 修复（防Bug回归）：蚁群已有独占分工决策，普通巡逻/捕猎感知的结果会被覆盖，不能再为每名成员重复扫描世界。
            if(AntColonyMember.IsManaged(_genes)){Set(EntityStateRegistry.Current.defaultStateId,null);return;}
            if(EcologyMotion.Immobilized(_genes)){Set(EntityStateRegistry.Current.defaultStateId,null);return;}
            var current=Current;
            foreach(var transition in current.transitions)
                if(_available.Contains(transition.to)&&PatrolRhythm.Eligible(_genes,EntityStateRegistry.Current.Get(transition.to))&&Matches(transition.any,out var target)&&Matches(EntityStateRegistry.Current.Get(transition.to).entryAny,out var entryTarget))
                {Set(transition.to,target!=null?target:entryTarget);return;}
            if(current.id!=EntityStateRegistry.Current.defaultStateId&&PatrolRhythm.Eligible(_genes,current)&&Matches(current.entryAny,out var found)){Set(current.id,found);return;}
            foreach(var state in Available.Where(s=>s.id!=EntityStateRegistry.Current.defaultStateId&&PatrolRhythm.Eligible(_genes,s)).OrderByDescending(s=>s.priority).ThenBy(s=>s.id,System.StringComparer.Ordinal))
                if(Matches(state.entryAny,out var target)){Set(state.id,target);return;}
            Set(EntityStateRegistry.Current.defaultStateId,null);
        }
        private void Set(string id,DarwinEntity detected)
        {if(_current!=id)HuntTarget=null;_current=id;DetectedTarget=detected;}
        private bool Matches(List<EntityStateRule> any,out DarwinEntity detected)
        {
            detected=null;if(any.Count==0)return true;
            foreach(var rule in any)
            {
                bool matched=true;DarwinEntity candidate=null;
                foreach(var condition in rule.all)
                {
                    if(condition.kind=="stat")
                    {
                        var stats=GetComponent<EntityStats>();
                        if(stats==null||!stats.TryGet(condition.attributeId,out float value)||(condition.comparison=="gt"?value<=condition.value:value>condition.value)){matched=false;break;}
                    }
                    else
                    {
                        var nearby=FindMatchingEntity(condition);
                        if((condition.comparison=="present")!=(nearby!=null)){matched=false;break;}
                        if(nearby!=null)candidate=nearby;
                    }
                }
                if(matched){detected=candidate;return true;}
            }
            return false;
        }
        private DarwinEntity FindMatchingEntity(EntityStateCondition condition)
        {
            float range=_genes.GetAttribute(condition.rangeAttributeId);if(range<=0)return null;
            DarwinEntity best=null;float distance=float.MaxValue;
            foreach(var entity in EntityRegistry.All)
            {
                if(entity.gameObject==gameObject||!EcologyQueries.Alive(entity))continue;
                // ★ 修复（防Bug回归）：同群士兵的捕猎倾向不能被误认成附近敌对捕猎者。
                if(GetComponent<PopulationBehavior>()?.SameGroup(entity)==true)continue;
                var gene=EcologyQueries.Active(entity.Genes,condition.geneId);
                if(gene?.State?.StateId!=condition.geneStateId||!EcologyMotion.Detectable(entity,transform.position,range,GetComponent<DarwinEntity>()))continue;
                float current=(entity.transform.position-transform.position).sqrMagnitude;
                if(current<distance){distance=current;best=entity;}
            }
            return best;
        }
        // ★ 适配：条件没有计时或随机副作用；读档先恢复标签与状态，下一模拟步再按恢复完整的周边环境判定。
        public void Restore(string id)
        {RebuildGrants();Set(_available.Contains(id??"")?id:EntityStateRegistry.Current.defaultStateId,null);}
    }
}
