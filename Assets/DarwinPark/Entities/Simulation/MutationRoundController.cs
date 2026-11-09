using System;
using System.Collections.Generic;
using System.IO;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics.Mutation;
using UnityEngine;

namespace DarwinPark.Entities.Simulation
{
    [Serializable]public sealed class MutationRoundSave
    {
        public int version=1;public double elapsed;public long rounds,attempts,successes;
        public int population,quota,selected;public List<string> pending=new();
    }
    // ★ 新增：每轮只选一次不重复身份，逐帧消化队列；计时、剩余名单与计数随世界保存，阶段切换不再另抽。
    [DefaultExecutionOrder(-210)]public sealed class MutationRoundController:MonoBehaviour
    {
        private static MutationRoundController _instance;private static MutationRoundSave _state=new();
        private static readonly List<DarwinEntity> Candidates=new();private static readonly Queue<string> Pending=new();
        public static long Rounds=>_state.rounds;public static long Attempts=>_state.attempts;public static long Successes=>_state.successes;
        public static int LastPopulation=>_state.population;public static int LastSelected=>_state.selected;public static int Queued=>Pending.Count;
        public static void Ensure(GameObject owner)
        {
            // ★ 修复（防Bug回归）：脚本重载可能清掉静态引用，读档时先认领场景已有组件，不能重复安装两个计时器。
            if(_instance!=null)return;_instance=FindFirstObjectByType<MutationRoundController>();if(_instance==null)owner.AddComponent<MutationRoundController>();
        }
        private void Awake(){if(_instance!=null&&_instance!=this){Destroy(this);return;}_instance=this;}
        private void OnDestroy(){if(_instance==this)_instance=null;}
        private void Update(){if(WorldRuntimeGate.Managed&&(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading))return;GlobalMutationManager.Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);}
        private static bool Eligible(DarwinEntity entity)=>EcologyQueries.Alive(entity)&&entity.Genes!=null&&entity.Genes.isActiveAndEnabled&&entity.Genes.Genome?.Genes.Count>0&&entity.GetComponent<AntColonyMember>()?.Frozen!=true&&entity.GetComponent<EggIncubation>()?.Incubating!=true;
        public static void Advance(float dt)
        {
            if(dt<=0||WorldAuthority.IsReplica)return;var tuning=GameplayTuning.Current;if(!tuning.mutationRoundsEnabled||tuning.mutationMode!=0)return;
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.MutationRounds);
            _state.elapsed+=dt;
            if(Pending.Count==0&&_state.elapsed>=tuning.mutationRoundSeconds)
            {
                _state.elapsed-=tuning.mutationRoundSeconds;_state.rounds++;Candidates.Clear();int population=0;
                foreach(var entity in EntityRegistry.All)if(EcologyQueries.Alive(entity)){population++;if(Eligible(entity))Candidates.Add(entity);}
                _state.population=population;_state.quota=Mathf.Min(population,Mathf.CeilToInt(population*tuning.mutationPopulationFraction));_state.selected=Mathf.Min(_state.quota,Candidates.Count);
                // 部分Fisher-Yates保证无放回随机，不会总挑同组序号或注册表前几个实体。
                for(int i=0;i<_state.selected;i++){int pick=UnityEngine.Random.Range(i,Candidates.Count);var swap=Candidates[i];Candidates[i]=Candidates[pick];Candidates[pick]=swap;Pending.Enqueue(EcologyIdentity.Of(Candidates[i]));}
                Candidates.Clear();
            }
            int budget=Mathf.FloorToInt(tuning.mutationAttemptsPerFrame);
            while(budget-->0&&Pending.Count>0)
            {
                var entity=EcologyIdentity.Find(Pending.Dequeue());if(!Eligible(entity))continue;
                var genes=entity.Genes;var settings=GeneMutationSettings.CreateDefault();settings.AdaptationGeneId=genes.EnvironmentGeneId;settings.AdaptationMutationChance=genes.EnvironmentMutationChance;settings.AdaptationTargetValue=genes.EnvironmentAdaptationTarget;
                _state.attempts++;var result=GeneMutator.Mutate(genes.Genome,settings);if(result.Success)_state.successes++;genes.Recalculate();
            }
        }
        public static MutationRoundSave Capture()
        {var save=JsonUtility.FromJson<MutationRoundSave>(JsonUtility.ToJson(_state));save.pending=new List<string>(Pending);return save;}
        public static void Restore(MutationRoundSave save)
        {
            Validate(save);Pending.Clear();Candidates.Clear();_state=save==null||save.version==0?new MutationRoundSave():JsonUtility.FromJson<MutationRoundSave>(JsonUtility.ToJson(save));
            foreach(string id in _state.pending)Pending.Enqueue(id);_state.pending.Clear();
        }
        public static void Validate(MutationRoundSave save)
        {
            if(save==null)return;
            if(save.version==0&&save.elapsed==0&&save.rounds==0&&save.attempts==0&&save.successes==0&&save.population==0&&save.quota==0&&save.selected==0&&(save.pending==null||save.pending.Count==0))return;
            if(save.version!=1||double.IsNaN(save.elapsed)||double.IsInfinity(save.elapsed)||save.elapsed<0||save.rounds<0||save.attempts<0||save.successes<0||save.successes>save.attempts||save.population<0||save.quota<0||save.quota>save.population||save.selected<0||save.selected>save.quota||save.pending==null||save.pending.Count>save.selected)throw new InvalidDataException("全局变异轮次状态无效");
            var ids=new HashSet<string>();foreach(string id in save.pending)if(!Guid.TryParseExact(id,"N",out _)||!ids.Add(id))throw new InvalidDataException("全局变异待执行名单无效或重复");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_instance=null;Restore(null);}
    }
}
