using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics.Mutation;
using UnityEngine;
namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：阶段模式只响应首次出生/成年/老年事件；阶段位由原生命周期保存，切换规则不补抽旧阶段。
    public static class StageMutationPolicy
    {
        public static long Attempts {get;private set;}
        public static bool TryMutate(EntityGeneController genes)
        {
            var tuning=GameplayTuning.Current;
            if(!tuning.mutationRoundsEnabled||tuning.mutationMode!=1||WorldAuthority.IsReplica||WorldRuntimeGate.Loading||genes==null||genes.Genome?.Genes.Count==0||genes.GetComponent<AntColonyMember>()?.Frozen==true||genes.GetComponent<EggIncubation>()?.Incubating==true)return false;
            float chance=Mathf.Clamp01(tuning.stageMutationProbability+genes.EnvironmentMutationChance*tuning.stageEnvironmentBoost);
            if(chance<=0||Random.value>=chance)return false;
            var settings=GeneMutationSettings.CreateDefault();settings.AdaptationGeneId=genes.EnvironmentGeneId;settings.AdaptationMutationChance=genes.EnvironmentMutationChance;settings.AdaptationTargetValue=genes.EnvironmentAdaptationTarget;
            Attempts++;return GeneMutator.Mutate(genes.Genome,settings).Success;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Attempts=0;
    }
}
