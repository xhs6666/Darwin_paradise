using System;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    public enum PopulationRuleCategory { Source=0,Producers=1,Mobile=2 }
    // ★ 新增：70/30按实际生态能力统计，变异/工匠/王后同样计入，避免只匹配模板导致两条100%分区无限补充。
    public static class EcologyMaintenancePolicy
    {
        private static int _eligible;
        public static void ResetCounts()=>_eligible=0;
        public static PopulationRuleCategory Observe(EntityGeneController genes)
        {
            var category=EcologyEnergyBudget.IsProducer(genes)?PopulationRuleCategory.Producers:EcologyMobility.CanMove(genes)?PopulationRuleCategory.Mobile:PopulationRuleCategory.Source;
            if(category!=PopulationRuleCategory.Source)_eligible++;return category;
        }
        public static bool Matches(PopulationMaintenanceRule rule,EntityGeneController genes,PopulationRuleCategory category)
            =>rule.category==PopulationRuleCategory.Source?rule.source.Matches(genes.Genome):rule.category==category;
        public static bool Matches(PopulationMaintenanceRule rule,EntityGeneController genes)
        {
            if(rule.category==PopulationRuleCategory.Source)return rule.source.Matches(genes.Genome);
            return rule.category==PopulationRuleCategory.Producers?EcologyEnergyBudget.IsProducer(genes):EcologyMobility.CanMove(genes);
        }
        public static int Denominator(PopulationMaintenanceRule rule,int all)=>rule.category==PopulationRuleCategory.Source?all:_eligible;
        public static bool Disjoint(PopulationMaintenanceRule a,PopulationMaintenanceRule b,Func<RandomSpawnRecipe,RandomSpawnRecipe,bool> original)
        {
            if(a.category==PopulationRuleCategory.Source&&b.category==PopulationRuleCategory.Source)return original(a.source,b.source);
            return a.category!=PopulationRuleCategory.Source&&b.category!=PopulationRuleCategory.Source&&a.category!=b.category;
        }
        public static string Describe(PopulationMaintenanceRule rule)=>rule.category==PopulationRuleCategory.Source?"":(rule.category==PopulationRuleCategory.Producers?"按实际静止自养能力统计":"按实际移动能力统计")+" · 保底 "+rule.minimumCount;
        // ★ 新增：默认生产者优先投放到缺少植物的定居蚁群活动区，仍由原地形射线检查陆地，不能在空中/海底硬生成。
        public static Vector3 SpawnDirection(PopulationMaintenanceRule rule,Vector3 planetCenter)
        {
            var settings=EcologyDefaults.Current;
            if(rule?.category!=PopulationRuleCategory.Producers||settings.localProducerTarget<=0)return UnityEngine.Random.onUnitSphere;
            foreach(var queen in AntColonyMember.All)
            {
                if(queen.Role!=AntRole.Queen||queen.State.founding)continue;int count=0;
                foreach(var entity in EntityRegistry.All)if(EcologyQueries.Alive(entity)&&ColonyActivityRange.Contains(queen.Population,entity.transform.position)&&EcologyEnergyBudget.IsProducer(entity.Genes))count++;
                if(count>=settings.localProducerTarget)continue;
                var up=EcologyQueries.Up(queen.transform.position);var tangent=Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere,up).normalized;
                var point=queen.transform.position+tangent*UnityEngine.Random.Range(Mathf.Min(settings.plantSpacing,queen.Radius*.4f),queen.Radius*.8f);
                bool crowded=false;foreach(var entity in EntityRegistry.All)if(EcologyQueries.Alive(entity)&&(entity.transform.position-point).sqrMagnitude<settings.plantSpacing*settings.plantSpacing){crowded=true;break;}
                if(!crowded)return (point-planetCenter).normalized;
            }
            return UnityEngine.Random.onUnitSphere;
        }
    }
}
