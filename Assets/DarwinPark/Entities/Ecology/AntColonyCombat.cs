using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：最终伤害入口执行同群识别和分工限制，玩家攻击也不能绕过工匠/王后的零战斗能力。
    public static class AntColonyCombat
    {
        public static bool CanAttack(DarwinEntity attacker,DarwinEntity target)
        {
            if(attacker==null||target==null)return true;
            if(attacker.GetComponent<PopulationBehavior>()?.SameGroup(target)==true)return false;
            var member=attacker.GetComponent<AntColonyMember>();
            return member==null||!member.Active||member.Role==AntRole.None||member.Role==AntRole.Soldier&&!member.Young;
        }
        public static bool SettleSoldierAttack(EntityGeneController genes,DarwinEntity target,bool killed)
        {
            var member=genes.GetComponent<AntColonyMember>();if(member==null||!member.Active||member.Role!=AntRole.Soldier)return false;
            if(killed){ColonyResource.LeaveCorpse(target,member.GroupId);member.AfterKill();}
            return true;
        }
    }
}
