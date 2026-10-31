using System;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class EcologySettings
    {
        public string territoryGene,populationGene,parasiticGene,oviparousGene;
        public string antState,wolfState,externalState,internalState;
        public float patrolInterval,settleDelay,fleeDistance,fleeSpeedMultiplier;
        public float joinRadius,scanInterval,commandLifetime,duelCooldown,duelHealthWeight,duelAdvantage;
        public float arrivalDistance,formationSpacing,antBodyMultiplier,antGatherRate,antGatherBonus,antSpeedBonus,wolfAttackBonus,shareFraction;
        public int maximumCoordinators;
        public int oreSearchSamples;
        public float hostSearchRadius,attachReach,minimumHostSizeRatio,parasiteSeekSpeed,hostSearchInterval;
        public float incubationSeconds,maximumCommandRange;
        private static EcologySettings _cached;
        // ★ 新增：行为半径、周期与战斗收益集中配置，统一使用模拟秒。
        public static EcologySettings Load()
        {
            if(_cached!=null)return _cached;
            var asset=Resources.Load<TextAsset>("Entities/social_ecology");
            if(asset==null)throw new InvalidOperationException("缺少 Entities/social_ecology");
            return _cached=JsonUtility.FromJson<EcologySettings>(asset.text);
        }
    }
}
