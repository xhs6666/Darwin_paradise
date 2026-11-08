using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：配偶候选来自共享活体坐标；只把寻找节奏降频，已找到的配偶每次先实时校验。
    public sealed class MateCoordinates
    {
        private readonly List<EntityCoordinate> _candidates=new();private DarwinEntity _mate;private float _remaining;
        public void Advance(float dt)=>_remaining-=dt;
        private static bool Valid(EntityGeneController owner,DarwinEntity entity,float range,float required)
        {
            if(entity==null||entity.Genes==owner||!EcologyQueries.Alive(entity)||AntColonyReproduction.BlocksOrdinary(entity.Genes)||entity.Genes.CurrentAge!=AgeState.Adult||entity.Genes.IsGestating||(entity.transform.position-owner.transform.position).sqrMagnitude>range*range)return false;
            var stats=entity.GetComponent<EntityStats>();if(stats==null||!stats.TryGet("storedNutrient",out var reserve)||reserve<required)return false;
            foreach(var gene in entity.Genes.Genome.Genes)if(entity.Genes.GetLogic(gene) is ReproductionGeneLogic&&GeneBehaviorAuthority.IsPrimary<ReproductionGeneLogic>(entity.Genes,gene)&&entity.Genes.GetEvaluation(gene)?.State?.StateId=="sexual")return true;
            return false;
        }
        public DarwinEntity Find(EntityGeneController owner,float range,float required)
        {
            if(Valid(owner,_mate,range,required))return _mate;
            _mate=null;if(_remaining>0)return null;_remaining=SimulationOptimization.Current.mateSearchSeconds;
            EntityCoordinateGrid.Query(owner.transform.position,range,_candidates);float best=float.PositiveInfinity;
            foreach(var coordinate in _candidates){float d=(coordinate.position-owner.transform.position).sqrMagnitude;if(d<best&&Valid(owner,coordinate.entity,range,required)){best=d;_mate=coordinate.entity;}}_candidates.Clear();return _mate;
        }
    }
}
