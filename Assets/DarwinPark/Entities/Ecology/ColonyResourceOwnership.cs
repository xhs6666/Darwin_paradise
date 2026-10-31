using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：旧群灭绝后遗留的能量不能永远锁给不存在的种群；每帧共用归属索引，避免逐成员逐块遍历全群。
    public static class ColonyResourceOwnership
    {
        private static readonly HashSet<string> LivingGroups=new HashSet<string>();
        private static int _frame=-1;private static long _revision=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){LivingGroups.Clear();_frame=-1;_revision=-1;}
        public static bool HasOwner(string group)
        {
            if(_frame!=Time.frameCount||_revision!=PopulationGroupIndex.Revision)
            {
                LivingGroups.Clear();foreach(var member in AntColonyMember.All)if(!string.IsNullOrEmpty(member.GroupId))LivingGroups.Add(member.GroupId);
                _frame=Time.frameCount;_revision=PopulationGroupIndex.Revision;
            }
            return LivingGroups.Contains(group);
        }
        public static bool Available(ColonyResource resource,AntColonyMember member)
        {
            // ★ 适配：主动吐出的通用能量块允许共享，不沿用工匠专用补给的群体锁。
            if(resource.State.kind==ColonyResourceKind.Energy)return true;
            if(resource.State.group==member.GroupId)return true;
            // 加载完成前不判定灭绝；仍有任何存活成员的资源继续受同群限制保护。
            return !WorldRuntimeGate.Loading&&!HasOwner(resource.State.group)&&
                EcologyQueries.Distance(resource.transform.position,member.transform.position)<=AntColonySettings.Current.transportSearchRadius;
        }
    }
}
