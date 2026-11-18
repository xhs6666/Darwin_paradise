using System.Linq;
using DarwinPark.Core;
using DarwinPark.Genetics;
using DarwinPark.Planet;
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
            GameplayPresentation.ClearWorld?.Invoke();var entities=EntityRegistry.All.ToArray();
            // ★ 修复（防Bug回归）：先释放运输关系再注销全体，避免清理工匠时将已清理的活体重新激活。
            foreach(var entity in entities){entity.GetComponent<EntityManualControl>()?.Release();var member=entity.GetComponent<AntColonyMember>();if(member!=null)AntColonyWorker.Release(member);}
            foreach(var entity in entities)if(entity!=null){entity.gameObject.SetActive(false);Object.Destroy(entity.gameObject);}
            EntityRegistry.Clear();
            var world=Object.FindFirstObjectByType<WorldSessionManager>();if(world?.Current!=null)world.SaveNow(ServiceLocator.Get<SimulationClock>().IsRunning);
            Debug.Log("[实体清理] 已清除全部实体："+entities.Length);return true;
        }
        // ★ 适配：批量落点和权限属于玩法服务，不由界面工具承担权威计算。
        public static int SpawnBatch(Genome genome,Vector3 point,Vector3 outward,float height,int count,float spacing)
        {
            if(WorldAuthority.IsReplica||!WorldAuthority.CanEdit)return 0;
            var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            if(builder==null)return 0;
            var ground=builder.GetComponent<MeshCollider>();if(ground==null)return 0;
            var a=Vector3.Cross(outward,Mathf.Abs(outward.y)>.95f?Vector3.right:Vector3.up).normalized;
            var b=Vector3.Cross(outward,a);int spawned=0;
            float outer=ground.bounds.extents.magnitude+height;
            for(int i=0;i<count&&EntityPopulationLimit.CanSpawn;i++)
            {
                float radius=spacing*Mathf.Sqrt(i), angle=i*2.39996323f;
                var dir=(point-builder.transform.position+a*(Mathf.Cos(angle)*radius)+b*(Mathf.Sin(angle)*radius)).normalized;
                if(ground.Raycast(new Ray(builder.transform.position+dir*outer,-dir),out var hit,outer) &&
                    LanEditActions.Spawn(genome,hit.point+dir*height))spawned++;
            }
            return spawned;
        }
    }
}
