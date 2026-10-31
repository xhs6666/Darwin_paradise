using System;
using System.IO;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：独立营养利用规则不复用光合作用/蚁群的属性数值，支持 mods 与明确的配置校验。
    [Serializable] public sealed class NutrientUtilizationSettings
    {
        public int version;
        public string geneId,nutrientAttribute,reserveAttribute,absorptionAttribute,releaseAttribute;
        public float feedThreshold,feedRadius,searchRadius,releaseDistance,minimumFoodAfterRelease;
        private static NutrientUtilizationSettings _current;
        public static NutrientUtilizationSettings Current
        {
            get
            {
                if(_current!=null)return _current;
                string path=Path.Combine(GamePaths.Mods,"Default","nutrient_utilization.json");
                var asset=Resources.Load<TextAsset>("Entities/nutrient_utilization");
                if(!File.Exists(path)&&asset==null)throw new InvalidDataException("缺少 Entities/nutrient_utilization");
                var data=JsonUtility.FromJson<NutrientUtilizationSettings>(File.Exists(path)?File.ReadAllText(path):asset.text);
                if(data==null||data.version!=1||string.IsNullOrWhiteSpace(data.geneId)||string.IsNullOrWhiteSpace(data.nutrientAttribute)||string.IsNullOrWhiteSpace(data.reserveAttribute)||string.IsNullOrWhiteSpace(data.absorptionAttribute)||string.IsNullOrWhiteSpace(data.releaseAttribute))throw new InvalidDataException("营养利用配置字段无效");
                foreach(float value in new[]{data.feedThreshold,data.feedRadius,data.searchRadius,data.releaseDistance,data.minimumFoodAfterRelease})
                    if(float.IsNaN(value)||float.IsInfinity(value)||value<0)throw new InvalidDataException("营养利用参数必须为非负有限值");
                if(data.feedRadius<=0||data.searchRadius<data.feedRadius||data.releaseDistance<=data.feedRadius)throw new InvalidDataException("营养利用搜索或吐出距离无效");
                return _current=data;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>_current=null;
    }
}
