using System;
using System.Collections.Generic;
using System.IO;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using UnityEngine;
namespace DarwinPark.Entities.Simulation
{
    [Serializable]public sealed class CorpseCleanupSave
    {public int version=1;public double elapsed;public long cycles,removed;public List<string> pending=new();}
    // ★ 新增：按模拟时间生成清理快照并分帧执行；领取/运输中的尸体和所有能量块受到保护。
    [DefaultExecutionOrder(-205)]public sealed class CorpseCleanupController:MonoBehaviour
    {
        private static CorpseCleanupController _instance;private static CorpseCleanupSave _state=new();private static readonly Queue<string> Pending=new();
        public static long Removed=>_state.removed;public static int Queued=>Pending.Count;public static double Elapsed=>_state.elapsed;
        public static void Ensure(GameObject owner){if(_instance!=null)return;_instance=FindFirstObjectByType<CorpseCleanupController>();if(_instance==null)owner.AddComponent<CorpseCleanupController>();}
        private void Awake(){if(_instance!=null&&_instance!=this){Destroy(this);return;}_instance=this;}
        private void OnDestroy(){if(_instance==this)_instance=null;}
        private void Update()=>Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);
        public static void Advance(float dt)
        {
            var tuning=GameplayTuning.Current;if(dt<=0||!tuning.corpseCleanupEnabled||WorldAuthority.IsReplica||WorldRuntimeGate.Managed&&(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading))return;
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.CorpseCleanup);_state.elapsed+=dt;
            if(Pending.Count==0&&_state.elapsed>=tuning.corpseCleanupSeconds)
            {
                _state.elapsed-=tuning.corpseCleanupSeconds;_state.cycles++;
                foreach(var resource in ColonyResource.All)if(resource.State.kind==ColonyResourceKind.Corpse&&!resource.HasLiveClaim)Pending.Enqueue(resource.State.id);
            }
            int budget=tuning.corpseCleanupPerFrame;
            while(budget-->0&&Pending.Count>0)
            {
                var resource=ColonyResource.Find(Pending.Dequeue());
                // 快照后仍可被取走或转换，删除时必须重新核对，不得吞掉工匠货物或新能量块。
                if(resource==null||resource.State.kind!=ColonyResourceKind.Corpse||resource.HasLiveClaim)continue;resource.Remove();_state.removed++;
            }
        }
        public static CorpseCleanupSave Capture(){var copy=JsonUtility.FromJson<CorpseCleanupSave>(JsonUtility.ToJson(_state));copy.pending=new List<string>(Pending);return copy;}
        public static void Restore(CorpseCleanupSave state)
        {Validate(state);Pending.Clear();_state=state==null||state.version==0?new():JsonUtility.FromJson<CorpseCleanupSave>(JsonUtility.ToJson(state));foreach(var id in _state.pending)Pending.Enqueue(id);_state.pending.Clear();}
        public static void Validate(CorpseCleanupSave state)
        {
            if(state==null)return;if(state.version==0&&state.elapsed==0&&state.cycles==0&&state.removed==0&&(state.pending==null||state.pending.Count==0))return;
            if(state.version!=1||double.IsNaN(state.elapsed)||double.IsInfinity(state.elapsed)||state.elapsed<0||state.cycles<0||state.removed<0||state.pending==null)throw new InvalidDataException("尸体清理规则进度无效");
            var ids=new HashSet<string>();foreach(var id in state.pending)if(!Guid.TryParseExact(id,"N",out _)||!ids.Add(id))throw new InvalidDataException("尸体清理名单无效或重复");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_instance=null;Restore(null);}
    }
}
