using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.Environment;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：退出后实体仍在画面中央、无游泳能力入水停止推进并掉血的播放回归。</summary>
    public static class WaterAndExitRegression
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private static void Call(object o,string method)=>o.GetType().GetMethod(method,Private).Invoke(o,null);
        [MenuItem("Tools/Darwin Park/Validate Water and Exit")]
        public static void Validate()
        {
            Check(Application.isPlaying,"请先播放并等待星球生成");
            var b=UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var clock=ServiceLocator.Get<SimulationClock>();var view=EntityViewController.Instance;
            var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();
            var oldPos=camera.transform.position;var oldRot=camera.transform.rotation;
            bool running=clock.IsRunning;float scale=clock.TimeScale;float oldDt=clock.GameDeltaTime;
            var orbitState=new System.Collections.Generic.Dictionary<string,object>();
            foreach(string field in new[]{"x","y","targetDistance","_isFocusing","targetPosition","targetRotation"})orbitState[field]=typeof(OrbitCamera).GetField(field,Private).GetValue(orbit);
            float oldDistance=orbit.distance;
            var before=EntityRegistry.All.ToArray();var genome=new Genome();
            genome.Add(GeneInstance.Create("gene_growth",10,0));genome.Add(GeneInstance.Create("gene_locomotion",75,0));
            clock.Pause();EntityPlacementUtility.Spawn(genome,b.transform.position+Vector3.up*(b.WaterRadius+10));
            var entity=EntityRegistry.All.First(e=>!before.Contains(e));
            entity.GetComponent<EntityGeneController>().mutationChance=0;entity.GetComponent<EntityGeneController>().enabled=false;entity.GetComponent<EntityVitals>().enabled=false;
            var hazard=entity.GetComponent<EntityEnvironmentHazard>();hazard.enabled=false;
            // ★ 适配：等实际 Start 完成后再测重力，避免测试假初始化掩盖生命周期问题。
            double ready=EditorApplication.timeSinceStartup+.5;
            EditorApplication.CallbackFunction wait=null;
            wait=()=>{
                if(!Application.isPlaying){EditorApplication.update-=wait;return;}
                if(EditorApplication.timeSinceStartup<ready)return;
                EditorApplication.update-=wait;
                var report=new StringBuilder();
                try
                {
                    clock.Play();clock.SetTimeScale(1);
                    var body=entity.GetComponent<Rigidbody>();body.interpolation=RigidbodyInterpolation.None;var genes=entity.GetComponent<EntityGeneController>();
                    var stats=entity.GetComponent<EntityStats>();var cfg=ServiceLocator.Get<EnvironmentSystem>().Config;
                    Check(view.EnterView(entity),"进入实体视角失败");
                    var original=(oldPos-b.transform.position).normalized;
                    Vector3 moved=Vector3.Cross(original,Vector3.up).normalized;
                    if(moved.sqrMagnitude<.5f)moved=Vector3.right;
                    entity.transform.position=body.position=b.transform.position+moved*(b.WaterRadius+10);Physics.SyncTransforms();
                    Call(view,"LateUpdate");view.ExitView();
                    for(int frame=0;frame<5;frame++)Call(orbit,"LateUpdate");
                    Check(view.Target==null && !orbit.EntityViewActive && Cursor.visible && Cursor.lockState==CursorLockMode.None,"退出未释放控制");
                    var viewport=camera.WorldToViewportPoint(entity.transform.position);
                    Check(viewport.z>0 && Mathf.Abs(viewport.x-.5f)<.01f && Mathf.Abs(viewport.y-.5f)<.01f,"退出后未对准移动后的实体，或被旧轨道覆盖");
                    report.AppendLine("PASS: move entity to another hemisphere, exit, advance orbit 5 frames; entity remains centred; cursor/control released");
                    Check(view.EnterView(entity),"水中测试无法进入操控");
                    var manual=entity.GetComponent<EntityManualControl>();
                    var jump=entity.GetComponent<EntityJumpMotor>();
                    entity.transform.position=body.position=b.transform.position+moved*(b.WaterRadius-.2f);Physics.SyncTransforms();
                    Check(hazard.IsSubmerged,"未识别浅层浸没");
                    body.linearVelocity=-moved*2+Vector3.Cross(moved,Vector3.up)*5;
                    manual.SetInput(Vector3.Cross(moved,Vector3.up),false);Call(manual,"FixedUpdate");
                    Check(Vector3.ProjectOnPlane(body.linearVelocity,moved).magnitude<.001f && Vector3.Dot(body.linearVelocity,moved)<0,"无游泳能力仍可手动推进/停止下沉");
                    manual.Release();body.linearVelocity=Vector3.zero;
                    var action=genes.Genome.Genes.First(g=>g.DefinitionId=="gene_locomotion");genes.GetLogic(action).OnUpdate(.1f);
                    Check(body.linearVelocity.sqrMagnitude<.001f,"AI 在水中仍推进");
                    Call(entity.GetComponent<PlanetGravity>(),"FixedUpdate");
                    Check(Vector3.Dot(body.GetAccumulatedForce(),-moved)>10,"入水仍享受无条件的十分之一重力");
                    Check(!entity.transform.Find("SwimLight").GetComponent<Light>().enabled && !jump.TryJump(true),"无游泳能力获得光效或海底跳跃");
                    typeof(EntityEnvironmentHazard).GetField("_config",Private).SetValue(hazard,cfg);
                    stats.TryGet("health",out float hp);
                    typeof(EntityEnvironmentHazard).GetMethod("UpdateDrowning",Private).Invoke(hazard,new object[]{1f});
                    stats.TryGet("health",out float damaged);
                    Check(hazard.IsDrowning && hp-damaged>=cfg.drowningHealthDamagePerSecond*cfg.minimumDrowningSeverity-.001f,"近水面溺水未持续扣血");
                    entity.transform.position=body.position=b.transform.position+moved*(b.WaterRadius-5);Physics.SyncTransforms();
                    typeof(EntityEnvironmentHazard).GetMethod("UpdateDrowning",Private).Invoke(hazard,new object[]{1f});
                    stats.TryGet("health",out float deepHp);
                    Check(damaged-deepHp>=cfg.drowningHealthDamagePerSecond-.001f,"深水伤害未到达配置值");
                    // 验证真实 Update 的暂停门控及模拟步长，而不只调用伤害内部函数。
                    typeof(SimulationClock).GetField("<GameDeltaTime>k__BackingField",Private).SetValue(clock,.5f);
                    Call(hazard,"Update");stats.TryGet("health",out float updateHp);Check(updateHp<deepHp,"实际环境 Update 没有结算溺水");
                    clock.Pause();Call(hazard,"Update");stats.TryGet("health",out float pausedHp);Check(Mathf.Abs(updateHp-pausedHp)<.001f,"暂停仍溺水扣血");
                    entity.transform.position=body.position=b.transform.position+moved*(b.WaterRadius+10);Physics.SyncTransforms();
                    typeof(EntityEnvironmentHazard).GetMethod("UpdateDrowning",Private).Invoke(hazard,new object[]{1f});
                    stats.TryGet("health",out float dryHp);Check(!hazard.IsDrowning && dryHp==pausedHp,"离水后仍结算溺水");
                    report.AppendLine($"PASS: land-only manual/AI movement blocked underwater; full inward gravity; no swim light or underwater jump; shallow damage {hp-damaged:F1}, deep damage {damaged-deepHp:F1}; real Update damages; pause and leaving water stop drowning");
                    Directory.CreateDirectory("Artifacts/WaterAndExitReview");
                    File.WriteAllText("Artifacts/WaterAndExitReview/runtime-checks.txt",report.ToString());
                    Debug.Log("[WaterAndExitReview] "+report);
                }
                finally
                {
                    view.ExitView();entity.gameObject.SetActive(false);UnityEngine.Object.Destroy(entity.gameObject);
                    camera.transform.SetPositionAndRotation(oldPos,oldRot);orbit.distance=oldDistance;
                    foreach(var pair in orbitState)typeof(OrbitCamera).GetField(pair.Key,Private).SetValue(orbit,pair.Value);
                    clock.SetTimeScale(scale>0?scale:1);if(running)clock.Play();else clock.Pause();
                    typeof(SimulationClock).GetField("<GameDeltaTime>k__BackingField",Private).SetValue(clock,oldDt);
                }
            };
            EditorApplication.update+=wait;
        }
    }
}
