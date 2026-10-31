using System;
using System.Linq;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    public enum RtsOrderKind { Move,Attack,Collect,Guard,Stop }
    [Serializable] public sealed class RtsOrderSave
    {public bool active;public RtsOrderKind kind;public Vector3 point;public string target;}
    // ★ 新增：RTS只改变行动意图，实际移动、战斗、搬运仍使用原有基因和体型判定；命令随世界保存。
    public sealed class RtsOrderController : MonoBehaviour
    {
        public float arrivalDistance=2;
        public RtsOrderSave State {get;private set;}=new RtsOrderSave();
        private EntityGeneController _genes;
        private void Awake(){_genes=GetComponent<EntityGeneController>();}
        public static RtsOrderController Ensure(DarwinEntity entity)=>entity.GetComponent<RtsOrderController>()??entity.gameObject.AddComponent<RtsOrderController>();
        public static bool Supports(DarwinEntity entity,RtsOrderKind kind)
        {
            if(!EcologyQueries.Alive(entity)||EcologyMotion.Immobilized(entity.Genes))return false;
            if(kind==RtsOrderKind.Stop)return true;
            if(entity.Genes.GetAttribute("moveSpeed")<=0)return false;
            if(kind==RtsOrderKind.Attack)return entity.Genes.GetAttribute("attack")>0;
            if(kind==RtsOrderKind.Collect)return entity.GetComponent<AntColonyMember>() is AntColonyMember ant&&ant.Active&&!ant.Young&&ant.Role==AntRole.Worker;
            return true;
        }
        public bool Issue(RtsOrderKind kind,Vector3 worldPoint,DarwinEntity target=null,ColonyResource resource=null)
        {
            var entity=GetComponent<DarwinEntity>();if(!Supports(entity,kind))return false;
            if(kind==RtsOrderKind.Attack&&(!EcologyQueries.Alive(target)||!EcologyMotion.CanHunt(_genes,target)))return false;
            if(kind==RtsOrderKind.Collect)
            {
                var worker=GetComponent<AntColonyMember>();
                if(resource!=null&&resource.State.kind==ColonyResourceKind.Corpse&&resource.State.group==worker.GroupId)
                {AntColonyWorker.Release(worker);worker.State.resourceTarget=resource.State.id;worker.State.scan=1;}
                else if(target!=null&&!worker.Population.SameGroup(target)&&target.Genes.GetAttribute("moveSpeed")<=0)
                {AntColonyWorker.Release(worker);worker.State.livingTarget=EcologyIdentity.Of(target);worker.State.scan=1;}
                else return false;
            }
            State=new RtsOrderSave{active=true,kind=kind,point=EcologyQueries.ToLocal(worldPoint),target=target!=null?EcologyIdentity.Of(target):resource?.State.id};return true;
        }
        public bool Destination(out Vector3 point,out DarwinEntity target)
        {
            point=transform.position;target=null;if(!State.active||WorldAuthority.IsReplica)return false;
            if(State.kind==RtsOrderKind.Stop)return true;
            if(!Supports(GetComponent<DarwinEntity>(),State.kind)){State.active=false;return false;}
            if(State.kind==RtsOrderKind.Collect)
            {var worker=GetComponent<AntColonyMember>();if(ColonyResource.Find(State.target)==null&&EcologyIdentity.Find(State.target)==null&&string.IsNullOrEmpty(worker.State.carriedResource)&&string.IsNullOrEmpty(worker.State.carriedLiving))State.active=false;return false;}
            point=EcologyQueries.ToWorld(State.point);
            if(State.kind==RtsOrderKind.Attack)
            {target=EcologyIdentity.Find(State.target);if(!EcologyQueries.Alive(target)||!EcologyMotion.CanHunt(_genes,target)){State.active=false;target=null;return false;}point=target.transform.position;return true;}
            if(State.kind==RtsOrderKind.Guard&&_genes.GetAttribute("attack")>0)
            {
                float range=_genes.GetAttribute("senseRange");
                var guardPoint=point;target=EntityRegistry.All.FirstOrDefault(e=>e!=GetComponent<DarwinEntity>()&&(e.transform.position-guardPoint).sqrMagnitude<=range*range&&EcologyQueries.Alive(e)&&EcologyMotion.CanHunt(_genes,e));
                if(target!=null)point=target.transform.position;
            }
            if(State.kind==RtsOrderKind.Move&&EcologyQueries.Distance(transform.position,point)<=arrivalDistance){State.active=false;return true;}
            return true;
        }
        public RtsOrderSave Capture()=>JsonUtility.FromJson<RtsOrderSave>(JsonUtility.ToJson(State));
        public void Restore(RtsOrderSave state){if(state!=null)State=JsonUtility.FromJson<RtsOrderSave>(JsonUtility.ToJson(state));}
    }
}
