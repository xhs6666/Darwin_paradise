using UnityEngine;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using DarwinPark.Planet;

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
            bool running = !ServiceLocator.TryGet<SimulationClock>(out var clock) || clock.IsRunning;
            if (!running) return;

            // ★ 修复（防Bug回归）：代谢、修复及饥饿伤害随模拟倍率推进。
            float dt = clock != null ? clock.GameDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            // ★ 修复（防Bug回归）：高倍率也按小步结算容量、仓储与修复阈值，避免一次大步吞掉营养溢出。
            while (dt > 0f)
            {
                float step = Mathf.Min(dt, .1f); dt -= step;
                AdvanceVitals(step);
            }
        }

        private void AdvanceVitals(float dt)
        {
            // ★ 适配：卵不进行自主觅食/运动代谢，但保留生命值与可被攻击破坏的碰撞体。
            if(GetComponent<DarwinPark.Entities.Ecology.EggIncubation>()?.Incubating==true)
            {if(_stats.TryGet("health",out var eggHealth)&&eggHealth<=0)Destroy(gameObject);return;}
            // 1. 营养吸收与消耗
            // ★ 修复（防Bug回归）：营养基因已结算生产，此处重复凭空产粮会掩盖生态收支；这里只计算消耗。
            float consumed = Ecology.EcologyEnergyBudget.Consumption(_genes,baseMetabolism,moveMetabolism)*dt;
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
            float repairEfficiency = _genes.GetAttribute("repairEfficiency");
            float repairCapability = _genes.GetAttribute("repairCapability");
            if (_stats.TryGet("health", out var hp) && _stats.TryGet("nutrient", out var nut2))
            {
                // ★ 修复（防Bug回归）：缺氧溺水期间暂停自然修复，避免持续伤害被同帧修复抵消成“不掉血”。
                if (hp < 100f && hp > repairCapability && nut2 > 5f && repairEfficiency > 0f &&
                    GetComponent<EntityEnvironmentHazard>()?.IsDrowning != true)
                {
                    float hpToRepair = repairEfficiency * dt;
                    _stats.Modify("health", hpToRepair);
                    _stats.Modify("nutrient", -hpToRepair * 0.5f);
                }
            }

            // ★ 修复（防Bug回归）：旧版用 heightScale * seaLevelRatio 估算水位，
            // 与真实海平面 Data.SeaLevel 不一致，导致陆地低地被误判为水下。
            // 溺水与温度伤害已迁移到 EntityEnvironmentHazard，由 EnvironmentConfig 数据驱动。

            if (_stats.TryGet("nutrient", out var finalNutrient) && finalNutrient <= 0f)
                _stats.Modify("health", -1f * dt);

            if (_stats.TryGet("health", out var finalHp) && finalHp <= 0f)
                Destroy(gameObject);
        }
    }
}
