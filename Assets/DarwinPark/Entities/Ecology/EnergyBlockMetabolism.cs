using System;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：普通实体的能量块吸收/吐出与蚁群运输分离，所有流转均从实际营养扣减，不再免费生成。
    public sealed class EnergyBlockMetabolism : MonoBehaviour
    {
        private EntityGeneController _genes;private DarwinEntity _entity;
        private void Awake(){_genes=GetComponent<EntityGeneController>();_entity=GetComponent<DarwinEntity>();}
        public static EnergyBlockMetabolism Ensure(GameObject host)=>host.GetComponent<EnergyBlockMetabolism>()??host.AddComponent<EnergyBlockMetabolism>();
        public bool Active=>EcologyQueries.Alive(_entity)&&EcologyQueries.Active(_genes,NutrientUtilizationSettings.Current.geneId)!=null&&!EcologyMotion.Immobilized(_genes);
        private ColonyResource Find(bool nearby)
        {
            if(!Active)return null;var settings=NutrientUtilizationSettings.Current;var stats=GetComponent<EntityStats>();
            if(!stats.TryGet(settings.nutrientAttribute,out float food)||food>=settings.feedThreshold)return null;
            float range=nearby?settings.feedRadius:settings.searchRadius;ColonyResource nearest=null;float best=float.PositiveInfinity;
            foreach(var resource in ColonyResource.All)
            {
                // ★ 适配：普通实体也能利用旧群废弃的黄色补给，但仍不能吞食存活蚁群的专用口粮或尸体。
                if(resource.State.kind==ColonyResourceKind.Corpse||resource.State.kind!=ColonyResourceKind.Energy&&(WorldRuntimeGate.Loading||ColonyResourceOwnership.HasOwner(resource.State.group)))continue;
                float distance=(resource.transform.position-transform.position).sqrMagnitude;
                if(distance>=best||EcologyQueries.Distance(resource.transform.position,transform.position)>range)continue;
                best=distance;nearest=resource;
            }
            return nearest;
        }
        public bool Destination(out Vector3 point)
        {var resource=Find(false);point=resource!=null?resource.transform.position:transform.position;return resource!=null;}
        public void Advance(float dt)
        {
            // ★ 修复（防Bug回归）：蚁群已在分工中吸收，同一实体拥有新基因时不能重复进食；卵和被搬运活体也不能抢食。
            if(dt<=0||AntColonyMember.IsManaged(_genes))return;
            var resource=Find(true);if(resource==null)return;
            var stats=GetComponent<EntityStats>();var settings=NutrientUtilizationSettings.Current;
            float room=stats.CaptureValues().Where(v=>v.id==settings.nutrientAttribute||v.id==settings.reserveAttribute).Sum(v=>Mathf.Max(0,v.max-v.value));
            float amount=Mathf.Min(room,resource.State.energy,_genes.GetAttribute(settings.absorptionAttribute)*dt);if(amount<=0)return;
            stats.Feed(amount);resource.State.energy-=amount;if(resource.State.energy<=0)resource.Remove();
        }
        public bool TryRelease(Vector3 forward,out ColonyResource resource)
        {
            resource=null;
            if(!Active||WorldAuthority.IsReplica||GameInputGate.Blocked&&!WorldAuthority.RemoteCommand||WorldRuntimeGate.Managed&&!WorldRuntimeGate.Ready||
                ServiceLocator.TryGet<SimulationClock>(out var clock)&&!clock.IsRunning)return false;
            var settings=NutrientUtilizationSettings.Current;var stats=GetComponent<EntityStats>();
            stats.TryGet(settings.nutrientAttribute,out float food);stats.TryGet(settings.reserveAttribute,out float reserve);
            float amount=_genes.GetAttribute(settings.releaseAttribute),availableFood=Mathf.Max(0,food-settings.minimumFoodAfterRelease);
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||Mathf.Max(0,reserve)+availableFood<amount)return false;
            var up=EcologyQueries.Up(transform.position);var direction=Vector3.ProjectOnPlane(forward,up).normalized;
            if(direction.sqrMagnitude<.1f)direction=Vector3.ProjectOnPlane(Vector3.forward,up).normalized;
            if(direction.sqrMagnitude<.1f)direction=Vector3.ProjectOnPlane(Vector3.right,up).normalized;
            var point=EcologyQueries.Surface(transform.position+direction*settings.releaseDistance);
            var ant=GetComponent<AntColonyMember>();string group=ant!=null&&ant.Active?ant.GroupId:EcologyIdentity.Of(_entity);
            // 资源创建成功后才扣账，出现在吸收半径之外，避免同帧又被自己自动吸回；其他实体可直接吸收。
            resource=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=group,kind=ColonyResourceKind.Energy,energy=amount,local=EcologyQueries.ToLocal(point),scale=Vector3.one});
            float fromReserve=Mathf.Min(Mathf.Max(0,reserve),amount);stats.Modify(settings.reserveAttribute,-fromReserve,false);stats.Modify(settings.nutrientAttribute,-(amount-fromReserve),false);return true;
        }
    }
}
