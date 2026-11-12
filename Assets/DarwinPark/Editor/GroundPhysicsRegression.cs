using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：直接三角形与真实PhysX射线逐点比对；通过实际模拟验证落地/休眠/击退/移动/穿地/水下及地形失效。
    [InitializeOnLoad]public static class GroundPhysicsRegression
    {
        private const string Key="DarwinPark.GroundRegression";private static bool _started;private static readonly List<string> Passed=new();private static string Folder=>Path.GetFullPath("Artifacts/GroundPhysicsRegression");private static SimulationMode _mode;private static bool _changedPhysics;
        static GroundPhysicsRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            if(_changedPhysics)Physics.simulationMode=_mode;File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Wait(Func<bool> ready){double start=EditorApplication.timeSinceStartup;while(!ready()){if(EditorApplication.timeSinceStartup-start>120)throw new TimeoutException("world");yield return null;}}
        private static void Compare(PlanetTerrainMeshBuilder planet,Vector3 direction)
        {
            var ground=planet.GetComponent<MeshCollider>();var world=planet.transform.TransformPoint(direction.normalized*1000);Check(RadialTerrainSurface.TrySample(planet,ground,world,out var direct),"直接定位成功 "+Passed.Count);
            var center=planet.transform.position;Check(ground.Raycast(new Ray(world,(center-world).normalized),out var physical,Vector3.Distance(world,center)*2),"PhysX定位成功 "+Passed.Count);
            if(Vector3.Distance(direct.point,physical.point)>.01f||Vector3.Dot(direct.normal,physical.normal)<.98f)throw new InvalidOperationException("直接接地与物理三角形不一致："+direction+" direct="+direct.point+" physics="+physical.point+" normalDot="+Vector3.Dot(direct.normal,physical.normal));
        }
        private static DarwinEntity Spawn(Vector3 position)
        {
            var genome=new Genome();foreach(var id in new[]{"gene_growth","gene_storage","gene_nutrition"})genome.Add(GeneInstance.Create(id,50,GeneDatabase.Get(id).Tier));DarwinEntity actor=null;void Added(DarwinEntity e)=>actor=e;EntityRegistry.OnRegistered+=Added;try{DarwinEntity.Spawner(genome,position,"接地回归");}finally{EntityRegistry.OnRegistered-=Added;}if(actor==null)throw new InvalidOperationException("spawn");return actor;
        }
        private static void Step(DarwinEntity actor,int count)
        {for(int i=0;i<count;i++){actor.GetComponent<PlanetGravity>().SendMessage("FixedUpdate");actor.GetComponent<EntitySurfaceAnchor>().SendMessage("FixedUpdate");Physics.Simulate(Time.fixedDeltaTime);}}
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=32;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=32;PlanetOreSystem.Instance.mapResolution=16;session.Create("真实三角形接地回归",184331,.5f,20,44);yield return Wait(()=>!session.Busy);
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);GameplayTuning.Current.mutationRoundsEnabled=false;foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);ColonyResource.ClearWorld();
            UnityEngine.Random.InitState(9361);for(int i=0;i<120;i++)Compare(planet,UnityEngine.Random.onUnitSphere);Check(true,"120个随机方向真实三角形位置和法线与PhysX一致");
            // 接缝处物理引擎可选择相邻面的法线，只验证焊接交点不产生孔洞。
            foreach(var direction in new[]{new Vector3(1,1,0),new Vector3(-1,0,1),new Vector3(0,-1,-1),Vector3.one})
            {var point=planet.transform.TransformPoint(direction.normalized*1000);Check(RadialTerrainSurface.TrySample(planet,planet.GetComponent<MeshCollider>(),point,out var sample)&&planet.GetComponent<MeshCollider>().Raycast(new Ray(point,(planet.transform.position-point).normalized),out var physical,2000)&&Vector3.Distance(sample.point,physical.point)<.01f,"立方球边/角接缝交点一致 "+direction);}
            planet.transform.SetPositionAndRotation(new Vector3(12,-7,31),Quaternion.Euler(19,72,31));planet.transform.localScale=Vector3.one*1.25f;Physics.SyncTransforms();for(int i=0;i<12;i++)Compare(planet,UnityEngine.Random.onUnitSphere);Check(true,"移动/旋转/均匀缩放后的直接查询与PhysX一致");planet.transform.localScale=new Vector3(1,2,1);Physics.SyncTransforms();Check(!RadialTerrainSurface.TrySample(planet,planet.GetComponent<MeshCollider>(),planet.transform.position+Vector3.up*1000,out _),"非均匀缩放拒绝错误直接法线，交回物理射线");planet.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);planet.transform.localScale=Vector3.one;Physics.SyncTransforms();
            var land=planet.Data.Vertices.OrderByDescending(v=>v.magnitude).First();var up=land.normalized;Check(RadialTerrainSurface.TrySample(planet,planet.GetComponent<MeshCollider>(),up*1000,out var surface),"高地接触点可用");var actor=Spawn(surface.point+up*3);yield return null;clock.Pause();foreach(var behaviour in actor.GetComponents<MonoBehaviour>())if(behaviour is not DarwinEntity&&behaviour is not PlanetGravity&&behaviour is not EntitySurfaceAnchor)behaviour.enabled=false;
            var body=actor.GetComponent<Rigidbody>();body.isKinematic=false;var gravity=actor.GetComponent<PlanetGravity>();var probe=gravity.Grounding;_mode=Physics.simulationMode;_changedPhysics=true;Physics.simulationMode=SimulationMode.Script;clock.Play();
            var sampleA=probe.Read(planet.transform);long directBefore=EntityGroundProbe.DirectSamples,reusedBefore=EntityGroundProbe.ReusedSamples;probe.Read(planet.transform);Check(EntityGroundProbe.DirectSamples==directBefore&&EntityGroundProbe.ReusedSamples==reusedBefore+1,"同物理位置的重力/安全网复用一次地面样本");body.position+=up*.2f;probe.Read(planet.transform);Check(EntityGroundProbe.DirectSamples==directBefore+1,"同一步传送立即失效，不沿用旧接触点");
            Step(actor,400);Check(StationaryBodyRest.IsResting(body),"真实模拟后非移动实体落地并进入接触验证休眠");directBefore=EntityGroundProbe.DirectSamples;long rotations=EntityGroundProbe.RotationWrites;Step(actor,60);Check(EntityGroundProbe.DirectSamples==directBefore&&EntityGroundProbe.RotationWrites==rotations,"休眠实体不再采样地面或重复写旋转");
            body.AddForce(up*4,ForceMode.VelocityChange);Physics.Simulate(Time.fixedDeltaTime);Check(!body.IsSleeping()&&!StationaryBodyRest.RemainsAsleep(body,actor.Genes),"真实外力仍唤醒并保持击退/下落处理");Step(actor,300);Check(StationaryBodyRest.IsResting(body),"击退后重新落地可以再次安全休眠");
            planet.transform.localScale=Vector3.one*1.01f;Check(!StationaryBodyRest.IsResting(body),"星球缩放立即使旧休眠接触失效");planet.transform.localScale=Vector3.one;Physics.SyncTransforms();body.position=surface.point+up*3;body.linearVelocity=Vector3.zero;Step(actor,400);Check(StationaryBodyRest.IsResting(body),"星球变换恢复后重新建立真实接触");
            actor.Genes.Genome.Add(GeneInstance.Create("gene_locomotion",50,0));actor.Genes.Recalculate();Check(!StationaryBodyRest.RemainsAsleep(body,actor.Genes)&&!body.IsSleeping(),"获得行动基因立即唤醒，不被零速度缓存冻结");
            clock.Pause();body.linearVelocity=Vector3.one;body.angularVelocity=Vector3.one;gravity.SendMessage("FixedUpdate");Check(body.linearVelocity==Vector3.zero&&body.angularVelocity==Vector3.zero,"暂停清除真实线速度和角速度");long writes=EntityGroundProbe.VelocityWrites;gravity.SendMessage("FixedUpdate");Check(EntityGroundProbe.VelocityWrites==writes,"重复暂停不改写已归零速度");clock.Play();
            body.position=surface.point+up*70;body.linearVelocity=up*5;
            // ★ 新增：高空检查失败时记录实际水位/接地/绑定信息，不能以放宽断言掩盖救援失效。
            var highSample=probe.Read(planet.transform);File.WriteAllText(Path.Combine(Folder,"rescue-state.txt"),"position="+body.position+" surface="+highSample.point+" altitude="+highSample.altitude+" water="+highSample.underwater+" hasSurface="+highSample.hasSurface+" running="+clock.IsRunning+" kinematic="+body.isKinematic+" sleeping="+StationaryBodyRest.IsResting(body)+" builder="+typeof(EntitySurfaceAnchor).GetField("_builder",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(actor.GetComponent<EntitySurfaceAnchor>()));actor.GetComponent<EntitySurfaceAnchor>().SendMessage("FixedUpdate");File.AppendAllText(Path.Combine(Folder,"rescue-state.txt"),"\nafter="+body.position+" velocity="+body.linearVelocity+" delta="+Vector3.Dot(body.position-surface.point,up));Check(// ★ 修复（防Bug回归）：径向投影在不同高地朝向会留下浮点尾差；沿用跳跃检查的微小速度容限，仍要求救回到2米内。
            Vector3.Dot(body.position-surface.point,up)<2&&Vector3.Dot(body.linearVelocity,up)<=.0001f,"高空实体仍由安全网救回，向外冲量被清除");
            body.position=surface.point-up*3;actor.GetComponent<EntitySurfaceAnchor>().SendMessage("FixedUpdate");Check(Vector3.Dot(body.position-surface.point,up)>.4f,"穿入地形仍推回真实三角形表面");
            var jump=actor.GetComponent<EntityJumpMotor>()??actor.gameObject.AddComponent<EntityJumpMotor>();jump.Bind(planet.transform);body.position=surface.point;body.linearVelocity=-up;Physics.SyncTransforms();Check(jump.ResolveGroundContact()&&Vector3.Dot(body.linearVelocity,up)>=-.0001f,"操控穿地兜底复用地面且去掉向下速度");
            var before=probe.Read(planet.transform);planet.GetComponent<MeshCollider>().enabled=false;Check(!probe.Read(planet.transform).hasSurface&&!StationaryBodyRest.IsResting(body),"禁用地形碰撞立即撤销接触，不能继续休眠");planet.GetComponent<MeshCollider>().enabled=true;Physics.SyncTransforms();
            var ocean=planet.Data.Vertices.OrderBy(v=>v.magnitude).First().normalized;body.position=ocean*(planet.WaterRadius-2);body.linearVelocity=Vector3.Cross(ocean,Vector3.right).normalized*2-ocean;gravity.SendMessage("FixedUpdate");Check(Vector3.ProjectOnPlane(body.linearVelocity,ocean).sqrMagnitude<.000001f&&Vector3.Dot(body.linearVelocity,ocean)<=0,"水中仍清除陆地切向惯性并保留下沉");
            var config=planet.CaptureConfiguration();config.seed++;var oldData=planet.Data;planet.ApplyGeneratedData(PlanetTerrainGenerator.Generate(config,planet.seaLevelRatio,planet.continentRatio));Physics.SyncTransforms();directBefore=EntityGroundProbe.DirectSamples;probe.Read(planet.transform);Check(!ReferenceEquals(oldData,planet.Data)&&EntityGroundProbe.DirectSamples==directBefore+1,"重建地形同一步立即刷新共享样本");
            SimulationOptimization.Current.directGroundQueries=false;long rays=EntityGroundProbe.Raycasts;body.position=up*1000;Check(probe.Read(planet.transform).hasSurface&&EntityGroundProbe.Raycasts==rays+1,"可热关闭直接查询，仍通过真实射线取得地面");SimulationOptimization.Current.directGroundQueries=true;
            clock.Pause();
            // ★ 新增：小场景检查之外，再用玩家完整320细分网格验证高分辨率下的浮点精度及索引映射。
            Physics.simulationMode=_mode;_changedPhysics=false;session.Open(WorldSaveStore.Read(Path.GetFullPath("Artifacts/GroundPhysicsReview/input/world.json")));yield return Wait(()=>!session.Busy);clock.Pause();
            Check(planet.Data.Subdivisions==320,"完整存档保留320细分真实地形");UnityEngine.Random.InitState(47731);for(int i=0;i<120;i++)Compare(planet,UnityEngine.Random.onUnitSphere);Check(true,"320细分完整网格120方向与PhysX位置/法线一致");
        }
    }
}
