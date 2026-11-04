using System.Linq;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Logic;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：吸收只扣实际增加的营养，满仓、部分消化及群体分配不能凭空吞掉剩余尸体。
    public static class NutritionTransfer
    {
        public static float Room(EntityStats stats)=>stats==null?0:stats.CapacityRemaining("nutrient")+stats.CapacityRemaining("storedNutrient");
        public static float Feed(EntityStats stats,float amount)
        {
            float accepted=Mathf.Min(Mathf.Max(0,amount),Room(stats));if(accepted>0)stats.Feed(accepted);return accepted;
        }
        public static bool CanDigest(EntityGeneController genes)
        {
            if(genes==null)return false;
            var settings=NutritionEcologySettings.Current;
            // ★ 适配：食腐替代正常异养消化，攻击能力仍由行动基因授权，攻击本身不再产生额外营养。
            if(EcologyQueries.Active(genes,settings.scavengerGene)!=null)return false;
            var state=EcologyQueries.Active(genes,settings.heterotrophyGene)?.State?.StateId;
            return state==settings.digestState||settings.adultParasitesDigest&&state==settings.parasiteState&&genes.CurrentAge!=AgeState.Infant;
        }
        public static bool CanHuntForFood(EntityGeneController genes)
        {
            var ant=genes.GetComponent<AntColonyMember>();return ant!=null&&ant.Active&&ant.Role==AntRole.Soldier||CanDigest(genes);
        }
        // ★ 适配：行动的食草倾向只挑静止实体，捕猎倾向可挑移动实体；两者都必须有消化食性才能自主猎食。
        public static bool FoodTargetAllowed(EntityGeneController genes,DarwinEntity target)
        {
            if(!CanHuntForFood(genes)||!EcologyQueries.Alive(target))return false;
            var movement=genes.Evaluation?.Genes.FirstOrDefault(g=>g.IsActive&&genes.GetLogic(g.Gene) is LocomotionGeneLogic&&GeneBehaviorAuthority.IsPrimary<LocomotionGeneLogic>(genes,g.Gene));
            return movement?.State?.StateId!="herbivore"||target.Genes.GetAttribute("moveSpeed")<=0;
        }
        public static float Digest(DarwinEntity attacker,float available)
        {
            if(!EcologyQueries.Alive(attacker)||!CanDigest(attacker.Genes))return 0;
            var soldier=attacker.GetComponent<AntColonyMember>();if(soldier!=null&&soldier.Active&&soldier.Role==AntRole.Soldier)return 0;
            float budget=available*Mathf.Clamp01(attacker.Genes.GetAttribute(NutritionEcologySettings.Current.digestionAttribute));
            var population=attacker.GetComponent<PopulationBehavior>();float absorbed=0;
            if(population!=null&&population.Active)
            {
                var recipients=population.GroupMembers.Where(m=>m!=population&&EcologyQueries.Distance(attacker.transform.position,m.transform.position)<=population.Aura).ToArray();
                if(recipients.Length>0)
                {
                    float share=budget*Mathf.Clamp01(EcologySettings.Load().shareFraction)/recipients.Length;
                    foreach(var recipient in recipients)absorbed+=Feed(recipient.GetComponent<EntityStats>(),share);
                }
            }
            return absorbed+Feed(attacker.GetComponent<EntityStats>(),budget-absorbed);
        }
    }
}
