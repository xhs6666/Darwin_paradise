using UnityEngine;
using DarwinPark.Core;
using DarwinPark.Genetics;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.AI;

namespace DarwinPark.GameRoot
{
    public static class EntityFactory
    {
        public static DarwinEntity CreateFromScratch(Genome genome, Vector3 position, string displayName)
        {
            var go = new GameObject("Entity");
            go.transform.position = position;
            return AttachComponents(go, genome, displayName);
        }

        public static DarwinEntity CreateFromTemplate(GameObject template, Genome genome, Vector3 position, string displayName)
        {
            if (template == null) return CreateFromScratch(genome, position, displayName);

            var go = Object.Instantiate(template, position, Quaternion.identity);
            if (!string.IsNullOrEmpty(displayName)) go.name = displayName;

            return AttachComponents(go, genome, displayName);
        }

        private static DarwinEntity AttachComponents(GameObject go, Genome genome, string displayName)
        {
            // 1. 确保有碰撞体
            var col = go.GetComponent<Collider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = false;

            // 2. 先挂载实体核心（必须在挂载基因控制器之前，因为基因控制器 Awake 时需要找它）
            var entity = go.GetComponent<DarwinEntity>() ?? go.AddComponent<DarwinEntity>();

            // 3. 挂载基因控制器
            var genes = go.GetComponent<EntityGeneController>();
            if (genes == null) genes = go.AddComponent<EntityGeneController>();

            // 如果传入了外部的 genome，就覆盖控制器里的
            if (genome != null && genome.Genes.Count > 0)
            {
                genes.Genome = genome;
            }

            // ★ 修复名字处理逻辑：如果没有指定名字，向基因控制器要一个随机名字
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = genes.GetRandomName();
            }
            entity.DisplayName = displayName;

            // 4. 挂载属性与生命系统
            if (go.GetComponent<EntityStats>() == null) go.AddComponent<EntityStats>();
            if (go.GetComponent<EntityStatsGeneBridge>() == null) go.AddComponent<EntityStatsGeneBridge>();
            if (go.GetComponent<EntityVitals>() == null) go.AddComponent<EntityVitals>();
            // ★ 新增：温度/溺水环境伤害独立挂载，数据来自 EnvironmentConfig。
            if (go.GetComponent<EntityEnvironmentHazard>() == null) go.AddComponent<EntityEnvironmentHazard>();

            // 5. 挂载 AI 控制器
            if (go.GetComponent<EntityAIController>() == null) go.AddComponent<EntityAIController>();

            // 6. 挂载物理刚体
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();

            rb.mass = 1f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 5f;

            // 7. 挂载星球引力脚本
            PlanetGravity gravity = go.GetComponent<PlanetGravity>();
            if (gravity == null) gravity = go.AddComponent<PlanetGravity>();
            gravity.baseGravity = 15f;

            // ★ 新增：地表高度安全约束。攻击/碰撞把实体顶飞后，
            // 仅靠随高度衰减的引力可能永远回不来；这里做硬上限与救援。
            if (go.GetComponent<EntitySurfaceAnchor>() == null) go.AddComponent<EntitySurfaceAnchor>();

            // 8. 初始化基因计算
            genes.Recalculate();

            var newStats = go.GetComponent<EntityStats>();
            if (newStats != null)
            {
                // 强制营养为 10（不满足繁殖的 80），强迫它们去吃食物长大
                //newStats.Modify("nutrient", -90f);

                // 如果出生就带有生长基因，强制将其归零为幼年（初始值 10）
                var growth = genes.Genome.Genes.Find(g => g.DefinitionId == "gene_growth");
                if (growth != null) growth.Value = 10f;
            }

            return entity;
        }
    }
}