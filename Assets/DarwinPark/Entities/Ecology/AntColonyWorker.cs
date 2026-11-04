using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：单个工匠独占运输任务；活植株仍为原实体，读档/搬运中断都能恢复其刚体。
    public static class AntColonyWorker
    {
        private static readonly Dictionary<DarwinEntity,AntColonyMember> Carriers=new Dictionary<DarwinEntity,AntColonyMember>();
        private static readonly Dictionary<AntColonyMember,DarwinEntity> Cargo=new Dictionary<AntColonyMember,DarwinEntity>();
        private static readonly Dictionary<string,AntColonyMember> Claims=new Dictionary<string,AntColonyMember>();
        private static readonly Dictionary<AntColonyMember,string> Targets=new Dictionary<AntColonyMember,string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset(){Carriers.Clear();Cargo.Clear();Claims.Clear();Targets.Clear();}
        private static DarwinEntity Entity(string id)=>EcologyIdentity.Find(id);
        // ★ 修复（防Bug回归）：移动属性只需判定是否被搬运，不应每次扫描所有蚁群并重算每名成员的表达。
        private static void TrackCargo(AntColonyMember worker,DarwinEntity entity)
        {
            // ★ 修复（防Bug回归）：Unity 已销毁对象会与 null 比较相等，必须比较托管引用，否则途中死亡的运载物会永久留在索引中。
            if(Cargo.TryGetValue(worker,out var previous)&&!ReferenceEquals(previous,entity)){Carriers.Remove(previous);Cargo.Remove(worker);}
            if(entity!=null){Cargo[worker]=entity;Carriers[entity]=worker;}
        }
        private static void TrackTarget(AntColonyMember worker)
        {
            string id=worker.State.livingTarget;
            if(Targets.TryGetValue(worker,out var previous)&&previous!=id){if(Claims.TryGetValue(previous,out var owner)&&owner==worker)Claims.Remove(previous);Targets.Remove(worker);}
            if(string.IsNullOrEmpty(id))return;
            Targets[worker]=id;if(!Claims.TryGetValue(id,out var current)||current==worker||current==null||!current.Active)Claims[id]=worker;
        }
        private static bool Claimed(DarwinEntity entity,AntColonyMember worker)
        {
            string id=EcologyIdentity.Of(entity);
            if(!Claims.TryGetValue(id,out var owner)||owner==worker)return false;
            if(owner!=null&&owner.Active&&(owner.State.livingTarget==id||owner.State.carriedLiving==id))return true;
            Claims.Remove(id);return false;
        }
        // ★ 适配：体内隐藏的寄生幼体不是可搬运植株；不能把被宿主带到附近的幼体当作零速食物。
        private static bool Valid(DarwinEntity entity,AntColonyMember worker)=>EcologyQueries.Alive(entity)&&ColonyActivityRange.Contains(worker.Population,entity.transform.position)&&entity.GetComponent<NutritionParasite>()?.Host==null&&!worker.Population.SameGroup(entity)&&entity.Genes.GetAttribute("moveSpeed")<=0&&entity.GetComponent<EntityManualControl>()?.IsControlled!=true;
        public static void Advance(AntColonyMember worker,float dt)
        {
            var state=worker.State;var settings=AntColonySettings.Current;
            var carried=ColonyResource.Find(state.carriedResource);var live=Entity(state.carriedLiving);
            TrackTarget(worker);TrackCargo(worker,live);
            if(carried!=null||live!=null)
            {
                worker.Activity=ColonyActivity.Transport;
                Vector3 held=worker.transform.position+EcologyQueries.Up(worker.transform.position)*settings.transportOffset;
                if(carried!=null)carried.CarryTo(held);
                // ★ 适配：搬运会在目标自己的Update之后传送位置，同帧更新邻域，避免食物与竞争还停留在旧格子。
                if(live!=null){live.transform.position=held;EntityNeighborhood.ObservePosition(live.Genes);}
                // ★ 修复（防Bug回归）：运输中允许先补给但保留独占货物，不能无限执行运输直到饿死，也不能中断后复制货物。
                if(ColonyFeeding.Destination(worker,out _))return;
                // ★ 修复（防Bug回归）：王后移动后育幼送达点必须随之更新，避免永远送往旧位置。
                if(state.deliveryNursery&&worker.Queen!=null)state.deliveryPoint=EcologyQueries.ToLocal(worker.Queen.transform.position);
                var destination=EcologyQueries.ToWorld(state.deliveryPoint);
                if(EcologyQueries.Distance(worker.transform.position,destination)>settings.arrivalDistance)return;
                float energy=carried!=null?carried.State.energy:ColonyResource.Biomass(live);
                if(carried!=null)carried.Remove();
                if(live!=null){live.gameObject.SetActive(false);Object.Destroy(live.gameObject);}
                ColonyResource.Convert(worker.GroupId,destination,energy,state.deliveryNursery);
                state.carriedResource=state.carriedLiving=state.resourceTarget=state.livingTarget=null;TrackCargo(worker,null);TrackTarget(worker);return;
            }
            state.carriedResource=state.carriedLiving=null;
            // ★ 修复（防Bug回归）：未取货时同样先解决饥饿，原任务保留，吃饱后继续执行。
            if(ColonyFeeding.Destination(worker,out _))return;
            var resource=ColonyResource.Find(state.resourceTarget);var plant=Entity(state.livingTarget);
            if(resource!=null&&(!ColonyActivityRange.Contains(worker.Population,resource.transform.position)||!resource.Claim(worker)))
            {resource.Release(worker.Id);state.resourceTarget=null;resource=null;}
            if(plant!=null&&(!Valid(plant,worker)||Claimed(plant,worker))){state.livingTarget=null;plant=null;}
            if(resource==null&&plant==null&&state.scan<=0)
            {
                state.scan=settings.scanInterval;
                // ★ 适配：活动圆决定采集边界，搜索距离仍决定单次感知；二者不能被个人出生领地替代。
                foreach(var body in ColonyResource.All.Where(r=>ColonyCorpseAccess.Available(r,worker)&&ColonyActivityRange.Contains(worker.Population,r.transform.position)&&EcologyQueries.Distance(worker.transform.position,r.transform.position)<=settings.transportSearchRadius).OrderBy(r=>(r.transform.position-worker.transform.position).sqrMagnitude))
                    if(body.Claim(worker)){resource=body;state.resourceTarget=body.State.id;break;}
                if(resource==null)
                {
                    // ★ 适配：先筛廉价距离再读基因属性，远处种群不参与运输能力与占用判定。
                    plant=EntityRegistry.All.Where(e=>(e.transform.position-worker.transform.position).sqrMagnitude<=settings.transportSearchRadius*settings.transportSearchRadius&&Valid(e,worker)&&!Claimed(e,worker)&&EcologyQueries.Distance(worker.transform.position,e.transform.position)<=settings.transportSearchRadius).OrderBy(e=>(e.transform.position-worker.transform.position).sqrMagnitude).FirstOrDefault();
                    state.livingTarget=plant!=null?EcologyIdentity.Of(plant):null;
                    TrackTarget(worker);
                }
            }
            if(resource==null&&plant==null){worker.Activity=ColonyActivity.Rest;return;}
            worker.Activity=ColonyActivity.Collect;Vector3 point=resource!=null?resource.transform.position:plant.transform.position;
            if(EcologyQueries.Distance(worker.transform.position,point)>settings.transportReach)return;
            if(resource!=null)state.carriedResource=resource.State.id;
            else
            {
                state.carriedLiving=EcologyIdentity.Of(plant);var body=plant.GetComponent<Rigidbody>();
                TrackCargo(worker,plant);
                // ★ 修复（防Bug回归）：静止活体也可能原本就是运动学刚体，不能向这种刚体写速度；释放时仍恢复原始模式。
                if(body!=null){state.originalKinematic=body.isKinematic;state.originalDetect=body.detectCollisions;if(!body.isKinematic)body.linearVelocity=Vector3.zero;body.isKinematic=true;body.detectCollisions=false;}
            }
            var queen=worker.Queen;
            state.deliveryNursery=UnityEngine.Random.value<settings.nurseryDeliveryFraction;
            if(queen==null){state.deliveryPoint=EcologyQueries.ToLocal(worker.Center);state.deliveryNursery=true;}
            else
            {
                queen.EnsureSupplyPoints();
                // ★ 适配：优先补足当前不存在的两类补给，再按配置分配后续货物，避免随机连抽使王后永久断粮。
                if(!ColonyResource.All.Any(r=>r.State.group==worker.GroupId&&r.State.kind==ColonyResourceKind.Nursery))state.deliveryNursery=true;
                else if(!ColonyResource.All.Any(r=>r.State.group==worker.GroupId&&r.State.kind==ColonyResourceKind.Supply))state.deliveryNursery=false;
                state.deliveryPoint=state.deliveryNursery?EcologyQueries.ToLocal(queen.transform.position):queen.State.supplyPoints[UnityEngine.Random.Range(0,queen.State.supplyPoints.Count)];
            }
        }
        public static Vector3 Destination(AntColonyMember worker)
        {
            var state=worker.State;
            if(!string.IsNullOrEmpty(state.carriedResource)||!string.IsNullOrEmpty(state.carriedLiving))return EcologyQueries.ToWorld(state.deliveryPoint);
            var resource=ColonyResource.Find(state.resourceTarget);if(resource!=null)return resource.transform.position;
            var plant=Entity(state.livingTarget);if(plant!=null)return plant.transform.position;
            // 工匠休息时靠近群内补给点，避免饥饿时被无效运输目标锁住。
            var supply=ColonyResource.All.Where(r=>r.State.group==worker.GroupId&&r.State.kind==ColonyResourceKind.Supply).OrderBy(r=>(r.transform.position-worker.transform.position).sqrMagnitude).FirstOrDefault();
            return supply!=null?supply.transform.position:worker.Center;
        }
        public static void Release(AntColonyMember worker)
        {
            if(worker==null||worker.Entity==null)return;var state=worker.State;
            ColonyResource.Find(state.resourceTarget)?.Release(worker.Id);ColonyResource.Find(state.carriedResource)?.Release(worker.Id);
            var live=Entity(state.carriedLiving);
            if(live!=null)
            {var body=live.GetComponent<Rigidbody>();if(body!=null){body.isKinematic=state.originalKinematic;body.detectCollisions=state.originalDetect;}live.transform.position=EcologyQueries.Surface(worker.transform.position);EntityNeighborhood.ObservePosition(live.Genes);}
            state.carriedResource=state.carriedLiving=state.resourceTarget=state.livingTarget=null;
            TrackCargo(worker,null);TrackTarget(worker);
        }
        public static void Resolve(AntColonyMember worker)
        {
            TrackTarget(worker);var live=Entity(worker.State.carriedLiving);TrackCargo(worker,live);if(live==null)return;
            var body=live.GetComponent<Rigidbody>();if(body!=null){body.isKinematic=true;body.detectCollisions=false;}
        }
        public static bool IsCarried(DarwinEntity entity)=>entity!=null&&Carriers.TryGetValue(entity,out var worker)&&worker!=null&&worker.Active&&worker.State.carriedLiving==EcologyIdentity.Of(entity);
    }
}
