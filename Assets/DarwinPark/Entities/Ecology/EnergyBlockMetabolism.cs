using System;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using UnityEngine;

// ★ 适配：降低获取速度时只扣实际转移量，避免营养凭空损失。
// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：普通实体的能量块吸收/吐出与蚁群运输分离，所有流转均从实际营养扣减，不再免费生成。
    public sealed class EnergyBlockMetabolism : MonoBehaviour
    {
        private EntityGeneController _genes;private DarwinEntity _entity;
        private void Awake(){_genes=GetComponent<EntityGeneController>();_entity=GetComponent<DarwinEntity>();}
        public static EnergyBlockMetabolism Ensure(GameObject host)=>DarwinPark.Core.OptionalComponent.Read<EnergyBlockMetabolism>(host)??host.AddComponent<EnergyBlockMetabolism>();
        public bool Active=>EcologyQueries.Alive(_entity)&&EcologyQueries.Active(_genes,NutrientUtilizationSettings.Current.geneId)!=null&&!EcologyMotion.Immobilized(_genes);
        private ColonyResource Find(bool nearby)
        {
            if(!Active)return null;var settings=NutrientUtilizationSettings.Current;var stats=GetComponent<EntityStats>();
            if(!stats.TryGet(settings.nutrientAttribute,out float food)||food>=settings.feedThreshold)return null;
            // ★ 适配：普通实体同样按全局资源坐标生成顺序计划，当前能量耗尽再接续。
            return EntityResourcePlan.FindEnergy(_genes,nearby?settings.feedRadius:settings.searchRadius,nearby);
        }

        public bool Destination(out Vector3 point)
        {var resource=Find(false);point=resource!=null?resource.transform.position:transform.position;return resource!=null;}
        public void Advance(float dt)
        {
            // ★ 修复（防Bug回归）：蚁群已在分工中吸收，同一实体拥有新基因时不能重复进食；卵和被搬运活体也不能抢食。
            if(dt<=0||AntColonyMember.IsManaged(_genes))return;
            EntityResourcePlan.TickEnergy(_genes,dt);
            var resource=Find(true);if(resource==null)return;
            var stats=GetComponent<EntityStats>();var settings=NutrientUtilizationSettings.Current;
            // ★ 修复（防Bug回归）：容量是直接查询，不为每次进食复制整个属性表。
            float room=stats.CapacityRemaining(settings.nutrientAttribute)+stats.CapacityRemaining(settings.reserveAttribute);
            float amount=Mathf.Min(room,resource.State.energy,_genes.GetAttribute(settings.absorptionAttribute)*dt*DarwinPark.Core.GameplayTuning.Current.nutritionGainMultiplier);if(amount<=0)return;
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
            var ant=DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(this);string group=ant!=null&&ant.Active?ant.GroupId:EcologyIdentity.Of(_entity);
            // 资源创建成功后才扣账，出现在吸收半径之外，避免同帧又被自己自动吸回；其他实体可直接吸收。
            resource=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=group,kind=ColonyResourceKind.Energy,energy=amount,local=EcologyQueries.ToLocal(point),scale=Vector3.one});
            float fromReserve=Mathf.Min(Mathf.Max(0,reserve),amount);stats.Modify(settings.reserveAttribute,-fromReserve,false);stats.Modify(settings.nutrientAttribute,-(amount-fromReserve),false);return true;
        }
    }
}
