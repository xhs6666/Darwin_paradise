using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Genetics
{
    public static class GeneDatabase
    {
        private static Dictionary<string, GeneDefinition> _definitions;
        private static bool _initialized = false;

        public static void EnsureLoaded()
        {
            if (_initialized) return;
            _definitions = new Dictionary<string, GeneDefinition>();

            // ★ 适配：自定义工具配置覆盖内置同 id 基因，失败明确告警并回退。
            var assets = System.Linq.Enumerable.ToArray(GeneTreeStorage.RuntimeAssets());
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[GeneDatabase] 致命错误：Resources/Genes 下找不到任何 JSON 文件！");
                return;
            }

            foreach (var asset in assets)
            {
                try
                {
                    var json = JsonUtility.FromJson<GeneDefinitionJson>(asset.text);
                    if (json == null || string.IsNullOrEmpty(json.id))
                    {
                        Debug.LogError($"[GeneDatabase] JSON 解析为空或缺少 id：{asset.name}");
                        continue;
                    }

                    var def = new GeneDefinition
                    {
                        Id = json.id,
                        DisplayName = json.displayName,
                        Category = (GeneCategory)Enum.Parse(typeof(GeneCategory), json.category, true),
                        ParentGeneId = json.parentGeneId,
                        BaseGeneId = json.baseGeneId,
                        IsBaseGene = json.isBaseGene,
                        Tier = json.tier,
                        LogicScriptId = json.logicScriptId,
                        AiModuleId = json.aiModuleId,
                        AttributeCurves = new List<AttributeCurve>(),
                        Linkages = new List<AttributeLinkage>(), // ★ 确保这里初始化了
                        States = new List<GeneStateDefinition>(),
                        SuppressionRules = new List<GeneSuppressionRule>()
                    };

                    // 映射属性曲线
                    if (json.attributeCurves != null)
                    {
                        foreach (var c in json.attributeCurves)
                        {
                            def.AttributeCurves.Add(new AttributeCurve
                            {
                                attributeId = c.attributeId,
                                minValue = c.minValue,
                                maxValue = c.maxValue
                            });
                        }
                    }

                    // 映射状态
                    if (json.states != null)
                    {
                        foreach (var s in json.states)
                        {
                            def.States.Add(new GeneStateDefinition
                            {
                                StateId = s.stateId,
                                DisplayName = s.displayName,
                                Center = s.center,
                                Color = (s.color != null && s.color.Length >= 3) ? new Color(s.color[0], s.color[1], s.color[2]) : Color.white,
                                Tier = s.tier,
                                BaseGeneId = s.baseGeneId,
                                AiModuleId = s.aiModuleId,
                                // ★ 修复：补齐状态迁移数据，旧版只读 stateId/displayName，基因树会丢失后继状态。
                                AllowedNextStates = s.allowedNextStates != null
                                    ? new List<string>(s.allowedNextStates)
                                    : new List<string>()
                            });
                        }
                    }

                    // 映射抑制规则
                    if (json.suppressionRules != null)
                    {
                        foreach (var r in json.suppressionRules)
                        {
                            def.SuppressionRules.Add(new GeneSuppressionRule
                            {
                                TargetGeneId = r.targetGeneId,
                                Coefficient = r.coefficient
                            });
                        }
                    }

                    // ★ 核心插入位置：映射基因联动规则
                    if (json.attributeLinkages != null)
                    {
                        def.Linkages.AddRange(json.attributeLinkages);
                    }

                    // ★ 适配：红/绿关系转为运行中的抑制/促进系数。
                    GeneTreeStorage.ApplyInfluences(def);
                    _definitions[json.id] = def;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GeneDatabase] 加载 {asset.name} 失败：{e.Message}");
                }
            }

            _initialized = true;
            Debug.Log($"[GeneDatabase] 初始化完成，共加载 {_definitions.Count} 个基因。");
        }

        public static IEnumerable<GeneDefinition> All
        {
            get
            {
                EnsureLoaded();
                return _definitions.Values;
            }
        }

        public static GeneDefinition Get(string id)
        {
            EnsureLoaded();
            if (_definitions.TryGetValue(id, out var def))
                return def;

            Debug.LogWarning($"[GeneDatabase] 找不到基因 ID: {id}");
            return null;
        }

        public static void Reload() => _initialized = false;
    }
}