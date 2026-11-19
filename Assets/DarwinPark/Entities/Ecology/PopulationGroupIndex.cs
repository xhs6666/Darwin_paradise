using System.Collections.Generic;
using UnityEngine;

// ★ 修复（防Bug回归）：普通CPU层级确认空组件诊断字符串仍在生命/基因等高频入口分配；正常缺失改TryGet即时读取。
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
            public bool authorityChecked;
        }
        private static readonly Dictionary<(string,string),Group> Groups=new Dictionary<(string,string),Group>();
        private static readonly List<PopulationBehavior> Empty=new List<PopulationBehavior>();
        private static readonly Group EmptyGroup=new Group();
        private static bool _built;
        public static long BuildCount {get;private set;}
        // ★ 适配：资源归属索引同步成员变化，不能用上一帧的“已灭绝”结果回收仍有主人的补给。
        public static long Revision {get;private set;}
        public static void Invalidate(){Groups.Clear();_built=false;Revision++;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){Groups.Clear();_built=false;BuildCount=0;Revision=0;}
        private static bool Earlier(PopulationBehavior a,PopulationBehavior b)=>b==null||a.JoinOrder<b.JoinOrder||a.JoinOrder==b.JoinOrder&&string.CompareOrdinal(EcologyIdentity.Of(a.GetComponent<DarwinEntity>()),EcologyIdentity.Of(b.GetComponent<DarwinEntity>()))<0;
        private static Group Get(PopulationBehavior owner)
        {
            var key=(owner.GroupId,owner.Mode);
            if(!_built)Build();
            if(!Groups.TryGetValue(key,out var group))return EmptyGroup;
            if(!group.authorityChecked)CheckAuthority(group,key.Item2==EcologySettings.Load().antState);
            return group;
        }
        private static void Build()
        {
            // ★ 适配：一次注册表遍历为所有群建索引。发生死亡或换群后，多个群不再各扫一遍全体。
            long revision;
            do
            {
                revision=Revision;
                Groups.Clear();
                foreach(var member in PopulationBehavior.RegisteredMembers)
                {
                    if(member==null||string.IsNullOrEmpty(member.GroupId))continue;
                    var key=(member.GroupId,member.Mode);
                    if(!Groups.TryGetValue(key,out var group))
                    {
                        group=new Group();
                        Groups.Add(key,group);
                    }
                    group.members.Add(member);
                }
                // 读取 Mode 可能首次刷新表达并触发失效。此时重建，避免其他群只收录部分成员。
            }while(revision!=Revision);
            _built=true;
            BuildCount++;
        }
        private static void CheckAuthority(Group group,bool antMode)
        {
            // ★ 适配：领袖仍在本群首次查询时按实时生命和分工选择，避免预建索引提前冻结其资格。
            foreach(var member in group.members)
            {
                if(member==null||!member.Active)continue;
                if(group.leader==null||member.LeaderFlag&&!group.leader.LeaderFlag||member.LeaderFlag==group.leader.LeaderFlag&&Earlier(member,group.leader))group.leader=member;
                if(antMode&&DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(member) is AntColonyMember ant&&
                    ant.Role==AntRole.Queen&&Earlier(member,group.queen!=null?group.queen.Population:null))group.queen=ant;
            }
            group.authorityChecked=true;
        }
        public static IEnumerable<PopulationBehavior> Members(PopulationBehavior owner)
        {
            if(string.IsNullOrEmpty(owner.GroupId))return Empty;
            return AliveMembers(Get(owner).members,owner.GroupId,owner.Mode);
        }
        // ★ 适配：王后同步派工复用成员索引。回调可变更成员归属，派工时再核对当前群体。
        internal static IReadOnlyList<PopulationBehavior> DispatchMembers(PopulationBehavior owner)
            =>string.IsNullOrEmpty(owner.GroupId)?Empty:Get(owner).members;
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
