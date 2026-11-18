using System.Collections.Generic;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：非工匠分工也记录当前及后续任务；沿用已有实际完成条件，补给时保留原顺序。
    public static class MemberTaskSequence
    {
        public static void Record(AntColonyMember member,ColonyActivity activity)
        {
            if(EntityActivityPlan.BlocksAutomatic(member)||member.State.role==null||member.Role==AntRole.Worker||ColonyFoundingSupport.Collecting(member)||activity==ColonyActivity.Feed||member.State.plan?.feeding==true)return;
            var tasks=WorkerTaskQueue.Tasks(member);int index=tasks.Count>0&&tasks[0].kind==PlannedTaskKind.Feed?1:0;
            var kind=activity switch {ColonyActivity.Patrol=>PlannedTaskKind.Patrol,ColonyActivity.Hunt=>PlannedTaskKind.Hunt,ColonyActivity.Rest=>PlannedTaskKind.Rest,ColonyActivity.Founding=>PlannedTaskKind.Founding,ColonyActivity.Reproduce=>PlannedTaskKind.Reproduce,_=>PlannedTaskKind.Follow};
            if(index<tasks.Count&&tasks[index].kind==kind)return;
            // 完成巡逻进入休息，结束休息再开始巡逻；临时猎食结束回到巡逻队列。
            while(index<tasks.Count&&tasks[index].kind!=kind)tasks.RemoveAt(index);
            if(index==tasks.Count)tasks.Add(new PlannedEntityTask{kind=kind});
            if(member.Role!=AntRole.Soldier)return;
            if(kind==PlannedTaskKind.Hunt){Append(tasks,PlannedTaskKind.Patrol);Append(tasks,PlannedTaskKind.Rest);}
            else if(kind==PlannedTaskKind.Patrol)Append(tasks,PlannedTaskKind.Rest);
            else if(kind==PlannedTaskKind.Rest)Append(tasks,PlannedTaskKind.Patrol);
        }
        private static void Append(List<PlannedEntityTask> tasks,PlannedTaskKind kind){foreach(var task in tasks)if(task.kind==kind)return;tasks.Add(new PlannedEntityTask{kind=kind});}
    }
}
