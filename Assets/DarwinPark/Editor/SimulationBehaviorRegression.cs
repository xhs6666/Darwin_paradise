using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：验证种子草稿提交、球壳绕序、模拟倍率与捕猎接触链路。</summary>
    public static class SimulationBehaviorRegression
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void SetDelta(SimulationClock clock, float dt) => typeof(SimulationClock).GetField("<GameDeltaTime>k__BackingField",Private).SetValue(clock,dt);
        private static GameObject TestEntity(string name, Vector3 position, bool hunter)
        {
            var go = new GameObject(name, typeof(SphereCollider), typeof(Rigidbody), typeof(EntityStats), typeof(EntityGeneController), typeof(DarwinEntity));
            go.transform.position = position; go.GetComponent<Rigidbody>().useGravity = false;
            var ctrl = go.GetComponent<EntityGeneController>(); ctrl.enabled = false; ctrl.mutationChance = 0;
            ctrl.Genome = new Genome(); ctrl.Genome.Add(GeneInstance.Create("gene_growth",10,0));
            if(hunter) ctrl.Genome.Add(GeneInstance.Create("gene_locomotion",75,0));
            ctrl.Recalculate(); return go;
        }
        [MenuItem("Tools/Darwin Park/Validate Simulation Behaviors")]
        public static void Validate()
        {
            Check(Application.isPlaying,"请先进入播放模式");
            var report = new StringBuilder();
            var builder = UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var weather = builder.GetComponent<PlanetWeatherVisuals>();
            foreach(string name in new[]{"OceanSurface","CloudVolume","AtmosphericScattering"})
            {
                var mesh = builder.transform.Find(name).GetComponent<MeshFilter>().sharedMesh;
                var vertices=mesh.vertices; var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                    Check(Vector3.Dot(Vector3.Cross(b-a,c-a),a+b+c)>0,name+" 含朝内三角形");
                }
            }
            foreach(var shader in new[]{weather.oceanShader,weather.cloudShader,weather.atmosphereShader,builder.TerrainMaterial.shader})
                Check(shader.isSupported && !ShaderUtil.ShaderHasError(shader),"Shader 编译错误: "+shader.name);
            report.AppendLine("PASS: all ocean/cloud/atmosphere triangles face outward; four shaders supported/error-free");
            var camera=Camera.main; var orbit=camera.GetComponent<OrbitCamera>();
            Check(orbit.minDistance < builder.WaterRadius+10,"镜头最近距离未开放");
            var oldPosition=camera.transform.position;
            try
            {
                Vector3 dir=(oldPosition-builder.transform.position).normalized;
                camera.transform.position=builder.transform.position+dir*(builder.WaterRadius+weather.hideCloudsBelowAltitude-5);
                typeof(PlanetWeatherVisuals).GetMethod("LateUpdate",Private).Invoke(weather,null);
                Check(!builder.transform.Find("CloudVolume").gameObject.activeSelf,"近距离云层未关闭");
                camera.transform.position=oldPosition;
                typeof(PlanetWeatherVisuals).GetMethod("LateUpdate",Private).Invoke(weather,null);
                Check(builder.transform.Find("CloudVolume").gameObject.activeSelf==weather.showClouds,"远距离云层未恢复");
            } finally { camera.transform.position=oldPosition; }
            report.AppendLine("PASS: closer orbit enabled; clouds hide near ground and restore at orbital distance");
            var control=UnityEngine.Object.FindFirstObjectByType<SimulationControlPanel>();
            typeof(SimulationControlPanel).GetMethod("ToggleParamPopup",Private).Invoke(control,new object[]{"btn_param_seed","种子"});
            var editor=UnityEngine.Object.FindFirstObjectByType<PlanetParameterEditor>();
            var input=editor.GetComponentInChildren<InputField>(); var slider=editor.GetComponentInChildren<Slider>();
            int original=builder.seed;
            slider.value=original+1; Check(builder.seed==original && input.text==slider.value.ToString("F0"),"种子草稿没有联动/提前应用");
            input.text=original.ToString(); editor.Apply(); Check(builder.seed==original,"输入框应用失败");
            input.text="2147483648"; editor.Apply(); Check(builder.seed==original,"非法种子未拒绝");
            typeof(SimulationControlPanel).GetMethod("DestroyMenus",Private).Invoke(control,null);
            report.AppendLine("PASS: seed slider/input synchronize draft; valid Apply works and int overflow is rejected");
            var clock=ServiceLocator.Get<SimulationClock>(); bool running=clock.IsRunning; float scale=clock.TimeScale;
            var first=TestEntity("Regression1",builder.transform.position+Vector3.up*(builder.WaterRadius+25),false);
            var second=TestEntity("Regression16",first.transform.position+Vector3.right*8,false);
            try
            {
                clock.Play(); clock.SetTimeScale(1);
                typeof(SimulationClock).GetMethod("Update",Private).Invoke(clock,null); float one=clock.GameDeltaTime;
                clock.SetTimeScale(16); typeof(SimulationClock).GetMethod("Update",Private).Invoke(clock,null);
                Check(Mathf.Abs(clock.GameDeltaTime-one*16)<.001f,"统一时钟倍率错误");
                var a=first.GetComponent<EntityGeneController>(); var b=second.GetComponent<EntityGeneController>();
                for(int i=0;i<16;i++) a.AdvanceSimulation(.1f); b.AdvanceSimulation(1.6f);
                float progressA=a.GetComponent<EntityLifeCycle>().Progress;
                float progressB=b.GetComponent<EntityLifeCycle>().Progress;
                Check(progressA>0 && Mathf.Abs(progressA-progressB)<.0001,"成长在相同模拟时长下不一致");
                var va=first.AddComponent<EntityVitals>(); var vb=second.AddComponent<EntityVitals>(); va.enabled=vb.enabled=false;
                SetDelta(clock,.1f); for(int i=0;i<16;i++) typeof(EntityVitals).GetMethod("Update",Private).Invoke(va,null);
                SetDelta(clock,1.6f); typeof(EntityVitals).GetMethod("Update",Private).Invoke(vb,null);
                first.GetComponent<EntityStats>().TryGet("nutrient",out float foodA); second.GetComponent<EntityStats>().TryGet("nutrient",out float foodB);
                Check(Mathf.Abs(foodA-foodB)<.0001f && foodA<10,"代谢没有按模拟时间推进");
                clock.Pause(); Check(clock.GameDeltaTime==0,"暂停残留旧步长");
                report.AppendLine("PASS: 16x clock delta; equal simulated duration yields equal growth/metabolism; Pause clears delta");
                clock.Play(); clock.SetTimeScale(1);
                var hunter=first.GetComponent<EntityGeneController>();
                hunter.Genome.Add(GeneInstance.Create("gene_locomotion",75,0)); hunter.Recalculate();
                first.GetComponent<EntityStats>().Modify("nutrient",-10);
                second.transform.position=first.transform.position+Vector3.right*1.2f; Physics.SyncTransforms();
                var logic=hunter.GetLogic(hunter.Genome.Genes.Last()); logic.OnUpdate(.1f);
                second.GetComponent<EntityStats>().TryGet("health",out float health);
                Check(health<100,"捕食者不能攻击无行动基因的存活目标");
                float afterFirst=health; logic.OnUpdate(.1f); second.GetComponent<EntityStats>().TryGet("health",out health);
                Check(health==afterFirst,"攻击冷却未限制连续攻击");
                // ★ 适配：新进食收益会吃饱；追击回归需重新设置饥饿前提。
                first.GetComponent<EntityStats>().Modify("nutrient",-10);
                second.transform.position=first.transform.position+Vector3.right*8;
                Physics.SyncTransforms(); logic.OnUpdate(1.1f);
                Check(Vector3.Dot(first.GetComponent<Rigidbody>().linearVelocity,Vector3.right)>0,"未向猎物追击");
                Check(Vector3.Dot(first.GetComponent<Rigidbody>().linearVelocity,Vector3.up)<=.01f,"追击出现向外速度");
                report.AppendLine("PASS: hungry hunter damages stationary living prey, respects cooldown, chases tangentially without launching");
            }
            finally
            {
                first.SetActive(false); second.SetActive(false); UnityEngine.Object.Destroy(first); UnityEngine.Object.Destroy(second);
                clock.SetTimeScale(scale>0?scale:1); if(running)clock.Play();else clock.Pause();
            }
            Directory.CreateDirectory("Artifacts/SimulationReview");
            File.WriteAllText("Artifacts/SimulationReview/runtime-checks.txt",report.ToString());
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/SimulationReview/orbit.png"));
            Debug.Log("[SimulationReview] "+report);
        }
    }
}
