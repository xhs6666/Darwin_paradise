using System.Collections;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.UI
{
    // ★ 新增：仅新建世界投放一次少量种源；这不是持续补充规则，旧存档和灭绝后均不重发。
    public sealed class NaturalWorldSeeder:MonoBehaviour
    {
        public static void Begin(WorldSessionManager session)
        {
            if(!NaturalPopulationPace.Current.enabled||WorldAuthority.IsReplica)return;
            // ★ 修复（防Bug回归）：组件随会话复用，连续新建世界不留下多个后台种源协程。
            var seed=session.GetComponent<NaturalWorldSeeder>()??session.gameObject.AddComponent<NaturalWorldSeeder>();seed.StopAllCoroutines();seed.StartCoroutine(Seed(session));
        }
        private static IEnumerator Seed(WorldSessionManager session)
        {
            string world=session.Current.id;var cfg=NaturalPopulationPace.Current;var planet=EcologyQueries.Planet;Vector3 home=Vector3.zero;bool found=false;
            for(int i=0;i<512;i++)
            {
                float y=1-2*(i+.5f)/512,angle=i*2.39996323f;var up=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));var point=EcologyQueries.Surface(planet.transform.position+up*(planet.radius+planet.heightScale));var climate=PlanetClimateSystem.Instance.GetClimateAt(point);
                // ★ 修复（防Bug回归）：种源和局部落点必须避开内陆河湖，不能只检查海平面。
                if((point-planet.transform.position).magnitude>planet.LocalWaterRadius(point)+2&&climate.Temperature>8&&climate.Temperature<32){home=point;found=true;break;}if(i%32==31)yield return null;
            }
            if(!found){Debug.LogWarning("[自然种源] 没有适宜陆地，保留空世界供手动投放");yield break;}
            int producers=Mathf.RoundToInt(cfg.initialPopulation*EcologyDefaults.Current.producerRatio),made=0;var templates=EcologyDefaults.Current.templates;
            for(int attempt=0;made<cfg.initialPopulation&&attempt<cfg.initialPopulation*64;attempt++)
            {
                if(session.Current?.id!=world||!WorldRuntimeGate.Ready)yield break;if(!EntityPopulationLimit.CanSpawn||DarwinEntity.Spawner==null)break;var direction=EcologyQueries.Up(home);var tangent=Vector3.ProjectOnPlane(Random.onUnitSphere,direction).normalized;var point=EcologyQueries.Surface(home+tangent*(4+Mathf.Sqrt(attempt+1)*3));
                if((point-planet.transform.position).magnitude<=planet.LocalWaterRadius(point)+1)continue;
                string id=made<producers?cfg.producerTemplate:cfg.mobileTemplate;var template=templates.FirstOrDefault(x=>x.id==id);
                if(template==null)
                {
                    var bundled=Resources.Load<TextAsset>("Entities/ecology_defaults");template=bundled==null?null:EcologyDefaults.Parse(bundled.text).templates.FirstOrDefault(x=>x.id==id);
                    if(template==null){Debug.LogWarning("[自然种源] 缺少初始模板："+id);break;}
                    Debug.LogWarning("[自然种源] 世界模板缺少"+id+"，使用内置同名种源；不覆盖世界模板");
                }
                var genome=template.Recipe().CreateGenome();var child=EntitySpawnCapture.Create(genome,point,template.name);
                if(child!=null){child.GetComponent<EntityLifeCycle>().RestoreProgress(0);child.Genes.RestoreStageState(DarwinPark.Entities.AgeState.Infant,1,1);child.Genes.Recalculate();made++;}yield return null;
            }
            if(session.Current?.id==world){session.SaveNow();Debug.Log("[自然种源] 已建立初始种源 "+made+"；后续依靠自然繁殖");}
        }
    }
}
