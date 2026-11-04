using System;
using DarwinPark.Entities.Stats;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：真实攻击显式携带攻击者，饥饿/气候扣血不误触发领地驱逐；收益使用实际伤害。
    [RequireComponent(typeof(EntityStats))]
    public sealed class EntityCombat : MonoBehaviour
    {
        public event Action<DarwinEntity,float> Attacked;
        public static EntityCombat Ensure(GameObject obj)=>obj.GetComponent<EntityCombat>()??obj.AddComponent<EntityCombat>();
        public float Receive(DarwinEntity attacker,float damage)
        {
            // ★ 适配：最终入口统一同群与角色限制，手动攻击和旧目标缓存不能绕过。
            if(!AntColonyCombat.CanAttack(attacker,GetComponent<DarwinEntity>()))return 0;
            if(GetComponent<EntityGeneController>()!=null&&GetComponent<CombatIntent>()==null)gameObject.AddComponent<CombatIntent>();
            var stats=GetComponent<EntityStats>();if(damage<=0||!stats.TryGet("health",out float hp)||hp<=0)return 0;
            float defense=GetComponent<TerritoryBehavior>()?.Defense??0;
            if(GetComponent<EggIncubation>()?.Incubating==true)defense+=GetComponent<EntityGeneController>().GetAttribute("eggDefense");
            float actual=Mathf.Min(hp,damage/(1+Mathf.Max(0,defense)/100));
            stats.Modify("health",-actual);if(attacker!=null)Attacked?.Invoke(attacker,actual);
            // ★ 适配：致死攻击先统一消化/尸体结算，其他调用者不再复制击杀营养奖励。
            if(actual>=hp)EntityDeathSettlement.Die(GetComponent<DarwinEntity>(),attacker);
            return actual;
        }
    }
}
