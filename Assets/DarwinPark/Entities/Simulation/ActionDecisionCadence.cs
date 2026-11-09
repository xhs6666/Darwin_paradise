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
        private float _remaining;private int _token;private bool _ready;
        public Vector3 point;public DarwinEntity target;public float multiplier=1;public bool directive;
        public long Decisions {get;private set;}
        public bool Due(EntityGeneController genes,float dt)
        {
            _remaining-=dt;var hash=new HashCode();hash.Add(genes.LogicRevision);hash.Add(EntityResourcePlan.Token(genes));
            var ant=ActiveSimulationBatch.Read<AntColonyMember>(genes);if(ant!=null&&ant.Active){hash.Add(EntityTaskPlan.Feeding(ant));hash.Add(ant.State.resourceTarget);hash.Add(ant.State.livingTarget);hash.Add(ant.State.carriedResource);hash.Add(ant.State.carriedLiving);hash.Add(ant.State.plan?.supply);hash.Add(ant.State.founding);hash.Add(ant.State.patrolRemaining>0);}
            var combat=ActiveSimulationBatch.Read<CombatIntent>(genes);hash.Add(combat?.Target);
            var rts=ActiveSimulationBatch.Read<RtsOrderController>(genes);hash.Add(rts?.State);hash.Add(rts?.State.active??false);
            var pop=ActiveSimulationBatch.Read<PopulationBehavior>(genes);hash.Add(pop?.Command);hash.Add(pop?.CommandPointLocal);hash.Add(pop?.Target);
            hash.Add(ActiveSimulationBatch.Read<EntityStateController>(genes)?.Current?.id);
            if(ActiveSimulationBatch.Read<Stats.EntityStats>(genes) is Stats.EntityStats stats){hash.Add(EcologyEnergyBudget.WantsFood(genes,stats,Logic.LocomotionSettings.Load().hungerThreshold));}
            int token=hash.ToHashCode();bool lost=target!=null&&!EcologyQueries.Alive(target);
            if(_ready&&_remaining>0&&_token==token&&!lost)return false;
            _token=token;_ready=true;_remaining=SimulationOptimization.Current.decisionSeconds;Decisions++;return true;
        }
        public void Reset(){_ready=false;target=null;}
    }
}
