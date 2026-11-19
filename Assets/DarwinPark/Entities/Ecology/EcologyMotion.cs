using DarwinPark.Entities.Simulation; // ★ 适配：同步模拟批内复用组件，不缓存实时状态数值。
using DarwinPark.Entities.Logic;
using DarwinPark.Genetics;
using UnityEngine;
// ★ 修复（防Bug回归）：普通CPU层级确认空组件诊断字符串仍在生命/基因等高频入口分配；正常缺失改TryGet即时读取。
namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：逃离 > 群体命令 > 领地边界 > 配置的实体状态；领地不再无条件抢占觅食。
    public static class EcologyMotion
    {
        public static bool Immobilized(EntityGeneController genes)=>ActiveSimulationBatch.Read<NutritionParasite>(genes)?.Host!=null||ActiveSimulationBatch.Read<ParasiteBehavior>(genes)?.Active==true||ActiveSimulationBatch.Read<EggIncubation>(genes)?.Incubating==true||AntColonyWorker.IsCarried(ActiveSimulationBatch.Read<DarwinEntity>(genes));
        public static bool Directive(EntityGeneController genes,out Vector3 point,out DarwinEntity target,out float multiplier)
        {
            target=null;point=genes.transform.position;multiplier=1;
            // ★ 适配：补给插队先维持生命，原反击/运输命令仍保留，完成后恢复。
            var supplyMember=ActiveSimulationBatch.Read<AntColonyMember>(genes);
            if(supplyMember!=null&&supplyMember.Active&&supplyMember.TrySupplyDestination(out point))return true;
            if(OverrideDirective(genes,out point,out target))return true;
            var population=ActiveSimulationBatch.Read<PopulationBehavior>(genes);
            if(supplyMember!=null&&supplyMember.Destination(out point,out target,out multiplier,false))return true;
            // ★ 适配：普通实体获得独立营养利用基因后也能实际走向能量块，不只等待食物贴到身体旁。
            var energy=ActiveSimulationBatch.Read<EnergyBlockMetabolism>(genes);if(energy!=null&&energy.Destination(out point))return true;
            // ★ 适配：食性选择食物目标，行动基因仍是唯一移动写入者；无行动能力时只能在接触范围吸收。
            if(ActiveSimulationBatch.Read<NutritionParasite>(genes)?.Destination(out point)==true)return true;
            if(ActiveSimulationBatch.Read<CorpseFeeding>(genes)?.Destination(out point)==true)return true;
            var territory=ActiveSimulationBatch.Read<TerritoryBehavior>(genes);
            if(territory!=null&&territory.Active&&territory.EvictionRemaining>0)return territory.Destination(out point,out multiplier);
            if(population!=null&&population.Destination(out point,out target))return true;
            if(territory!=null&&territory.Destination(out point,out multiplier))return true;
            // ★ 修复（防Bug回归）：休息及状态回退不能落回行动基因的自动捕食；反击已在前面由真实受击意图处理。
            if(PatrolRhythm.Defensive(genes))
            {
                if(ActiveSimulationBatch.Read<EntityStateController>(genes)?.Current.Behavior==EntityStateBehavior.TerritoryPatrol&&territory!=null)territory.PatrolDestination(out point);
                return true;
            }
            if(PatrolRhythm.Resting(genes)){point=genes.transform.position;return true;}
            // ★ 适配：仅巡逻实体状态生成巡逻点，捕猎实体状态交给同一行动器追击。
            return EntityStateMotion.Directive(genes,out point,out target,out multiplier);
        }

        // ★ 适配：王后派工和独立中枢共用即时覆盖，受击、RTS、领袖手动命令无需等待普通派工刷新。
        internal static bool OverrideDirective(EntityGeneController genes,out Vector3 point,out DarwinEntity target)
        {
            if(CombatIntent.Destination(genes,out point,out target))return true;
            var rts=ActiveSimulationBatch.Read<RtsOrderController>(genes);
            if(rts?.State.active==true&&rts.Destination(out point,out target))return true;
            var population=ActiveSimulationBatch.Read<PopulationBehavior>(genes);
            return population?.ManualRemaining>0&&population.Destination(out point,out target);
        }
        public static bool CanHunt(EntityGeneController genes,DarwinEntity target)
        {
            // ★ 适配：统一实际体型限制，所有自主捕猎路径共用同一判断。
            if(!PredationSizePolicy.Allows(genes,target)||!AntColonyCombat.CanAttack(ActiveSimulationBatch.Read<DarwinEntity>(genes),target)||AntColonyWorker.IsCarried(target))return false;
            if(ActiveSimulationBatch.Read<PopulationBehavior>(genes)?.SameGroup(target)==true)return false;
            // ★ 修复（防Bug回归）：士兵以王后种群范围巡猎，不能被尚未初始化或留在出生位置的个人领地拒绝合法目标。
            var member=ActiveSimulationBatch.Read<AntColonyMember>(genes);
            if(member!=null&&member.Active)return ColonyActivityRange.Contains(member.Population,target.transform.position);
            var territory=ActiveSimulationBatch.Read<TerritoryBehavior>(genes);return territory==null||territory.AllowsTarget(target.transform.position);
        }
        public static float ResolveAttribute(EntityGeneController genes,string id,float value)
        {
            // ★ 修复（防Bug回归）：移动/攻击后续均为乘法修饰，零基础值无需解析所有群体/附着组件。
            if(value==0&&(id=="moveSpeed"||id=="attack"))return 0;
            // ★ 适配：附着期间不能攻击、跳跃或自行移动，移动只随宿主。
            if((id=="moveSpeed"||id=="attack"||id=="jumpAbility")&&ActiveSimulationBatch.Read<NutritionParasite>(genes)?.Host!=null)return 0;
            // ★ 修复（防Bug回归）：群体和角色只修饰移动/攻击；成长、营养、光环等属性不能绕一遍群体关系，尤其避免光环查询嵌套放大。
            if(id=="jumpAbility")return EntityMovementLimits.Attribute(genes,id,value);
            if(id!="moveSpeed"&&id!="attack")return value;
            if(id=="moveSpeed"&&Immobilized(genes))return 0;
            return ApplyRoleAndPopulation(genes,id,value);
        }

        // ★ 适配：中枢已确认未被搬运、附着或孵化；沿相同速度修饰链推进，无需再检查一遍。
        internal static float MobileSpeed(EntityGeneController genes)
            => genes.Evaluation!=null&&genes.Evaluation.Attributes.TryGetValue("moveSpeed",out float value)&&value!=0
                ? ApplyRoleAndPopulation(genes,"moveSpeed",value) : 0;

        // ★ 适配：缓存表达和角色决定的陆地速度。是否入水仍按当前环境读取，避免用旧速度入水。
        internal static float MobileLandSpeed(EntityGeneController genes)
        {
            if(genes.Evaluation==null||!genes.Evaluation.Attributes.TryGetValue("moveSpeed",out float value)||value<=0)return 0;
            value=AntColonyMember.Attribute(genes,"moveSpeed",value);
            var population=ActiveSimulationBatch.Read<PopulationBehavior>(genes);
            return EntityMovementLimits.Speed(value*(population?.MoveMultiplier??1));
        }
        internal static float CurrentMobileSpeed(EntityGeneController genes,float landSpeed)
            => landSpeed<=0?0:EntityMovementLimits.Attribute(genes,"moveSpeed",landSpeed);

        private static float ApplyRoleAndPopulation(EntityGeneController genes,string id,float value)
        {
            // ★ 修复（防Bug回归）：固定分工已把移动/攻击乘为零时，不再解析领袖光环；后续乘法保持零。
            value=AntColonyMember.Attribute(genes,id,value);if(value==0)return 0;
            var population=ActiveSimulationBatch.Read<PopulationBehavior>(genes);
            if(id=="moveSpeed"&&population!=null)value*=population.MoveMultiplier;
            if(id=="attack"&&population!=null)value*=population.AttackMultiplier;
            return EntityMovementLimits.Attribute(genes,id,value);
        }
        public static bool Detectable(DarwinEntity target,Vector3 observer,float range,DarwinEntity observerEntity=null)
        {
            // ★ 适配：体内营养寄生取消渲染与世界碰撞，不能被自主捕猎当作外露实体。
            if(DarwinPark.Core.OptionalComponent.Read<NutritionParasite>(target)?.Host!=null)return false;
            var parasite=DarwinPark.Core.OptionalComponent.Read<ParasiteBehavior>(target);
            if(parasite!=null&&parasite.Host!=null&&!parasite.VisibleTo(observerEntity))return false;
            float conceal=parasite!=null&&parasite.Host!=null?Mathf.Clamp01(target.Genes.GetAttribute("hide_from_host")):0;
            return (target.transform.position-observer).sqrMagnitude<=range*range*(1-conceal)*(1-conceal);
        }
    }
}
