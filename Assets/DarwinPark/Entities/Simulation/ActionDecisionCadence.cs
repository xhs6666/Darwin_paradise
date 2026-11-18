using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.Entities.Simulation
{
    // ★ 适配：普通目标按游戏秒更新；受击、命令和状态切换可中断等待，不在每个小步重建整份任务签名。
    public sealed class ActionDecisionCadence
    {
        private float _remaining;
        private bool _ready, _orderActive;
        private DarwinEntity _retaliation;
        private RtsOrderSave _order;
        private int _revision;
        private string _stateId;
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
            var order = rts?.State;
            bool orderActive = order?.active == true;
            var stateId = ActiveSimulationBatch.Read<EntityStateController>(genes)?.Current?.id;
            bool lost=!ReferenceEquals(target,null)&&(target==null||!EcologyQueries.Alive(target));
            bool immediate=!_ready||lost||combat?.Target!=_retaliation||order!=_order||
                orderActive!=_orderActive||genes.LogicRevision!=_revision||stateId!=_stateId||
                manualCommand!=_manualCommand||manualPoint!=_manualPoint||manualTarget!=_manualTarget;
            if(!immediate&&(_remaining>0||!RoutineTurn(genes)))return false;
            _manualCommand=manualCommand;_manualPoint=manualPoint;_manualTarget=manualTarget;
            _retaliation=combat?.Target;
            _order=order;
            _orderActive=orderActive;
            _revision=genes.LogicRevision;
            _stateId=stateId;
            _ready=true;_remaining=SimulationOptimization.Current.decisionSeconds;Decisions++;return true;
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

        public void Reset(){_ready=false;target=null;}
    }
}
