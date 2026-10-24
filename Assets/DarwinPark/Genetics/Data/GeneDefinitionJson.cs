using System;
using System.Collections.Generic;

namespace DarwinPark.Genetics
{
    [Serializable]
    public class GeneDefinitionJson
    {
        public string id;
        public string displayName;
        public string category;
        public string parentGeneId = "";
        public string baseGeneId = "";
        public bool isBaseGene;
        public int tier;
        public string logicScriptId = ""; // 基因逻辑脚本
        public string aiModuleId = "";

        // ★ 核心修复：添加属性曲线字段
        public List<AttributeCurveJson> attributeCurves = new List<AttributeCurveJson>();
        public List<AttributeLinkage> attributeLinkages = new List<AttributeLinkage>(); // ★ 新增
        public List<GeneStateJson> states = new List<GeneStateJson>();
        public List<GeneSuppressionJson> suppressionRules = new List<GeneSuppressionJson>();
    }

    // ★ 新增：属性曲线 JSON 结构
    [Serializable]
    public class AttributeCurveJson
    {
        public string attributeId;
        public float minValue;
        public float maxValue;
    }

    [Serializable]
    public class GeneStateJson
    {
        public string stateId;
        public string displayName;
        public float center;
        public float[] color;
        public int tier;
        public string baseGeneId = "";
        public string aiModuleId = "";
        // 之前的 AttributeEffects 可以保留，也可以删掉，因为现在用曲线了，但留着不影响
        public List<AttributeEffect> attributeEffects = new List<AttributeEffect>();
        public List<string> allowedNextStates = new List<string>();
    }

    [Serializable]
    public class GeneSuppressionJson
    {
        public string targetGeneId;
        public float coefficient = 1000f;
    }



}