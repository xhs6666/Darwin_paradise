using DarwinPark.Entities.Logic;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：逃离 > 群体命令 > 领地边界 > 配置的实体状态；领地不再无条件抢占觅食。
    public static class EcologyMotion
    {
        public static bool Immobilized(EntityGeneController genes)=>genes.GetComponent<NutritionParasite>()?.Host!=null||genes.GetComponent<ParasiteBehavior>()?.Active==true||genes.GetComponent<EggIncubation>()?.Incubating==true||AntColonyWorker.IsCarried(genes.GetComponent<DarwinEntity>());
        public static bool Directive(EntityGeneController genes,out Vector3 point,out DarwinEntity target,out float multiplier)
        {
            target=null;point=genes.transform.position;multiplier=1;
            // ★ 适配：反击使用真实目标，固定分工随后接管意图，不落回普通随机漫游。
            if(CombatIntent.Destination(genes,out point,out target))return true;
            // ★ 适配：RTS接入同一行动器，指令不能绕过基因速度、伤害与搬运规则。
            var rts=genes.GetComponent<RtsOrderController>();if(rts!=null&&rts.Destination(out point,out target))return true;
            var ant=genes.GetComponent<AntColonyMember>();if(ant!=null&&ant.Destination(out point,out target,out multiplier))return true;
            // ★ 适配：普通实体获得独立营养利用基因后也能实际走向能量块，不只等待食物贴到身体旁。
            var energy=genes.GetComponent<EnergyBlockMetabolism>();if(energy!=null&&energy.Destination(out point))return true;
            // ★ 适配：食性选择食物目标，行动基因仍是唯一移动写入者；无行动能力时只能在接触范围吸收。
            if(genes.GetComponent<NutritionParasite>()?.Destination(out point)==true)return true;
            if(genes.GetComponent<CorpseFeeding>()?.Destination(out point)==true)return true;
            var territory=genes.GetComponent<TerritoryBehavior>();
            if(territory!=null&&territory.Active&&territory.EvictionRemaining>0)return territory.Destination(out point,out multiplier);
            var population=genes.GetComponent<PopulationBehavior>();
            if(population!=null&&population.Destination(out point,out target))return true;
            if(territory!=null&&territory.Destination(out point,out multiplier))return true;
            // ★ 修复（防Bug回归）：休息及状态回退不能落回行动基因的自动捕食；反击已在前面由真实受击意图处理。
            if(PatrolRhythm.Defensive(genes))
            {
                if(genes.GetComponent<EntityStateController>()?.Current.Behavior==EntityStateBehavior.TerritoryPatrol)territory.PatrolDestination(out point);
                return true;
            }
            if(PatrolRhythm.Resting(genes)){point=genes.transform.position;return true;}
            // ★ 适配：仅巡逻实体状态生成巡逻点，捕猎实体状态交给同一行动器追击。
            return EntityStateMotion.Directive(genes,out point,out target,out multiplier);
        }
        public static bool CanHunt(EntityGeneController genes,DarwinEntity target)
        {
            // ★ 适配：统一实际体型限制，所有自主捕猎路径共用同一判断。
            if(!PredationSizePolicy.Allows(genes,target)||!AntColonyCombat.CanAttack(genes.GetComponent<DarwinEntity>(),target)||AntColonyWorker.IsCarried(target))return false;
            if(genes.GetComponent<PopulationBehavior>()?.SameGroup(target)==true)return false;
            // ★ 修复（防Bug回归）：士兵以王后种群范围巡猎，不能被尚未初始化或留在出生位置的个人领地拒绝合法目标。
            var member=genes.GetComponent<AntColonyMember>();
            if(member!=null&&member.Active)return ColonyActivityRange.Contains(member.Population,target.transform.position);
            var territory=genes.GetComponent<TerritoryBehavior>();return territory==null||territory.AllowsTarget(target.transform.position);
        }
        public static float ResolveAttribute(EntityGeneController genes,string id,float value)
        {
            // ★ 适配：附着期间不能攻击、跳跃或自行移动，移动只随宿主。
            if((id=="moveSpeed"||id=="attack"||id=="jumpAbility")&&genes.GetComponent<NutritionParasite>()?.Host!=null)return 0;
            // ★ 修复（防Bug回归）：群体和角色只修饰移动/攻击；成长、营养、光环等属性不能绕一遍群体关系，尤其避免光环查询嵌套放大。
            if(id=="jumpAbility")return EntityMovementLimits.Attribute(genes,id,value);
            if(id!="moveSpeed"&&id!="attack")return value;
            var population=genes.GetComponent<PopulationBehavior>();
            if(id=="moveSpeed"&&population!=null)value*=population.MoveMultiplier;
            if(id=="attack"&&population!=null)value*=population.AttackMultiplier;
            if(id=="moveSpeed"&&Immobilized(genes))return 0;
            return EntityMovementLimits.Attribute(genes,id,AntColonyMember.Attribute(genes,id,value));
        }
        public static bool Detectable(DarwinEntity target,Vector3 observer,float range,DarwinEntity observerEntity=null)
        {
            // ★ 适配：体内营养寄生取消渲染与世界碰撞，不能被自主捕猎当作外露实体。
            if(target.GetComponent<NutritionParasite>()?.Host!=null)return false;
            var parasite=target.GetComponent<ParasiteBehavior>();
            if(parasite!=null&&parasite.Host!=null&&!parasite.VisibleTo(observerEntity))return false;
            float conceal=parasite!=null&&parasite.Host!=null?Mathf.Clamp01(target.Genes.GetAttribute("hide_from_host")):0;
            return (target.transform.position-observer).sqrMagnitude<=range*range*(1-conceal)*(1-conceal);
        }
    }
}
