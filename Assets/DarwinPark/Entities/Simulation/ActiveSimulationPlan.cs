using System;
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：活跃实体只剔除确定为空的被动调用及无副作用的重复行为；时间结算和原行为顺序不改变。
    public sealed class ActiveSimulationPlan
    {
        private int _revision=-1;private bool[] _run=Array.Empty<bool>();private readonly Dictionary<IGeneLogic,GeneInstance> _genes=new();
        internal readonly Dictionary<Type,Component> components=new();public bool KnownOnly{get;private set;}
        public static long Executed,Skipped,Builds,ComponentReads,ComponentHits;
        public void Invalidate()=>_revision=-1;
        private static bool Passive(Type type)=>type==typeof(GrowthGeneLogic)||type==typeof(RepairGeneLogic)||type==typeof(StorageGeneLogic)||type==typeof(EnvironmentAdaptationGeneLogic)||type==typeof(ObservationGeneLogic)||type==typeof(HeterotrophyGeneLogic)||type==typeof(ScavengingGeneLogic)||type==typeof(ParasiticGeneLogic);
        private static bool Known(Type type)=>Passive(type)||type==typeof(NutritionGeneLogic)||type==typeof(EnergyBlockGeneLogic)||type==typeof(LocomotionGeneLogic)||type==typeof(TerritoryGeneLogic)||type==typeof(PopulationGeneLogic)||type==typeof(ReproductionGeneLogic)||type==typeof(OviparousGeneLogic)||type==typeof(MultipleBirthGeneLogic)||type==typeof(EggParasiticGeneLogic);
        public void Prepare(EntityGeneController owner,IReadOnlyList<IGeneLogic> logics)
        {
            if(_revision==owner.LogicRevision&&_run.Length>=logics.Count)return;_revision=owner.LogicRevision;Builds++;KnownOnly=true;
            if(_run.Length<logics.Count)_run=new bool[Mathf.NextPowerOfTwo(Mathf.Max(1,logics.Count))];_genes.Clear();foreach(var gene in owner.Genome.Genes){var logic=owner.GetLogic(gene);if(logic!=null)_genes[logic]=gene;}
            for(int i=0;i<logics.Count;i++)
            {
                var logic=logics[i];var type=logic.GetType();KnownOnly&=Known(type);bool run=!Passive(type);
                if(run&&_genes.TryGetValue(logic,out var gene))
                {
                    if(type==typeof(NutritionGeneLogic))run=GeneBehaviorAuthority.IsPrimary<NutritionGeneLogic>(owner,gene);
                    else if(type==typeof(EnergyBlockGeneLogic))run=GeneBehaviorAuthority.IsPrimary<EnergyBlockGeneLogic>(owner,gene);
                    else if(type==typeof(LocomotionGeneLogic)||type==typeof(TerritoryGeneLogic)||type==typeof(PopulationGeneLogic))run=GeneBehaviorAuthority.IsPrimary<LocomotionGeneLogic>(owner,gene);
                    // 繁殖副本仍运行：原入口会推进配偶缓存并清掉非主表达的等待计时，不能当空调用删除。
                }
                _run[i]=run;
            }
        }
        public void Run(EntityGeneController owner,IReadOnlyList<IGeneLogic> logics,float dt)
        {
            Prepare(owner,logics);int revision=_revision;bool enabled=SimulationOptimization.Current.activeExecutionPlan;
            for(int i=0;i<logics.Count;i++)
            {
                // 回调中重算后，原快照剩余部分恢复逐项调用；下个小步才重建，避免漏掉刚变为主表达的行为。
                if(enabled&&owner.LogicRevision==revision&&!_run[i]){Skipped++;continue;}Executed++;logics[i].OnUpdate(dt);
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){Executed=Skipped=Builds=ComponentReads=ComponentHits=0;}
    }
    // ★ 新增：只在一次同步模拟批次内复用组件（包括不存在的可选组件）；未知扩展行为保留原查询路径。
    public static class ActiveSimulationBatch
    {
        private struct Frame{public EntityGeneController owner;public ActiveSimulationPlan plan;public int revision;}
        private static Frame _current;private static readonly Stack<Frame> Parents=new();
        public static void Begin(EntityGeneController owner,ActiveSimulationPlan plan){if(_current.owner!=null)Parents.Push(_current);plan.components.Clear();_current=new Frame{owner=owner,plan=plan,revision=owner.LogicRevision};}
        public static void End(EntityGeneController owner){if(_current.owner==owner)_current=Parents.Count>0?Parents.Pop():default;}
        public static void Invalidate(EntityGeneController owner){if(_current.owner==owner&&_current.plan!=null)_current.plan.components.Clear();}
        public static T Read<T>(EntityGeneController owner)where T:Component
        {
            if(owner==null)return null;ActiveSimulationPlan.ComponentReads++;
            if(_current.owner!=owner||_current.plan==null||!_current.plan.KnownOnly||!SimulationOptimization.Current.activeComponentReuse)return owner.GetComponent<T>();
            if(_current.revision!=owner.LogicRevision){_current.plan.components.Clear();_current.revision=owner.LogicRevision;}
            var values=_current.plan.components;var type=typeof(T);if(values.TryGetValue(type,out var cached)){if(ReferenceEquals(cached,null)||cached!=null){ActiveSimulationPlan.ComponentHits++;return cached as T;}values.Remove(type);}
            var result=owner.GetComponent<T>();values[type]=result;return result;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_current=default;Parents.Clear();}
    }
}
