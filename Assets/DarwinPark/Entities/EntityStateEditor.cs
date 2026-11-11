using System;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using UnityEngine;
// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities
{
    // ★ 新增：显式编辑阶段不抽新变异、不重置孕育或身份；修改与存档仍使用同一寿命进度和真实数值。
    public static class EntityStateEditor
    {
        public static bool SetStage(DarwinEntity entity,AgeState age)
        {
            if(!WorldAuthority.CanEdit||WorldAuthority.IsReplica||!EcologyQueries.Alive(entity)||!Enum.IsDefined(typeof(AgeState),age))return false;
            var life=entity.GetComponent<EntityLifeCycle>();if(life==null)return false;var settings=LifeCycleSettings.Load();
            // ★ 修复（防Bug回归）：退回幼年会停止运输；必须先释放旧货物和领取，不能把任务永久锁在幼体身上。
            if(age==AgeState.Infant){var member=DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(entity);if(member!=null)AntColonyWorker.Release(member);}
            life.RestoreProgress(age==AgeState.Infant?0:age==AgeState.Adult?settings.adultProgress:settings.elderProgress);
            entity.Genes.CurrentAge=age;entity.Genes.Recalculate();return true;
        }
        public static bool SetStat(DarwinEntity entity,string id,float value)
        {
            if(!WorldAuthority.CanEdit||WorldAuthority.IsReplica||!EcologyQueries.Alive(entity)||!float.IsFinite(value)||value<0||id!="health"&&id!="nutrient"&&id!="storedNutrient")return false;
            var stats=entity.GetComponent<EntityStats>();if(stats==null||!stats.TryGet(id,out float old)||value>old+stats.CapacityRemaining(id))return false;
            stats.Modify(id,value-old,false);DarwinPark.Entities.Simulation.EntityWorkScheduler.Wake(entity.Genes);
            if(id=="health"&&value<=0)EntityDeathSettlement.Die(entity);return true;
        }
    }
}
