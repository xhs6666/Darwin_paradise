using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：一次同步投放片段共用活体/生产者坐标和缺粮王后列表；跨帧、出生/注销后重新采集，不冻结世界实体。
    public sealed class ProducerPlacementSnapshot
    {
        private readonly struct Home
        {public readonly Vector3 point,up;public readonly float radius;public Home(AntColonyMember queen){point=queen.transform.position;up=EcologyQueries.Up(point);radius=queen.Radius;}}
        private readonly struct Area{public readonly Vector3 point,up;public readonly float radius;public Area(ActivityCircle circle,Vector3 center){point=circle.Center;up=(point-center).normalized;radius=circle.Radius;}}
        private readonly List<Area> _areas=new();private readonly List<Vector3> _living=new(),_producers=new(),_producerUps=new();private readonly List<Home> _homes=new();
        private int _frame=-1,_target;private long _membership=-1;private EcologyDefaultsSettings _settings;
        public int Builds{get;private set;}public long ProducerChecks{get;private set;}
        public void Invalidate(){_frame=-1;}
        private void Prepare(EcologyDefaultsSettings settings)
        {
            if(_frame==Time.frameCount&&_membership==EntityRegistry.MembershipRevision&&ReferenceEquals(settings,_settings)&&_target==settings.localProducerTarget)return;
            _living.Clear();_producers.Clear();_producerUps.Clear();_homes.Clear();
            var planet=EcologyQueries.Planet;bool spherical=planet!=null;var center=spherical?planet.transform.position:Vector3.zero;float scale=spherical?Mathf.Max(planet.radius,planet.WaterRadius):0;
            foreach(var entity in EntityRegistry.All)if(EcologyQueries.Alive(entity)){var point=entity.transform.position;_living.Add(point);if(EcologyEnergyBudget.IsProducer(entity.Genes)){_producers.Add(point);_producerUps.Add((point-center).normalized);}}
            foreach(var queen in AntColonyMember.All)
            {
                if(queen.Role!=AntRole.Queen||queen.State.founding)continue;var circles=ActivityCircleCache.Read(queen.Population);int count=0;
                // ★ 修复（防Bug回归）：球面角距继续用原acos判定，但球心/水位和单位方向只读一次，候选循环不再跨Unity边界。
                _areas.Clear();foreach(var circle in circles)_areas.Add(new Area(circle,center));
                for(int i=0;i<_producers.Count;i++)
                {
                    ProducerChecks++;bool inside=false;foreach(var area in _areas){float distance=spherical?Mathf.Acos(Mathf.Clamp(Vector3.Dot(area.up,_producerUps[i]),-1,1))*scale:Vector3.Distance(area.point,_producers[i]);if(distance<=area.radius){inside=true;break;}}
                    if(inside&&++count>=settings.localProducerTarget)break;
                }
                if(count<settings.localProducerTarget)_homes.Add(new Home(queen));
            }
            _frame=Time.frameCount;_membership=EntityRegistry.MembershipRevision;_settings=settings;_target=settings.localProducerTarget;Builds++;
        }
        public Vector3 Direction(Vector3 center,EcologyDefaultsSettings settings)
        {
            Prepare(settings);float squared=settings.plantSpacing*settings.plantSpacing;
            foreach(var home in _homes)
            {
                var tangent=Vector3.ProjectOnPlane(Random.onUnitSphere,home.up).normalized;
                var point=home.point+tangent*Random.Range(Mathf.Min(settings.plantSpacing,home.radius*.4f),home.radius*.8f);bool crowded=false;
                foreach(var occupied in _living)if((occupied-point).sqrMagnitude<squared){crowded=true;break;}
                if(!crowded)return (point-center).normalized;
            }
            return Random.onUnitSphere;
        }
    }
}
