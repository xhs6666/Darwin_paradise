using System;
using System.Linq;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Logic
{
    // ★ 新增：复制基因保留属性叠加，但每个实体只有一个移动/繁殖行为写入者，避免重复攻击、孕育和抢方向。
    public static class GeneBehaviorAuthority
    {
        public static bool IsPrimary<T>(EntityGeneController controller,GeneInstance gene) where T:class,IGeneLogic
        {
            // ★ 修复（防Bug回归）：每个模拟步只需找最大优先级，不能为四类行为反复创建和排序相同的基因集合。
            // ★ 适配：缓存随逻辑挂载版本更新，表达事件先于挂载时也不会留下旧主行为。
            return GeneAuthorityCache.IsPrimary<T>(controller,gene);
        }
    }
}
