using System;
using System.Collections.Generic;
using System.IO;
using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    public enum EntityActivityKind { Feed,Hunt,Transport,Rest }
    [Serializable] public sealed class EntityActivityTask {public EntityActivityKind kind;public string target;public float remaining;public bool started;}
    [Serializable] public sealed class EntityActivityPlanSave {public bool enabled=true;public List<EntityActivityTask> tasks=new();}
    // ★ 新增：只在用户安排任务的实体上安装；按模拟时间推进，补给/战斗/运输仍复用原能力和资源结算。
    public sealed class EntityActivityPlan:MonoBehaviour
    {
        public EntityActivityPlanSave State {get;private set;}=new();
        public bool Active=>State.enabled&&State.tasks.Count>0;
        public string Status {get;private set;}="自动行为";
        private EntityGeneController _genes;private EntityStats _stats;private DarwinEntity _entity;private float _scan;
        private void Awake(){_genes=GetComponent<EntityGeneController>();_stats=GetComponent<EntityStats>();_entity=GetComponent<DarwinEntity>();Simulation.ActiveSimulationBatch.Invalidate(_genes);}
        public static EntityActivityPlan Ensure(DarwinEntity entity)=>OptionalComponent.Read<EntityActivityPlan>(entity)??entity.gameObject.AddComponent<EntityActivityPlan>();
        public static void Validate(EntityActivityPlanSave state)
        {
            if(state==null)return;
            if(state.tasks==null||state.tasks.Count>SimulationOptimization.Current.workerQueueLimit)throw new InvalidDataException("任务列表为空或过长");
            foreach(var task in state.tasks)if(task==null||!Enum.IsDefined(typeof(EntityActivityKind),task.kind)||!float.IsFinite(task.remaining)||task.remaining<0||task.remaining>86400||!string.IsNullOrEmpty(task.target)&&!Guid.TryParseExact(task.target,"N",out _))throw new InvalidDataException("任务类型、计时或目标无效");
        }
        public void Restore(EntityActivityPlanSave state){Validate(state);State=state==null?new():JsonUtility.FromJson<EntityActivityPlanSave>(JsonUtility.ToJson(state));_scan=0;Status=Active?"等待执行":"自动行为";}
        public EntityActivityPlanSave Capture()=>JsonUtility.FromJson<EntityActivityPlanSave>(JsonUtility.ToJson(State));
        public static bool Apply(DarwinEntity entity,EntityActivityPlanSave state)
        {
            if(!EcologyQueries.Alive(entity)||state==null)return false;
            try{Validate(state);}catch(InvalidDataException){return false;}
            // 修改顺序不释放正在运输的货物；恢复到运输任务时仍使用原独占货物。
            Ensure(entity).Restore(state);Simulation.EntityWorkScheduler.Wake(entity.Genes);return true;
        }
        public static bool BlocksAutomatic(AntColonyMember member)=>OptionalComponent.Read<EntityActivityPlan>(member) is EntityActivityPlan plan&&plan.Active&&plan.State.tasks[0].kind!=EntityActivityKind.Transport;
        public static string Name(EntityActivityKind kind)=>kind switch{EntityActivityKind.Feed=>"补给",EntityActivityKind.Hunt=>"捕猎",EntityActivityKind.Transport=>"运输",_=>"休息"};
        public static string Limitation(DarwinEntity entity,EntityActivityKind kind)
        {
            if(kind==EntityActivityKind.Transport&&!RtsOrderController.Supports(entity,RtsOrderKind.Collect))return "需成年工匠及行动能力";
            if(kind==EntityActivityKind.Hunt&&(entity.Genes.GetAttribute("attack")<=0||GeneCapabilities.Active(entity.Genes,BiologySettings.Current.centralGene)==null||GeneCapabilities.Active(entity.Genes,BiologySettings.Current.locomotionGene)==null))return "需中枢、行动和攻击能力";
            return "";
        }
        public void Advance(float dt)
        {
            if(!Active||dt<=0||WorldAuthority.IsReplica)return;_scan=Mathf.Max(0,_scan-dt);var task=State.tasks[0];
            if(!string.IsNullOrEmpty(Limitation(_entity,task.kind))){Status=Limitation(_entity,task.kind);return;}
            bool complete=false;
            if(task.kind==EntityActivityKind.Rest){if(!task.started){task.started=true;task.remaining=LocomotionSettings.Load().wanderInterval;}task.remaining=Mathf.Max(0,task.remaining-dt);complete=task.remaining<=0;}
            if(task.kind==EntityActivityKind.Feed&&_stats.TryGet("nutrient",out float food))
            {float threshold=OptionalComponent.Read<AntColonyMember>(this)?.Active==true?GameplayTuning.Current.memberFeedResume:NutrientUtilizationSettings.Current.feedThreshold;complete=food>=Mathf.Min(food+_stats.CapacityRemaining("nutrient"),threshold);}
            if(task.kind==EntityActivityKind.Hunt&&task.started&&!EcologyQueries.Alive(EcologyIdentity.Find(task.target)))complete=true;
            if(task.kind==EntityActivityKind.Transport)
            {var worker=OptionalComponent.Read<AntColonyMember>(this);bool carrying=!string.IsNullOrEmpty(worker.State.carriedResource)||!string.IsNullOrEmpty(worker.State.carriedLiving);if(carrying)task.started=true;else if(task.started)complete=true;}
            if(complete)
            {
                State.tasks.RemoveAt(0);_scan=0;Status=Active?"继续下一任务":"任务完成，恢复自动行为";
                // ★ 修复（防Bug回归）：最后任务完成后刷新调度资格，让静止植物重新进入批结算。
                Simulation.EntityWorkScheduler.Wake(_genes);
            }
        }
        public bool Destination(out Vector3 point,out DarwinEntity target)
        {
            point=transform.position;target=null;if(!Active||WorldAuthority.IsReplica)return false;var task=State.tasks[0];Status=Name(task.kind);
            string reason=Limitation(_entity,task.kind);if(!string.IsNullOrEmpty(reason)){Status=reason;return true;}
            if(task.kind==EntityActivityKind.Rest)return true;
            if(task.kind==EntityActivityKind.Transport){point=AntColonyWorker.Destination(OptionalComponent.Read<AntColonyMember>(this));return true;}
            if(task.kind==EntityActivityKind.Hunt)
            {
                bool graze=GeneCapabilities.Diet(_genes)=="herbivore";target=EcologyIdentity.Find(task.target);
                if(!PerceivedTargets.Valid(_genes,target,graze,out point,true))target=null;
                if(target==null&&_scan<=0){_scan=SimulationOptimization.Current.decisionSeconds;target=PerceivedTargets.Nearest(_genes,graze,null,out point,true);if(target!=null){task.target=EcologyIdentity.Of(target);task.started=true;}}
                if(target==null){point=transform.position;Status="等待可见且可捕食的目标";}return true;
            }
            var ant=OptionalComponent.Read<AntColonyMember>(this);if(ant?.Active==true&&ant.TrySupplyDestination(out point))return true;
            var energy=OptionalComponent.Read<EnergyBlockMetabolism>(this);if(energy!=null&&energy.Destination(out point))return true;
            var corpse=OptionalComponent.Read<CorpseFeeding>(this);if(corpse!=null&&corpse.Destination(out point))return true;
            bool grazing=GeneCapabilities.Diet(_genes)=="herbivore";
            target=EcologyIdentity.Find(task.target);if(!PerceivedTargets.Valid(_genes,target,grazing,out point,true))target=null;
            if(_genes.GetAttribute("attack")>0&&target==null&&_scan<=0){_scan=SimulationOptimization.Current.decisionSeconds;target=PerceivedTargets.Nearest(_genes,grazing,null,out point,true);if(target!=null)task.target=EcologyIdentity.Of(target);}
            if(target==null){point=transform.position;Status="等待营养来源（自养仍正常进行）";}return true;
        }
    }
}
