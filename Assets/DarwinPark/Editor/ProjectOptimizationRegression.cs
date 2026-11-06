using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.AI;
using DarwinPark.UI;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：优化使用旧公式做等价/分配对照，另测缓存失效、重入及自休眠，避免用单纯编译代替行为验证。
    [InitializeOnLoad] public static class ProjectOptimizationRegression
    {
        private const string Key = "DarwinPark.ProjectOptimization";
        private static bool _started;
        private static readonly List<string> Results = new();
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static string Folder => Path.GetFullPath("Artifacts/ProjectOptimizationReview");
        static ProjectOptimizationRegression()
        {EditorApplication.update += () => {if (!_started && Application.isPlaying && SessionState.GetBool(Key, false) && LanSession.Instance != null) {_started = true; LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch()
        {Application.runInBackground = true; SessionState.SetBool(Key, true); EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity"); EditorApplication.EnterPlaymode();}
        private static void Check(bool value, string text)
        {if (!value) throw new InvalidOperationException(text); Results.Add("PASS: " + text);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder); Results.Clear(); File.WriteAllText(Path.Combine(Folder, "result.txt"), "RUNNING");
            string error = null; var run = Run();
            while (true)
            {
                bool next = false; object current = null;
                try {next = run.MoveNext(); if (next) current = run.Current;} catch (Exception ex) {error = ex.ToString(); UnityEngine.Debug.LogException(ex);}
                if (!next || error != null) break;
                yield return current;
            }
            File.WriteAllText(Path.Combine(Folder, "result.txt"), (error == null ? "PASS" : "FAIL") + "\n" + error + "\n" + string.Join("\n", Results));
            SessionState.SetBool(Key, false); EditorApplication.Exit(error == null ? 0 : 1);
        }
        private static EntityGeneController Genes(params string[] ids)
        {
            var go = new GameObject("优化隔离个体"); go.transform.position = Vector3.up * 350;
            var genes = go.AddComponent<EntityGeneController>(); genes.enabled = false; genes.mutationChance = 0;
            genes.Genome = new Genome(); foreach (var id in ids) genes.Genome.Add(GeneInstance.Create(id, 50, GeneDatabase.Get(id).Tier));
            genes.Recalculate(); return genes;
        }
        private static DarwinEntity Entity(string id)
        {
            var genes = Genes(id); genes.gameObject.AddComponent<SphereCollider>();
            var entity = genes.gameObject.AddComponent<DarwinEntity>();
            genes.gameObject.AddComponent<EntityStats>(); return entity;
        }
        // ★ 新增：保留原LINQ公式仅用于对照，禁用基因仍算携带，复制基因不改变必有/必无语义。
        private static bool LegacyMatches(RandomSpawnRecipe recipe, Genome genome)
        {return recipe.included.All(id => genome.Genes.Any(g => g.DefinitionId == id)) && !recipe.excluded.Any(id => genome.Genes.Any(g => g.DefinitionId == id));}
        private static GeneEvaluation LegacyLookup(EntityGeneController genes, GeneInstance gene)
        {return genes.Evaluation.Genes.Find(e => e.Gene.InstanceId == gene.InstanceId);}
        private static string LegacyName()
        {
            var pool = JsonUtility.FromJson<NamePoolJson>(Resources.Load<TextAsset>("Names/name_pool").text);
            return pool.familyNames[UnityEngine.Random.Range(0, pool.familyNames.Count)] + "·" + pool.givenNames[UnityEngine.Random.Range(0, pool.givenNames.Count)];
        }
        private static double Measure(Action action, int count)
        {
            for (int i = 0; i < 32; i++) action();
            var watch = new Stopwatch(); var times = new List<double>();
            for (int pass = 0; pass < 5; pass++)
            {
                GC.Collect(); watch.Restart();
                for (int i = 0; i < count; i++) action();
                watch.Stop(); times.Add(watch.Elapsed.TotalMilliseconds);
            }
            times.Sort(); return times[2];
        }
        // ★ 修复（防Bug回归）：Unity Mono的GetAllocatedBytesForCurrentThread在本机恒为0；使用主线程GC.Alloc记录器并校验容量和旧路径阳性对照。
        private static int Allocations(Action action)
        {
            using var recorder = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 65536, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if (!recorder.Valid) throw new InvalidOperationException("GC.Alloc recorder unavailable");
            recorder.Start(); for (int i = 0; i < 64; i++) action(); recorder.Stop();
            if (recorder.Count >= recorder.Capacity || recorder.WrappedAround) throw new InvalidOperationException("GC.Alloc recorder capacity exceeded");
            return recorder.Count;
        }
        private static void Benchmark(string name, Action before, Action after, int count)
        {
            double a = Measure(before, count), b = Measure(after, count); int oldAllocs = Allocations(before), newAllocs = Allocations(after);
            Results.Add(string.Format(CultureInfo.InvariantCulture, "BENCH {0} x{1}: old={2:F3}ms; new={3:F3}ms (median/5); GC.Alloc x64 old={4}/new={5} events", name, count, a, b, oldAllocs, newAllocs));
            Check(oldAllocs > 0 && newAllocs < oldAllocs, name + " 分配低于旧实现（主线程GC.Alloc）");
        }
        private sealed class ProbeLogic : IGeneLogic
        {
            public int ticks; public Action callback;
            public void Initialize(EntityGeneController c, GeneInstance g) { }
            public void OnUpdate(float dt) {ticks++; callback?.Invoke();}
            public string GetTooltipInfo() => "probe";
        }
        private sealed class ProbeAI : IGeneAIModule
        {
            public string ModuleId => "optimization_probe";
            public int ticks, disabled; public Action callback;
            public void Initialize(EntityAIContext context) { }
            public void Tick(float dt) {ticks++; callback?.Invoke();}
            public void OnDisable() {disabled++;}
        }
        private static IEnumerator Run()
        {
            var clock = ServiceLocator.Get<SimulationClock>(); clock.Pause();
            foreach (var entity in EntityRegistry.All.ToArray()) Object.DestroyImmediate(entity.gameObject);
            PopulationMaintenance.Restore(null); WorldAuthority.CanEdit = true; WorldAuthority.IsReplica = false;
            var genes = Genes("gene_environment", "gene_growth");
            var duplicate = GeneInstance.Create("gene_environment", 1, 0); duplicate.InstanceId = genes.Genome.Genes[0].InstanceId; genes.Genome.Add(duplicate);
            var missing = GeneInstance.Create("gene_environment", 20, 0); missing.InstanceId = null; genes.Genome.Add(missing);
            genes.Recalculate();
            Check(genes.Genome.Genes.All(g => ReferenceEquals(genes.GetEvaluation(g), genes.Evaluation.Genes.Find(e => e.Gene.InstanceId == g.InstanceId))), "表达索引保持重复及空ID的首项语义");
            duplicate.Enabled = false; genes.Recalculate();
            Check(genes.GetEvaluation(duplicate) == genes.Evaluation.Genes[0] && genes.GetEvaluation(GeneInstance.Create("gene_environment", 20, 0)) == null && genes.GetEvaluation(null) == null, "重算/禁用/未知实例的查询与旧Find一致");
            var lookup = genes.Genome.Genes.Last();
            Benchmark("gene-lookup", () => LegacyLookup(genes, lookup), () => genes.GetEvaluation(lookup), 100000);
            var state = UnityEngine.Random.state; UnityEngine.Random.InitState(72631); var expected = Enumerable.Range(0, 128).Select(_ => LegacyName()).ToArray();
            UnityEngine.Random.InitState(72631); var actual = Enumerable.Range(0, 128).Select(_ => genes.GetRandomName()).ToArray(); UnityEngine.Random.state = state;
            Check(expected.SequenceEqual(actual), "缓存词库保持固定种子的名称和随机抽样顺序");
            Benchmark("name", () => LegacyName(), () => genes.GetRandomName(), 1000);
            var recipe = new RandomSpawnRecipe {method = RandomSpawnMethod.Genes, included = new() {"gene_environment"}, excluded = new() {"gene_locomotion"}};
            var random = new System.Random(45); bool equivalent = true;
            for (int trial = 0; trial < 500; trial++)
            {
                var genome = new Genome(); foreach (var id in new[] {"gene_environment", "gene_growth", "gene_locomotion"}) if (random.Next(2) == 1) {var gene = GeneInstance.Create(id, random.Next(100), 0); gene.Enabled = random.Next(2) == 1; genome.Add(gene); if (random.Next(3) == 0) genome.Add(GeneInstance.Create(id, 10, 0));}
                equivalent &= recipe.Matches(genome) == LegacyMatches(recipe, genome);
            }
            Check(equivalent && !recipe.Matches(null), "500组复制/禁用/空基因组的条件匹配与旧公式一致");
            Benchmark("gene-match", () => LegacyMatches(recipe, genes.Genome), () => recipe.Matches(genes.Genome), 100000);
            Object.DestroyImmediate(genes.gameObject); genes = Genes("gene_environment");
            // ★ 适配：此处只测稳定快照，先完成出生初始化并去掉成长基因，生命周期阶段重算另由完整回归覆盖。
            (genes.GetComponent<EntityLifeCycle>() ?? genes.gameObject.AddComponent<EntityLifeCycle>()).RestoreProgress(0);
            var active = (Dictionary<GeneInstance, IGeneLogic>) typeof(EntityGeneController).GetField("_activeLogics", Private).GetValue(genes);
            active.Clear(); var probe = new ProbeLogic(); active[genes.Genome.Genes[0]] = probe;
            typeof(EntityGeneController).GetField("_state", Private).SetValue(genes, null);
            int snapshots = genes.LogicSnapshotBuilds;
            for (int i = 0; i < 1000; i++) genes.AdvanceSimulation(.1f);
            Check(probe.ticks == 1000 && genes.LogicSnapshotBuilds - snapshots == 1, "1000个稳定子步只建一次行为快照，Tick次数不变 (ticks=" + probe.ticks + ", builds=" + (genes.LogicSnapshotBuilds - snapshots) + ")");
            probe.callback = () => genes.Recalculate(); genes.AdvanceSimulation(.1f); genes.AdvanceSimulation(.1f);
            Check(probe.ticks == 1001 && genes.GetLogic(genes.Genome.Genes[0]) != probe, "Tick内重算不修改遍历快照，下一子步切换到新逻辑");
            Action<GenomeEvaluation> throwing = _ => throw new InvalidOperationException("expected evaluation subscriber"); genes.OnEvaluationChanged += throwing;
            bool threw = false; try {genes.Recalculate();} catch (InvalidOperationException) {threw = true;} finally {genes.OnEvaluationChanged -= throwing;}
            int revision = genes.LogicRevision; genes.Recalculate(); Check(threw && genes.LogicRevision == revision + 1, "表达订阅者异常后释放重算锁，下一次重算可恢复");
            Object.DestroyImmediate(genes.gameObject);
            var first = Entity("gene_environment"); var rule = new PopulationMaintenanceRule {id = Guid.NewGuid().ToString("N"), source = recipe, ratio = .3f};
            bool applied = PopulationMaintenance.Set(rule); int matched = PopulationMaintenance.Count(rule);
            Check(applied && matched == 1, "维持计数识别初始匹配个体 (applied=" + applied + ", count=" + matched + ", total=" + PopulationMaintenance.Total + ", registry=" + EntityRegistry.All.Count + ")");
            long version = EntityRegistry.MembershipRevision; Object.DestroyImmediate(first.gameObject); var second = Entity("gene_growth");
            Check(EntityRegistry.All.Count == 1 && EntityRegistry.MembershipRevision > version && PopulationMaintenance.Count(rule) == 0, "同帧等量替换成员使维持统计缓存失效");
            PopulationMaintenance.SuspendAll(); Check(PopulationMaintenance.Needed(PopulationMaintenance.Rules[0]) == 0 && PopulationMaintenance.BreedingScale(second.Genes) == 1, "停用规则既不补充也不限制繁殖");
            Object.DestroyImmediate(second.gameObject); PopulationMaintenance.Restore(null);
            genes = Genes("gene_environment"); var ai = genes.gameObject.AddComponent<EntityAIController>(); yield return null;
            Check(!ai.enabled, "没有AI模块的实体关闭空Update");
            var def = GeneDatabase.Get("gene_environment"); string original = def.AiModuleId; var module = new ProbeAI(); GeneAIModuleRegistry.Register("optimization_probe", () => module);
            var update = typeof(EntityAIController).GetMethod("Update", Private);
            var delta = typeof(SimulationClock).GetField("<GameDeltaTime>k__BackingField", Private); float oldDelta = clock.GameDeltaTime; delta.SetValue(clock, .05f);
            try
            {
                def.AiModuleId = "optimization_probe"; genes.Recalculate(); Check(ai.enabled, "表达事件自动唤醒休眠AI并通过注册表创建扩展模块");
                update.Invoke(ai, null); Check(module.ticks == 1, "扩展模块仍获得正常模拟Tick");
                WorldAuthority.IsReplica = true; update.Invoke(ai, null); WorldAuthority.IsReplica = false; Check(module.ticks == 1, "联机副本不独立推进扩展AI");
                delta.SetValue(clock, 0f); update.Invoke(ai, null); Check(module.ticks == 1, "暂停不推进扩展AI"); delta.SetValue(clock, .05f);
                ai.enabled = false; genes.Recalculate(); Check(!ai.enabled, "表达刷新不会覆盖外部主动禁用AI"); ai.enabled = true;
                module.callback = () => {def.AiModuleId = "ai_flight"; genes.Recalculate();}; update.Invoke(ai, null);
                Check(module.disabled == 1 && !ai.enabled, "Tick内卸载模块安全且仅释放一次，兼容空模块继续休眠");
                module = new ProbeAI(); def.AiModuleId = "optimization_probe"; genes.Recalculate(); Object.DestroyImmediate(ai);
                Check(module.disabled == 1, "销毁控制器成对释放模块"); genes.Recalculate(); Check(module.disabled == 1, "销毁后基因事件不残留控制器订阅");
                Check(GeneAIModuleRegistry.Create("ai_autotroph") is IPassiveGeneAIModule && GeneAIModuleRegistry.Create("ai_flight") is IPassiveGeneAIModule && GeneAIModuleRegistry.Create(null) == null, "保留两种内置模块ID和未知工厂语义");
            }
            finally {def.AiModuleId = original; WorldAuthority.IsReplica = false; delta.SetValue(clock, oldDelta); Object.DestroyImmediate(genes.gameObject);}
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); int missingScripts = 0;
            foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true)) missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            Check(missingScripts == 0 && !Object.FindObjectsByType<DarwinPark.Planet.OreGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(), "正式场景没有丢失脚本且已移除禁用的旧矿石生成器");
        }
    }
}
