using System;
using UnityEngine;

namespace DarwinPark.Genetics.Mutation
{
    [Serializable]
    public class GeneMutationSettings
    {
        // ★ 修复（防Bug回归）：成功触发变异后的类型权重，数值 90%、复制 5%、缺失 5%，不再随机插入。
        public float ErrorWeight = 0.90f;
        public float DuplicateWeight = 0.05f;
        public float DeletionWeight = 0.05f;
        public float InsertionWeight = 0f;
        public static GeneMutationSettings CreateDefault()
        {
            var json=Resources.Load<TextAsset>("Entities/mutation_settings");
            if(json==null){Debug.LogError("缺少 Entities/mutation_settings，使用 90/5/5 默认权重");return new GeneMutationSettings();}
            return JsonUtility.FromJson<GeneMutationSettings>(json.text);
        }

        public float MaxErrorDelta = 15f;
        public float ErrorBiasPower = 3f;
        public float MaxInteractionDistance = 25f;
        public float DuplicateMinStrength = 0.5f;
        public float DuplicateMaxStrength = 1.5f;

        [Header("环境选择压力")]
        [Tooltip("压力下优先变异的环境适应基因 id")]
        public string AdaptationGeneId = "";
        [Tooltip("选择该基因进行变异的概率；0 表示关闭压力偏置")]
        [Range(0f, 1f)] public float AdaptationMutationChance = 0f;
        [Tooltip("该基因的目标值（耐寒/耐热状态中心）；<0 表示随机偏移")]
        public float AdaptationTargetValue = -1f;
    }
}
