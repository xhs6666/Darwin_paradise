using System.Linq;
using DarwinPark.Core;
using UnityEngine;

// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：首批工匠之后优先培养迁出繁殖者，并保留有限出生名额；仍尊重真正的世界总上限。
    public static class ColonyContinuityPriority
    {
        private static bool Enabled=>WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading&&!WorldAuthority.IsReplica&&EcologyDefaults.Current.enableFoundingSupport;
        public static bool NeedsSuccessor(AntColonyMember queen)
        {
            if(!Enabled||queen.Role!=AntRole.Queen)return false;
            var group=queen.Group.ToArray();
            return group.Length>=GameplayTuning.Current.colonySuccessorPopulation&&group.Count(m=>m.Role==AntRole.Worker)>=EcologyDefaults.Current.minimumWorkers&&group.Count(m=>m.Role==AntRole.Reproductive)<AntColonySettings.Current.minimumReproductives;
        }
        public static bool CanSpawn(EntityGeneController parent=null)
        {
            if(!EntityPopulationLimit.CanSpawn)return false;
            if(!Enabled||EntityPopulationLimit.Maximum==0)return true;
            int reserved=Mathf.Min(Mathf.FloorToInt(GameplayTuning.Current.colonyBirthReservedSlots),Mathf.Max(0,EntityPopulationLimit.Maximum-1));
            if(EntityPopulationLimit.Maximum-EntityRegistry.All.Count>reserved)return true;
            var queen=parent!=null?DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(parent):null;
            if(queen!=null&&queen.Active&&queen.Role==AntRole.Queen)return true;
            // ★ 修复（防Bug回归）：自动维持和光合繁殖不能抢光全部名额，让已完成孕育的王后无限等待至死。
            return !AntColonyMember.All.Any(m=>m.Role==AntRole.Queen&&!m.Young);
        }
    }
}
