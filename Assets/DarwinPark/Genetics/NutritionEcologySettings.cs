using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：营养食性、寄生安全线和卵寄生容量统一配置，运行、演化与存档迁移使用同一ID。
    [Serializable] public sealed class NutritionEcologySettings
    {
        public int version,maximumEggsPerHost;
        public string nutritionGene,heterotrophyGene,scavengerGene,eggGene,parasiteState,digestState,legacyEggLogic;
        public string parasiteDrainAttribute,digestionAttribute,scavengeAttribute;
        public float scanInterval,searchRadius,attachReach,minimumHostSizeRatio,hostProtectedNutrient,feedingReach,foodThreshold,eggHostRadius,eggIncubationSeconds;
        public bool adultParasitesDigest;
        private static NutritionEcologySettings _current;
        public static NutritionEcologySettings Current
        {
            get
            {
                if(_current!=null)return _current;
                var asset=Resources.Load<TextAsset>("Genetics/nutrition_ecology");if(asset==null)throw new InvalidDataException("缺少Genetics/nutrition_ecology");
                var settings=JsonUtility.FromJson<NutritionEcologySettings>(asset.text);
                // ★ 适配：有限值和ID一起校验，避免NaN/空属性使营养转移或孕育悄悄失效。
                if(settings==null||settings.version!=1||settings.maximumEggsPerHost<1||
                    new[]{settings.scanInterval,settings.searchRadius,settings.attachReach,settings.minimumHostSizeRatio,settings.hostProtectedNutrient,settings.feedingReach,settings.foodThreshold,settings.eggHostRadius,settings.eggIncubationSeconds}.Any(v=>!float.IsFinite(v)||v<=0)||
                    new[]{settings.nutritionGene,settings.heterotrophyGene,settings.scavengerGene,settings.eggGene,settings.parasiteState,settings.digestState,settings.legacyEggLogic,settings.parasiteDrainAttribute,settings.digestionAttribute,settings.scavengeAttribute}.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("营养生态配置无效");
                return _current=settings;
            }
        }
    }
}
