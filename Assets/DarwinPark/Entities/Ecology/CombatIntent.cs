using System;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class CombatIntentSave {public string target;public float remaining;}
    // ★ 新增：仅真实受击建立反击目标；同群、死亡、失去感知与超过体型条件时立即撤销。
    public sealed class CombatIntent : MonoBehaviour
    {
        public DarwinEntity Target {get;private set;}
        public float Remaining {get;private set;}
        private EntityCombat _combat;
        private void OnEnable(){_combat=EntityCombat.Ensure(gameObject);_combat.Attacked+=OnAttacked;}
        private void OnDisable(){if(_combat!=null)_combat.Attacked-=OnAttacked;}
        public static bool CanRetaliate(EntityGeneController genes,DarwinEntity attacker)=>genes!=null&&attacker!=null&&EcologyQueries.Alive(attacker)&&genes.GetAttribute("attack")>0&&EcologyMotion.CanHunt(genes,attacker)&&AntColonyCombat.CanAttack(genes.GetComponent<DarwinEntity>(),attacker);
        private void OnAttacked(DarwinEntity attacker,float damage)
        {if(damage>0&&CanRetaliate(GetComponent<EntityGeneController>(),attacker)){Target=attacker;Remaining=AntColonySettings.Current.retaliationSeconds;}}
        public void Advance(float dt){Remaining=Mathf.Max(0,Remaining-dt);if(Remaining<=0)Target=null;}
        public static bool Destination(EntityGeneController genes,out Vector3 point,out DarwinEntity target)
        {
            point=genes.transform.position;target=null;var intent=genes.GetComponent<CombatIntent>();
            if(intent==null||intent.Remaining<=0)return false;
            if(!CanRetaliate(genes,intent.Target)||!EcologyMotion.Detectable(intent.Target,genes.transform.position,genes.GetAttribute("senseRange"),genes.GetComponent<DarwinEntity>())){intent.Target=null;return false;}
            target=intent.Target;point=target.transform.position;return true;
        }
        public CombatIntentSave Capture()=>new CombatIntentSave{target=Target!=null?EcologyIdentity.Of(Target):null,remaining=Remaining};
        public void Restore(DarwinEntity target,float remaining){Target=target;Remaining=remaining;}
        public static void AdvanceFor(EntityGeneController genes,float dt)
        {(genes.GetComponent<CombatIntent>()??genes.gameObject.AddComponent<CombatIntent>()).Advance(dt);}
    }
}
