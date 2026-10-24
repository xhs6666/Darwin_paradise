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
        public float moveMetabolism = 0.5f;

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
            // 1. 营养吸收与消耗
            float absorbEfficiency = _genes.GetAttribute("absorbEfficiency");
            float moveSpeed = _genes.GetAttribute("moveSpeed");

            float gained = 2f * absorbEfficiency * dt;
            float consumed = baseMetabolism * dt;
            if (moveSpeed > 0 && GetComponent<Rigidbody>().linearVelocity.magnitude > 1f)
                consumed += (moveSpeed * moveMetabolism) * dt;

            if (_stats.TryGet("nutrient", out var currentNutrient))
            {
                // ★ 核心机制 1：获取营养，如果饱食度满了，溢出部分存入储存营养
                if (gained > 0)
                {
                    _stats.Modify("nutrient", gained);
                    // ★ 修复（防Bug回归）：饱食度上限是属性配置的 10，不能用写死的 100 计算仓储溢出。
                    _stats.TryGet("nutrient", out float afterGain);
                    float actualGained = afterGain - currentNutrient;
                    float overflow = gained - actualGained;
                    if (overflow > 0) _stats.Modify("storedNutrient", overflow);
                }

                _stats.Modify("nutrient", -consumed);
            }

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
                if (hp < 100f && hp > repairCapability && nut2 > 5f && repairEfficiency > 0f)
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
