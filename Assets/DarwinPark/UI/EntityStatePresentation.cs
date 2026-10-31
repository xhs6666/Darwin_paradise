using System.Linq;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;

namespace DarwinPark.UI
{
    // ★ 新增：运行活动状态与基因倾向分别展示，注册表名称始终是唯一文案来源。
    public static class EntityStatePresentation
    {
        public static string Current(DarwinEntity entity)
        {
            // ★ 适配：蚁群优先显示实际分工活动，避免工匠运输仍被标为普通巡逻。
            if(ColonyStatusPresentation.TryCurrent(entity,out string colony))return colony;
            var controller=entity.GetComponent<EntityStateController>();
            return controller!=null?controller.Current.displayName:EntityStateRegistry.Current.Get(EntityStateRegistry.Current.defaultStateId).displayName;
        }
        public static string GeneTags(GeneDefinition gene)
        {
            if(gene.EntityStateTags.Count==0)return "";
            return "实体状态标签："+string.Join(" / ",gene.EntityStateTags.Select(id=>EntityStateRegistry.Current.Get(id).displayName));
        }
    }
}
