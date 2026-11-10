using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
namespace DarwinPark.EditorTools
{
    // ★ 新增：真实生成协程拒绝前两次工厂调用，验证局部失败回退和分帧后仍完成全部数量，而非靠停止投放提高FPS。
    internal static class SpawnBudgetChecks
    {
        internal static IEnumerator Verify(Action<bool,string> check)
        {
            var spawner=UnityEngine.Object.FindFirstObjectByType<RandomPlanetSpawner>();bool enabled=spawner.enabled;spawner.Cancel();spawner.enabled=false;
            var clock=ServiceLocator.Get<SimulationClock>();bool running=clock.IsRunning;var saved=PopulationMaintenance.Capture();var config=SimulationOptimization.Current;float milliseconds=config.spawnFrameBudgetMilliseconds;int failures=config.localPlacementFailureLimit;
            var original=DarwinEntity.Spawner;var created=new List<DarwinEntity>();void Capture(DarwinEntity actor)=>created.Add(actor);int calls=0;
            try
            {
                var source=EcologyDefaults.Template("producer");var rule=new PopulationMaintenanceRule{id=Guid.NewGuid().ToString("N"),source=source,category=PopulationRuleCategory.Producers,ratio=.7f,minimumCount=30};PopulationMaintenance.Restore(new PopulationMaintenanceState{rules=new List<PopulationMaintenanceRule>{rule}});
                config.spawnFrameBudgetMilliseconds=.0001f;config.localPlacementFailureLimit=1;DarwinEntity.Spawner=(genome,point,name)=>{calls++;if(calls>2)original(genome,point,name);};EntityRegistry.OnRegistered+=Capture;clock.Play();int frame=Time.frameCount;
                bool started=(bool)typeof(RandomPlanetSpawner).GetMethod("StartInternal",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(spawner,new object[]{source,3,true,rule});check(started,"生成预算夹具启动真实自动补充协程");
                double began=EditorApplication.timeSinceStartup;while(spawner.IsBusy){if(EditorApplication.timeSinceStartup-began>20)throw new TimeoutException(spawner.Status);yield return null;}
                check(spawner.Spawned==3&&spawner.Requested==3&&created.Count==3,"前两次出生失败后仍补齐请求的三个实体");
                check(spawner.GlobalFallbacks>0,"局部投放失败后实际转全局搜索");check(spawner.YieldedSlices>=3&&Time.frameCount>frame+2,"时间预算让真实生成跨帧完成，不降低请求数量");
            }
            finally
            {
                DarwinEntity.Spawner=original;EntityRegistry.OnRegistered-=Capture;spawner.Cancel();spawner.enabled=enabled;config.spawnFrameBudgetMilliseconds=milliseconds;config.localPlacementFailureLimit=failures;PopulationMaintenance.Restore(saved);foreach(var actor in created)if(actor!=null)UnityEngine.Object.DestroyImmediate(actor.gameObject);if(!running)clock.Pause();
            }
        }
    }
}
