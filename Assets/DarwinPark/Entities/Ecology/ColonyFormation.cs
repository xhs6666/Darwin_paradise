using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：休息/跟随不再以王后身体为共同终点；出生避开占位，行动只添加球面切向避让，不传送成员。
    public static class ColonyFormation
    {
        [Serializable] private sealed class Config {public float spacing=2.4f,margin=.35f,avoidanceRadius=6,avoidanceStrength=1.8f,birthRadiusRatio=.9f;public int birthAttempts=32;}
        private static Config _config;
        private static Config Settings
        {
            get
            {
                if(_config!=null)return _config;var asset=Resources.Load<TextAsset>("Entities/colony_formation");if(asset==null)throw new InvalidOperationException("缺少 Entities/colony_formation");
                var value=JsonUtility.FromJson<Config>(asset.text);if(value==null||new[]{value.spacing,value.margin,value.avoidanceRadius,value.avoidanceStrength,value.birthRadiusRatio}.Any(v=>!float.IsFinite(v)||v<=0)||value.birthRadiusRatio>=1||value.birthAttempts<1)throw new InvalidOperationException("蚁群队形配置无效");return _config=value;
            }
        }
        private static readonly Dictionary<AntColonyMember,int> Slots=new();private static readonly HashSet<string> Indexed=new();private static long _revision=-1;
        private static readonly List<DarwinEntity> Neighbors=new();
        private static int Slot(AntColonyMember member)
        {
            if(_revision!=PopulationGroupIndex.Revision){Slots.Clear();Indexed.Clear();_revision=PopulationGroupIndex.Revision;}
            if(Indexed.Add(member.GroupId)){int i=0;foreach(var peer in member.Group.OrderBy(m=>m.Population.JoinOrder).ThenBy(m=>m.Id))Slots[peer]=i++;}
            return Slots.TryGetValue(member,out int slot)?slot:0;
        }
        private static Vector3 Offset(Vector3 center,float radius,float angle)
        {
            var up=EcologyQueries.Up(center);var side=Vector3.Cross(up,Mathf.Abs(up.y)>.9f?Vector3.right:Vector3.up).normalized;
            var offset=(side*Mathf.Cos(angle)+Vector3.Cross(up,side)*Mathf.Sin(angle))*radius;
            var planet=EcologyQueries.Planet;return planet!=null?planet.transform.position+(center-planet.transform.position+offset).normalized*(center-planet.transform.position).magnitude:center+offset;
        }
        public static Vector3 Home(AntColonyMember member,Vector3 center,float radius)
        {
            int slot=Slot(member)+1;float distance=Mathf.Min(radius,Settings.spacing*Mathf.Sqrt(slot));
            return Offset(center,distance,slot*2.39996323f);
        }
        public static float BodyRadius(DarwinEntity entity)
        {var col=entity.GetComponent<Collider>();return col!=null?Mathf.Max(col.bounds.extents.x,col.bounds.extents.y,col.bounds.extents.z):Settings.spacing*.25f;}
        public static Vector3 Avoid(EntityGeneController genes,Vector3 desired,float speed)
        {
            var member=genes.GetComponent<AntColonyMember>();if(member==null||!member.Active||member.Role==AntRole.Queen||member.Young||speed<=0)return desired;
            var here=genes.transform.position;var up=EcologyQueries.Up(here);var away=Vector3.zero;float radius=BodyRadius(member.Entity);EntityNeighborhood.Query(here,Settings.avoidanceRadius,Neighbors);
            foreach(var other in Neighbors)
            {
                if(other==member.Entity||!EcologyQueries.Alive(other)||!member.Population.SameGroup(other)||IsHidden(other))continue;
                Vector3 delta=Vector3.ProjectOnPlane(here-other.transform.position,up);float distance=delta.magnitude,clearance=Mathf.Max(Settings.spacing*.5f,radius+BodyRadius(other)+Settings.margin);
                if(distance>=clearance)continue;
                if(distance<.001f){bool first=string.CompareOrdinal(member.Id,EcologyIdentity.Of(other))<0;delta=Vector3.ProjectOnPlane(Mathf.Abs(up.y)>.9f?Vector3.right:Vector3.up,up).normalized*(first?1:-1);}
                away+=delta.normalized*(1-distance/clearance);
            }
            Neighbors.Clear();return Vector3.ClampMagnitude(desired+away*speed*Settings.avoidanceStrength,speed);
        }
        private static bool IsHidden(DarwinEntity entity)=>entity.GetComponent<NutritionParasite>()?.Host!=null||AntColonyWorker.IsCarried(entity);
        public static bool TryBirthPosition(AntColonyMember queen,out Vector3 point)
        {
            float body=BodyRadius(queen.Entity),minimum=Mathf.Max(Settings.spacing,body*2+Settings.margin),maximum=Mathf.Max(minimum,AntColonySettings.Current.feedRadius*Settings.birthRadiusRatio);
            for(int attempt=0;attempt<Settings.birthAttempts;attempt++)
            {
                point=EcologyQueries.Surface(Offset(queen.transform.position,UnityEngine.Random.Range(minimum,maximum),UnityEngine.Random.Range(0,Mathf.PI*2)));
                EntityNeighborhood.Query(point,Settings.avoidanceRadius,Neighbors);bool free=true;
                foreach(var entity in Neighbors)if(EcologyQueries.Alive(entity)&&!IsHidden(entity)&&EcologyQueries.Distance(point,entity.transform.position)<Mathf.Max(Settings.spacing,body+BodyRadius(entity)+Settings.margin)){free=false;break;}
                Neighbors.Clear();if(free)return true;
            }
            point=default;return false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset(){Slots.Clear();Indexed.Clear();Neighbors.Clear();_revision=-1;_config=null;}
    }
}
