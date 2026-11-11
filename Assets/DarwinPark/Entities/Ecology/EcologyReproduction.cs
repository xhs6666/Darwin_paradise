using System.Linq;
using DarwinPark.Genetics;
using UnityEngine;
// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Ecology
{
    public static class EcologyReproduction
    {
        // ★ 新增：卵生后代先作为可攻击的独立卵存在，孵化前不进入普通生命周期。
        public static bool Spawn(EntityGeneController parent,Genome genome,Vector3 position)
        {
            // ★ 适配：保留有限建群出生名额，实际出生仍受全局总上限约束。
            if(!ColonyContinuityPriority.CanSpawn(parent))return false;
            // ★ 适配：卵寄生将亲本已组合的后代基因放入外来孕育载荷，不读取或修改宿主的基因。
            if(EcologyQueries.Active(parent,NutritionEcologySettings.Current.eggGene)!=null)return EggHostPregnancy.TryDeposit(parent,genome);
            // ★ 适配：出生上限与投放共用，拒绝时保留母体孕育。
            if(!EntityPopulationLimit.CanSpawn)return false;
            // ★ 适配：区分实际创建与全表寻找新生者，避免将出生尖峰误归因于孕育计时。
            DarwinEntity child;
            using(DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.BirthFactory))child=EntitySpawnCapture.Create(genome,position);
            if(child==null)return false;
            // ★ 适配：出生立即固定分工与种群标记，早于幼年变异和任何 AI 帧。
            AntColonyMember.OnBirth(parent,child);
            // ★ 修复（防Bug回归）：先确立孵化状态，不能让尚未孵化的狼群卵被统计为已独立建群。
            if(EcologyQueries.Active(parent,EcologySettings.Load().oviparousGene)!=null)
            {var egg=DarwinPark.Core.OptionalComponent.Read<EggIncubation>(child)??child.gameObject.AddComponent<EggIncubation>();egg.Begin(EcologySettings.Load().incubationSeconds/Mathf.Max(.01f,parent.GetAttribute("hatchSpeed")));}
            // ★ 新增：非蚁群后代若实际在外独立成群，保留来源；同群出生或加入既有群不产生虚构分支。
            var source=DarwinPark.Core.OptionalComponent.Read<PopulationBehavior>(parent);var population=DarwinPark.Core.OptionalComponent.Read<PopulationBehavior>(child);
            if(source!=null&&source.Active&&population!=null&&population.Active&&population.Mode!=EcologySettings.Load().antState)
            {population.EnsureMembership();if(population.LeaderFlag&&population.GroupId!=source.GroupId)PopulationArchive.RecordFounding(source.GroupId,population.GroupId,population.Mode,child.DisplayName,"后代独立形成新种群");}
            return true;
        }
    }
}
