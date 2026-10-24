using UnityEngine;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Stats
{
    /// <summary>
    /// 基因 ↔ 属性桥接。监听基因变化刷新属性（当前为极简版）。
    /// </summary>
    [RequireComponent(typeof(EntityGeneController), typeof(EntityStats))]
    public class EntityStatsGeneBridge : MonoBehaviour
    {
        private EntityGeneController _genes;
        private EntityStats _stats;

        private void Awake()
        {
            _genes = GetComponent<EntityGeneController>();
            _stats = GetComponent<EntityStats>();
        }

        private void OnEnable()
        {
            if (_genes != null) _genes.OnEvaluationChanged += OnGenesChanged;
        }

        private void OnDisable()
        {
            if (_genes != null) _genes.OnEvaluationChanged -= OnGenesChanged;
        }

        private void OnGenesChanged(GenomeEvaluation eval)
        {
            // 占位：真正接入基因属性计算后，在这里刷新 _stats 的上限
        }
    }
}