using System;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class PatrolRhythmSave {public float rest,active;}
    // ★ 新增：巡逻具有明确休息期，抵达一个巡逻点不会立即刷新下一段无限巡逻。
    public sealed class PatrolRhythm : MonoBehaviour
    {
        private float _rest,_active;
        private void Awake(){_rest=UnityEngine.Random.Range(AntColonySettings.Current.patrolRestMin,AntColonySettings.Current.patrolRestMax);}
        public bool Patrolling=>_active>0;
        public static PatrolRhythm Ensure(GameObject obj)=>obj.GetComponent<PatrolRhythm>()??obj.AddComponent<PatrolRhythm>();
        public void Advance(float dt)
        {
            if(dt<=0)return;
            var settings=AntColonySettings.Current;
            if(_active>0){_active=Mathf.Max(0,_active-dt);if(_active<=0)_rest=UnityEngine.Random.Range(settings.patrolRestMin,settings.patrolRestMax);}
            else{_rest=Mathf.Max(0,_rest-dt);if(_rest<=0)_active=settings.patrolDuration;}
        }
        public static bool Allows(EntityGeneController genes)=>Ensure(genes.gameObject).Patrolling;
        // ★ 修复（防Bug回归）：普通领地巡逻仅受击反击，附近捕猎倾向和饥饿状态不能自行授予攻击目标；群体仍执行自己的分工。
        public static bool Defensive(EntityGeneController genes)=>AntColonySettings.Current.patrolRetaliationOnly&&genes.GetComponent<TerritoryBehavior>()?.Active==true&&genes.GetComponent<PopulationBehavior>()?.Active!=true;
        public static bool Eligible(EntityGeneController genes,Genetics.EntityStateDefinition state)
        {
            if(state.Behavior==Genetics.EntityStateBehavior.Hunt&&Defensive(genes))return false;
            return state.Behavior!=Genetics.EntityStateBehavior.TerritoryPatrol||Allows(genes);
        }
        public static bool Resting(EntityGeneController genes)
        {
            if(genes.GetComponent<TerritoryBehavior>()?.Active!=true||Allows(genes)||genes.GetComponent<EntityStateController>()?.Current.Behavior==Genetics.EntityStateBehavior.Hunt)return false;
            var stats=genes.GetComponent<Stats.EntityStats>();return stats!=null&&!EcologyEnergyBudget.WantsFood(genes,stats,AntColonySettings.Current.memberFeedThreshold);
        }
        public PatrolRhythmSave Capture()=>new PatrolRhythmSave{rest=_rest,active=_active};
        public void Restore(PatrolRhythmSave state){if(state!=null){_rest=state.rest;_active=state.active;}}
    }
}
