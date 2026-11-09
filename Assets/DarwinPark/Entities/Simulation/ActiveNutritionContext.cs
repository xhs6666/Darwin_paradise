using System.Runtime.CompilerServices;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：生产资格随表达版本解析，仍逐小步真实Feed/扣矿，不能把饱食容量与有限矿产批量近似。
    public static class ActiveNutritionContext
    {
        private sealed class Context{public int revision=-1;public GenomeEvaluation evaluation;public NutritionEcologySettings nutrition;public bool blocked;}
        private static readonly ConditionalWeakTable<EntityGeneController,Context> Cache=new();
        public static bool Blocked(EntityGeneController owner)
        {
            if(!SimulationOptimization.Current.activeComponentReuse)return EcologyQueries.Active(owner,NutritionEcologySettings.Current.heterotrophyGene)!=null||EcologyQueries.Active(owner,NutritionEcologySettings.Current.scavengerGene)!=null;
            var settings=NutritionEcologySettings.Current;var context=Cache.GetValue(owner,_=>new Context());
            if(context.revision!=owner.LogicRevision||!ReferenceEquals(context.evaluation,owner.Evaluation)||!ReferenceEquals(context.nutrition,settings))
            {context.revision=owner.LogicRevision;context.evaluation=owner.Evaluation;context.nutrition=settings;context.blocked=EcologyQueries.Active(owner,settings.heterotrophyGene)!=null||EcologyQueries.Active(owner,settings.scavengerGene)!=null;}
            return context.blocked;
        }
    }
}
