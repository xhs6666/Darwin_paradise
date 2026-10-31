using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Environment;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：发送世界使用内存快照，连接/同步不触发磁盘存档；仅房主运行生态与出生/死亡。
    public static class LanWorldSnapshot
    {
        public static WorldSaveData Capture(WorldSessionManager session)
        {
            var data=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(session.Current));var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var clock=ServiceLocator.Get<SimulationClock>();
            data.seed=planet.seed;data.terrainParameters=JsonUtility.ToJson(planet);data.environmentParameters=JsonUtility.ToJson(ServiceLocator.Get<EnvironmentSystem>());
            data.gameTime=clock.GameTime;data.timeScale=clock.ConfiguredTimeScale;data.running=clock.IsRunning;
            var camera=Camera.main;data.cameraPosition=camera.transform.position;data.cameraRotation=camera.transform.rotation;data.cameraDistance=camera.GetComponent<OrbitCamera>().distance;
            data.planetRotation=planet.transform.rotation;data.sunAngle=Object.FindFirstObjectByType<SunSystem>().OrbitAngle;
            data.entities=EntityRegistry.All.Where(EcologyQueries.Alive).Select(EntitySaveState.Capture).ToList();data.colony=ColonyWorldState.Capture();
            data.ore=PlanetOreSystem.Instance.CaptureAmounts();data.oreResolution=PlanetOreSystem.Instance.mapResolution;data.oreTimer=PlanetOreSystem.Instance.RegenTimer;
            data.definitions=(GeneRuntimeProfile.Catalog??GeneTreeStorage.ReadCatalog()).Select(p=>new SavedGeneDefinition{id=p.Key,json=p.Value}).ToList();data.tree=GeneTreeStorage.Active;
            data.entityStateRegistry=EntityStateRegistry.CurrentJson;data.ecologyBalanceSettings=EcologyBalanceSettings.CurrentJson;return data;
        }
        public static string Configuration()
        {
            return JsonUtility.ToJson(Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>())+"|"+JsonUtility.ToJson(ServiceLocator.Get<EnvironmentSystem>())+"|"+
                string.Join("|",(GeneRuntimeProfile.Catalog??GeneTreeStorage.ReadCatalog()).OrderBy(p=>p.Key).Select(p=>p.Value))+JsonUtility.ToJson(GeneTreeStorage.Active);
        }
        public static LanFrame Frame(long sequence,bool includeOre=false)
        {
            var clock=ServiceLocator.Get<SimulationClock>();var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var frame=new LanFrame{sequence=sequence,time=clock.GameTime,scale=clock.ConfiguredTimeScale,running=clock.IsRunning,sun=Object.FindFirstObjectByType<SunSystem>().OrbitAngle,rotation=planet.transform.rotation,
                actors=EntityRegistry.All.Where(EcologyQueries.Alive).Select(e=>new LanActor{state=EntitySaveState.Capture(e),suppression=e.Genes.EnvironmentGeneSuppression}).ToList(),resources=ColonyResource.All.Select(r=>r.Capture()).ToList()};
            var random=Object.FindFirstObjectByType<RandomPlanetSpawner>();if(random!=null){frame.randomBusy=random.IsBusy;frame.randomSpawned=random.Spawned;frame.randomRequested=random.Requested;frame.randomStatus=random.Status;}
            if(includeOre){frame.ore=PlanetOreSystem.Instance.CaptureAmounts();frame.oreResolution=PlanetOreSystem.Instance.mapResolution;frame.oreTimer=PlanetOreSystem.Instance.RegenTimer;}return frame;
        }
    }
}
