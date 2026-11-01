using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
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
                definitions=definitions.Select(d=>new SavedGeneDefinition{id=d.Key,json=d.Value}).ToList(),tree=tree,entityStateRegistry=EntityStateRegistry.ReadConfiguredJson(),ecologyBalanceSettings=DarwinPark.Entities.Ecology.EcologyBalanceSettings.ReadConfiguredJson()};
            // ★ 适配：新世界保存自己使用的蚁群参数，不受之后 mods 修改影响。
            data.colony=new DarwinPark.Entities.Ecology.ColonyWorldState{settings=DarwinPark.Entities.Ecology.AntColonySettings.ReadConfiguredJson()};
            data.gameSettings=WorldGameSettings.Normalize(null);
            Begin(data,true);
        }
        public void Open(WorldSaveData data){if(!Busy){EnsureInitialized();WorldSaveStore.Validate(data);Begin(data,false);}}
        private void Begin(WorldSaveData data,bool isNew)
        {
            WorldSaveStore.Validate(data);
            Busy=true;WorldRuntimeGate.Ready=false;WorldRuntimeGate.Loading=true;Progress=0;Status="准备世界";ServiceLocator.Get<SimulationClock>().Pause();
            // ★ 新增：只记录世界边界与保存结果，不逐帧打印地形进度或实体数值。
            Debug.Log("[世界] 开始加载 id="+data.id+"，seed="+data.seed+"，新建="+isNew+"，联机副本="+WorldAuthority.IsReplica+"，实体="+data.entities.Count);
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
            // ★ 适配：先释放旧运输关系，再按新世界配置装载独立资源。
            PopulationRangeLayer.ClearWorld();DarwinPark.Entities.Ecology.ColonyWorldState.Prepare(data.colony);
            foreach(var entity in EntityRegistry.All.ToArray()){entity.gameObject.SetActive(false);Destroy(entity.gameObject);}yield return null;
            // ★ 适配：注册表先于基因标签加载，存档中自定义状态无需依赖当前 mods。
            EntityStateRegistry.UseSnapshot(data.entityStateRegistry);
            DarwinPark.Entities.Ecology.EcologyBalanceSettings.UseSnapshot(data.ecologyBalanceSettings);
            GeneRuntimeProfile.Catalog=data.definitions.ToDictionary(d=>d.id,d=>d.json);GeneRuntimeProfile.Tree=data.tree;
            // ★ 适配：新版本只补齐缺失基因，不覆盖该世界已经保存的基因定义。
            GeneCatalogExpansion.Expand(GeneRuntimeProfile.Catalog,GeneRuntimeProfile.Tree);
            GeneDatabase.Reload();GeneDatabase.EnsureLoaded();
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
            // ★ 适配：先恢复上限，但已有实体恢复不受上限裁剪。
            EntityPopulationLimit.UseSnapshot(data.gameSettings);
            var restored=new Dictionary<string,DarwinEntity>();
            for(int i=0;i<data.entities.Count;i++)
            {
                var saved=data.entities[i];var before=EntityRegistry.All.ToArray();
                if(DarwinEntity.Spawner==null)throw new InvalidOperationException("实体生成器尚未就绪");
                DarwinEntity spawned;using(EntityPopulationLimit.RestoreScope()){DarwinEntity.Spawner(saved.genome,saved.position,saved.name);spawned=EntityRegistry.All.Single(e=>!before.Contains(e));saved.Restore(spawned);}
                if(saved.ecology!=null)restored[saved.ecology.id]=spawned;
                if(saved.favorite)EntityFavoritesManager.Instance?.ToggleFavorite(spawned);
                if(i%16==0){Progress=.9f+.06f*(i+1)/Mathf.Max(1,data.entities.Count);yield return null;}
            }
            // ★ 适配：宿主可能排在寄生体之后，必须全体建立后进行第二遍引用恢复。
            foreach(var saved in data.entities)if(saved.ecology!=null&&restored.TryGetValue(saved.ecology.id,out var entity))saved.ecology.Resolve(entity,restored);
            DarwinPark.Entities.Ecology.ColonyWorldState.Restore(data.colony);
            // ★ 适配：旧长孕育必须与新的生长表达同时迁移，否则读档后可能在生产前衰老死亡。
            EcologyLegacySaveUpgrade.Apply(data,EntityRegistry.All);
            var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();
            if(isNew){var position=_builder.transform.position+new Vector3(0,.35f,-1).normalized*orbit.distance;orbit.RestoreView(position,Quaternion.LookRotation(_builder.transform.position-position,Vector3.up),orbit.distance);}
            else orbit.RestoreView(data.cameraPosition,data.cameraRotation,data.cameraDistance);
            ServiceLocator.Get<SimulationClock>().RestoreState(data.gameTime,data.timeScale);
            Current=data;Progress=.98f;Status="完成画面与界面准备";yield return null;yield return new WaitForEndOfFrame();
            if(isNew)SaveNow();Progress=1;Status="加载完成";yield return null;
            Busy=false;WorldRuntimeGate.Loading=false;WorldRuntimeGate.Ready=true;_autoSave=0;Debug.Log("[世界] 加载完成 id="+data.id+"，实体="+EntityRegistry.All.Count);Loaded?.Invoke();
        }
        // ★ 新增：返回主界面先完成保存；失败直接抛回菜单，禁止先清场再丢失玩家进度。
        public void ReturnToMenu(bool running)
        {
            // ★ 修复（防Bug回归）：连接中断时连正在加载的副本也必须取消，不能在断线后继续加载成一个可自动保存的本地世界。
            if(WorldAuthority.IsReplica){StopAllCoroutines();Busy=false;}
            if(Busy||Current==null&&!WorldAuthority.IsReplica)throw new InvalidOperationException("当前世界尚未就绪");
            SaveNow(running);
            ServiceLocator.Get<SimulationClock>().Pause();
            WorldRuntimeGate.Ready=false;WorldRuntimeGate.Loading=false;GameInputGate.Blocked=true;
            EntityViewController.Instance?.ExitView();EntityPlacementTool.Instance?.CancelPlacement();
            foreach(var inspector in FindObjectsByType<GeneInspectorUI>(FindObjectsSortMode.None))inspector.HideEntityInfo();
            EntityFavoritesManager.Instance?.ClearWorld();
            foreach(var entity in EntityRegistry.All.ToArray()){entity.gameObject.SetActive(false);Destroy(entity.gameObject);}
            GeneRuntimeProfile.Clear();GeneDatabase.Reload();
            // ★ 适配：返回菜单清理资源和范围线，下一世界不能继承旧种群的物件。
            PopulationRangeLayer.ClearWorld();DarwinPark.Entities.Ecology.ColonyWorldState.Clear();
            DarwinPark.Entities.Ecology.EcologyBalanceSettings.Clear();
            Debug.Log("[世界] 返回主界面 id="+Current?.id+"，联机副本="+WorldAuthority.IsReplica);
            EntityPopulationLimit.Clear();
            Current=null;_autoSave=0;Progress=0;Status="";SaveStatus="";
        }
        public void SaveNow(bool? running=null)
        {
            // ★ 适配：成员副本不写世界文件，防止自动保存/退出将远端世界误写成本地权威存档。
            if(WorldAuthority.IsReplica){SaveStatus="联机世界由管理员保存";return;}
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
            // ★ 适配：规则与基因快照同次写盘，旧世界不会被后续工具阈值修改静默改变。
            Current.entityStateRegistry=EntityStateRegistry.CurrentJson;
            Current.ecologyBalanceSettings=DarwinPark.Entities.Ecology.EcologyBalanceSettings.CurrentJson;
            Current.colony=DarwinPark.Entities.Ecology.ColonyWorldState.Capture();
            Current.gameSettings=EntityPopulationLimit.Capture();
            WorldSaveStore.Write(Current);SaveStatus="已保存 "+DateTime.Now.ToString("HH:mm:ss");
            Debug.Log("[存档] 保存成功 id="+Current.id+"，实体="+Current.entities.Count+"，模拟时间="+Current.gameTime+"，文件="+WorldSaveStore.FileFor(Current.id));
        }
        // ★ 修复（防Bug回归）：离线录制的临时相机/星球相位不能被自动存档写入世界。
        private void Update(){if(!Busy&&!WorldRuntimeGate.Loading&&!WorldRuntimeGate.Recording&&WorldRuntimeGate.Ready&&Current!=null){_autoSave+=Time.unscaledDeltaTime;if(_autoSave>=autoSaveInterval){_autoSave=0;try{SaveNow();}catch(Exception ex){SaveStatus="自动保存失败："+ex.Message;Debug.LogException(ex);}}}}
        private void OnApplicationQuit(){if(Current!=null&&!Busy)try{SaveNow();}catch(Exception ex){Debug.LogException(ex);}}
    }
}
