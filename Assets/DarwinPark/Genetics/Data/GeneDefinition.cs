using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Genetics
{
    public enum GeneCategory { Reproduction = 0, Nutrition = 1, Locomotion = 2, Custom = 99 }
    public enum AttributeApplyMode { Add = 0, Multiply = 1, Override = 2 }

    [System.Serializable]
    public struct AttributeEffect
    {
        public string attributeId;
        public AttributeApplyMode mode;
        public float value;
    }

    [System.Serializable]
    public struct GeneSuppressionRule
    {
        public string TargetGeneId;
        public float Coefficient;
    }

    // ★ 新增：数据驱动的联动规则
    [System.Serializable]
    public struct AttributeLinkage
    {
        public string sourceAttributeId; // 来源属性 (可以是基因属性，也可以是系统虚拟属性)
        public string targetAttributeId; // 目标属性
        public string mode;              // 运算模式："Add" (加算), "Multiply" (乘算)
        public float factor;             // 影响系数
    }

    // ★ 核心修复：新增属性曲线结构体
    [System.Serializable]
    public struct AttributeCurve
    {
        public string attributeId;
        public float minValue; // 基因值为 0 时的属性
        public float maxValue; // 基因值为 100 时的属性
    }

    [System.Serializable]
    public class GeneStateDefinition
    {
        public string StateId;
        public string DisplayName;
        public float Center;
        public Color Color = Color.white;
        public int Tier;
        public string BaseGeneId;
        public string AiModuleId;
        public List<string> AllowedNextStates = new List<string>();

        public Color GetDisplayColor(float geneValue)
        {
            Color.RGBToHSV(Color, out var h, out var s, out _);
            float v = Mathf.Lerp(0.30f, 1f, Mathf.Clamp01(geneValue / 100f));
            return Color.HSVToRGB(h, s, v);
        }
    }

    public class GeneDefinition
    {
        public string Id;
        public string DisplayName;
        public GeneCategory Category;
        public string ParentGeneId;
        public string BaseGeneId;
        public bool IsBaseGene;
        public int Tier;
        public string LogicScriptId; // ★ 新增：标识要挂载的逻辑脚本
        public string AiModuleId;

        // ★ 核心：属性曲线列表
        public List<AttributeCurve> AttributeCurves = new List<AttributeCurve>();
        public List<AttributeLinkage> Linkages = new List<AttributeLinkage>(); // ★ 新增
        public List<GeneStateDefinition> States = new List<GeneStateDefinition>();
        public List<GeneSuppressionRule> SuppressionRules = new List<GeneSuppressionRule>();

        public GeneStateDefinition ResolveState(float value, ICollection<string> missingStates)
        {
            GeneStateDefinition best = null;
            float bestDist = float.MaxValue;
            foreach (var s in States)
            {
                if (missingStates != null && missingStates.Contains(s.StateId)) continue;
                float d = Mathf.Abs(s.Center - value);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }
    }
}