using DarwinPark.Core;
using DarwinPark.Genetics;
using DarwinPark.Entities;
using UnityEngine;

namespace DarwinPark.GameRoot
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("实体配置")]
        [Tooltip("在这里拖入做好的实体预制体")]
        [SerializeField] private GameObject entityPrefab; // ★ 必须有这句，你把它删了

        private void Awake()
        {
            // ★ 删掉了 EntityPrefab = entityPrefab; 这行不需要了

            // 改用 CreateFromTemplate，并通过闭包捕获 entityPrefab
            DarwinEntity.Spawner = (genome, pos, name) =>
            {
                EntityFactory.CreateFromTemplate(entityPrefab, genome, pos, name);
            };

            GeneDatabase.EnsureLoaded();
        }

        private void Start()
        {
            // ★ 适配：全局调度随游戏根节点安装，不依赖编辑器域重载或修改预制实体。
            Entities.Simulation.EntityWorkScheduler.Ensure(gameObject);
            // ★ 适配：全局变异与个体阶段拆开，独立根组件统一推进。
            Entities.Simulation.MutationRoundController.Ensure(gameObject);
            // ★ 适配：清理规则由独立根组件统一推进，不给每个尸体新增Update。
            Entities.Simulation.CorpseCleanupController.Ensure(gameObject);
            if (entityPrefab == null)
            {
                Debug.LogError("[Bootstrap] 没有指定实体预制体！请把预制体拖到 GameRoot 的 Inspector 面板里");
                return;
            }
        }

        private void OnDestroy() => ServiceLocator.Clear();
    }
}
