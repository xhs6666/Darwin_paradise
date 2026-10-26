using System;
using UnityEngine;

namespace DarwinPark.Entities
{
    /// <summary>★ 新增：生命周期与繁殖边界集中配置，游戏日与现有日历统一为十模拟秒。</summary>
    [Serializable]
    public sealed class LifeCycleSettings
    {
        public float adultProgress=30,elderProgress=80,deathProgress=100;
        public float growthProgressScale=.2f,agingProgressScale=.1f,minimumAgingSpeed=1;
        public float secondsPerDay=10,minimumGestationDays=7,maximumGestationDays=30,baseGestationDays=30,cooldownDays=15;
        public float nutrientCost=20,mateSearchRadius=20,gestationNutrientDrain=3;
        public float stationaryBirthMinRadius=2,stationaryBirthMaxRadius=8,movingBirthRadius=2,birthHeight=1;
        private static LifeCycleSettings _cached;
        public static LifeCycleSettings Load()
        {
            if(_cached!=null)return _cached;
            var json=Resources.Load<TextAsset>("Entities/lifecycle_settings");
            if(json==null){Debug.LogError("缺少 Entities/lifecycle_settings，使用生命周期默认配置");return _cached=new LifeCycleSettings();}
            return _cached=JsonUtility.FromJson<LifeCycleSettings>(json.text);
        }
    }
}
