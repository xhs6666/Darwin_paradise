using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class ForeignEggSave {public string parent;public Genome genome;public float remaining;}
    // ★ 新增：宿主仅保存外来卵载荷，不设置宿主IsGestating，不混入宿主基因或宿主自己的孕育计时。
    public sealed class EggHostPregnancy:MonoBehaviour
    {
        private readonly List<ForeignEggSave> _eggs=new List<ForeignEggSave>();
        public int Count=>_eggs.Count;
        public static bool TryDeposit(EntityGeneController parent,Genome offspring)
        {
            // ★ 适配：副本/死亡亲本不能放卵；载荷必须已经由亲本繁殖逻辑构造，绝不回退为宿主基因。
            if(parent==null||WorldAuthority.IsReplica||!EcologyQueries.Alive(parent.GetComponent<DarwinEntity>())||offspring?.Genes==null||offspring.Genes.Count==0)return false;
            var settings=NutritionEcologySettings.Current;DarwinEntity nearest=null;float best=settings.eggHostRadius*settings.eggHostRadius;
            foreach(var host in EntityRegistry.All)
            {
                if(!EcologyQueries.Alive(host)||host.gameObject==parent.gameObject||host.GetComponent<EggIncubation>()?.Incubating==true||host.GetComponent<NutritionParasite>()?.Host!=null||parent.GetComponent<PopulationBehavior>()?.SameGroup(host)==true||(host.GetComponent<EggHostPregnancy>()?.Count??0)>=settings.maximumEggsPerHost)continue;
                float distance=(host.transform.position-parent.transform.position).sqrMagnitude;if(distance<best){best=distance;nearest=host;}
            }
            if(nearest==null)return false;
            var pregnancy=nearest.GetComponent<EggHostPregnancy>()??nearest.gameObject.AddComponent<EggHostPregnancy>();
            pregnancy._eggs.Add(new ForeignEggSave{parent=EcologyIdentity.Of(parent.GetComponent<DarwinEntity>()),genome=JsonUtility.FromJson<Genome>(JsonUtility.ToJson(offspring)),remaining=settings.eggIncubationSeconds/Mathf.Max(.01f,parent.GetAttribute("hatchSpeed"))});return true;
        }
        private void Update(){if(!WorldAuthority.IsReplica)Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);}
        public void Advance(float dt)
        {
            if(dt<=0||!EcologyQueries.Alive(GetComponent<DarwinEntity>()))return;
            for(int i=_eggs.Count-1;i>=0;i--)
            {
                var egg=_eggs[i];egg.remaining=Mathf.Max(0,egg.remaining-dt);if(egg.remaining>0||!EntityPopulationLimit.CanSpawn||DarwinEntity.Spawner==null)continue;
                if(!OffspringPlacement.TryGetPosition(transform.position,false,out var point))continue;
                var before=new HashSet<DarwinEntity>(EntityRegistry.All);DarwinEntity.Spawner(JsonUtility.FromJson<Genome>(JsonUtility.ToJson(egg.genome)),point,"");
                var child=EntityRegistry.All.FirstOrDefault(e=>!before.Contains(e));if(child==null)continue;
                var parent=EcologyIdentity.Find(egg.parent);if(parent!=null)AntColonyMember.OnBirth(parent.Genes,child);
                _eggs.RemoveAt(i);
            }
        }
        public List<ForeignEggSave> Capture()=>_eggs.Select(e=>new ForeignEggSave{parent=e.parent,genome=JsonUtility.FromJson<Genome>(JsonUtility.ToJson(e.genome)),remaining=e.remaining}).ToList();
        // ★ 修复（防Bug回归）：恢复同样深拷贝，推进运行计时不能顺带改写读入的存档/联机快照。
        public void Restore(List<ForeignEggSave> eggs)
        {_eggs.Clear();if(eggs!=null)_eggs.AddRange(eggs.Select(e=>new ForeignEggSave{parent=e.parent,genome=JsonUtility.FromJson<Genome>(JsonUtility.ToJson(e.genome)),remaining=e.remaining}));}
    }
}
