using System;
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities
{
    [RequireComponent(typeof(Collider), typeof(EntityGeneController))]
    public class DarwinEntity : MonoBehaviour
    {
        public string DisplayName;

        public EntityGeneController Genes { get; private set; }

        public IReadOnlyCollection<string> Traits => _traits;
        private readonly HashSet<string> _traits = new();
        private float _energy;

        /// <summary>繁殖请求：子代 Genome 就绪时触发。</summary>
        public event Action<DarwinEntity, Genome> OnOffspringReady;

        // ★ 关键修改 1：定义一个静态委托，让上层（GameBootstrap）把生成逻辑注入进来
        public static Action<Genome, Vector3, string> Spawner;

        private void Awake()
        {
            Genes = GetComponent<EntityGeneController>();
        }

        private void OnEnable() => EntityRegistry.Register(this);
        private void OnDisable() => EntityRegistry.Unregister(this);

        public void EnableTrait(string id) { if (!string.IsNullOrEmpty(id)) _traits.Add(id); }
        public void DisableTrait(string id) { if (!string.IsNullOrEmpty(id)) _traits.Remove(id); }
        public bool HasTrait(string id) => _traits.Contains(id);

        public void AddEnergy(float amount) => _energy += amount;
        public float Energy => _energy;

        public void RequestReproduction(DarwinEntity partner, Genome offspringGenome)
        {
            if (OnOffspringReady == null)
            {
                SpawnOffspring(offspringGenome, name + "_child");
                return;
            }
            OnOffspringReady.Invoke(partner, offspringGenome);
        }

        public void SpawnOffspring(Genome genome, string childName)
        {
            if (genome == null)
            {
                Log.Warn(Log.Tag.Entity, $"{name} 子代基因组为空，跳过");
                return;
            }

            // ★ 关键修改 2：删掉原有的 EntityFactory 调用，改为通过委托触发
            if (Spawner != null)
            {
                Spawner.Invoke(genome, transform.position + UnityEngine.Random.insideUnitSphere * 1.5f, childName);
            }
            else
            {
                Log.Error(Log.Tag.Entity, "尚未注入实体生成器 (Spawner)，请检查 GameBootstrap 是否初始化");
            }
        }
    }
}