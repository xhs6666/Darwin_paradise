using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Stats
{
    public static class EntityAttributeProcessor
    {
        public static Dictionary<string, float> Process(
            Dictionary<string, float> baseAttributes,
            List<GeneEvaluation> activeGenes,
            AgeState ageState,
            float size,
            bool isGestating)
        {
            var finalAttrs = new Dictionary<string, float>(baseAttributes);

            // ================= 1. 注入系统级虚拟属性 =================
            float ageMult = ageState switch
            {
                AgeState.Infant => 0.5f,
                AgeState.Adult => 1.2f,
                AgeState.Elder => 0.8f,
                _ => 1.0f
            };
            finalAttrs["sys_age_mult"] = ageMult;
            finalAttrs["sys_size"] = size;
            finalAttrs["sys_gestating"] = isGestating ? 1f : 0f;

            // 将所有激活的基因ID注入，值为 1，供 JSON 引用
            foreach (var eval in activeGenes)
            {
                if (eval.IsActive && !string.IsNullOrEmpty(eval.Definition.Id))
                {
                    finalAttrs[eval.Definition.Id] = 1f;
                }
            }

            // ================= 2. 收集所有联动规则 =================
            var activeLinkages = new List<AttributeLinkage>();
            foreach (var eval in activeGenes)
            {
                if (!eval.IsActive || eval.Definition.Linkages == null) continue;
                activeLinkages.AddRange(eval.Definition.Linkages);
            }

            // ================= 3. 按优先级执行联动 =================
            // 优先级：Add (加算) -> Multiply/Divide (乘除) -> Override (覆盖归零/锁定)

            // 3.1 先执行所有加算
            foreach (var link in activeLinkages.Where(l => l.mode == "Add"))
            {
                if (!finalAttrs.TryGetValue(link.sourceAttributeId, out float sourceVal)) continue;
                if (!finalAttrs.ContainsKey(link.targetAttributeId)) finalAttrs[link.targetAttributeId] = 0f;

                float effectVal = sourceVal * link.factor;
                finalAttrs[link.targetAttributeId] += effectVal;
            }

            // 3.2 再执行所有乘除算
            foreach (var link in activeLinkages.Where(l => l.mode == "Multiply" || l.mode == "Divide"))
            {
                if (!finalAttrs.TryGetValue(link.sourceAttributeId, out float sourceVal)) continue;
                if (!finalAttrs.ContainsKey(link.targetAttributeId)) continue;

                float effectVal = sourceVal * link.factor;

                if (link.mode == "Multiply")
                {
                    // ★ 修复：允许乘数为 0
                    finalAttrs[link.targetAttributeId] *= effectVal;
                }
                else
                {
                    if (effectVal > 0.01f) finalAttrs[link.targetAttributeId] /= effectVal;
                }
            }

            // 3.3 ★ 核心修复：最后执行所有覆盖（强制归零/改变）
            foreach (var link in activeLinkages.Where(l => l.mode == "Override"))
            {
                if (!finalAttrs.TryGetValue(link.sourceAttributeId, out float sourceVal)) continue;
                // 直接覆盖，完全无视前面加了多少
                finalAttrs[link.targetAttributeId] = sourceVal * link.factor;
            }

            return finalAttrs;
        }
    }
}