using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：真实菜单到新建/读档的回归；覆盖加载计时、矿量、实体和暂停状态成对恢复。
    public static class WorldFlowRegression
    {
        // ★ 新增：保留实际画面和状态证据，供 Esc/F10 与背景视频人工操作后复核。
        [MenuItem("Tools/Darwin Park/Capture World Flow %&F8")]
        public static void Capture()
        {
            Directory.CreateDirectory("Artifacts/WorldFlowReview");
            var session=FindFirstObjectByType<WorldSessionManager>();var menu=FindFirstObjectByType<GameMainMenu>();
            string name=session!=null&&session.Current!=null?(menu.Visible?"pause":"game"):"menu";
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/WorldFlowReview/"+name+".png"));
            var video=FindFirstObjectByType<UnityEngine.Video.VideoPlayer>();
            File.WriteAllText("Artifacts/WorldFlowReview/"+name+"-state.txt","menu="+menu.Visible+"; ready="+WorldRuntimeGate.Ready+"; busy="+session.Busy+"; time="+ServiceLocator.Get<SimulationClock>().GameTime+"; videoPlaying="+(video!=null&&video.isPlaying)+"; videoFrame="+(video!=null?video.frame:-1));
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Darwin Park/Validate World Flow %&F6")]
        public static void Validate()
        {
            Check(Application.isPlaying,"请先进入播放模式");

            FindFirstObjectByType<WorldSessionManager>().StartCoroutine(RunSafe());
        }
        private static IEnumerator RunSafe()
        {
            var operation=Run();
            while(true)
            {
                bool next;object current=null;
                try{next=operation.MoveNext();if(next)current=operation.Current;}
                catch(Exception ex){File.WriteAllText("Artifacts/WorldFlowReview/result.txt","FAIL: "+ex);Debug.LogException(ex);yield break;}
                if(!next)yield break;yield return current;
            }
        }
        private static IEnumerator Run()
        {
            Directory.CreateDirectory("Artifacts/WorldFlowReview");
            var session=FindFirstObjectByType<WorldSessionManager>();var menu=FindFirstObjectByType<GameMainMenu>();
            var builder=FindFirstObjectByType<PlanetTerrainMeshBuilder>();var clock=ServiceLocator.Get<SimulationClock>();
            Check(session!=null&&menu!=null,"场景未挂载菜单或世界会话");
            Check(builder.Data==null&&clock.GameTime==0&&menu.Visible,"启动菜单提前生成或计时");
            Check(Path.GetDirectoryName(GamePaths.Saves)==GamePaths.Root&&Path.GetDirectoryName(GamePaths.Mods)==GamePaths.Root,"默认目录不在游戏目录");
            Check(WorldSaveStore.SeedFromText("42")==42&&WorldSaveStore.SeedFromText("达尔文")==WorldSaveStore.SeedFromText(" 达尔文 "),"种子不稳定");
            session.Create("",742031,.5f,20,44);float previous=0;
            while(session.Busy)
            {
                Check(clock.GameTime==0&&!clock.IsRunning,"新世界加载期间计时");Check(session.Progress>=previous,"加载进度回退");previous=session.Progress;
                if(previous>.1f&&previous<.5f)ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/WorldFlowReview/loading.png"));
                yield return null;
            }
            Check(WorldRuntimeGate.Ready&&session.Current!=null&&!menu.Visible,"完成加载未进入游戏");
            Check(session.Current.name.EndsWith("_742031")&&File.Exists(WorldSaveStore.FileFor(session.Current.id)),"默认名字或新世界落盘失败");
            string savePath=WorldSaveStore.FileFor(session.Current.id);File.WriteAllText("Artifacts/WorldFlowReview/test-save-path.txt",savePath);
            yield return null;Check(clock.GameTime>0&&clock.GameTime<5,"开局累计了加载时间");
            menu.ShowPause();float paused=clock.GameTime;yield return new WaitForSecondsRealtime(.2f);
            Check(menu.Visible&&!clock.IsRunning&&clock.GameTime==paused&&GameInputGate.Blocked,"Esc 面板未暂停");
            typeof(GameMainMenu).GetMethod("Resume",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,null);
            Check(!menu.Visible&&!GameInputGate.Blocked&&clock.IsRunning,"返回游戏失败");
            clock.RestoreState(123.5f,4);var genome=new Genome();genome.Add(GeneInstance.Create("gene_growth",10,0));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,builder.transform.position+Vector3.up*(builder.WaterRadius+5),"存档回归实体");
            var entity=EntityRegistry.All.Single(e=>!before.Contains(e));yield return null;
            EntityFavoritesManager.Instance.ToggleFavorite(entity);
            var position=entity.transform.position;var ore=PlanetOreSystem.Instance;ore.ConsumeOre(position,5);float amount=ore.GetOreAmount(position);
            session.SaveNow(false);var saved=WorldSaveStore.Read(savePath);string original=JsonUtility.ToJson(saved);
            Check(saved.entities.Count==1&&saved.entities[0].name==entity.DisplayName&&saved.entities[0].favorite,"实体或收藏未保存");
            Check(saved.gameTime==123.5f&&saved.timeScale==4&&!saved.running,"时钟复合状态未保存");
            saved.name="备份往返";WorldSaveStore.Write(saved);Check(File.Exists(savePath+".bak")&&WorldSaveStore.Read(savePath+".bak").gameTime==123.5f,"原子备份无效");
            var invalid=JsonUtility.FromJson<WorldSaveData>(original);invalid.definitions=null;bool rejected=false;try{session.Open(invalid);}catch{rejected=true;}
            Check(rejected&&!session.Busy&&entity!=null,"损坏存档未在清场前拒绝");
            saved=JsonUtility.FromJson<WorldSaveData>(original);menu.ShowPause();session.Open(saved);float loadTime=clock.GameTime;
            while(session.Busy){Check(clock.GameTime==loadTime&&!clock.IsRunning,"读档期间计时");yield return null;}
            Check(WorldRuntimeGate.Ready&&clock.GameTime==123.5f&&clock.ConfiguredTimeScale==4&&!clock.IsRunning,"读档时钟/暂停恢复失败");
            entity=EntityRegistry.All.Single();Check(entity.DisplayName=="存档回归实体"&&Vector3.Distance(entity.transform.position,position)<.01f,"实体位置或名字未恢复");
            Check(EntityFavoritesManager.Instance.IsFavorite(entity)&&Mathf.Abs(ore.GetOreAmount(position)-amount)<.001f,"收藏或矿量未恢复");
            Check(menu.Visible==false,"暂停状态存档不应停在加载屏");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/WorldFlowReview/loaded.png"));
            File.WriteAllText("Artifacts/WorldFlowReview/result.txt","PASS: deferred initial world; local save/mods paths; stable seeds; new-world default name and disk save; responsive monotonic loading; zero loading-time simulation; pause/resume; entity/favorite/ore/clock round trip; atomic backup; damaged save rejected before clear.");
            Debug.Log("[WorldFlowReview] PASS");
        }
    }
}
