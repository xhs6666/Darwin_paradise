using System;
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;

// ★ 适配：降低获取速度时只扣实际转移量，避免营养凭空损失。
// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class NutritionParasiteSave {public string host;public bool originalKinematic,originalDetect=true;}
    // ★ 新增：幼年营养寄生独立于卵寄生；附着时隐藏渲染/碰撞，宿主仍是独立实体，成年/失效/宿主死亡立即恢复。
    [DefaultExecutionOrder(-180)] public sealed class NutritionParasite:MonoBehaviour
    {
        private static readonly Dictionary<DarwinEntity,int> Counts=new Dictionary<DarwinEntity,int>();
        private readonly Dictionary<Renderer,bool> _renderers=new Dictionary<Renderer,bool>();
        private readonly Dictionary<Collider,bool> _colliders=new Dictionary<Collider,bool>();
        private EntityGeneController _genes;private DarwinEntity _entity,_candidate;private EntityStats _stats;
        private readonly List<EntityCoordinate> _coordinates=new();
        private Rigidbody _body;private bool _attached,_kinematic,_detect;private float _scan;
        public DarwinEntity Host {get;private set;}
        // ★ 适配：已孵化的幼体才可营养寄生，食腐出错分支优先接管食性，避免复制基因同时执行两套吸收。
        public bool Active=>_genes!=null&&_genes.CurrentAge==AgeState.Infant&&DarwinPark.Core.OptionalComponent.Read<EggIncubation>(this)?.Incubating!=true&&EcologyQueries.Active(_genes,NutritionEcologySettings.Current.scavengerGene)==null&&EcologyQueries.Active(_genes,NutritionEcologySettings.Current.nutrientParasiteGene)?.State?.StateId==NutritionEcologySettings.Current.parasiteState;
        public static int CountOn(DarwinEntity host)=>host!=null&&Counts.TryGetValue(host,out int count)?count:0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Counts.Clear();
        private void Awake(){_genes=GetComponent<EntityGeneController>();_entity=GetComponent<DarwinEntity>();_stats=GetComponent<EntityStats>();EcologyIdentity.Ensure(gameObject);}
        private void Update(){if(!WorldAuthority.IsReplica)Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);}
        private void OnDisable()=>Detach();
        private bool CanHost(DarwinEntity host)
        {
            if(!EcologyQueries.Alive(host)||host==_entity||CountOn(_entity)>0||AntColonyWorker.IsCarried(host)||AntColonyWorker.IsCarried(_entity)||DarwinPark.Core.OptionalComponent.Read<EggIncubation>(host)?.Incubating==true||DarwinPark.Core.OptionalComponent.Read<NutritionParasite>(host)?.Active==true||DarwinPark.Core.OptionalComponent.Read<PopulationBehavior>(this)?.SameGroup(host)==true)return false;
            _stats.TryGet("size",out float own);host.GetComponent<EntityStats>().TryGet("size",out float size);
            return size>=own*NutritionEcologySettings.Current.minimumHostSizeRatio;
        }
        public void Advance(float dt)
        {
            if(!Active||!EcologyQueries.Alive(_entity)){Detach();return;}
            if(_attached&&!EcologyQueries.Alive(Host))Detach();if(dt<=0)return;
            var settings=NutritionEcologySettings.Current;
            if(Host!=null)
            {
                var stats=Host.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);
                float amount=Mathf.Min(Mathf.Max(0,food-settings.hostProtectedNutrient),Mathf.Max(0,_genes.GetAttribute(settings.parasiteDrainAttribute))*dt*DarwinPark.Core.GameplayTuning.Current.nutritionGainMultiplier,NutritionTransfer.Room(_stats));
                if(amount>0){stats.Modify("nutrient",-amount,false);_stats.Feed(amount);}Follow();return;
            }
            _scan-=dt;if(_scan<=0)
            {
                _scan=settings.scanInterval;_candidate=null;float best=settings.searchRadius*settings.searchRadius;
                // ★ 修复（防Bug回归）：营养寄生也通过全局坐标投递并排除隔海宿主，不能继续全表扫描和锁定不可达目标。
                using var measured=PerformanceTimings.Measure(PerformanceTimings.Area.ParasiteHostSearch);EntityCoordinateGrid.Query(transform.position,settings.searchRadius,_coordinates);
                foreach(var coordinate in _coordinates){var entity=coordinate.entity;if(!CanHost(entity)||!LandReachability.CanReach(_genes,entity))continue;float distance=(coordinate.position-transform.position).sqrMagnitude;if(distance<best){best=distance;_candidate=entity;}}
            }
            if(CanHost(_candidate)&&EcologyQueries.Distance(transform.position,_candidate.transform.position)<=settings.attachReach+(_candidate.GetComponent<Collider>()?.bounds.extents.magnitude??0))TryAttach(_candidate);
        }
        public bool Destination(out Vector3 point)
        {point=transform.position;if(!Active||Host!=null||!CanHost(_candidate))return false;point=_candidate.transform.position;return true;}
        public bool TryAttach(DarwinEntity host,NutritionParasiteSave restore=null)
        {
            if(!Active||!CanHost(host))return false;
            if(restore==null&&EcologyQueries.Distance(transform.position,host.transform.position)>NutritionEcologySettings.Current.attachReach+(host.GetComponent<Collider>()?.bounds.extents.magnitude??0))return false;
            Detach();Host=host;_attached=true;Counts[host]=CountOn(host)+1;_body=GetComponent<Rigidbody>();
            if(_body!=null){_kinematic=restore?.originalKinematic??_body.isKinematic;_detect=restore?.originalDetect??_body.detectCollisions;if(!_body.isKinematic)_body.linearVelocity=Vector3.zero;_body.isKinematic=true;_body.detectCollisions=false;}
            foreach(var renderer in GetComponentsInChildren<Renderer>(true)){_renderers[renderer]=renderer.enabled;renderer.enabled=false;}
            foreach(var collider in GetComponentsInChildren<Collider>(true)){_colliders[collider]=collider.enabled;collider.enabled=false;}
            Follow();return true;
        }
        private void LateUpdate(){if(_attached)Follow();}
        private void Follow(){if(Host!=null){transform.position=Host.transform.position;EntityNeighborhood.ObservePosition(_genes);}}
        public NutritionParasiteSave Capture()=>Host==null?null:new NutritionParasiteSave{host=EcologyIdentity.Of(Host),originalKinematic=_kinematic,originalDetect=_detect};
        public void Detach()
        {
            if(!_attached)return;
            if(!ReferenceEquals(Host,null)&&Counts.TryGetValue(Host,out int count)){if(count<=1)Counts.Remove(Host);else Counts[Host]=count-1;}
            foreach(var item in _renderers)if(item.Key!=null)item.Key.enabled=item.Value;_renderers.Clear();
            foreach(var item in _colliders)if(item.Key!=null)item.Key.enabled=item.Value;_colliders.Clear();
            if(_body!=null){_body.isKinematic=_kinematic;_body.detectCollisions=_detect;}
            _attached=false;Host=null;_candidate=null;_scan=0;
            transform.position=EcologyQueries.Surface(transform.position+Vector3.ProjectOnPlane(Vector3.right,EcologyQueries.Up(transform.position))*NutritionEcologySettings.Current.attachReach);EntityNeighborhood.ObservePosition(_genes);
        }
    }
}
