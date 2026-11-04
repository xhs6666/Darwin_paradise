using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：所有真正死亡统一结算一次；清场、读档、工匠整株转化不是死亡，不在OnDestroy里制造尸体。
    public sealed class EntityDeathSettlement:MonoBehaviour
    {
        private bool _settled;
        public static ColonyResource Die(DarwinEntity victim,DarwinEntity attacker=null)
        {
            if(victim==null||WorldAuthority.IsReplica)return null;
            var marker=victim.GetComponent<EntityDeathSettlement>()??victim.gameObject.AddComponent<EntityDeathSettlement>();
            if(marker._settled)return null;marker._settled=true;
            float remaining=ColonyResource.Biomass(victim);remaining-=NutritionTransfer.Digest(attacker,remaining);
            var member=attacker!=null?attacker.GetComponent<AntColonyMember>():null;
            string group=member!=null&&member.Active&&member.Role==AntRole.Soldier?member.GroupId:"";
            ColonyResource corpse=null;if(remaining>0){corpse=ColonyResource.LeaveCorpse(victim,group);corpse.State.energy=remaining;}
            victim.GetComponent<NutritionParasite>()?.Detach();victim.GetComponent<EntityManualControl>()?.Release();
            victim.gameObject.SetActive(false);Destroy(victim.gameObject);return corpse;
        }
    }
}
