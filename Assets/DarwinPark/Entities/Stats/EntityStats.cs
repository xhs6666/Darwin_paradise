using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Entities.Stats
{
    public class EntityStats : MonoBehaviour
    {
        private Dictionary<string, float> _stats = new();
        private Dictionary<string, float> _maxStats = new();

        // ★ 新增：用于控制受击闪烁的协程与颜色记录
        private Coroutine _flashCoroutine;
        private Dictionary<Renderer, Color[]> _rendererOriginalColors = new();
        private bool _isFlashing = false;

        private void Awake()
        {
            _stats["health"] = 100f; _maxStats["health"] = 100f;
            _stats["nutrient"] = 10f; _maxStats["nutrient"] = 10f;
            _stats["storedNutrient"] = 0f; _maxStats["storedNutrient"] = 100f;
            _stats["growth"] = 0f; _maxStats["growth"] = 100f;
            _stats["aging"] = 0f; _maxStats["aging"] = 100f;
            _stats["size"] = 1.0f; _maxStats["size"] = 10.0f;
        }

        private void Start()
        {
            // ★ 记录实体原本的材质颜色
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                // ★ 修复（防Bug回归）：记录颜色无需实例化材质；数百静止个体原来在Start就破坏了材质共享。
                var materials=r.sharedMaterials;
                if (materials.Length>0&&materials[0]!=null)
                {
                    // 受击时才创建独立材质，静止且未受击的个体继续共享资源
                    Color[] colors = new Color[materials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        colors[i] = materials[i]!=null?materials[i].color:Color.white;
                    }
                    _rendererOriginalColors[r] = colors;
                }
            }
        }

        // ★ 适配：数值与上限成对持久化，恢复时不触发受击闪烁或进食副作用。
        public List<DarwinPark.Entities.StatSaveValue> CaptureValues()
        {var values=new List<DarwinPark.Entities.StatSaveValue>();foreach(var pair in _stats)values.Add(new DarwinPark.Entities.StatSaveValue{id=pair.Key,value=pair.Value,max=_maxStats.TryGetValue(pair.Key,out var max)?max:100});return values;}
        public void RestoreValues(List<DarwinPark.Entities.StatSaveValue> values)
        {foreach(var value in values){_stats[value.id]=value.value;_maxStats[value.id]=value.max;}Ecology.EntityNeighborhood.Changed(GetComponent<EntityGeneController>());}
        public bool TryGet(string id, out float value) => _stats.TryGetValue(id, out value);
        // ★ 适配：营养转移只需当前空位，不应每帧构造整份存档数值列表。
        public float CapacityRemaining(string id)=>_stats.TryGetValue(id,out float value)&&_maxStats.TryGetValue(id,out float maximum)?Mathf.Max(0,maximum-value):0;

        /// <summary>★ 新增：进食先补饱食度，溢出进入储存；猎食收益不因饱食度上限而丢失。</summary>
        public void Feed(float amount)
        {
            if (amount <= 0 || !TryGet("nutrient", out float before)) return;
            Modify("nutrient", amount);
            TryGet("nutrient", out float after);
            float overflow = Mathf.Max(0, amount - (after - before));
            if (overflow > 0) Modify("storedNutrient", overflow);
        }

        public void Modify(string id, float delta)
        {
            Modify(id, delta, true);
        }

        // ★ 适配：环境持续伤害每帧都会调用，关闭闪烁避免实体长期变红；
        // 捕食/饥饿等瞬时伤害仍走原来的闪烁路径。
        public void Modify(string id, float delta, bool playHitFlash)
        {
            if (_stats.ContainsKey(id))
            {
                float max = _maxStats.ContainsKey(id) ? _maxStats[id] : 100f;
                float before=_stats[id];_stats[id] = Mathf.Clamp(before + delta, 0f, max);
                // ★ 适配：同帧死亡或恢复立刻失效邻居计数；普通扣血不重建空间数据。
                if(id=="health"&&(before>0)!=(_stats[id]>0))Ecology.EntityNeighborhood.Changed(GetComponent<EntityGeneController>());

                // ★ 核心修改：如果是扣血，触发变红闪烁
                if (playHitFlash && id == "health" && delta < 0)
                {
                    PlayHitFlash();
                }
            }
        }

        public void SetMax(string id, float max)
        {
            if (_maxStats.ContainsKey(id)) _maxStats[id] = max;
        }

        /// <summary>
        /// 触发受击变红闪烁
        /// </summary>
        private void PlayHitFlash()
        {
            if (_isFlashing)
            {
                // 如果正在闪烁，刷新持续时间
                if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _isFlashing = true;
            SetColor(Color.red); // 变红
            yield return new WaitForSeconds(0.15f); // 红0.15秒
            RestoreColor(); // 恢复
            _isFlashing = false;
            _flashCoroutine = null;
        }

        private void SetColor(Color color)
        {
            foreach (var kvp in _rendererOriginalColors)
            {
                var renderer = kvp.Key;
                if (renderer == null) continue;
                foreach (var mat in renderer.materials)
                {
                    mat.color = color;
                }
            }
        }

        private void RestoreColor()
        {
            foreach (var kvp in _rendererOriginalColors)
            {
                var renderer = kvp.Key;
                if (renderer == null) continue;
                Color[] originalColors = kvp.Value;
                for (int i = 0; i < renderer.materials.Length && i < originalColors.Length; i++)
                {
                    renderer.materials[i].color = originalColors[i];
                }
            }
        }
    }
}
