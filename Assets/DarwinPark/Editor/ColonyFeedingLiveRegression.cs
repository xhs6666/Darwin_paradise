using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：从真实问题存档出发，保持实际地形和完整 Update/FixedUpdate，验收移动→领取废弃块→进食及操控按钮。
    [InitializeOnLoad]
    public static class ColonyFeedingLiveRegression
    {
        private const string Key="DarwinPark.ColonyFeedingLive";
        private static bool _started;private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/ColonyFeedingLiveReview");
        static ColonyFeedingLiveRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&Object.FindFirstObjectByType<WorldSessionManager>()!=null){_started=true;Object.FindFirstObjectByType<WorldSessionManager>().StartCoroutine(Guard());}};}
        public static void RunBatch()
        {
            Application.runInBackground=true;EditorApplication.isPaused=false;
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(20,20,1440,960);view.Show();EditorApplication.EnterPlaymode();
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllText(Path.Combine(Folder,"progress.txt"),string.Join("\n",Passed));}
        private static float Food(AntColonyMember member){var stats=member.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);return food+reserve;}
        private static IEnumerator Guard()
        {
            Application.runInBackground=true;
            yield return null;Directory.CreateDirectory(Folder);Passed.Clear();File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var run=Run();
            while(true){bool next=false;object value=null;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);}if(error!=null||!next)break;yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));
            SessionState.SetBool(Key,false);Debug.Log("[ColonyFeedingLiveReview] "+(error==null?"PASS":"FAIL"));EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();var clock=ServiceLocator.Get<SimulationClock>();
            var data=WorldSaveStore.Read(Path.Combine(Folder,"repro-world.json"));data.id=Guid.NewGuid().ToString("N");data.name="废弃补给与操控独立验证";data.running=false;
            session.Open(data);double start=EditorApplication.timeSinceStartup;
            while(session.Busy){if(EditorApplication.timeSinceStartup-start>180)throw new TimeoutException(session.Status);yield return null;}
            Check(session.Current!=null,"真实存档完整地形与实体加载成功");clock.Pause();
            var queen=AntColonyMember.All.Single(m=>m.Role==AntRole.Queen);var child=queen.Group.Single(m=>m.Young);
            foreach(var entity in EntityRegistry.All)entity.Genes.mutationChance=0;
            string queenId=queen.Id,childId=child.Id;var original=queen.transform.position;
            Check(Food(queen)==0&&Food(child)==0,"复现王后和幼体均无营养的存档状态");
            var abandoned=ColonyFeeding.Find(queen,false);
            Check(abandoned!=null&&abandoned.State.group!=queen.GroupId&&!ColonyResourceOwnership.HasOwner(abandoned.State.group),"复现旧群灭绝、能量仍锁在旧群的归属问题");
            queen.Destination(out var destination,out var target,out _);Check(target==null&&EcologyQueries.Distance(destination,abandoned.transform.position)<=AntColonySettings.Current.feedRadius,"饥饿王后的实际目的地指向废弃育幼块");
            var resourceState=abandoned.State;var resourcePoint=abandoned.transform.position;
            float beforeEnergy=resourceState.energy;clock.SetTimeScale(4);clock.Play();float before=clock.GameTime;start=EditorApplication.timeSinceStartup;float report=0;
            // ★ 修复（防Bug回归）：营养基因自身也可能短暂产粮，必须观察实际能量块扣减和归属领取，不能把饱食度变正误判为块已被吸收。
            while(queen!=null&&child!=null&&(Food(queen)<=0||Food(child)<=0||resourceState.group!=queen.GroupId||resourceState.energy>=beforeEnergy))
            {
                if(clock.GameTime-before>=report){report+=5;Debug.Log("[LiveFeeding] t="+(clock.GameTime-before)+" food="+Food(queen)+" child="+Food(child)+" distance="+EcologyQueries.Distance(queen.transform.position,resourcePoint)+" speed="+queen.Genes.GetAttribute("moveSpeed")+" activity="+queen.Activity+" block="+resourceState.energy);}
                if(clock.GameTime-before>100||EditorApplication.timeSinceStartup-start>50)
                    throw new TimeoutException("实际进食未完成：queen="+(queen!=null?Food(queen):-1)+" child="+(child!=null?Food(child):-1)+" distance="+(queen!=null?EcologyQueries.Distance(queen.transform.position,resourcePoint):-1)+" speed="+(queen!=null?queen.Genes.GetAttribute("moveSpeed"):-1));
                yield return null;
            }
            clock.Pause();Check(queen!=null&&child!=null&&Food(queen)>0&&Food(child)>0,"完整更新和物理步中王后与幼体自主进食且存活");
            Check(EcologyQueries.Distance(original,queen.transform.position)>.2f&&resourceState.energy<beforeEnergy,"王后实际移动接近能量块且能量被扣除");
            Check(resourceState.group==queen.GroupId,"废弃块在实际进食后归入新群");
            var view=EntityViewController.Instance;Check(view.EnterView(queen.Entity),"双击操控的实际入口可进入王后视角");yield return null;
            var input=view.GetComponent<EntityPossessionInput>();var hotkey=Object.FindFirstObjectByType<SimulationPauseHotkey>();
            Check(hotkey.HandlePauseKey(KeyCode.P,true,false)&&clock.IsRunning,"操控中P可启动");
            Check(!hotkey.HandlePauseKey(KeyCode.Space,true,false)&&clock.IsRunning,"操控中空格只保留跳跃不切换暂停");
            Check(hotkey.HandlePauseKey(KeyCode.P,true,false)&&!clock.IsRunning,"操控中P可暂停");yield return null;
            Check(view.Target==queen.Entity,"P暂停保留操控目标");
            Check(!hotkey.HandlePauseKey(KeyCode.P,true,true)&&!clock.IsRunning,"输入框输入P不会启动模拟");
            // ★ 适配：新要求取消所有新增功能UI，以Q实际扣账、独立基因和按住Alt的完整流程替代旧按钮验收。
            var utilization=NutrientUtilizationRegression.Verify(Check);while(utilization.MoveNext())yield return utilization.Current;
            input.Suspend();Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"possession-controls.png"));yield return null;
            view.ExitView();yield return null;
            session.SaveNow(false);var saved=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));float savedEnergy=saved.colony.resources.Sum(r=>r.energy);
            session.Open(saved);start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>180)throw new TimeoutException(session.Status);yield return null;}clock.Pause();
            queen=AntColonyMember.All.Single(m=>m.Id==queenId);child=AntColonyMember.All.Single(m=>m.Id==childId);
            Check(Mathf.Abs(ColonyResource.All.Sum(r=>r.State.energy)-savedEnergy)<.01f&&Food(queen)>0&&Food(child)>0,"重新读档保留回收归属、新增能量和已进食状态");
            var restored=EcologyIdentity.Find(NutrientUtilizationRegression.ActorId);
            Check(restored!=null&&restored.GetComponent<EnergyBlockMetabolism>()?.Active==true&&ColonyResource.All.Any(r=>r.State.kind==ColonyResourceKind.Energy),"独立营养利用基因与Q吐出的通用能量块均可读档恢复");
            var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.ShowPause();menu.ReturnToMainMenu();yield return null;
        }
    }
}
