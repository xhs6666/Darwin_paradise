using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：寄生以独立物体跟随宿主并只忽略宿主碰撞；宿主销毁不会连带销毁寄生体。
    [DefaultExecutionOrder(-180)]
    public sealed class ParasiteBehavior : MonoBehaviour
    {
        private EntityGeneController _genes;private DarwinEntity _entity,_candidate;private EcologySettings _settings;
        private Rigidbody _body;private bool _kinematic,_attached,_attachedInternal;private Vector3 _offset;private float _search;
        private bool _detectedByHost;private float _detectionRemaining;
        private readonly List<(Collider parasite,Collider host)> _ignored=new List<(Collider,Collider)>();
        private readonly Dictionary<Renderer,bool> _renderers=new Dictionary<Renderer,bool>();
        public DarwinEntity Host {get;private set;}
        public Vector3 Offset=>_offset;
        public bool DetectedByHost=>_detectedByHost;
        public float DetectionRemaining=>_detectionRemaining;
        public bool VisibleTo(DarwinEntity observer)=>observer!=Host||_detectedByHost;
        public void RestoreDetection(bool detected,float remaining){_detectedByHost=detected;_detectionRemaining=remaining;}
        // ★ 修复（防Bug回归）：热更新或旧组件残留时，新卵寄生逻辑不能继续触发旧的吸血/跟随行为。
        public bool Active=>EcologyQueries.Active(_genes,_settings.parasiticGene)?.Definition.LogicScriptId==DarwinPark.Genetics.NutritionEcologySettings.Current.legacyEggLogic;
        public bool Internal=>EcologyQueries.Active(_genes,_settings.parasiticGene)?.State?.StateId==_settings.internalState;
        public bool OriginalKinematic=>Host!=null?_kinematic:_body!=null&&_body.isKinematic;
        private void Awake(){_genes=GetComponent<EntityGeneController>();_entity=GetComponent<DarwinEntity>();_settings=EcologySettings.Load();EcologyIdentity.Ensure(gameObject);}
        private void Update(){Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);}
        private void OnDisable(){Detach();}
        public void Advance(float dt)
        {
            if(!Active){Detach();return;}if(dt<=0)return;
            if(_body==null)_body=GetComponent<Rigidbody>();
            if(_attached&&(!EcologyQueries.Alive(Host)||_attachedInternal!=Internal))Detach();
            if(Host!=null)
            {
                // ★ 新增：宿主发现按模拟周期抽样并缓存，不能每个 AI 子步重复掷骰而必然发现寄生体。
                _detectionRemaining-=dt;
                while(_detectionRemaining<=0){_detectionRemaining+=Mathf.Max(.01f,_settings.hostSearchInterval);_detectedByHost=Random.value>=Mathf.Clamp01(_genes.GetAttribute("hide_from_host"));}
                var stats=Host.GetComponent<EntityStats>();float requested=Mathf.Max(0,_genes.GetAttribute("host_drain_rate"))*dt;
                stats.TryGet("nutrient",out var food);float drained=Mathf.Min(food,requested);stats.Modify("nutrient",-drained);
                stats.TryGet("storedNutrient",out var stored);float extra=Mathf.Min(stored,requested-drained);stats.Modify("storedNutrient",-extra);
                _entity.GetComponent<EntityStats>().Feed(drained+extra);Follow();return;
            }
            _search-=dt;
            if(_search<=0){_search=_settings.hostSearchInterval;_candidate=FindHost();}
            if(!CanHost(_candidate))return;
            if(Vector3.Distance(transform.position,_candidate.transform.position)<=_settings.attachReach+HostRadius(_candidate)){TryAttach(_candidate);return;}
            if(_body==null||_body.isKinematic)return;
            Vector3 up=EcologyQueries.Up(transform.position),direction=Vector3.ProjectOnPlane(_candidate.transform.position-transform.position,up).normalized;
            /* ★ 适配：与实际交付模拟时间一致。 */ float scale=ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.MotionTimeScale:1;
            _body.linearVelocity=direction*_settings.parasiteSeekSpeed*scale+up*Mathf.Min(0,Vector3.Dot(_body.linearVelocity,up));
        }
        private DarwinEntity FindHost()
        {
            DarwinEntity best=null;float distance=_settings.hostSearchRadius*_settings.hostSearchRadius;
            foreach(var entity in EntityRegistry.All)
            {if(!CanHost(entity))continue;float d=(entity.transform.position-transform.position).sqrMagnitude;if(d<distance){best=entity;distance=d;}}
            return best;
        }
        private bool CanHost(DarwinEntity entity)
        {
            if(!EcologyQueries.Alive(entity)||entity==_entity)return false;
            _entity.GetComponent<EntityStats>().TryGet("size",out var own);entity.GetComponent<EntityStats>().TryGet("size",out var size);
            if(size<Mathf.Max(.01f,own)*_settings.minimumHostSizeRatio)return false;
            var seen=new HashSet<DarwinEntity>();var current=entity;
            while(current!=null){if(current==_entity||!seen.Add(current))return false;current=current.GetComponent<ParasiteBehavior>()?.Host;}
            return true;
        }
        private static float HostRadius(DarwinEntity entity)=>entity.GetComponent<Collider>()?.bounds.extents.magnitude??0;
        public bool TryAttach(DarwinEntity host,bool restoring=false,Vector3 savedOffset=default,bool originalKinematic=false)
        {
            if(!Active||!CanHost(host))return false;
            if(!restoring&&Vector3.Distance(transform.position,host.transform.position)>_settings.attachReach+HostRadius(host))return false;
            Detach();Host=host;_attached=true;_attachedInternal=Internal;_body=GetComponent<Rigidbody>();
            _detectedByHost=!restoring&&Random.value>=Mathf.Clamp01(_genes.GetAttribute("hide_from_host"));_detectionRemaining=_settings.hostSearchInterval;
            if(_body!=null){_kinematic=restoring?originalKinematic:_body.isKinematic;if(!_body.isKinematic){_body.linearVelocity=Vector3.zero;_body.angularVelocity=Vector3.zero;}_body.isKinematic=true;}
            _offset=restoring?savedOffset:host.transform.InverseTransformVector(Internal?Vector3.zero:EcologyQueries.Up(host.transform.position)*(HostRadius(host)+.1f));
            foreach(var parasite in GetComponentsInChildren<Collider>())foreach(var collider in host.GetComponentsInChildren<Collider>())
            {if(parasite==collider)continue;Physics.IgnoreCollision(parasite,collider,true);_ignored.Add((parasite,collider));}
            if(Internal)foreach(var renderer in GetComponentsInChildren<Renderer>()){_renderers[renderer]=renderer.enabled;renderer.enabled=false;}
            Follow();return true;
        }
        private void LateUpdate(){if(Host!=null)Follow();}
        // ★ 适配：宿主跟随可能发生在基因模拟之后，及时同步空间索引。
        private void Follow(){if(Host!=null){transform.position=Host.transform.position+Host.transform.TransformVector(_offset);EntityNeighborhood.ObservePosition(GetComponent<EntityGeneController>());}}
        public void Detach()
        {
            // Host 可能已变成 Unity 的假 null，清理以缓存集合为准，不能只检查 Host != null。
            foreach(var pair in _ignored)if(pair.parasite!=null&&pair.host!=null)Physics.IgnoreCollision(pair.parasite,pair.host,false);
            _ignored.Clear();foreach(var pair in _renderers)if(pair.Key!=null)pair.Key.enabled=pair.Value;_renderers.Clear();
            if(_body!=null&&_attached){_body.isKinematic=_kinematic;if(!_body.isKinematic)_body.linearVelocity=Vector3.zero;}
            _attached=false;Host=null;_candidate=null;_search=0;
        }
    }
}
