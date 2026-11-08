using System;
using System.Collections.Generic;
using System.IO;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    public enum PlannedTaskKind { CollectCorpse,CollectLiving,Deliver,Feed,Patrol,Rest,Hunt,Founding,Reproduce,Follow }
    [Serializable]public sealed class PlannedEntityTask {public PlannedTaskKind kind;public string target;}
    // ★ 新增：只领取当前任务，后续目标保存在顺序计划中；补给插队不丢原任务和货物。
    public static class WorkerTaskQueue
    {
        private static readonly List<ResourceCoordinate> Resources=new();private static readonly List<EntityCoordinate> Entities=new();private static readonly List<ActivityCircle> Circles=new();
        private static readonly HashSet<string> Included=new();
        public static List<PlannedEntityTask> Tasks(AntColonyMember worker)=>(worker.State.plan??=new EntityTaskPlanSave()).tasks??=new();
        public static void Feeding(AntColonyMember worker,bool feeding)
        {
            var tasks=Tasks(worker);for(int i=tasks.Count-1;i>=0;i--)if(tasks[i].kind==PlannedTaskKind.Feed)tasks.RemoveAt(i);
            if(feeding)tasks.Insert(0,new PlannedEntityTask{kind=PlannedTaskKind.Feed});
        }
        private static int WorkIndex(List<PlannedEntityTask> tasks)=>tasks.Count>0&&tasks[0].kind==PlannedTaskKind.Feed?1:0;
        public static void Complete(AntColonyMember worker)
        {var tasks=Tasks(worker);int i=WorkIndex(tasks);if(i<tasks.Count)tasks.RemoveAt(i);}
        public static void PickedUp(AntColonyMember worker)
        {var tasks=Tasks(worker);int i=WorkIndex(tasks);if(i<tasks.Count)tasks[i].kind=PlannedTaskKind.Deliver;}
        public static void Select(AntColonyMember worker)
        {
            // 关闭多项排队时仍执行一个当前任务，便于保持可比较的旧工作语义。
            var state=worker.State;var tasks=Tasks(worker);int current=WorkIndex(tasks);
            // ★ 修复（防Bug回归）：Unity把旧null字符串读成空串；不能用??选目标。明确命令替换当前目标时插入首位，保留后续计划。
            string selected=!string.IsNullOrEmpty(state.resourceTarget)?state.resourceTarget:state.livingTarget;
            if(!string.IsNullOrEmpty(selected)&&(current==tasks.Count||tasks[current].target!=selected)&&string.IsNullOrEmpty(state.carriedResource)&&string.IsNullOrEmpty(state.carriedLiving))
            {
                for(int i=tasks.Count-1;i>=current;i--)if(tasks[i].target==selected)tasks.RemoveAt(i);
                tasks.Insert(current,new PlannedEntityTask{kind=!string.IsNullOrEmpty(state.resourceTarget)?PlannedTaskKind.CollectCorpse:PlannedTaskKind.CollectLiving,target=selected});
                int maximum=(SimulationOptimization.Current.taskQueue?SimulationOptimization.Current.workerQueueLimit:1)+current;while(tasks.Count>maximum)tasks.RemoveAt(tasks.Count-1);
            }
            while(current<tasks.Count)
            {
                var task=tasks[current];
                if(task.kind==PlannedTaskKind.Deliver&&(!string.IsNullOrEmpty(state.carriedResource)||!string.IsNullOrEmpty(state.carriedLiving)))return;
                var resource=task.kind==PlannedTaskKind.CollectCorpse?ColonyResource.Find(task.target):null;var entity=task.kind==PlannedTaskKind.CollectLiving?EcologyIdentity.Find(task.target):null;
                if(resource!=null&&ColonyCorpseAccess.Available(resource,worker)&&ColonyActivityRange.Contains(worker.Population,resource.transform.position)&&resource.Claim(worker)){state.resourceTarget=task.target;state.livingTarget=null;return;}
                if(entity!=null&&AntColonyWorker.Valid(entity,worker)&&!AntColonyWorker.Claimed(entity,worker)){state.livingTarget=task.target;state.resourceTarget=null;return;}
                tasks.RemoveAt(current);state.resourceTarget=state.livingTarget=null;
            }
            if(state.scan>0)return;state.scan=AntColonySettings.Current.scanInterval;
            Circles.Clear();foreach(var circle in ColonyActivityRange.Circles(worker.Population))Circles.Add(circle);
            Included.Clear();foreach(var task in tasks)if(task.target!=null)Included.Add(task.target);
            var origin=worker.transform.position;var settings=AntColonySettings.Current;int limit=SimulationOptimization.Current.taskQueue?SimulationOptimization.Current.workerQueueLimit:1;
            ResourceCoordinates.Query(origin,settings.transportSearchRadius,true,true,Resources);
            Resources.Sort((a,b)=>(a.position-origin).sqrMagnitude.CompareTo((b.position-origin).sqrMagnitude));
            foreach(var item in Resources){var r=item.resource;if(tasks.Count>=limit)break;if(ColonyCorpseAccess.Available(r,worker)&&!r.HasLiveClaim&&ColonyActivityRange.Contains(Circles,item.position)&&Included.Add(r.State.id))tasks.Add(new PlannedEntityTask{kind=PlannedTaskKind.CollectCorpse,target=r.State.id});}Resources.Clear();
            EntityCoordinateGrid.Query(origin,ResourceCoordinates.BroadRadius(origin,settings.transportSearchRadius),Entities);
            Entities.Sort((a,b)=>(a.position-origin).sqrMagnitude.CompareTo((b.position-origin).sqrMagnitude));
            foreach(var item in Entities){var e=item.entity;if(tasks.Count>=limit)break;if(EcologyQueries.Distance(origin,item.position)<=settings.transportSearchRadius&&ColonyActivityRange.Contains(Circles,item.position)&&AntColonyWorker.Valid(e,worker,Circles)&&!AntColonyWorker.Claimed(e,worker)){string id=EcologyIdentity.Of(e);if(Included.Add(id))tasks.Add(new PlannedEntityTask{kind=PlannedTaskKind.CollectLiving,target=id});}}Entities.Clear();Included.Clear();Circles.Clear();
            // 下一项在同一次推进激活，完成任务不必再等待扫描周期。
            if(current<tasks.Count)Select(worker);
        }
        public static void Validate(List<PlannedEntityTask> tasks)
        {
            if(tasks==null)return;if(tasks.Count>129)throw new InvalidDataException("实体任务队列过长");var ids=new HashSet<string>();int feeds=0;
            for(int i=0;i<tasks.Count;i++){var task=tasks[i];if(task==null||!Enum.IsDefined(typeof(PlannedTaskKind),task.kind))throw new InvalidDataException("实体任务类型无效");if(task.kind==PlannedTaskKind.Feed){if(++feeds>1||i!=0)throw new InvalidDataException("补给任务必须位于首位");}else if((task.kind==PlannedTaskKind.CollectCorpse||task.kind==PlannedTaskKind.CollectLiving||task.kind==PlannedTaskKind.Deliver)&&(!Guid.TryParseExact(task.target,"N",out _)||!ids.Add(task.target)))throw new InvalidDataException("实体任务目标重复或无效");}
        }
    }
}
