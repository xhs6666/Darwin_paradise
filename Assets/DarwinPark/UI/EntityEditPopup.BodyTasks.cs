using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.UI
{
    // ★ 新增：在原编辑弹窗空白区编辑部位及顺序任务，草稿提交后才影响实体；操作共用权限和持久化入口。
    public sealed partial class EntityEditPopup
    {
        private List<ExpressedPart> _parts=new();private int _partIndex,_editTab;private string[] _partNames=Array.Empty<string>();
        private readonly string[] _partValues=new string[9];private EntityActivityPlanSave _taskDraft;private EntityActivityKind _newTask;
        private static readonly string[] Axes={"X","Y","Z"};

        private void LoadBodyAndTasks()
        {
            _parts=MorphologyExpression.Resolve(_entity.Genes.Genome,_entity.Genes.Evaluation);_partNames=_parts.Select(p=>{var slot=MorphologyRegistry.Body(_entity.Genes.Genome.BodyPlanId).slots.Find(b=>b.id==p.slotId);return string.IsNullOrEmpty(slot.displayName)?p.slotId:slot.displayName;}).ToArray();_partIndex=0;_editTab=0;LoadPartFields();
            _taskDraft=OptionalComponent.Read<EntityActivityPlan>(_entity)?.Capture()??new EntityActivityPlanSave();
        }
        private void LoadPartFields()
        {
            if(_parts.Count==0)return;var item=_entity.Genes.Genome.BodyOverrides.Find(p=>p.slotId==_parts[_partIndex].slotId);var scale=item?.scale??Vector3.one;var position=item?.position??Vector3.zero;var rotation=item?.rotation??Vector3.zero;
            for(int i=0;i<3;i++){_partValues[i]=scale[i].ToString("0.###");_partValues[i+3]=position[i].ToString("0.###");_partValues[i+6]=rotation[i].ToString("0.###");}
        }
        public bool ApplyPart(PartOverride part,bool reset=false)
        {
            bool ok=_entity!=null&&LanEditActions.Submit(new LanCommand{kind="bodyPart",entity=EcologyIdentity.Of(_entity),bodyPart=part,enabled=!reset});
            _status=ok?(WorldAuthority.IsReplica?"部位修改已提交":"部位已更新"):"无法修改：检查数值范围、权限及固定分工锁定";return ok;
        }
        public bool ApplyTasks(EntityActivityPlanSave plan)
        {
            bool ok=_entity!=null&&LanEditActions.Submit(new LanCommand{kind="activityPlan",entity=EcologyIdentity.Of(_entity),activityPlan=plan});
            _status=ok?(WorldAuthority.IsReplica?"任务已提交":"任务顺序已应用"):"任务无效或没有编辑权限";return ok;
        }
        private void VectorFields(string label,int start)
        {
            GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(80));for(int i=0;i<3;i++){GUILayout.Label(Axes[i],GUILayout.Width(14));_partValues[start+i]=GUILayout.TextField(_partValues[start+i],20,GUILayout.MinWidth(35));}GUILayout.EndHorizontal();
        }
        private void DrawBodyAndTasks()
        {
            // ★ 新增：部位和任务分标签展示，保留原窗口尺寸也能立即看到添加及应用按钮。
            GUILayout.Space(10);_editTab=GUILayout.Toolbar(_editTab,new[]{"实体部位","任务列表"});GUILayout.Space(6);
            bool previous=GUI.enabled;GUI.enabled=previous&&WorldAuthority.CanEdit;
            if(_editTab==0&&_parts.Count>0)
            {
                GUILayout.BeginHorizontal();if(GUILayout.Button("‹",GUILayout.Width(32))){_partIndex=(_partIndex+_parts.Count-1)%_parts.Count;LoadPartFields();}GUILayout.Label(_partNames[_partIndex]+" · "+(_partIndex+1)+"/"+_parts.Count);if(GUILayout.Button("›",GUILayout.Width(32))){_partIndex=(_partIndex+1)%_parts.Count;LoadPartFields();}GUILayout.EndHorizontal();
                VectorFields("形状倍率",0);VectorFields("身体偏移",3);VectorFields("旋转角度",6);
                GUILayout.Label("部位受基因尺寸范围约束；修改父部位后子部位跟随。",RuntimeMenuTheme.Muted);
                GUILayout.BeginHorizontal();if(GUILayout.Button("应用此部位"))
                {
                    var values=new float[9];bool valid=true;for(int i=0;i<9;i++)valid&=float.TryParse(_partValues[i],out values[i])&&float.IsFinite(values[i]);
                    if(valid)ApplyPart(new PartOverride{slotId=_parts[_partIndex].slotId,scale=new Vector3(values[0],values[1],values[2]),position=new Vector3(values[3],values[4],values[5]),rotation=new Vector3(values[6],values[7],values[8])});else _status="请输入有限的数值";
                }
                if(GUILayout.Button("恢复基因形态")){if(ApplyPart(new PartOverride{slotId=_parts[_partIndex].slotId},true))LoadPartFields();}GUILayout.EndHorizontal();
            }
            if(_editTab==1)
            {
            GUILayout.Label("实体任务列表",RuntimeMenuTheme.Title);
            var runtime=OptionalComponent.Read<EntityActivityPlan>(_entity);GUILayout.Label(runtime?.Status??"当前由基因及种群自动安排",RuntimeMenuTheme.Muted);
            _taskDraft.enabled=GUILayout.Toggle(_taskDraft.enabled,"执行此任务列表（关闭则恢复自动行为）");
            for(int i=0;i<_taskDraft.tasks.Count;i++)
            {
                var task=_taskDraft.tasks[i];GUILayout.BeginHorizontal();GUILayout.Label((i+1)+". "+EntityActivityPlan.Name(task.kind),GUILayout.Width(100));
                var limitation=EntityActivityPlan.Limitation(_entity,task.kind);GUILayout.Label(string.IsNullOrEmpty(limitation)?"完成后继续下一项":limitation,RuntimeMenuTheme.Muted);
                int action=0;if(GUILayout.Button("↑",GUILayout.Width(28)))action=-1;if(GUILayout.Button("↓",GUILayout.Width(28)))action=1;bool remove=GUILayout.Button("×",GUILayout.Width(28));GUILayout.EndHorizontal();
                if(remove){_taskDraft.tasks.RemoveAt(i);break;}if(action!=0&&i+action>=0&&i+action<_taskDraft.tasks.Count){var other=_taskDraft.tasks[i+action];_taskDraft.tasks[i+action]=task;_taskDraft.tasks[i]=other;break;}
            }
            _newTask=(EntityActivityKind)GUILayout.SelectionGrid((int)_newTask,Enum.GetValues(typeof(EntityActivityKind)).Cast<EntityActivityKind>().Select(EntityActivityPlan.Name).ToArray(),4);
            GUILayout.BeginHorizontal();bool enabled=GUI.enabled;GUI.enabled=enabled&&_taskDraft.tasks.Count<SimulationOptimization.Current.workerQueueLimit;
            if(GUILayout.Button("添加任务"))_taskDraft.tasks.Add(new EntityActivityTask{kind=_newTask});GUI.enabled=enabled;
            if(GUILayout.Button("清空草稿"))_taskDraft.tasks.Clear();if(GUILayout.Button("应用任务列表"))ApplyTasks(_taskDraft);GUILayout.EndHorizontal();
            GUILayout.Label("按顺序执行；目标自动寻找。运输需要成年工匠，编辑不会赠送能力。",RuntimeMenuTheme.Muted);
            }
            GUI.enabled=previous;
        }
    }
}
