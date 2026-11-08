using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：独立工程加载冻结现场，记录补给资格/距离/真实营养变化及细分性能，避免只测手动瞬间Feed。
    [InitializeOnLoad]public static class ColonyFeedingAuditRegression
    {
        private const string Key="DarwinPark.ColonyFeedingAudit";private static bool _started;
        private static string Folder=>Path.GetFullPath("Artifacts/ColonyFeedingReview");
        static ColonyFeedingAuditRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error);SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static void Describe(string file)
        {
            var lines=new List<string>();
            foreach(var member in AntColonyMember.All)
            {
                var stats=member.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);stats.TryGet("health",out float hp);
                var target=ColonyFeeding.Find(member,false);var nearby=ColonyFeeding.Find(member,true);var hazard=member.GetComponent<EntityEnvironmentHazard>();var body=member.GetComponent<Rigidbody>();
                lines.Add($"id={member.Id} role={member.Role} age={member.Genes.CurrentAge} active={member.Active} hp={hp} food={food} reserve={reserve} move={member.Genes.GetAttribute("moveSpeed")} position={member.transform.position} velocity={body.linearVelocity} kinematic={body.isKinematic} submerged={hazard.IsSubmerged} stress={hazard.StressLevel} feeding={ColonyFeeding.WantsFood(member)} founding={member.State.founding} target={target?.State.id} kind={target?.State.kind} targetEnergy={target?.State.energy} distance={(target!=null?EcologyQueries.Distance(member.transform.position,target.transform.position):-1)} nearby={nearby?.State.id}");
                foreach(var resource in ColonyResource.All.Where(r=>r.State.kind!=ColonyResourceKind.Corpse).OrderBy(r=>(r.transform.position-member.transform.position).sqrMagnitude).Take(5))
                    lines.Add($" resource={resource.State.id} kind={resource.State.kind} energy={resource.State.energy} sameGroup={resource.State.group==member.GroupId} available={ColonyResourceOwnership.Available(resource,member)} distance={EcologyQueries.Distance(member.transform.position,resource.transform.position)}");
                foreach(var evaluation in member.Genes.Evaluation.Genes)lines.Add($" gene={evaluation.Definition.Id} strength={evaluation.EffectiveStrength} state={evaluation.State?.StateId}");
            }
            File.WriteAllLines(Path.Combine(Folder,file),lines);
        }
        private static IEnumerator Run()
        {
            var world=JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(Path.Combine(Folder,"input/world.json")));
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;session.Open(world);double start=EditorApplication.timeSinceStartup;
            while(session.Busy){if(EditorApplication.timeSinceStartup-start>300)throw new TimeoutException("load");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);FramePacingSettings.SetLimit(0);
            EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=EntityRegistry.All.Count});foreach(var entity in EntityRegistry.All)entity.Genes.mutationChance=0;
            Describe("members-before.txt");clock.RestoreState(world.gameTime,1);clock.Play();yield return new WaitForSecondsRealtime(3);
            start=EditorApplication.timeSinceStartup;double previous=start;int count=EntityRegistry.All.Count;using var sample=new PerformanceSampleSession();
            while(EditorApplication.timeSinceStartup-start<10){yield return null;double now=EditorApplication.timeSinceStartup;sample.RecordFrame((now-previous)*1000);previous=now;}
            var report=sample.Finish($"latest saved={world.savedUtc} entities={count}->{EntityRegistry.All.Count} resources={ColonyResource.All.Count()} members={AntColonyMember.All.Count()}");clock.Pause();Describe("members-after.txt");
            File.WriteAllText(Path.Combine(Folder,"performance.json"),JsonUtility.ToJson(report,true));
        }
    }
}
