using DarwinPark.Core;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：领地中心以星球本地坐标保存；遇袭脱离边界，安全期后在新位置定居，不再返回旧领地。
    [DefaultExecutionOrder(-200)]
    public sealed class TerritoryBehavior : MonoBehaviour
    {
        public Vector3 CenterLocal {get;private set;}
        public bool HasCenter {get;private set;}
        public float EvictionRemaining {get;private set;}
        public Vector3 ThreatLocal {get;private set;}
        private EntityGeneController _genes;private EntityCombat _combat;private EcologySettings _settings;
        private float _patrolTimer;private Vector3 _destination;
        public bool Active=>EcologyQueries.Active(_genes,_settings.territoryGene)!=null||EcologyQueries.Active(_genes,_settings.populationGene)!=null;
        public Vector3 Center=>EcologyQueries.ToWorld(CenterLocal);
        public float Radius=>Mathf.Max(_settings.arrivalDistance,_genes.GetAttribute("territory_radius"));
        public float Defense=>Active&&EvictionRemaining<=0&&AllowsTarget(transform.position)?_genes.GetAttribute("defend_bonus"):0;
        private void Awake(){_genes=GetComponent<EntityGeneController>();_settings=EcologySettings.Load();}
        private void OnEnable(){_combat=EntityCombat.Ensure(gameObject);_combat.Attacked+=OnAttacked;}
        private void OnDisable(){if(_combat!=null)_combat.Attacked-=OnAttacked;}
        private void Update(){float dt=ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime;Advance(dt);}
        public void Advance(float dt)
        {
            if(!Active||dt<=0)return;
            // ★ 适配：巡逻休息期独立保存，不靠抵达刷新巡逻点维持无限巡逻。
            PatrolRhythm.Ensure(gameObject).Advance(dt);
            if(!HasCenter){CenterLocal=EcologyQueries.ToLocal(transform.position);HasCenter=true;}
            if(EvictionRemaining>0)
            {
                EvictionRemaining=Mathf.Max(0,EvictionRemaining-dt);
                if(EvictionRemaining<=0){CenterLocal=EcologyQueries.ToLocal(transform.position);_patrolTimer=0;}
            }
            _patrolTimer-=dt;
        }
        private void OnAttacked(DarwinEntity attacker,float damage)
        {
            if(!Active||attacker==null||damage<=0)return;
            // ★ 修复（防Bug回归）：可反击的实体不再一律驱逐；无法对抗时保留原逃离逻辑。
            if(CombatIntent.CanRetaliate(_genes,attacker))return;
            ThreatLocal=EcologyQueries.ToLocal(attacker.transform.position);EvictionRemaining=_settings.settleDelay;
        }
        public bool AllowsTarget(Vector3 position)
        {
            // ★ 适配：种群成员共享活动区，普通领地仍使用自身领地圆。
            if(!Active||EvictionRemaining>0)return true;
            var population=GetComponent<PopulationBehavior>();
            return population!=null&&population.Active?ColonyActivityRange.Contains(population,position):EcologyQueries.Distance(Center,position)<=Radius;
        }
        public bool Destination(out Vector3 point,out float speed)
        {
            point=transform.position;speed=1;if(!Active)return false;
            if(!HasCenter){CenterLocal=EcologyQueries.ToLocal(transform.position);HasCenter=true;}
            if(EvictionRemaining>0)
            {
                Vector3 away=Vector3.ProjectOnPlane(transform.position-EcologyQueries.ToWorld(ThreatLocal),EcologyQueries.Up(transform.position));
                if(away.sqrMagnitude<.01f)away=Vector3.Cross(EcologyQueries.Up(transform.position),Vector3.right);
                point=EcologyQueries.Surface(transform.position+away.normalized*_settings.fleeDistance);speed=_settings.fleeSpeedMultiplier;return true;
            }
            if(!AllowsTarget(transform.position)){var pop=GetComponent<PopulationBehavior>();point=pop!=null&&pop.Active?ColonyActivityRange.NearestCenter(pop,transform.position):Center;return true;}
            // ★ 修复（防Bug回归）：领地只约束边界与驱逐，不能永远返回巡逻意图并阻断饥饿捕猎。
            return false;
        }
        // ★ 适配：仅由注册表中 TerritoryPatrol 行为调用，保留基因状态与独立实体状态。
        public bool PatrolDestination(out Vector3 point)
        {
            point=transform.position;if(!Active||!PatrolRhythm.Allows(_genes))return false;
            if(!HasCenter){CenterLocal=EcologyQueries.ToLocal(transform.position);HasCenter=true;}
            if(_patrolTimer<=0||EcologyQueries.Distance(transform.position,EcologyQueries.ToWorld(_destination))<_settings.arrivalDistance)
            {
                var tangent=Vector3.ProjectOnPlane(Random.onUnitSphere,EcologyQueries.Up(Center));
                _destination=EcologyQueries.ToLocal(EcologyQueries.Surface(Center+tangent.normalized*Random.Range(0,Radius)));_patrolTimer=_settings.patrolInterval;
            }
            point=EcologyQueries.ToWorld(_destination);return true;
        }
        public void Restore(Vector3 center,bool hasCenter,float eviction,Vector3 threat)
        {CenterLocal=center;HasCenter=hasCenter;EvictionRemaining=eviction;ThreatLocal=threat;_patrolTimer=0;}
    }
}
