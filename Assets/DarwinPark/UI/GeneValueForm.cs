using System;
using System.Collections.Generic;
using System.Globalization;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：右键属性表单把常用基因数值直接展示，完整 JSON 保留在高级编辑中。
    public sealed class GeneValueForm
    {
        private GeneDefinitionJson _definition;
        private string _name;
        private readonly List<string> _min=new List<string>(),_max=new List<string>();
        private readonly List<string> _centers=new List<string>();
        public void Load(string raw)
        {
            // ★ 适配：表单与运行库沿用同一旧字段迁移，避免编辑旧 mods 时吞掉新标签。
            _definition=JsonUtility.FromJson<GeneDefinitionJson>(EcologyGeneDefaults.Upgrade(GeneEntityStateTags.Upgrade(raw)));_name=_definition.displayName;
            _min.Clear();_max.Clear();_centers.Clear();
            foreach(var curve in _definition.attributeCurves){_min.Add(curve.minValue.ToString(CultureInfo.InvariantCulture));_max.Add(curve.maxValue.ToString(CultureInfo.InvariantCulture));}
            foreach(var state in _definition.states)_centers.Add(state.center.ToString(CultureInfo.InvariantCulture));
        }
        public void Draw()
        {
            GUILayout.Label("基因名称");_name=GUILayout.TextField(_name??"");
            // ★ 适配：可选标签由独立编辑器读取注册表生成。
            GeneEntityStateTagEditor.Draw(_definition);
            GUILayout.Label("属性范围：最小值 / 最大值");
            for(int i=0;i<_min.Count;i++)
            {
                GUILayout.Label(_definition.attributeCurves[i].attributeId);
                GUILayout.BeginHorizontal();_min[i]=GUILayout.TextField(_min[i]);_max[i]=GUILayout.TextField(_max[i]);GUILayout.EndHorizontal();
            }
            GUILayout.Label("状态中心值");
            for(int i=0;i<_centers.Count;i++){GUILayout.Label(_definition.states[i].displayName+" / "+_definition.states[i].stateId);_centers[i]=GUILayout.TextField(_centers[i]);}
        }
        public string Export()
        {
            _definition.displayName=_name;
            for(int i=0;i<_min.Count;i++){float min=Number(_min[i]),max=Number(_max[i]);if(min>max)throw new Exception("属性最小值不能大于最大值");_definition.attributeCurves[i].minValue=min;_definition.attributeCurves[i].maxValue=max;}
            for(int i=0;i<_centers.Count;i++)_definition.states[i].center=Number(_centers[i]);
            GeneEntityStateTags.Validate(_definition,EntityStateRegistry.Current);
            return JsonUtility.ToJson(_definition,true);
        }
        private static float Number(string input)
        {
            if(!float.TryParse(input,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)||float.IsNaN(value)||float.IsInfinity(value))throw new Exception("请输入有效有限数值");
            return value;
        }
    }
}
