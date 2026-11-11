using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics.Mutation;
using UnityEngine;
// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：阶段模式只响应首次出生/成年/老年事件；阶段位由原生命周期保存，切换规则不补抽旧阶段。
    public static class StageMutationPolicy
    {
        public static long Attempts {get;private set;}
        public static bool TryMutate(EntityGeneController genes)
        {
            var tuning=GameplayTuning.Current;
            if(!tuning.mutationRoundsEnabled||tuning.mutationMode!=1||WorldAuthority.IsReplica||WorldRuntimeGate.Loading||genes==null||genes.Genome?.Genes.Count==0||DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(genes)?.Frozen==true||DarwinPark.Core.OptionalComponent.Read<EggIncubation>(genes)?.Incubating==true)return false;
            float chance=Mathf.Clamp01(tuning.stageMutationProbability+genes.EnvironmentMutationChance*tuning.stageEnvironmentBoost);
            if(chance<=0||Random.value>=chance)return false;
            var settings=GeneMutationSettings.CreateDefault();settings.AdaptationGeneId=genes.EnvironmentGeneId;settings.AdaptationMutationChance=genes.EnvironmentMutationChance;settings.AdaptationTargetValue=genes.EnvironmentAdaptationTarget;
            Attempts++;return GeneMutator.Mutate(genes.Genome,settings).Success;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Attempts=0;
    }
}
