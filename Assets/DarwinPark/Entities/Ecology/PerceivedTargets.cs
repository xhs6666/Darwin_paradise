using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：行动只筛已投递的可见候选，追击点使用观察状态提供的采样/实时坐标，不再逐子步扫全体实体。
    public static class PerceivedTargets
    {
        public static bool Valid(EntityGeneController genes,DarwinEntity target,bool graze,out Vector3 point,bool foodOnly=false,float maximumRange=float.PositiveInfinity)
        {
            point=default;return target!=null&&target.gameObject!=genes.gameObject&&EcologyQueries.Alive(target)&&EntityPerceptionHub.TryLocate(genes,target,out point,maximumRange)&&(!graze||target.Genes.GetAttribute("moveSpeed")<=0)&&(!foodOnly||NutritionTransfer.FoodTargetAllowed(genes,target))&&EcologyMotion.CanHunt(genes,target)&&LandReachability.CanReach(genes,target);
        }
        public static DarwinEntity Nearest(EntityGeneController genes,bool graze,DarwinEntity previous,out Vector3 point,bool foodOnly=false,float maximumRange=float.PositiveInfinity)
        {
            if(Valid(genes,previous,graze,out point,foodOnly,maximumRange))return previous;
            DarwinEntity best=null;point=default;float distance=float.PositiveInfinity;var here=genes.transform.position;
            foreach(var sighting in EntityPerceptionHub.Read(genes))
            {
                if(!Valid(genes,sighting.entity,graze,out var candidate,foodOnly,maximumRange))continue;float squared=(candidate-here).sqrMagnitude;if(squared>=distance)continue;
                best=sighting.entity;point=candidate;distance=squared;
            }
            return best;
        }
    }
}
