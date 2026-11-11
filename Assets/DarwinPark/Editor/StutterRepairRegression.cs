using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：用完整玩家快照验证性能路径的正确性，覆盖资源身份/能量、点选、显示回退、接地失效和出生委托异常。
    [InitializeOnLoad]
    public static class StutterRepairRegression
    {
        private const string Key = "DarwinPark.StutterRepair";
        private static bool _started;
        private static readonly List<string> Passed = new();
        private static string Folder => Path.GetFullPath("Artifacts/StutterRepairReview");
        static StutterRepairRegression()
        { EditorApplication.update += () => { if (!_started && Application.isPlaying && SessionState.GetBool(Key, false) && LanSession.Instance != null) { _started = true; LanSession.Instance.StartCoroutine(Guard()); } }; }
        public static void RunBatch()
        { SessionState.SetBool(Key, true); Application.runInBackground = true; EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity"); EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")); EditorApplication.EnterPlaymode(); }
        private static void Check(bool pass, string text)
        { if (!pass) throw new InvalidOperationException(text); Passed.Add(text); File.WriteAllLines(Path.Combine(Folder, "progress.txt"), Passed); }
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder); string error = null; var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                bool next = false; object current = null;
                try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
                catch (Exception ex) { error = ex.ToString(); Debug.LogException(ex); break; }
                if (!next) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
            File.WriteAllText(Path.Combine(Folder, "result.txt"), (error == null ? "PASS" : "FAIL") + "\n" + error + "\n" + string.Join("\n", Passed));
            SessionState.SetBool(Key, false); EditorApplication.Exit(error == null ? 0 : 1);
        }
        private static DarwinEntity Spawn(string template, Vector3 position)
        {
            var actor = EntitySpawnCapture.Create(EcologyDefaults.Template(template).CreateGenome(), position, "性能回归");
            if(actor==null)throw new InvalidOperationException("测试探针生成被拒绝："+template);
            actor.GetComponent<Rigidbody>().isKinematic = true;
            actor.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);
            actor.Genes.RestoreStageState(AgeState.Adult, 3, 2); actor.Genes.Recalculate(); return actor;
        }
        private static void Set(EntityStats stats, string id, float value)
        { stats.TryGet(id, out float current); stats.Modify(id, value - current, false); }
        private static ColonyResource Resource(Vector3 point, string group, ColonyResourceKind kind, float energy = 30)
            => ColonyResource.Create(new ColonyResourceSave { id = Guid.NewGuid().ToString("N"), group = group, kind = kind, energy = energy, local = EcologyQueries.ToLocal(point), scale = Vector3.one });
        private static IEnumerator Run()
        {
            var session = Object.FindFirstObjectByType<WorldSessionManager>(); session.autoSaveInterval = 36000;
            session.Open(WorldSaveStore.Read(Path.GetFullPath("Artifacts/NearViewReview/input/world.json")));
            while (session.Busy) yield return null;
            var clock = ServiceLocator.Get<SimulationClock>(); clock.Pause(); GameplayTuning.Current.mutationRoundsEnabled = false;
            // ★ 适配：测试增加少量探针实体，隔离玩家快照已接近上限的配置，不修改原存档。
            EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=0});
            var planet = EcologyQueries.Planet; int resources = ColonyResource.All.Count(); float energy = ColonyResource.All.Sum(r => r.State.energy);
            int corpses = ColonyResource.All.Count(r => r.State.kind == ColonyResourceKind.Corpse);
            yield return null; yield return new WaitForEndOfFrame();
            Check(corpses > 5000 && CorpseInstanceRenderer.RegisteredParts == corpses * 2, "完整五千以上尸体的身体/叉号均登记批次，保留原资源对象");
            Check(CorpseInstanceRenderer.LastInstances == corpses * 2 && CorpseInstanceRenderer.LastBatches > 0, "真实帧实际提交尸体GPU实例，而非只勾选材质开关");
            SimulationOptimization.Current.corpseBatchRendering = false; yield return null; yield return new WaitForEndOfFrame();
            Check(ColonyResource.All.Where(r => r.State.kind == ColonyResourceKind.Corpse).All(r => r.GetComponentsInChildren<MeshRenderer>().All(v => v.enabled)), "关闭批次即时恢复原身体和叉号显示");
            Check(ColonyResource.All.Count() == resources && Math.Abs(ColonyResource.All.Sum(r => r.State.energy) - energy) < .01f, "切换显示没有删除尸体或改变真实营养");
            SimulationOptimization.Current.corpseBatchRendering = true; yield return null; yield return new WaitForEndOfFrame();
            Check(CorpseInstanceRenderer.LastInstances == corpses * 2, "再次开启恢复相同数量的尸体实例");
            var home = EntityRegistry.All.First(e => !planet.IsSubmerged(e.transform.position) && planet.Data.Navigation.Closest(PlanetTerrainSampler.WorldToLocalDirection(planet.transform,e.transform.position)) >= 0).transform.position;
            var point = home + EcologyQueries.Up(home) * 100;
            var actor = Spawn("producer", point); int count = EntityRegistry.All.Count;
            Check(EntityRegistry.All.Contains(actor) && actor.Genes != null && actor.GetComponent<EntityStats>() != null, "直接捕获返回已完成组件安装的新生实体");
            var original = DarwinEntity.Spawner; long captured = EntitySpawnCapture.Captured; bool threw = false;
            try { DarwinEntity.Spawner = (_, _, _) => throw new InvalidOperationException("expected spawn failure"); EntitySpawnCapture.Create(actor.Genes.Genome, point); }
            catch (InvalidOperationException) { threw = true; }
            finally { DarwinEntity.Spawner = original; }
            var next = Spawn("producer", point + Vector3.right * 5);
            Check(threw && EntitySpawnCapture.Captured == captured + 1 && next != actor && EntityRegistry.All.Count == count + 1, "生成异常解除事件订阅，下一次生成不泄漏捕获或错认旧身份");
            yield return Ground(actor, planet);
            yield return Feeding(point);
            ResourceQueries(point);
            yield return CorpseDisplay(planet, point);
        }
        // ★ 新增：稀疏扫描和格查询都必须与原球面距离逐对象结果一致，包含全球无穷范围与星球旋转后的重建。
        private static void ResourceQueries(Vector3 point)
        {
            var found=new List<ResourceCoordinate>();
            foreach(bool surface in new[]{false,true})foreach(bool corpses in new[]{false,true})foreach(float radius in new[]{3f,90f,float.PositiveInfinity})
            {
                ResourceCoordinates.Query(point,radius,surface,corpses,found);
                var expected=ColonyResource.All.Where(r=>(r.State.kind==ColonyResourceKind.Corpse)==corpses&&(float.IsPositiveInfinity(radius)||(surface?EcologyQueries.Distance(point,r.transform.position)<=radius:(point-r.transform.position).sqrMagnitude<=radius*radius))).ToHashSet();
                Check(expected.SetEquals(found.Select(c=>c.resource)),"资源坐标查询与实时逐对象判定一致："+surface+"/"+corpses+"/"+radius);
            }
            var planet=EcologyQueries.Planet;var rotation=planet.transform.rotation;long rebuilds=ResourceCoordinates.Rebuilds;planet.transform.Rotate(Vector3.up,5);
            ResourceCoordinates.Query(point,90,true,false,found);
            Check(ColonyResource.All.Where(r=>r.State.kind!=ColonyResourceKind.Corpse&&EcologyQueries.Distance(point,r.transform.position)<=90).ToHashSet().SetEquals(found.Select(c=>c.resource)),"同帧星球旋转后查询仍与实时球面坐标一致");
            Check(ResourceCoordinates.Rebuilds==rebuilds,"星球纯自转没有整体重建数千资源坐标桶");
            planet.transform.rotation=rotation;
            var scale=planet.transform.localScale;planet.transform.localScale=scale*1.1f;ResourceCoordinates.Query(point,90,true,false,found);
            Check(ResourceCoordinates.Rebuilds>rebuilds&&ColonyResource.All.Where(r=>r.State.kind!=ColonyResourceKind.Corpse&&EcologyQueries.Distance(point,r.transform.position)<=90).ToHashSet().SetEquals(found.Select(c=>c.resource)),"缩放确实重建资源坐标，并保持实时距离结果");planet.transform.localScale=scale;
        }
        private static IEnumerator Ground(DarwinEntity actor, PlanetTerrainMeshBuilder planet)
        {
            var body = actor.GetComponent<Rigidbody>(); var probe = actor.GetComponent<PlanetGravity>().Grounding;
            body.position = EcologyQueries.Surface(actor.transform.position) + EcologyQueries.Up(actor.transform.position) * 3;
            long direct = EntityGroundProbe.DirectSamples; probe.Read(planet.transform); long sampled = EntityGroundProbe.DirectSamples;
            Check(sampled == direct + 1, "首次接地仍定位真实三角形"); long reused = EntityGroundProbe.CrossStepReused;
            yield return new WaitForFixedUpdate(); probe.Read(planet.transform);
            Check(EntityGroundProbe.DirectSamples == sampled && EntityGroundProbe.CrossStepReused > reused, "跨物理步真实复用地表平面，接地降频配置生效");
            body.position += EcologyQueries.Up(body.position) * 1; probe.Read(planet.transform);
            Check(EntityGroundProbe.DirectSamples == sampled + 1, "超出容差的位移立即刷新接地，不冻结出生下落或传送");
            var rotation = planet.transform.rotation; sampled = EntityGroundProbe.DirectSamples;
            planet.transform.Rotate(Vector3.up, 1); probe.Read(planet.transform);
            Check(EntityGroundProbe.DirectSamples == sampled + 1, "同一步星球旋转立即使平面缓存失效"); planet.transform.rotation = rotation;
            var collider = planet.GetComponent<MeshCollider>(); collider.enabled = false;
            Check(!probe.Read(planet.transform).hasSurface, "禁用碰撞体立即撤销缓存接触"); collider.enabled = true; Physics.SyncTransforms();
            for (int i = 0; i < 12; i++)
            {
                Check(OffspringPlacement.TryGetPosition(body.position, true, out var position), "真实出生落点快查成功");
                var up = EcologyQueries.Up(position); var ray = new Ray(position + up * 1000, -up);
                Check(collider.Raycast(ray, out var physical, 2000) && Vector3.Distance(position - up * LifeCycleSettings.Load().birthHeight, physical.point) < .03f, "出生快查与真实PhysX表面一致");
            }
            actor.Genes.Genome.Add(GeneInstance.Create("gene_locomotion", 50, 0)); actor.Genes.Recalculate(); float speed = GroundMotionSnapshot.Speed(actor.Genes);
            actor.Genes.Genome.Genes.Last(g => g.DefinitionId == "gene_locomotion").Enabled = false; actor.Genes.Recalculate();
            Check(speed > 0 && GroundMotionSnapshot.Speed(actor.Genes) == 0, "同帧禁用行动基因立即失效物理移动资格缓存");
        }
        private static IEnumerator Feeding(Vector3 point)
        {
            var queen = Spawn("ant_founder", point); var member = AntColonyMember.Ensure(queen.GetComponent<PopulationBehavior>());
            member.Assign(AntRole.Queen, Guid.NewGuid().ToString("N")); var stats = queen.GetComponent<EntityStats>(); Set(stats, "nutrient", 1); Set(stats, "storedNutrient", 0);
            long computed = ColonyFeedDecision.Computations; for (int i = 0; i < 20; i++) Check(EntityTaskPlan.Feeding(member), "同帧真实饥饿持续补给");
            Check(ColonyFeedDecision.Computations == computed + 1, "没有属性变化的多入口只计算一次补给插队");
            Set(stats, "nutrient", 10); Set(stats, "storedNutrient", 100);
            Check(!EntityTaskPlan.Feeding(member), "同帧吃饱立即退出补给，没有沿用旧饥饿结果");
            Set(stats, "nutrient", 1); Set(stats, "storedNutrient", 0);
            var nursery = Resource(point + Vector3.forward * 2, member.GroupId, ColonyResourceKind.Nursery);
            var supply = Resource(point + Vector3.forward, member.GroupId, ColonyResourceKind.Supply);
            GameplayTuning.Current.colonyShareBackupFood = true;
            Check(ColonyFoodSources.Nearest(member, false) == supply, "远食仍取最近可吃块，不为专用类别绕过近粮");
            GameplayTuning.Current.colonyShareBackupFood = false;
            Check(ColonyFoodSources.Nearest(member, false) == nursery, "原地关闭备用粮使缓存即时失效");
            float before = nursery.State.energy; stats.TryGet("nutrient", out float food); stats.TryGet("storedNutrient", out float reserve);
            ColonyResource.Feed(member, 1); stats.TryGet("nutrient", out float after); stats.TryGet("storedNutrient", out float afterReserve);
            Check(Math.Abs(before - nursery.State.energy - (after - food + afterReserve - reserve)) < .001f && before > nursery.State.energy, "实际进食仍实时扣块并保持营养守恒");
            nursery.Remove(); Check(ColonyFoodSources.Nearest(member, false) != nursery, "耗尽/移除即使跨帧缓存尚未到期也立即失效");
            GameplayTuning.Current.colonyShareBackupFood = true; yield return null;
        }
        private static IEnumerator CorpseDisplay(PlanetTerrainMeshBuilder planet, Vector3 point)
        {
            var corpse = Resource(point + Vector3.up * 10, "", ColonyResourceKind.Corpse, 17); string id = corpse.State.id;
            yield return null; yield return new WaitForEndOfFrame();
            Check(WorldInspectablePicker.Pick(new Ray(corpse.transform.position + Vector3.up, Vector3.down), 2, out _, out var picked) && picked == corpse, "批次显示下仍能点选原尸体身份");
            long writes=CorpseInstanceRenderer.InstanceWrites;
            corpse.CarryTo(corpse.transform.position + Vector3.right * 20); yield return null; yield return new WaitForEndOfFrame();
            Check(WorldInspectablePicker.Pick(new Ray(corpse.transform.position + Vector3.up, Vector3.down), 2, out _, out picked) && picked == corpse && corpse.State.energy == 17, "运输改变坐标后显示和点选同步且能量不变");
            Check(CorpseInstanceRenderer.InstanceWrites-writes<64,"移动单具尸体仅更新自身及交换槽，没有整体重写数千实例");
            // ★ 新增：同镜头实际像素对照，防止把尸体隐藏或材质变粉误当作批次优化。
            corpse.transform.localScale=Vector3.one*4;
            var camera=Camera.main;camera.GetComponent<OrbitCamera>().enabled=false;var normal=EcologyQueries.Up(corpse.transform.position);
            camera.transform.position=corpse.transform.position+normal*10;camera.transform.rotation=Quaternion.LookRotation(-normal,Vector3.ProjectOnPlane(Vector3.forward,normal).normalized);
            planet.GetComponent<PlanetWeatherVisuals>().cloudSpeed=0;
            Color[][] images=new Color[2][];
            for(int mode=0;mode<2;mode++)
            {
                SimulationOptimization.Current.corpseBatchRendering=mode==1;yield return null;yield return null;yield return new WaitForEndOfFrame();
                var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Folder,mode==0?"corpse-individual.png":"corpse-batched.png"),texture.EncodeToPNG());
                int width=texture.width/12,height=texture.height/12;images[mode]=texture.GetPixels(texture.width/2-width/2,texture.height/2-height/2,width,height);Object.Destroy(texture);
            }
            double difference=0;for(int i=0;i<images[0].Length;i++){var delta=images[0][i]-images[1][i];difference+=Math.Abs(delta.r)+Math.Abs(delta.g)+Math.Abs(delta.b);}
            difference/=Math.Max(1,images[0].Length*3);File.WriteAllText(Path.Combine(Folder,"pixel-difference.txt"),difference.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            Check(difference<.03,"实例身体与原显示中心像素一致，没有消失/粉色材质或串色");
            int parts = CorpseInstanceRenderer.RegisteredParts; corpse.Remove();
            Check(CorpseInstanceRenderer.RegisteredParts == parts - 2 && ColonyResource.Find(id) == null, "移除即时注销两个实例，无幽灵尸体或可重复领取资源");
            // ★ 新增：同格三具尸体删除中间对象，真实验证交换槽而非只验证最后一项删除。
            var first=Resource(point,"",ColonyResourceKind.Corpse,11);var middle=Resource(point,"",ColonyResourceKind.Corpse,13);var last=Resource(point,"",ColonyResourceKind.Corpse,19);
            yield return null;yield return new WaitForEndOfFrame();int instances=CorpseInstanceRenderer.LastInstances;writes=CorpseInstanceRenderer.InstanceWrites;string removed=middle.State.id;middle.Remove();
            yield return null;yield return new WaitForEndOfFrame();
            Check(CorpseInstanceRenderer.LastInstances==instances-2&&ColonyResource.Find(removed)==null&&first.State.energy==11&&last.State.energy==19,"删除同格中间尸体只少两个实例，交换后的两具身份与营养保持");
            Check(CorpseInstanceRenderer.InstanceWrites-writes<=4,"中间删除只更新交换槽，没有重写同格或全世界其他实例");first.Remove();last.Remove();
        }
    }
}
