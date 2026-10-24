using System;
using UnityEngine;

namespace DarwinPark.Entities.Logic
{
    /// <summary>★ 新增：捕猎/漫游阈值独立配置，避免 AI 内容参数散落逻辑代码。</summary>
    [Serializable]
    public sealed class LocomotionSettings
    {
        public float hungerThreshold = 8, attackReach = .5f, attackInterval = 1;
        public float grazeDamage = 20, biteNutrition = .1f, killNutrition = 10;
        public float wanderInterval = 6, wanderDistance = 20, wanderSpeedRatio = .4f, steering = 12;
        private static LocomotionSettings _cached;
        public static LocomotionSettings Load()
        {
            if (_cached != null) return _cached;
            var json = Resources.Load<TextAsset>("Entities/locomotion_settings");
            if (json == null) { Debug.LogError("缺少 Entities/locomotion_settings，行动行为使用默认配置"); return _cached = new LocomotionSettings(); }
            return _cached = JsonUtility.FromJson<LocomotionSettings>(json.text);
        }
    }
}
