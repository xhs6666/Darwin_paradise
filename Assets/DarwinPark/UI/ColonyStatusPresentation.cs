using System.Linq;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;

namespace DarwinPark.UI
{
    // ★ 新增：分工状态与普通基因状态分开呈现，显示与战斗识别一致的种群标记。
    public static class ColonyStatusPresentation
    {
        public static bool TryCurrent(DarwinEntity entity,out string text)
        {
            text=null;var member=entity.GetComponent<AntColonyMember>();if(member==null||!member.Active||member.Role==AntRole.None)return false;
            string activity=member.Activity switch {ColonyActivity.Patrol=>"巡逻",ColonyActivity.Hunt=>"捕猎",ColonyActivity.Collect=>"收集",ColonyActivity.Transport=>"运输",ColonyActivity.Feed=>"前往补给",ColonyActivity.Reproduce=>entity.Genes.IsGestating?"孕育":"准备繁殖",ColonyActivity.Founding=>"建立新种群",ColonyActivity.Young=>"育幼",ColonyActivity.Mate=>"陪伴王后",_=>"休息"};
            // ★ 适配：活动状态在前、固定分工附在括号中，与普通实体状态保持同一阅读顺序。
            if(member.Activity==ColonyActivity.Rest)activity=DarwinPark.Genetics.EntityStateRegistry.Current.Get(DarwinPark.Genetics.EntityStateRegistry.Current.defaultStateId).displayName;
            text=activity+"（"+AntColonySettings.Current.Rule(member.Role).label+"）";return true;
        }
        public static string Summary(DarwinEntity entity)
        {
            var member=entity.GetComponent<AntColonyMember>();if(member==null||!member.Active||member.Role==AntRole.None)return "";
            string marker=member.GroupId.Length>=6?member.GroupId.Substring(0,6):member.GroupId;
            TryCurrent(entity,out string current);
            return "蚁群 "+marker+" · "+member.Group.Count()+" 成员\n"+current+"\n士兵 "+member.Group.Count(m=>m.Role==AntRole.Soldier)+"  工匠 "+member.Group.Count(m=>m.Role==AntRole.Worker)+"  繁殖 "+member.Group.Count(m=>m.Role==AntRole.Reproductive);
        }
    }
}
