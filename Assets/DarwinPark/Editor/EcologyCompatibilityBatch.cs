using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DarwinPark.Core;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    // ★ 新增：串行复用既有状态/社会回归，保持每项检查的隔离前提，并支持无窗口执行及失败退出码。
    [InitializeOnLoad]
    public static class EcologyCompatibilityBatch
    {
        private const string Key="DarwinPark.EcologyCompatibilityBatch";
        static EcologyCompatibilityBatch()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
                    EditorApplication.delayCall+=()=>UnityEngine.Object.FindFirstObjectByType<WorldSessionManager>().StartCoroutine(Run());
            };
        }
        public static void RunBatch()
        {
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();
        }
        private static IEnumerator Run()
        {
            var results=new List<string>();string folder=Path.GetFullPath("Artifacts/EcologyCompatibilityReview");Directory.CreateDirectory(folder);
            foreach(var test in new (string path,Action validate)[]{("Artifacts/EntityStateReview/result.txt",EntityStateRegression.Validate),("Artifacts/SocialEcologyReview/result.txt",SocialEcologyRegression.Validate)})
            {
                WorldRuntimeGate.Ready=false;GameInputGate.Blocked=true;ServiceLocator.Get<SimulationClock>().Pause();
                Directory.CreateDirectory(Path.GetDirectoryName(test.path));File.WriteAllText(test.path,"RUNNING");
                string error=null;try{test.validate();}catch(Exception ex){error=ex.ToString();}
                double started=EditorApplication.timeSinceStartup;
                while(error==null&&File.ReadAllText(test.path)=="RUNNING")
                {
                    if(EditorApplication.timeSinceStartup-started>150){error="回归超时："+test.path;break;}
                    yield return null;
                }
                string result=error??File.ReadAllText(test.path);results.Add(test.path+"\n"+result);
                if(error!=null||!result.StartsWith("PASS")){Finish(folder,results,false);yield break;}
                yield return null;
            }
            Finish(folder,results,true);
        }
        private static void Finish(string folder,List<string> results,bool passed)
        {
            File.WriteAllText(Path.Combine(folder,"result.txt"),(passed?"PASS":"FAIL")+"\n"+string.Join("\n\n",results));
            SessionState.SetBool(Key,false);EditorApplication.Exit(passed?0:1);
        }
    }
}
