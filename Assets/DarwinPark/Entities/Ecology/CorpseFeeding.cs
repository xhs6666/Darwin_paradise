using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：食腐是独立食性，不靠行动基因伤害凭空获得营养；只吃未被工匠领取的尸体。
    public sealed class CorpseFeeding:MonoBehaviour
    {
        private EntityGeneController _genes;private EntityStats _stats;private ColonyResource _target;private float _scan;
        private void Awake(){_genes=GetComponent<EntityGeneController>();_stats=GetComponent<EntityStats>();}
        private void Update(){if(!WorldAuthority.IsReplica)Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);}
        private bool Active=>EcologyQueries.Active(_genes,NutritionEcologySettings.Current.scavengerGene)!=null;
        private bool Valid(ColonyResource resource)=>resource!=null&&resource.isActiveAndEnabled&&resource.State.energy>0&&resource.State.kind==ColonyResourceKind.Corpse&&!resource.HasLiveClaim;
        public void Advance(float dt)
        {
            if(dt<=0||!Active||!EcologyQueries.Alive(GetComponent<DarwinEntity>())||EcologyMotion.Immobilized(_genes))return;
            var settings=NutritionEcologySettings.Current;_scan-=dt;
            if(!Valid(_target))_target=null;
            if(_target==null&&_scan<=0)
            {
                _scan=settings.scanInterval;float best=settings.searchRadius*settings.searchRadius;
                foreach(var resource in ColonyResource.All)if(Valid(resource)){float d=(resource.transform.position-transform.position).sqrMagnitude;if(d<best){best=d;_target=resource;}}
            }
            if(!Valid(_target)||EcologyQueries.Distance(transform.position,_target.transform.position)>settings.feedingReach)return;
            float amount=NutritionTransfer.Feed(_stats,Mathf.Min(_target.State.energy,_genes.GetAttribute(settings.scavengeAttribute)*dt));
            _target.State.energy-=amount;if(_target.State.energy<=0)_target.Remove();
        }
        public bool Destination(out Vector3 point)
        {
            point=transform.position;if(!Active||!Valid(_target)||NutritionTransfer.Room(_stats)<=0)return false;
            if(!EcologyEnergyBudget.WantsFood(_genes,_stats,NutritionEcologySettings.Current.foodThreshold))return false;
            point=_target.transform.position;return true;
        }
    }
}
