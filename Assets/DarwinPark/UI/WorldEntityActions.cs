using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：清理和世界设置只由权威端执行，UI隐藏之外仍保留服务端权限校验。
    public static class WorldEntityActions
    {
        public static bool SetLimit(int limit,bool running)
        {
            var world=Object.FindFirstObjectByType<WorldSessionManager>();
            if(WorldAuthority.IsReplica||!WorldAuthority.CanEdit||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||limit<0||world?.Current==null)return false;
            var previous=EntityPopulationLimit.Capture();
            try
            {
                EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=limit});world.Current.gameSettings=EntityPopulationLimit.Capture();world.SaveNow(running);return true;
            }
            catch{EntityPopulationLimit.UseSnapshot(previous);world.Current.gameSettings=previous;throw;}
        }
        public static bool ClearAll()
        {
            if(WorldAuthority.IsReplica||!WorldAuthority.CanEdit||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading)return false;
            foreach(var job in Object.FindObjectsByType<RandomPlanetSpawner>(FindObjectsSortMode.None))job.Cancel();
            PopulationMaintenance.SuspendAll();
            EntityPlacementTool.Instance?.CancelPlacement();EntityViewController.Instance?.ExitView();
            foreach(var inspector in Object.FindObjectsByType<GeneInspectorUI>(FindObjectsSortMode.None))inspector.HideEntityInfo();
            EntityFavoritesManager.Instance?.ClearWorld();var entities=EntityRegistry.All.ToArray();
            // ★ 修复（防Bug回归）：先释放运输关系再注销全体，避免清理工匠时将已清理的活体重新激活。
            foreach(var entity in entities){entity.GetComponent<EntityManualControl>()?.Release();var member=entity.GetComponent<AntColonyMember>();if(member!=null)AntColonyWorker.Release(member);}
            foreach(var entity in entities)if(entity!=null){entity.gameObject.SetActive(false);Object.Destroy(entity.gameObject);}
            EntityRegistry.Clear();PopulationRangeLayer.ClearWorld();
            var world=Object.FindFirstObjectByType<WorldSessionManager>();if(world?.Current!=null)world.SaveNow(ServiceLocator.Get<SimulationClock>().IsRunning);
            Debug.Log("[实体清理] 已清除全部实体："+entities.Length);return true;
        }
    }
}
