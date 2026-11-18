using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Genetics
{
    [Serializable]
    public class GeneInstance
    {
        public string InstanceId;
        public string DefinitionId;
        // ★ 新增：游泳/两栖演化方向独立保存，不可把方向当作实际游泳、陆行或跳跃能力。
        public string StateId;
        public float Value = 50f;
        public int Tier;
        public bool Enabled = true;
        public List<string> MissingStateIds = new();
        public string OriginInstanceId;
        public float CopyStrength = 1f;
        public Vector3 InteractionPosition;

        internal GeneInstance Snapshot()
        {
            var copy=(GeneInstance)MemberwiseClone();
            copy.MissingStateIds=MissingStateIds==null?null:new List<string>(MissingStateIds);
            return copy;
        }

        public static GeneInstance Create(string defId, float value, int tier)
        {
            return new GeneInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                DefinitionId = defId,
                StateId=GeneDatabase.Get(defId)?.DefaultStateId,
                Value = Mathf.Clamp(value, 0f, 100f),
                Tier = tier,
                InteractionPosition = UnityEngine.Random.insideUnitSphere * 50f // 简化版位置采样
            };
        }
    }
}
