using System;
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using UnityEngine;

namespace DarwinPark.Entities.Simulation
{
    // ✅ 新增：调度条目、分组和兼容键是共享状态，独立于逐帧编排，保持值类型散列与池化。
    internal sealed class EntityScheduleEntry
    {
        public EntityGeneController genes;public EntityStats stats;public EntityLifeCycle life;
        public EntityVitals vitals;public EntityEnvironmentHazard environment;public EntityStateController state;
        public DarwinEntity entity;public EntityManualControl manual;public NutritionParasite nutritionParasite;public ParasiteBehavior parasite;public EggIncubation egg;
        public int reproductionRevision=-1;public DarwinPark.Entities.Logic.ReproductionGeneLogic reproduction;
        public Vector3 position;public float quiet;public float pending {get=>genes.PendingSimulationTime;set=>genes.PendingSimulationTime=value;}public int revision=-1,frame=-1,ordinal,count;public bool dormant;
        public int signature;public string stateId;public bool supported;
        public double nextDue=double.NaN;
    }
    internal sealed class StationaryCohort {public readonly List<EntityScheduleEntry> members=new List<EntityScheduleEntry>();public float time,priority;public StationaryCohort next;}
    // ★ 修复（防Bug回归）：兼容条件使用值类型和预计算散列，27格查询不能反复拼接/散列完整基因字符串。
    internal readonly struct CohortCompatibility:IEquatable<CohortCompatibility>
    {
        private readonly int _signature,_timeBand,_foodBand,_reserveBand,_flags,_hash;
        private readonly string _state;private readonly AgeState _age;
        private readonly float _health,_size,_foodMax,_reserveMax;
        public CohortCompatibility(EntityScheduleEntry entry,GameplayTuning tuning)
        {
            _signature=entry.signature;_state=entry.stateId;_age=entry.genes.CurrentAge;
            _flags=(entry.genes.IsGestating?1:0)|(entry.vitals!=null&&entry.vitals.isActiveAndEnabled?2:0)|(entry.environment!=null&&entry.environment.isActiveAndEnabled?4:0);
            _timeBand=Mathf.FloorToInt(entry.pending/tuning.mergeInterval);
            entry.stats.TryGet("nutrient",out var food);entry.stats.TryGet("storedNutrient",out var reserve);
            entry.stats.TryGet("size",out _size);entry.stats.TryGet("health",out _health);
            _foodBand=Mathf.FloorToInt(food/tuning.mergeFoodTolerance);_reserveBand=Mathf.FloorToInt(reserve/tuning.mergeReserveTolerance);
            _foodMax=food+entry.stats.CapacityRemaining("nutrient");_reserveMax=reserve+entry.stats.CapacityRemaining("storedNutrient");
            var hash=new HashCode();hash.Add(_signature);hash.Add(_state);hash.Add(_age);hash.Add(_flags);hash.Add(_timeBand);hash.Add(_foodBand);hash.Add(_reserveBand);hash.Add(_health);hash.Add(_size);hash.Add(_foodMax);hash.Add(_reserveMax);_hash=hash.ToHashCode();
        }
        public bool Equals(CohortCompatibility other)=>_signature==other._signature&&_state==other._state&&_age==other._age&&_flags==other._flags&&_timeBand==other._timeBand&&_foodBand==other._foodBand&&_reserveBand==other._reserveBand&&_health==other._health&&_size==other._size&&_foodMax==other._foodMax&&_reserveMax==other._reserveMax;
        public override bool Equals(object value)=>value is CohortCompatibility other&&Equals(other);
        public override int GetHashCode()=>_hash;
    }
}
