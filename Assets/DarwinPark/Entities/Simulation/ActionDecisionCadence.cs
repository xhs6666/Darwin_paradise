using DarwinPark.Entities.Simulation; // ★ 适配：同步模拟批内复用组件，不缓存实时状态数值。
using System;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：决策独立于营养/攻击小步。任务、命令、受击、饥饿和状态变化立即打断缓存。
    public sealed class ActionDecisionCadence
    {
        private float _remaining;private int _token,_lastHashFrame=-1;private bool _ready;
        private DarwinEntity _retaliation;private RtsOrderSave _order;private int _revision;
        private PopulationCommand _manualCommand;private Vector3 _manualPoint;private DarwinEntity _manualTarget;
        public Vector3 point;public DarwinEntity target;public float multiplier=1;public bool directive;
        public long Decisions {get;private set;}
        public bool Due(EntityGeneController genes,float dt)
        {
            _remaining-=dt;
            var combat=ActiveSimulationBatch.Read<CombatIntent>(genes);
            var rts=ActiveSimulationBatch.Read<RtsOrderController>(genes);
            var population=ActiveSimulationBatch.Read<PopulationBehavior>(genes);
            bool manual=population?.ManualRemaining>0;
            var manualCommand=manual?population.Command:PopulationCommand.None;
            var manualPoint=manual?population.CommandPointLocal:Vector3.zero;
            var manualTarget=manual?population.Target:null;
            bool lost=target!=null&&!EcologyQueries.Alive(target);
            bool immediate=!_ready||lost||combat?.Target!=_retaliation||rts?.State!=_order||genes.LogicRevision!=_revision||
                manualCommand!=_manualCommand||manualPoint!=_manualPoint||manualTarget!=_manualTarget;
            int frame=Time.frameCount;
            if(!immediate&&SimulationOptimization.Current.staggerRoutineDecisions&&Application.isPlaying)
            {
                if(_lastHashFrame==frame||!RoutineTurn(genes))return false;
            }
            _manualCommand=manualCommand;_manualPoint=manualPoint;_manualTarget=manualTarget;
            _lastHashFrame=frame;
            _retaliation=combat?.Target;_order=rts?.State;_revision=genes.LogicRevision;
            var hash=new HashCode();hash.Add(genes.LogicRevision);hash.Add(EntityResourcePlan.Token(genes));
            var ant=ActiveSimulationBatch.Read<AntColonyMember>(genes);if(ant!=null&&ant.Active){hash.Add(EntityTaskPlan.Feeding(ant));hash.Add(ant.State.resourceTarget);hash.Add(ant.State.livingTarget);hash.Add(ant.State.carriedResource);hash.Add(ant.State.carriedLiving);hash.Add(ant.State.plan?.supply);hash.Add(ant.State.founding);hash.Add(ant.State.patrolRemaining>0);}
            hash.Add(combat?.Target);
            hash.Add(rts?.State);hash.Add(rts?.State.active??false);
            hash.Add(population?.Command);hash.Add(population?.CommandPointLocal);hash.Add(population?.Target);
            hash.Add(ActiveSimulationBatch.Read<EntityStateController>(genes)?.Current?.id);
            if(ActiveSimulationBatch.Read<Stats.EntityStats>(genes) is Stats.EntityStats stats){hash.Add(EcologyEnergyBudget.WantsFood(genes,stats,Logic.LocomotionSettings.Load().hungerThreshold));}
            int token=hash.ToHashCode();
            if(!immediate&&_token==token&&_remaining>0)return false;
            _token=token;_ready=true;_remaining=SimulationOptimization.Current.decisionSeconds;Decisions++;return true;
        }
        // ★ 新增：仅推迟普通决策刷新，受击、手控、近岸、显式任务和基因变更仍立即处理。
        internal static bool RoutineTurn(EntityGeneController genes)
        {
            var settings=SimulationOptimization.Current;
            if(!settings.staggerRoutineDecisions||!Application.isPlaying)return true;
            if(ActiveSimulationBatch.Read<CombatIntent>(genes)?.Remaining>0||
                ActiveSimulationBatch.Read<RtsOrderController>(genes)?.State.active==true||
                ActiveSimulationBatch.Read<EntityManualControl>(genes)?.IsControlled==true||
                ActiveSimulationBatch.Read<EntityActivityPlan>(genes)?.Active==true||
                ActiveSimulationBatch.Read<PlanetGravity>(genes)?.Grounding.NearWater(settings.shorePriorityDistance)==true)
                return true;
            int buckets=settings.decisionFrameBuckets;
            return (uint)genes.GetInstanceID()%(uint)buckets==(uint)Time.frameCount%(uint)buckets;
        }

        public void Reset(){_ready=false;_lastHashFrame=-1;target=null;}
    }
}
