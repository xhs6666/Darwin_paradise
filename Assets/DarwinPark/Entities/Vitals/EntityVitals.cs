using UnityEngine;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using DarwinPark.Planet;

// ★ 修复（防Bug回归）：普通CPU层级确认空组件诊断字符串仍在生命/基因等高频入口分配；正常缺失改TryGet即时读取。
namespace DarwinPark.Entities.Vitals
{
    public class EntityVitals : MonoBehaviour
    {
        private EntityStats _stats;
        private EntityGeneController _genes;

        [Header("代谢设置")]
        public float baseMetabolism = 1.0f;
        // ★ 修复（防Bug回归）：原移动消耗每秒约为一口猎食收益的数倍，改为可持续的运动代谢。
        public float moveMetabolism = 0.06f;

        private void Awake()
        {
            _stats = GetComponent<EntityStats>();
            _genes = GetComponent<EntityGeneController>();
        }

        private void Update()
        {
            // ★ 修复（防Bug回归）：副本仅展示房主快照，不能额外结算本机营养或环境伤害。
            if(WorldAuthority.IsReplica)return;
            // ★ 适配：静止合并组由代表结算，不能同帧再扣一次个体代谢。
            if(Simulation.EntityWorkScheduler.Dormant(_genes))return;
            bool running = !ServiceLocator.TryGet<SimulationClock>(out var clock) || clock.IsRunning;
            if (!running) return;

            // ★ 修复（防Bug回归）：代谢、修复及饥饿伤害随模拟倍率推进。
            float dt = clock != null ? clock.GameDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            AdvanceShared(dt);
        }

        // ★ 适配：同一次生命结算内物理姿态不变，移动代谢只取样一次。饱食、修复和伤害仍按原小步结算。
        public void AdvanceShared(float dt)
        {
            int steps=SimulationSubsteps.Count(dt);
            if(steps==0)return;
            if(IsIncubating()){AdvanceIncubating();return;}
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.Vitals);
            float rate=Ecology.EcologyEnergyBudget.Consumption(_genes,baseMetabolism,moveMetabolism);
            for(int i=0;i<steps&&gameObject.activeInHierarchy;i++)
                ApplyVitalsStep(SimulationSubsteps.Slice(dt,i,steps),rate);
        }

        private bool IsIncubating()=>OptionalComponent.Read<Ecology.EggIncubation>(this)?.Incubating==true;
        private void AdvanceIncubating()
        {
            if(_stats.TryGet("health",out float health)&&health<=0)
                Ecology.EntityDeathSettlement.Die(GetComponent<DarwinEntity>());
        }

        private void AdvanceVitals(float dt)
        {
            if(IsIncubating()){AdvanceIncubating();return;}
            ApplyVitalsStep(dt,Ecology.EcologyEnergyBudget.Consumption(_genes,baseMetabolism,moveMetabolism));
        }

        private void ApplyVitalsStep(float dt,float consumptionRate)
        {
            // 1. 营养吸收与消耗
            // ★ 修复（防Bug回归）：营养基因已结算生产，此处重复凭空产粮会掩盖生态收支；这里只计算消耗。
            float consumed = consumptionRate*dt;
            _stats.Modify("nutrient", -consumed);

            // 核心机制 2：饱食度低于 4 时，自动从仓库提取
            if (_stats.TryGet("nutrient", out var hunger) && _stats.TryGet("storedNutrient", out var stored))
            {
                if (hunger < 4f && stored > 0f)
                {
                    // 从仓库提取 10 点填满上限为 10 的饱食度
                    float transfer = Mathf.Min(10f * dt, stored);
                    _stats.Modify("storedNutrient", -transfer);
                    _stats.Modify("nutrient", transfer);
                }
            }

            // 修复逻辑
            // ★ 修复（防Bug回归）：满血或无口粮时不能修复，无须为每个健康个体重复查询两项基因属性。
            if (_stats.TryGet("health", out var hp) && hp<100f && _stats.TryGet("nutrient", out var nut2) && nut2>5f)
            {
                float repairEfficiency = _genes.GetAttribute("repairEfficiency");
                float repairCapability = _genes.GetAttribute("repairCapability");
                // ★ 修复（防Bug回归）：缺氧溺水期间暂停自然修复，避免持续伤害被同帧修复抵消成“不掉血”。
                if (hp < 100f && hp > repairCapability && nut2 > 5f && repairEfficiency > 0f &&
                    GetComponent<EntityEnvironmentHazard>()?.IsInWater != true)
                {
                    float hpToRepair = repairEfficiency * dt;
                    _stats.Modify("health", hpToRepair);
                    // ★ 适配：修复和饥饿数值与行动代谢一起由全局配置管理。
                    _stats.Modify("nutrient", -hpToRepair * GameplayTuning.Current.repairFoodPerHealth);
                }
            }

            // ★ 修复（防Bug回归）：旧版用 heightScale * seaLevelRatio 估算水位，
            // 与真实海平面 Data.SeaLevel 不一致，导致陆地低地被误判为水下。
            // 溺水与温度伤害已迁移到 EntityEnvironmentHazard，由 EnvironmentConfig 数据驱动。

            if (_stats.TryGet("nutrient", out var finalNutrient) && finalNutrient <= 0f)
                _stats.Modify("health", -GameplayTuning.Current.starvationDamagePerSecond * dt);

            if (_stats.TryGet("health", out var finalHp) && finalHp <= 0f)
                Ecology.EntityDeathSettlement.Die(GetComponent<DarwinEntity>());
        }
    }
}
