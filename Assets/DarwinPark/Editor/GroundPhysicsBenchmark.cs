using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Vitals;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：旧源码和新源码运行同一独立世界，分离完整行为与固定移动物理；测试限制仅在本Editor脚本应用。
    [InitializeOnLoad]public static class GroundPhysicsBenchmark
    {
        private const string Key="DarwinPark.GroundBenchmark";private static bool _started;private static string Folder=>Path.GetFullPath("Artifacts/GroundPhysicsReview");
        static GroundPhysicsBenchmark(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));EditorApplication.EnterPlaymode();}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error);SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static Dictionary<string,long> Counters()
        {var values=new Dictionary<string,long>();var type=typeof(PlanetGravity).Assembly.GetType("DarwinPark.Entities.EntityGroundProbe");if(type!=null)foreach(var f in type.GetFields(BindingFlags.Public|BindingFlags.Static))if(f.FieldType==typeof(long))values[f.Name]=(long)f.GetValue(null);return values;}
        private static IEnumerator Sample(string name)
        {
            yield return new WaitForSecondsRealtime(2);int population=EntityRegistry.All.Count;var before=Counters();double began=EditorApplication.timeSinceStartup,previous=began;using var sample=new PerformanceSampleSession();
            while(EditorApplication.timeSinceStartup-began<8){yield return null;double now=EditorApplication.timeSinceStartup;sample.RecordFrame((now-previous)*1000);previous=now;}
            var report=sample.Finish(name+" population="+population+"->"+EntityRegistry.All.Count+" api="+SystemInfo.graphicsDeviceType+" resolution="+Screen.width+"x"+Screen.height+" terrain=320 cap=0");File.WriteAllText(Path.Combine(Folder,name+".json"),JsonUtility.ToJson(report,true));
            var after=Counters();File.WriteAllLines(Path.Combine(Folder,name+"-counters.txt"),after.Select(p=>p.Key+"="+(p.Value-(before.TryGetValue(p.Key,out long value)?value:0))));var bodies=EntityRegistry.All.Select(e=>e.GetComponent<Rigidbody>()).Where(b=>b!=null).ToArray();File.WriteAllText(Path.Combine(Folder,name+"-bodies.txt"),"total="+bodies.Length+" sleeping="+bodies.Count(b=>b.IsSleeping())+" kinematic="+bodies.Count(b=>b.isKinematic));if(EntityRegistry.All.Count!=population)throw new InvalidOperationException("固定数量性能样本发生出生/死亡");
        }
        private static IEnumerator Run()
        {
            var world=WorldSaveStore.Read(Path.Combine(Folder,"input/world.json"));var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;session.Open(world);double started=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-started>300)throw new TimeoutException("load");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();PopulationMaintenance.Restore(null);Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;GameplayTuning.Current.mutationRoundsEnabled=false;GameplayTuning.Current.corpseCleanupEnabled=false;FramePacingSettings.SetLimit(0);Camera.main.GetComponent<OrbitCamera>().enabled=false;var weather=EcologyQueries.Planet.GetComponent<PlanetWeatherVisuals>();weather.cloudSpeed=0;weather.showClouds=false;
            EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=EntityRegistry.All.Count});foreach(var actor in EntityRegistry.All){actor.Genes.Evaluation.Attributes["attack"]=0;actor.GetComponent<EntityLifeCycle>().RestoreProgress(Mathf.Min(actor.GetComponent<EntityLifeCycle>().Progress,LifeCycleSettings.Load().deathProgress-30));actor.GetComponent<EntityVitals>().enabled=false;actor.GetComponent<EntityEnvironmentHazard>().enabled=false;}
            var actors=EntityRegistry.All.ToArray();var initial=actors.Select(EntitySaveState.Capture).ToArray();clock.Play();yield return Sample("full-running");clock.Pause();for(int i=0;i<actors.Length;i++)initial[i].Restore(actors[i]);clock.RestoreState(world.gameTime,1);var fixture=session.gameObject.AddComponent<GroundPhysicsMotionFixture>();fixture.planet=EcologyQueries.Planet.transform;
            foreach(var actor in EntityRegistry.All.ToArray())
            {
                float speed=actor.Genes.GetAttribute("moveSpeed");foreach(var behaviour in actor.GetComponents<MonoBehaviour>())if(behaviour is not DarwinEntity&&behaviour is not PlanetGravity&&behaviour is not EntitySurfaceAnchor)behaviour.enabled=false;
                var body=actor.GetComponent<Rigidbody>();if(!body.isKinematic)fixture.bodies.Add((body,Mathf.Min(2,speed)));
            }
            clock.Play();yield return Sample("physics-only");clock.Pause();fixture.enabled=false;
        }
    }
    public sealed class GroundPhysicsMotionFixture:MonoBehaviour
    {
        public Transform planet;public readonly List<(Rigidbody body,float speed)> bodies=new();
        private void FixedUpdate(){foreach(var entry in bodies)if(entry.body!=null&&!entry.body.isKinematic&&entry.speed>0){var up=(entry.body.position-planet.position).normalized;var tangent=Vector3.Cross(up,Mathf.Abs(up.y)>.9f?Vector3.right:Vector3.up).normalized;entry.body.linearVelocity=tangent*entry.speed+up*Vector3.Dot(entry.body.linearVelocity,up);}}
    }
}
