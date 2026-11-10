using System;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Vitals;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.EditorTools
{
    // ★ 新增：用旧完整算法作判定参照，固定随机种子比对稀疏、饱和和迁建状态；不能靠减少生成数量获得性能收益。
    internal static class ProducerPlacementChecks
    {
        private static Vector3 Reference(Vector3 center)
        {
            var settings=EcologyDefaults.Current;
            foreach(var queen in AntColonyMember.All)
            {
                if(queen.Role!=AntRole.Queen||queen.State.founding)continue;int count=0;
                foreach(var entity in EntityRegistry.All)if(EcologyQueries.Alive(entity)&&ColonyActivityRange.Contains(queen.Population,entity.transform.position)&&EcologyEnergyBudget.IsProducer(entity.Genes))count++;
                if(count>=settings.localProducerTarget)continue;
                var up=EcologyQueries.Up(queen.transform.position);var tangent=Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere,up).normalized;
                var point=queen.transform.position+tangent*UnityEngine.Random.Range(Mathf.Min(settings.plantSpacing,queen.Radius*.4f),queen.Radius*.8f);
                bool crowded=false;foreach(var entity in EntityRegistry.All)if(EcologyQueries.Alive(entity)&&(entity.transform.position-point).sqrMagnitude<settings.plantSpacing*settings.plantSpacing){crowded=true;break;}
                if(!crowded)return (point-center).normalized;
            }
            return UnityEngine.Random.onUnitSphere;
        }
        internal static void Verify(AntColonyMember queen,Action<bool,string> check)
        {
            var settings=EcologyDefaults.Current;int target=settings.localProducerTarget;bool founding=queen.State.founding;var random=UnityEngine.Random.state;var center=EcologyQueries.Planet.transform.position;DarwinEntity producer=null;
            try
            {
                void Capture(DarwinEntity entity)=>producer=entity;EntityRegistry.OnRegistered+=Capture;
                try{DarwinEntity.Spawner(EcologyDefaults.Template("producer").CreateGenome(),queen.transform.position,"投放参照生产者");}finally{EntityRegistry.OnRegistered-=Capture;}
                producer.GetComponent<EntityLifeCycle>().RestoreProgress(40);producer.Genes.RestoreStageState(AgeState.Adult,3,2);producer.Genes.Recalculate();
                check(EcologyEnergyBudget.IsProducer(producer.Genes),"饱和场景包含真实成年生产者");
                for(int mode=0;mode<3;mode++)
                {
                    settings.localProducerTarget=mode==0?int.MaxValue:1;queen.State.founding=mode==2;
                    for(int seed=0;seed<5;seed++)
                    {
                        UnityEngine.Random.InitState(98210+seed);long before=ColonyActivityRange.EnumerationCount;var expected=Reference(center);long old=ColonyActivityRange.EnumerationCount-before;
                        UnityEngine.Random.InitState(98210+seed);before=ColonyActivityRange.EnumerationCount;var actual=ProducerPlacementSearch.Direction(center,settings);long now=ColonyActivityRange.EnumerationCount-before;
                        check((actual-expected).sqrMagnitude<1e-10f,"生产者投放与旧算法同种子相同：场景"+mode+" 种子"+seed);
                        if(mode==0&&seed==0)check(old>0&&now==0,"投放范围由逐候选解析改为每王后同步快照");
                    }
                }
                check(OptionalComponent.Read<EggIncubation>(queen)==queen.GetComponent<EggIncubation>(),"可选组件查询保留存在或缺失的原语义");
                var probe=new GameObject("OptionalComponentProbe");var owner=probe.AddComponent<Light>();
                check(OptionalComponent.Read<BoxCollider>(owner)==null,"缺失可选组件返回空");var added=probe.AddComponent<BoxCollider>();check(OptionalComponent.Read<BoxCollider>(owner)==added,"同帧新增可选组件立即可见");UnityEngine.Object.DestroyImmediate(added);check(OptionalComponent.Read<BoxCollider>(owner)==null,"同帧销毁可选组件立即失效");UnityEngine.Object.DestroyImmediate(probe);
            }
            finally{if(producer!=null)UnityEngine.Object.DestroyImmediate(producer.gameObject);settings.localProducerTarget=target;queen.State.founding=founding;UnityEngine.Random.state=random;}
        }
    }
}
