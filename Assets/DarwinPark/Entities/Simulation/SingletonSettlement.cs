using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using UnityEngine;
namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：跳过孤立静止实体的空被动调用，生命/繁殖保持逐小步，环境/代谢保持原批次顺序。
    public static class SingletonSettlement
    {
        private sealed class Context
        {
            public int revision=-1;public bool supported;public EntityLifeCycle life;public CombatIntent combat;public EntityStateController state;
            public readonly List<IGeneLogic> logics=new(),useful=new();
            public void Refresh(EntityGeneController genes)
            {
                if(revision==genes.LogicRevision)return;revision=genes.LogicRevision;supported=StationaryCohortKernel.Supported(genes);state=genes.GetComponent<EntityStateController>();
                genes.CopySimulationLogics(logics);useful.Clear();foreach(var logic in logics)if(!supported||logic is NutritionGeneLogic||logic is ReproductionGeneLogic)useful.Add(logic);
            }
        }
        private static readonly Dictionary<EntityGeneController,Context> Contexts=new();
        public static void Advance(EntityGeneController genes,float elapsed)
        {
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.SingletonSettlement);
            if(!SimulationOptimization.Current.singletonKernel){genes.AdvanceSimulation(elapsed);if(genes!=null&&genes.isActiveAndEnabled)StationaryCohortKernel.AdvanceCore(genes,elapsed);return;}
            if(!Contexts.TryGetValue(genes,out var context)){context=new Context{life=genes.GetComponent<EntityLifeCycle>(),combat=genes.GetComponent<CombatIntent>()};Contexts.Add(genes,context);}
            context.Refresh(genes);int steps=SimulationSubsteps.Count(elapsed);
            for(int i=0;i<steps;i++)
            {
                if(genes==null||!genes.isActiveAndEnabled||context.life?.HasDied==true)return;
                float dt=SimulationSubsteps.Slice(elapsed,i,steps);context.life?.Advance(dt);if(context.life?.HasDied==true)return;
                context.combat?.Advance(dt);context.Refresh(genes);context.state?.Advance(dt);
                // 重算只使下一子步更新快照，不修改正在遍历的行为集合，保留原调用顺序。
                foreach(var logic in context.useful)logic.OnUpdate(dt);
            }
            // ★ 修复（防Bug回归）：先完成整批生产再按原入口结算环境/代谢。逐步交错会改变容量溢出，导致与原孤立路径营养不守恒。
            if(genes!=null&&genes.isActiveAndEnabled)StationaryCohortKernel.AdvanceCore(genes,elapsed);
        }
        public static void Remove(EntityGeneController genes){if(!ReferenceEquals(genes,null))Contexts.Remove(genes);}
        public static void Clear()=>Contexts.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
