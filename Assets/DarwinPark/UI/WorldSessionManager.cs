using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Environment;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.UI
{
    // ★ 新增：计算阶段后台执行，Unity 网格/纹理/实体分帧接入，进度完成前不推进游戏时间。
    public sealed class WorldSessionManager : MonoBehaviour
    {
        public float autoSaveInterval=120;
        public float defaultLand=.5f,defaultTemperature=20,defaultHumidity=44;
        // ★ 新增：创建选项范围由 Inspector 配置，菜单不另设一套内容阈值。
        public Vector2 landRange=new Vector2(.2f,.8f),temperatureRange=new Vector2(-30,50),humidityRange=new Vector2(0,100);
        public WorldSaveData Current {get;private set;}
        public float Progress {get;private set;}
        public string Status {get;private set;}="";
        public bool Busy {get;private set;}
        public string SaveStatus {get;private set;}="";
        public event Action Loaded;
        public event Action<string> Failed;
        private PlanetTerrainMeshBuilder _builder;
        private string _initialTerrain,_initialEnvironment;
        private float _autoSave;
        private void Start()=>EnsureInitialized();
        private void EnsureInitialized()
        {
            if(_builder!=null)return;
            _builder=FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            if(_builder==null)throw new InvalidOperationException("缺少星球地形组件");
            _initialTerrain=JsonUtility.ToJson(_builder);
            _initialEnvironment=JsonUtility.ToJson(ServiceLocator.Get<EnvironmentSystem>());
            defaultLand=_builder.continentRatio;var environment=ServiceLocator.Get<EnvironmentSystem>();defaultTemperature=environment.BaseTemperature;defaultHumidity=environment.BaseHumidity;
        }
        public void Create(string name,int seed,float land,float temperature,float humidity)
        {
            if(Busy)return;
            EnsureInitialized();
            var definitions=GeneTreeStorage.ReadCatalog();var tree=GeneTreeStorage.Load();
            GeneTreeStorage.Validate(tree,definitions);
            var material=_builder.TerrainMaterial;JsonUtility.FromJsonOverwrite(_initialTerrain,_builder);_builder.TerrainMaterial=material;
            _builder.seed=seed;_builder.continentRatio=land;
            var environment=ServiceLocator.Get<EnvironmentSystem>();JsonUtility.FromJsonOverwrite(_initialEnvironment,environment);environment.BaseTemperature=temperature;environment.BaseHumidity=humidity;
            var data=new WorldSaveData{id=Guid.NewGuid().ToString("N"),name=string.IsNullOrWhiteSpace(name)?WorldSaveStore.DefaultName(seed):name.Trim(),seed=seed,createdUtc=DateTime.UtcNow.ToString("O"),
                terrainParameters=JsonUtility.ToJson(_builder),environmentParameters=JsonUtility.ToJson(environment),planetRotation=Quaternion.identity,sunAngle=45,
                definitions=definitions.Select(d=>new SavedGeneDefinition{id=d.Key,json=d.Value}).ToList(),tree=tree};
            Begin(data,true);
        }
        public void Open(WorldSaveData data){if(!Busy){EnsureInitialized();WorldSaveStore.Validate(data);Begin(data,false);}}
        private void Begin(WorldSaveData data,bool isNew)
        {
            WorldSaveStore.Validate(data);
            Busy=true;WorldRuntimeGate.Ready=false;WorldRuntimeGate.Loading=true;Progress=0;Status="准备世界";ServiceLocator.Get<SimulationClock>().Pause();
            StartCoroutine(SafeLoad(Load(data,isNew)));
        }
        private IEnumerator SafeLoad(IEnumerator operation)
        {
            while(true)
            {
                bool next;object current=null;
                try{next=operation.MoveNext();if(next)current=operation.Current;}
                catch(Exception ex){Busy=false;Current=null;WorldRuntimeGate.Loading=false;WorldRuntimeGate.Ready=false;Status="加载失败："+ex.Message;Debug.LogException(ex);Failed?.Invoke(Status);yield break;}
                if(!next)yield break;yield return current;
            }
        }
        private IEnumerator Load(WorldSaveData data,bool isNew)
        {
            yield return null;yield return new WaitForEndOfFrame();
            EntityViewController.Instance?.ExitView();EntityPlacementTool.Instance?.CancelPlacement();
            foreach(var entity in EntityRegistry.All.ToArray()){entity.gameObject.SetActive(false);Destroy(entity.gameObject);}yield return null;
            GeneRuntimeProfile.Catalog=data.definitions.ToDictionary(d=>d.id,d=>d.json);GeneRuntimeProfile.Tree=data.tree;GeneDatabase.Reload();GeneDatabase.EnsureLoaded();
            var material=_builder.TerrainMaterial;JsonUtility.FromJsonOverwrite(data.terrainParameters,_builder);_builder.TerrainMaterial=material;
            var environment=ServiceLocator.Get<EnvironmentSystem>();JsonUtility.FromJsonOverwrite(data.environmentParameters,environment);
            ClimateDataHub.BaseTemperature=environment.BaseTemperature;ClimateDataHub.BaseHumidity=environment.BaseHumidity;
            var config=_builder.CaptureConfiguration();float sea=_builder.seaLevelRatio,land=_builder.continentRatio;
            Progress=.12f;Status="生成地形与山脉";var terrainTask=Task.Run(()=>PlanetTerrainGenerator.Generate(config,sea,land));
            while(!terrainTask.IsCompleted)yield return null;if(terrainTask.IsFaulted)throw terrainTask.Exception;
            Progress=.52f;Status="装配地表、碰撞与天气";yield return null;
            _builder.ApplyGeneratedData(terrainTask.Result);_builder.transform.rotation=data.planetRotation;yield return null;
            var climate=PlanetClimateMap.Instance;var climateConfig=JsonUtility.FromJson<PlanetClimateConfig>(JsonUtility.ToJson(PlanetClimateSystem.Instance.config));
            var terrain=_builder.Data;int resolution=climate.mapResolution;int seed=_builder.seed;
            float temp=environment.BaseTemperature-climateConfig.baseTemperatureReference,hum=environment.BaseHumidity-climateConfig.baseHumidityReference;
            Progress=.66f;Status="计算温度、湿度与全球气候";var climateTask=Task.Run(()=>new PlanetClimateModel(terrain,climateConfig,seed).Generate(resolution,temp,hum));
            while(!climateTask.IsCompleted)yield return null;if(climateTask.IsFaulted)throw climateTask.Exception;
            Progress=.85f;Status="上传气候与恢复资源";yield return null;climate.ApplyGeneratedClimate(climateTask.Result);
            var ore=PlanetOreSystem.Instance;if(data.ore!=null){ore.RestoreAmounts(data.ore,data.oreResolution);ore.RegenTimer=data.oreTimer;}else ore.ResetWorld();
            FindFirstObjectByType<SunSystem>().OrbitAngle=data.sunAngle;
            Progress=.9f;Status="恢复实体、基因与生命周期";yield return null;
            for(int i=0;i<data.entities.Count;i++)
            {
                var saved=data.entities[i];var before=EntityRegistry.All.ToArray();
                if(DarwinEntity.Spawner==null)throw new InvalidOperationException("实体生成器尚未就绪");
                DarwinEntity.Spawner(saved.genome,saved.position,saved.name);var spawned=EntityRegistry.All.Single(e=>!before.Contains(e));saved.Restore(spawned);
                if(saved.favorite)EntityFavoritesManager.Instance?.ToggleFavorite(spawned);
                if(i%16==0){Progress=.9f+.06f*(i+1)/Mathf.Max(1,data.entities.Count);yield return null;}
            }
            var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();
            if(isNew){var position=_builder.transform.position+new Vector3(0,.35f,-1).normalized*orbit.distance;orbit.RestoreView(position,Quaternion.LookRotation(_builder.transform.position-position,Vector3.up),orbit.distance);}
            else orbit.RestoreView(data.cameraPosition,data.cameraRotation,data.cameraDistance);
            ServiceLocator.Get<SimulationClock>().RestoreState(data.gameTime,data.timeScale);
            Current=data;Progress=.98f;Status="完成画面与界面准备";yield return null;yield return new WaitForEndOfFrame();
            if(isNew)SaveNow();Progress=1;Status="加载完成";yield return null;
            Busy=false;WorldRuntimeGate.Loading=false;WorldRuntimeGate.Ready=true;_autoSave=0;Loaded?.Invoke();
        }
        public void SaveNow(bool? running=null)
        {
            if(Current==null)throw new InvalidOperationException("没有可保存的世界");
            var clock=ServiceLocator.Get<SimulationClock>();var environment=ServiceLocator.Get<EnvironmentSystem>();var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();
            Current.seed=_builder.seed;Current.terrainParameters=JsonUtility.ToJson(_builder);Current.environmentParameters=JsonUtility.ToJson(environment);
            Current.gameTime=clock.GameTime;Current.timeScale=clock.ConfiguredTimeScale;
            if(running.HasValue)Current.running=running.Value;else if(!GameInputGate.Blocked)Current.running=clock.IsRunning;
            Current.cameraPosition=camera.transform.position;Current.cameraRotation=camera.transform.rotation;Current.cameraDistance=orbit.distance;
            Current.planetRotation=_builder.transform.rotation;Current.sunAngle=FindFirstObjectByType<SunSystem>().OrbitAngle;
            Current.entities=EntityRegistry.All.Where(e=>e!=null&&e.isActiveAndEnabled).Select(e=>{var state=EntitySaveState.Capture(e);state.favorite=EntityFavoritesManager.Instance?.IsFavorite(e)==true;return state;}).ToList();
            Current.ore=PlanetOreSystem.Instance.CaptureAmounts();Current.oreResolution=PlanetOreSystem.Instance.mapResolution;Current.oreTimer=PlanetOreSystem.Instance.RegenTimer;
            // 使用当前表达配置快照，保证工具应用到世界后也能持久化。
            var catalog=GeneRuntimeProfile.Catalog??GeneTreeStorage.ReadCatalog();Current.definitions=catalog.Select(d=>new SavedGeneDefinition{id=d.Key,json=d.Value}).ToList();Current.tree=GeneTreeStorage.Active;
            WorldSaveStore.Write(Current);SaveStatus="已保存 "+DateTime.Now.ToString("HH:mm:ss");
        }
        // ★ 修复（防Bug回归）：离线录制的临时相机/星球相位不能被自动存档写入世界。
        private void Update(){if(!Busy&&!WorldRuntimeGate.Recording&&WorldRuntimeGate.Ready&&Current!=null){_autoSave+=Time.unscaledDeltaTime;if(_autoSave>=autoSaveInterval){_autoSave=0;try{SaveNow();}catch(Exception ex){SaveStatus="自动保存失败："+ex.Message;Debug.LogException(ex);}}}}
        private void OnApplicationQuit(){if(Current!=null&&!Busy)try{SaveNow();}catch(Exception ex){Debug.LogException(ex);}}
    }
}
