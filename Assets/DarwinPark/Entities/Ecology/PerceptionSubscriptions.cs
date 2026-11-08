using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：死亡注销只通知真正看过目标的观察者，不扫描每个实体的收件箱。
    public static class PerceptionSubscriptions
    {
        private static readonly Dictionary<DarwinEntity,HashSet<EntityGeneController>> Observers=new();
        public static void Attach(EntityGeneController observer,DarwinEntity target){if(!Observers.TryGetValue(target,out var set)){set=new();Observers.Add(target,set);}set.Add(observer);}
        public static void Forget(EntityGeneController observer,IEnumerable<DarwinEntity> targets){foreach(var target in targets)if(Observers.TryGetValue(target,out var set)){set.Remove(observer);if(set.Count==0)Observers.Remove(target);}}
        public static HashSet<EntityGeneController> Take(DarwinEntity target){if(Observers.Remove(target,out var set))return set;return null;}
        public static void Clear()=>Observers.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
