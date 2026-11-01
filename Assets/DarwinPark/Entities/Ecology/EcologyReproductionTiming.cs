using DarwinPark.Entities.Stats;
using UnityEngine;
using System.Collections.Generic;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：使用基因相互促进/抑制后的孵化属性计时，遗传原值不再绕过表达结果。
    public static class EcologyReproductionTiming
    {
        private static readonly List<DarwinEntity> Neighbors=new List<DarwinEntity>();
        public static float GestationSeconds(EntityGeneController genes,float size)
        {
            var settings=EcologyBalanceSettings.Current;var life=LifeCycleSettings.Load();
            float days=EcologyEnergyBudget.IsProducer(genes)?settings.producerGestationDays:settings.consumerGestationDays;
            float speed=Mathf.Max(settings.minimumReproductionSpeed,genes.GetAttribute(settings.reproductionSpeedAttribute));
            days*=settings.referenceReproductionSpeed/speed*size;
            if(!EcologyEnergyBudget.IsProducer(genes))days*=FoodPressure(genes);
            float efficiency=genes.GetAttribute(settings.gestationEfficiencyAttribute);if(efficiency>0)days/=efficiency;
            return Mathf.Clamp(days,life.minimumGestationDays,life.maximumGestationDays)*life.secondsPerDay;
        }
        public static float CooldownSeconds(EntityGeneController genes)
        {var settings=EcologyBalanceSettings.Current;return (EcologyEnergyBudget.IsProducer(genes)?settings.producerCooldownDays:settings.consumerCooldownDays)*LifeCycleSettings.Load().secondsPerDay;}
        // ★ 新增：附近可食生产者不足时减慢孕育，避免已有储粮在食物衰退期间继续造成数量过冲；不直接增删实体。
        public static float FoodPressure(EntityGeneController genes)
        {
            var settings=EcologyBalanceSettings.Current;int producers=0,consumers=0;Vector3 position=genes.transform.position;
            // ★ 适配：食物压力只检查半径内共享快照，保留食性、体型和生产者判定。
            EntityNeighborhood.Query(position,settings.consumerFoodCompetitionRadius,Neighbors);
            foreach(var entity in Neighbors)
            {
                if(entity==null||(entity.transform.position-position).sqrMagnitude>settings.consumerFoodCompetitionRadius*settings.consumerFoodCompetitionRadius||!EcologyQueries.Alive(entity))continue;
                if(EcologyEnergyBudget.IsProducer(entity.Genes))
                {if(PredationSizePolicy.Allows(genes,entity))producers++;}
                else if(entity.Genes.GetAttribute(settings.movementAttribute)>0)consumers++;
            }
            // ★ 修复（防Bug回归）：临时查询结果用后清空引用，退出有消费者的世界后不保留旧实体。
            Neighbors.Clear();
            float pressure=consumers/(Mathf.Max(1,producers)*settings.consumersPerProducer);
            return 1+Mathf.Max(0,pressure-1)*settings.consumerFoodPressureStrength;
        }
    }
}
