using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：分类具有稳定值，面板按职责筛选；新增系统不把感知、中枢和社会行为混入行动。
    public enum GeneCategory { Reproduction = 0, Nutrition = 1, Locomotion = 2, Growth=3, Adaptation=4, Perception=5, Control=6, Social=7, Custom = 99 }
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
        // ★ 修复（防Bug回归）：行动分类始终由数值决定状态；世界内嵌定义或自定义树不能重新启用错误的模式控件。
        private bool _explicitState;
        public bool ExplicitState {get=>_explicitState&&Category!=GeneCategory.Locomotion;set=>_explicitState=value;}
        public string DefaultStateId;
        public float BodyColorWeight;
        public List<PartBinding> PartBindings=new();
        public List<PartModifier> PartModifiers=new();
        public List<string> RequiresGenes=new();
        // ★ 适配：由定义配置孕育条件，不把新增基因ID写死在求值器。
        public bool SuppressionOnlyWhileGestating;
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
        // ★ 适配：标签授予实体可用状态，不替换基因自身的食性/倾向状态。
        public List<string> EntityStateTags = new List<string>();
        public List<AttributeCurve> AttributeCurves = new List<AttributeCurve>();
        public List<AttributeLinkage> Linkages = new List<AttributeLinkage>(); // ★ 新增
        public List<GeneStateDefinition> States = new List<GeneStateDefinition>();
        public List<GeneSuppressionRule> SuppressionRules = new List<GeneSuppressionRule>();

        // ★ 修复（防Bug回归）：运行求值与只读检查共用状态入口，行动基因随数值变化，禁用状态也不另猜模式。
        public GeneStateDefinition ResolveState(GeneInstance gene)=>ExplicitState?States.Find(s=>s.StateId==gene.StateId&&!gene.MissingStateIds.Contains(s.StateId)):ResolveState(gene.Value,gene.MissingStateIds);

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
