using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 适配：通用能量块独立于育幼/成年专用补给，旧存档枚举编号保持不变。
    public enum ColonyResourceKind { Corpse,Nursery,Supply,Energy }
    [Serializable] public sealed class ColonyResourceSave
    {
        public string id,group,claim;public ColonyResourceKind kind;public Vector3 local,scale;public float energy;
    }
    // ★ 新增：尸体与能量块是独立世界物件；能量只能由尸体/被搬运植株转换，不能重复领取击杀奖励。
    public sealed class ColonyResource : MonoBehaviour
    {
        private static readonly List<ColonyResource> ResourcesInWorld=new List<ColonyResource>();
        private static readonly Dictionary<string,ColonyResource> ById=new Dictionary<string,ColonyResource>();
        private static Material _material;
        public ColonyResourceSave State {get;private set;}
        public static IEnumerable<ColonyResource> All=>ResourcesInWorld.Where(r=>r!=null&&r.isActiveAndEnabled&&r.State.energy>0);
        public static ColonyResource Find(string id)=>!string.IsNullOrEmpty(id)&&ById.TryGetValue(id,out var resource)&&resource!=null&&resource.isActiveAndEnabled&&resource.State.energy>0?resource:null;
        private void OnEnable(){if(!ResourcesInWorld.Contains(this))ResourcesInWorld.Add(this);if(State!=null)ById[State.id]=this;}
        private void OnDisable(){ResourcesInWorld.Remove(this);if(State!=null)ById.Remove(State.id);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset(){ResourcesInWorld.Clear();ById.Clear();_material=null;}
        public static float Biomass(DarwinEntity entity)
        {
            var settings=AntColonySettings.Current;var stats=entity.GetComponent<EntityStats>();stats.TryGet("size",out float size);stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);
            return Mathf.Max(settings.minimumBiomass,size*settings.biomassPerSize+Mathf.Max(0,food)+Mathf.Max(0,reserve));
        }
        public static ColonyResource LeaveCorpse(DarwinEntity victim,string group)
        {
            return Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=group,kind=ColonyResourceKind.Corpse,
                local=EcologyQueries.ToLocal(victim.transform.position),scale=victim.transform.lossyScale,energy=Biomass(victim)});
        }
        public static ColonyResource Create(ColonyResourceSave data)
        {
            var obj=GameObject.CreatePrimitive(data.kind==ColonyResourceKind.Corpse?PrimitiveType.Sphere:PrimitiveType.Cube);
            obj.name=data.kind==ColonyResourceKind.Corpse?"可搬运尸体":data.kind==ColonyResourceKind.Energy?"能量块":"种群能量块";
            var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            if(EcologyQueries.Planet!=null)obj.transform.SetParent(EcologyQueries.Planet.transform,false);
            obj.transform.position=EcologyQueries.ToWorld(data.local);obj.transform.localScale=data.kind==ColonyResourceKind.Corpse?data.scale:Vector3.one*AntColonySettings.Current.energyBlockSize;
            if(_material==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
                if(shader==null)throw new InvalidOperationException("能量块缺少可用材质 Shader");
                _material=new Material(shader){name="Colony Resource"};_material.EnableKeyword("_EMISSION");
            }
            var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=_material;var block=new MaterialPropertyBlock();
            ColorUtility.TryParseHtmlString(AntColonySettings.Current.energyColor,out var energy);
            var color=data.kind==ColonyResourceKind.Corpse?new Color(.28f,.25f,.2f):energy;
            block.SetColor("_BaseColor",color);block.SetColor("_Color",color);block.SetColor("_EmissionColor",data.kind==ColonyResourceKind.Corpse?Color.black:energy*AntColonySettings.Current.glowIntensity);renderer.SetPropertyBlock(block);
            if(data.kind!=ColonyResourceKind.Corpse)
            {var light=obj.AddComponent<Light>();light.type=LightType.Point;light.color=energy;light.intensity=AntColonySettings.Current.glowIntensity;light.range=AntColonySettings.Current.glowRange;light.shadows=LightShadows.None;}
            var resource=obj.AddComponent<ColonyResource>();resource.State=data;ById[data.id]=resource;return resource;
        }
        public bool Claim(AntColonyMember worker)
        {
            if(State.kind!=ColonyResourceKind.Corpse||State.group!=worker.GroupId)return false;
            if(!string.IsNullOrEmpty(State.claim)&&State.claim!=worker.Id)
            {
                // ★ 适配：领取者按持久 ID 直接定位，竞争一具尸体时不再让每名工匠扫描全部种群。
                var owner=EcologyIdentity.Find(State.claim)?.GetComponent<AntColonyMember>();
                if(owner!=null&&owner.Active&&owner.Role==AntRole.Worker)return false;
            }
            State.claim=worker.Id;return true;
        }
        public void Release(string owner){if(State.claim==owner)State.claim=null;}
        public void CarryTo(Vector3 point){transform.position=point;State.local=EcologyQueries.ToLocal(point);}
        public static ColonyResource Convert(string group,Vector3 point,float energy,bool nursery)
        {
            var kind=nursery?ColonyResourceKind.Nursery:ColonyResourceKind.Supply;
            var existing=All.FirstOrDefault(r=>r.State.group==group&&r.State.kind==kind&&EcologyQueries.Distance(r.transform.position,point)<=AntColonySettings.Current.arrivalDistance);
            if(existing!=null){existing.State.energy+=energy;return existing;}
            return Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=group,kind=kind,energy=energy,local=EcologyQueries.ToLocal(point),scale=Vector3.one});
        }
        public static void Feed(AntColonyMember member,float dt)
        {
            // ★ 修复（防Bug回归）：补给目的地与实际吸收条件必须一致，受抑制营养基因不影响共享能量的直接进食。
            if(dt<=0)return;
            var resource=ColonyFeeding.Find(member,true);
            if(resource==null)return;
            var stats=member.GetComponent<EntityStats>();var settings=AntColonySettings.Current;
            // ★ 修复（防Bug回归）：按实际剩余容量扣块能量，不能让满仓的成员吞掉共享储备。
            var capacity=stats.CaptureValues();float room=capacity.Where(v=>v.id=="nutrient"||v.id=="storedNutrient").Sum(v=>Mathf.Max(0,v.max-v.value));
            float amount=Mathf.Min(ColonyFeeding.AvailableEnergy(resource,member),settings.feedRate*dt,room);if(amount<=0)return;
            // ★ 修复（防Bug回归）：只有实际抵达进食才领取废弃资源，寻路预览不能抢走别群正在回收的补给；新归属沿原存档字段保存。
            resource.State.group=member.GroupId;
            resource.State.energy-=amount;stats.Feed(amount);if(resource.State.energy<=0)resource.Remove();
        }
        public void Remove(){gameObject.SetActive(false);Destroy(gameObject);}
        public static void ClearWorld(){foreach(var resource in All.ToArray())resource.Remove();ResourcesInWorld.Clear();ById.Clear();}
        public ColonyResourceSave Capture(){State.local=EcologyQueries.ToLocal(transform.position);return JsonUtility.FromJson<ColonyResourceSave>(JsonUtility.ToJson(State));}
    }
}
