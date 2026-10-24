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
        public float Value = 50f;
        public int Tier;
        public bool Enabled = true;
        public List<string> MissingStateIds = new();
        public string OriginInstanceId;
        public float CopyStrength = 1f;
        public Vector3 InteractionPosition;

        public static GeneInstance Create(string defId, float value, int tier)
        {
            return new GeneInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                DefinitionId = defId,
                Value = Mathf.Clamp(value, 0f, 100f),
                Tier = tier,
                InteractionPosition = UnityEngine.Random.insideUnitSphere * 50f // 简化版位置采样
            };
        }
    }
}