using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：维持生产者投放时每位王后只读一次活动圆，范围候选不再反复解析群体与缺失组件；保留全体实时坐标和原球面距离语义。
    public static class ProducerPlacementSearch
    {
        public static Vector3 Direction(Vector3 planetCenter,EcologyDefaultsSettings settings)
        {
            foreach(var queen in AntColonyMember.All)
            {
                if(queen.Role!=AntRole.Queen||queen.State.founding)continue;
                var circles=ActivityCircleCache.Read(queen.Population);int count=0;
                foreach(var entity in EntityRegistry.All)
                {
                    if(!EcologyQueries.Alive(entity)||!EcologyEnergyBudget.IsProducer(entity.Genes)||!ColonyActivityRange.Contains(circles,entity.transform.position))continue;
                    if(++count>=settings.localProducerTarget)break;
                }
                if(count>=settings.localProducerTarget)continue;
                float radius=queen.Radius;var up=EcologyQueries.Up(queen.transform.position);var tangent=Vector3.ProjectOnPlane(Random.onUnitSphere,up).normalized;
                var point=queen.transform.position+tangent*Random.Range(Mathf.Min(settings.plantSpacing,radius*.4f),radius*.8f);
                bool crowded=false;float squared=settings.plantSpacing*settings.plantSpacing;
                foreach(var entity in EntityRegistry.All)if(EcologyQueries.Alive(entity)&&(entity.transform.position-point).sqrMagnitude<squared){crowded=true;break;}
                if(!crowded)return (point-planetCenter).normalized;
            }
            return Random.onUnitSphere;
        }
    }
}
