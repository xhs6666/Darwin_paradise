using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：移动属性会高频询问领袖和王后，不能每次排序全世界成员；关系仅在成员、表达或角色变化时重建。
    public static class PopulationGroupIndex
    {
        private sealed class Group
        {
            public readonly List<PopulationBehavior> members=new List<PopulationBehavior>();
            public PopulationBehavior leader;
            public AntColonyMember queen;
        }
        private static readonly Dictionary<(string,string),Group> Groups=new Dictionary<(string,string),Group>();
        private static readonly List<PopulationBehavior> Empty=new List<PopulationBehavior>();
        public static long BuildCount {get;private set;}
        // ★ 适配：资源归属索引同步成员变化，不能用上一帧的“已灭绝”结果回收仍有主人的补给。
        public static long Revision {get;private set;}
        public static void Invalidate(){Groups.Clear();Revision++;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){Groups.Clear();BuildCount=0;Revision=0;}
        private static bool Earlier(PopulationBehavior a,PopulationBehavior b)=>b==null||a.JoinOrder<b.JoinOrder||a.JoinOrder==b.JoinOrder&&string.CompareOrdinal(EcologyIdentity.Of(a.GetComponent<DarwinEntity>()),EcologyIdentity.Of(b.GetComponent<DarwinEntity>()))<0;
        private static Group Get(PopulationBehavior owner)
        {
            var key=(owner.GroupId,owner.Mode);
            if(Groups.TryGetValue(key,out var group))return group;
            group=new Group();BuildCount++;
            foreach(var member in PopulationBehavior.RegisteredMembers)
            {
                if(member==null||member.GroupId!=key.Item1||member.Mode!=key.Item2||!member.Active)continue;
                group.members.Add(member);
                if(group.leader==null||member.LeaderFlag&&!group.leader.LeaderFlag||member.LeaderFlag==group.leader.LeaderFlag&&Earlier(member,group.leader))group.leader=member;
                var ant=member.GetComponent<AntColonyMember>();
                if(key.Item2==EcologySettings.Load().antState&&ant!=null&&ant.Role==AntRole.Queen&&Earlier(member,group.queen!=null?group.queen.Population:null))group.queen=ant;
            }
            Groups[key]=group;return group;
        }
        public static IEnumerable<PopulationBehavior> Members(PopulationBehavior owner)
        {
            if(string.IsNullOrEmpty(owner.GroupId))return Empty;
            return AliveMembers(Get(owner).members,owner.GroupId,owner.Mode);
        }
        private static IEnumerable<PopulationBehavior> AliveMembers(List<PopulationBehavior> members,string id,string mode)
        {foreach(var member in members)if(member!=null&&member.GroupId==id&&member.Mode==mode&&member.Active)yield return member;}
        public static AntColonyMember Queen(PopulationBehavior owner)
        {
            if(string.IsNullOrEmpty(owner.GroupId)||owner.Mode!=EcologySettings.Load().antState)return null;
            var group=Get(owner);
            if(group.queen!=null&&(!group.queen.Active||group.queen.Role!=AntRole.Queen)){Invalidate();group=Get(owner);}
            return group.queen;
        }
        public static PopulationBehavior Leader(PopulationBehavior owner)
        {
            if(string.IsNullOrEmpty(owner.GroupId))return null;
            var group=Get(owner);
            if(group.leader!=null&&!group.leader.Active){Invalidate();group=Get(owner);}
            return owner.Mode==EcologySettings.Load().antState&&group.queen!=null&&group.queen.Active?group.queen.Population:group.leader;
        }
    }
}
