using System;
using System.Collections.Generic;
using System.IO;
using DarwinPark.Core;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    [Serializable]public sealed class EntityResourcePlanSave
    {
        public int version=1;public List<string> energy=new(),corpses=new();public float energyScan,corpseScan;
        public bool IsEmpty=>(energy==null||energy.Count==0)&&(corpses==null||corpses.Count==0)&&energyScan==0&&corpseScan==0;
    }
    // ★ 新增：普通实体的能量块/食腐也用顺序计划；当前资源耗尽后接续，资格和实际营养仍逐次检查。
    public static class EntityResourcePlan
    {
        private sealed class Context {public EntityResourcePlanSave state=new();public long food=-1;public readonly List<ResourceCoordinate> candidates=new();}
        private static readonly Dictionary<EntityGeneController,Context> Plans=new();
        private static Context Get(EntityGeneController genes){if(!Plans.TryGetValue(genes,out var context)){context=new();Plans.Add(genes,context);}return context;}
        public static void TickEnergy(EntityGeneController genes,float dt){if(Plans.TryGetValue(genes,out var context))context.state.energyScan=Mathf.Max(0,context.state.energyScan-dt);}
        public static void TickCorpse(EntityGeneController genes,float dt){var context=Get(genes);context.state.corpseScan=Mathf.Max(0,context.state.corpseScan-dt);}
        private static bool Energy(ColonyResource resource)=>resource!=null&&resource.State.kind!=ColonyResourceKind.Corpse&&(resource.State.kind==ColonyResourceKind.Energy||!WorldRuntimeGate.Loading&&!ColonyResourceOwnership.HasOwner(resource.State.group));
        private static bool Corpse(ColonyResource resource)=>resource!=null&&resource.State.kind==ColonyResourceKind.Corpse&&!resource.HasLiveClaim;
        private static bool Valid(ColonyResource resource,Vector3 point,float radius,bool corpse)=>resource!=null&&(corpse?Corpse(resource)&&(resource.transform.position-point).sqrMagnitude<=radius*radius:Energy(resource)&&EcologyQueries.Distance(point,resource.transform.position)<=radius);
        private static ColonyResource Head(List<string> queue,Vector3 point,float radius,bool corpse)
        {
            while(queue.Count>0){var resource=ColonyResource.Find(queue[0]);if(Valid(resource,point,radius,corpse))return resource;queue.RemoveAt(0);}return null;
        }
        public static ColonyResource FindEnergy(EntityGeneController genes,float radius,bool nearby)
        {
            var context=Get(genes);var state=context.state;var point=genes.transform.position;long revision=ResourceCoordinates.FoodRevision;
            if(context.food!=revision){context.food=revision;state.energyScan=0;}
            // 附近可吃块可以临时插队，远处原任务顺延；实际吸收端仍核对真实距离。
            var current=Head(state.energy,point,nearby?float.PositiveInfinity:radius,false);
            if(current!=null&&Valid(current,point,radius,false))return current;
            if(!nearby&&state.energyScan>0)return null;
            ResourceCoordinates.Query(point,radius,true,false,context.candidates);
            context.candidates.Sort((a,b)=>(a.position-point).sqrMagnitude.CompareTo((b.position-point).sqrMagnitude));
            if(nearby)
            {
                foreach(var candidate in context.candidates)if(Energy(candidate.resource)){string id=candidate.resource.State.id;state.energy.Remove(id);state.energy.Insert(0,id);Trim(state.energy);context.candidates.Clear();return candidate.resource;}
                context.candidates.Clear();return null;
            }
            state.energyScan=SimulationOptimization.Current.decisionSeconds;Fill(state.energy,context.candidates,false);context.candidates.Clear();return Head(state.energy,point,radius,false);
        }
        public static ColonyResource FindCorpse(EntityGeneController genes,float radius,float interval)
        {
            var context=Get(genes);var state=context.state;var point=genes.transform.position;var current=Head(state.corpses,point,radius,true);if(current!=null)return current;
            if(state.corpseScan>0)return null;state.corpseScan=interval;ResourceCoordinates.Query(point,radius,false,true,context.candidates);context.candidates.Sort((a,b)=>(a.position-point).sqrMagnitude.CompareTo((b.position-point).sqrMagnitude));Fill(state.corpses,context.candidates,true);context.candidates.Clear();return Head(state.corpses,point,radius,true);
        }
        private static void Fill(List<string> queue,List<ResourceCoordinate> candidates,bool corpse)
        {
            int limit=SimulationOptimization.Current.taskQueue?SimulationOptimization.Current.workerQueueLimit:1;
            foreach(var candidate in candidates){if(queue.Count>=limit)break;if((corpse?Corpse(candidate.resource):Energy(candidate.resource))&&!queue.Contains(candidate.resource.State.id))queue.Add(candidate.resource.State.id);}
        }
        private static void Trim(List<string> queue){int limit=SimulationOptimization.Current.taskQueue?SimulationOptimization.Current.workerQueueLimit:1;while(queue.Count>limit)queue.RemoveAt(queue.Count-1);}
        public static EntityResourcePlanSave Capture(EntityGeneController genes)=>genes!=null&&Plans.TryGetValue(genes,out var context)&&!context.state.IsEmpty?JsonUtility.FromJson<EntityResourcePlanSave>(JsonUtility.ToJson(context.state)):null;
        public static void Restore(EntityGeneController genes,EntityResourcePlanSave state)
        {Validate(state);if(genes==null)return;Remove(genes);if(state==null||state.IsEmpty)return;var context=Get(genes);context.state=JsonUtility.FromJson<EntityResourcePlanSave>(JsonUtility.ToJson(state));context.state.energy??=new();context.state.corpses??=new();context.food=-1;}
        public static void Validate(EntityResourcePlanSave state)
        {
            if(state==null||state.IsEmpty&&(state.version==0||state.version==1))return;if(state.version!=1||!float.IsFinite(state.energyScan)||!float.IsFinite(state.corpseScan)||state.energyScan<0||state.corpseScan<0)throw new InvalidDataException("普通实体资源计划版本或计时无效");
            foreach(var queue in new[]{state.energy,state.corpses}){if(queue==null)continue;if(queue.Count>128)throw new InvalidDataException("普通实体资源计划过长");var ids=new HashSet<string>();foreach(var id in queue)if(!Guid.TryParseExact(id,"N",out _)||!ids.Add(id))throw new InvalidDataException("普通实体资源计划目标重复或无效");}
        }
        // ★ 适配：任务完成/失效立即打断行动缓存，不等待决策周期到期。
        public static int Token(EntityGeneController genes)
        {if(!Plans.TryGetValue(genes,out var context))return 0;var state=context.state;string energy=state.energy.Count>0?state.energy[0]:null,corpse=state.corpses.Count>0?state.corpses[0]:null;return HashCode.Combine(energy,corpse,ColonyResource.Find(energy)!=null,ColonyResource.Find(corpse)!=null);}
        public static void Remove(EntityGeneController genes){if(!ReferenceEquals(genes,null))Plans.Remove(genes);}
        public static void Clear()=>Plans.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
