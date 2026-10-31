using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：工具从注册表生成可选标签，扩充 JSON 状态后不必再修改表单代码。
    public static class GeneEntityStateTagEditor
    {
        public static void Draw(GeneDefinitionJson gene)
        {
            GUILayout.Label("实体状态标签（授予能力）");
            foreach(var state in EntityStateRegistry.Current.states)
            {
                if(state.id==EntityStateRegistry.Current.defaultStateId)continue;
                bool selected=gene.entityStateTags.Contains(state.id);
                bool next=GUILayout.Toggle(selected,state.displayName+" / "+state.id);
                if(next==selected)continue;
                if(next)gene.entityStateTags.Add(state.id);else gene.entityStateTags.Remove(state.id);
                gene.entityStateTagVersion=1;
            }
            GUILayout.Label("状态切换条件编辑 entity_states.json；标签不改变基因自身的状态。");
        }
    }
}
