using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Environment;
using DarwinPark.Planet;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.UI
{
    // ★ 新增：在原世界后台重算参数，进度动画期间冻结模拟；不重建实体、基因、日期、存档或镜头。
    public sealed class WorldParameterUpdate:MonoBehaviour
    {
        public static WorldParameterUpdate Instance {get;private set;}
        public bool Busy {get;private set;}public float Progress {get;private set;}public string Status {get;private set;}="";
        public string LastBackupPath {get;private set;}
        public string LastStatus {get;private set;}="";public Vector2 temperatureRange=new Vector2(-30,60),humidityRange=new Vector2(0,100),landPercentRange=new Vector2(20,80);
        public float minimumSurfaceOffset=.5f,maximumPreservedAltitude=12;
        private bool _blocked,_running,_loading;private IDisposable _presentation;
        private sealed class BodyState {public DarwinEntity entity;public Rigidbody body;public Vector3 velocity,angular,position,direction;public float altitude;public bool relocate;}
        private readonly List<BodyState> _bodies=new List<BodyState>();
        private void Awake()=>Instance=this;
        public static WorldParameterUpdate Ensure(){return Instance!=null?Instance:ServiceLocator.Get<SimulationClock>().gameObject.AddComponent<WorldParameterUpdate>();}
        public bool Request(string id,string raw,out string message)
        {
            message="";var world=Object.FindFirstObjectByType<WorldSessionManager>();var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            if(Busy||WorldRuntimeGate.Loading||world?.Busy==true){message="正在更新，请稍候";return false;}
            if(!WorldAuthority.CanEdit||WorldAuthority.IsReplica||!WorldRuntimeGate.Ready||builder?.Data==null){message="当前世界不可修改";return false;}
            string kind=WorldLandformEdits.Kind(id)??(id!=null&&id.Contains("seed")?"seed":id!=null&&id.Contains("terrain")?"terrain":id!=null&&id.Contains("temp")?"temp":id!=null&&id.Contains("hum")?"hum":null);
            if(kind==null){message="未知参数";return false;}
            int seed=builder.seed;float value;
            if(kind=="seed")
            {if(!int.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out seed)){message="请输入有效的32位整数种子";return false;}value=0;}
            else if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||!float.IsFinite(value)){message="请输入有效数字";return false;}
            var environment=ServiceLocator.Get<EnvironmentSystem>();
            var range=kind=="temp"?temperatureRange:kind=="hum"?humidityRange:landPercentRange;
            if(kind=="landforms"&&value!=0&&value!=1){message="地貌开关只接受0或1";return false;}
            if(kind!="seed"&&kind!="landforms"&&(value<range.x||value>range.y)){message="数值超出允许范围";return false;}
            bool unchanged=kind=="landforms"?builder.landforms.enabled==(value>0)&&(value<=0||builder.landforms.version==LandformSettings.CurrentVersion):kind=="seed"?seed==builder.seed:kind=="terrain"?Mathf.Approximately(value/100,builder.continentRatio):kind=="temp"?Mathf.Approximately(value,environment.BaseTemperature):Mathf.Approximately(value,environment.BaseHumidity);
            if(unchanged){message="参数未变化";LastStatus=message;return true;}
            LastBackupPath=null;BeginCapture();Busy=true;Progress=0;Status="准备更新参数";LastStatus="正在更新";
            StartCoroutine(Guard(UpdateWorld(builder,environment,world,kind,seed,value)));message="正在更新";return true;
        }
        private void BeginCapture()
        {
            _blocked=GameInputGate.Blocked;_loading=WorldRuntimeGate.Loading;_running=ServiceLocator.Get<SimulationClock>().IsRunning;
            GameInputGate.Blocked=true;WorldRuntimeGate.Loading=true;ServiceLocator.Get<SimulationClock>().Pause();
            _presentation=GameplayPresentation.BeginParameterUpdate?.Invoke();
            foreach(var job in Object.FindObjectsByType<RandomPlanetSpawner>(FindObjectsSortMode.None))job.Cancel();
            _bodies.Clear();
            foreach(var entity in EntityRegistry.All)
            {
                var body=entity.GetComponent<Rigidbody>();_bodies.Add(new BodyState{entity=entity,body=body,position=entity.transform.position,velocity=body!=null&&!body.isKinematic?body.linearVelocity:Vector3.zero,angular=body!=null&&!body.isKinematic?body.angularVelocity:Vector3.zero});
            }
        }
        private IEnumerator Guard(IEnumerator operation)
        {
            string error=null;
            while(true)
            {
                bool next=false;object current=null;try{next=operation.MoveNext();if(next)current=operation.Current;}catch(Exception ex){error=ex.Message;Debug.LogException(ex);}
                if(error!=null||!next)break;yield return current;
            }
            LastStatus=error==null?"已应用"+(string.IsNullOrEmpty(LastBackupPath)?"":"；旧世界备份："+LastBackupPath):"更新失败："+error;Finish();
        }
        private IEnumerator UpdateWorld(PlanetTerrainMeshBuilder builder,EnvironmentSystem environment,WorldSessionManager world,string kind,int seed,float value)
        {
            // ★ 修复（防Bug回归）：先让加载界面真正画出来，再启动重计算，不在应用按钮回调里同步Build造成假死。
            // ★ 修复（防Bug回归）：无图形设备也必须推进参数更新；表现适配器自行显示进度。
            yield return null;yield return null;
            var config=builder.CaptureConfiguration();var oldData=builder.Data;bool terrain=kind=="seed"||kind=="terrain"||kind=="landforms";
            // ★ 新增：显式升级先保存完整旧世界到独立备份目录，不覆盖原地形的唯一副本；失败则停止重建。
            if(kind=="landforms"&&world?.Current!=null)
            {world.SaveNow(_running);string original=WorldSaveStore.FileFor(world.Current.id);string folder=Path.Combine(Path.GetDirectoryName(original),"backups");Directory.CreateDirectory(folder);LastBackupPath=Path.Combine(folder,"terrain-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".json");WorldSaveStore.Write(world.Current,LastBackupPath);}
            WorldLandformEdits.Apply(config,kind,value);TerrainResourceRelocation resources=terrain?new TerrainResourceRelocation(builder,minimumSurfaceOffset,maximumPreservedAltitude):null;
            config.seed=kind=="seed"?seed:builder.seed;float land=kind=="terrain"?value/100:builder.continentRatio;
            float temp=kind=="temp"?value:environment.BaseTemperature,humidity=kind=="hum"?value:environment.BaseHumidity;
            var data=oldData;
            if(terrain)
            {
                foreach(var body in _bodies)
                {
                    if(body.entity==null)continue;var offset=body.position-builder.transform.position;body.direction=offset.normalized;
                    float oldRadius=builder.radius+PlanetTerrainSampler.SampleHeight(oldData,builder.transform.InverseTransformDirection(body.direction))*builder.heightScale;
                    body.altitude=Mathf.Clamp(offset.magnitude-oldRadius,minimumSurfaceOffset,maximumPreservedAltitude);
                    // ★ 适配：新营养寄生体的位置由宿主恢复，不能被地形重算独立投到地面。
                    body.relocate=!AntColonyWorker.IsCarried(body.entity)&&body.entity.GetComponent<ParasiteBehavior>()?.Host==null&&body.entity.GetComponent<NutritionParasite>()?.Host==null;
                }
                Progress=.12f;Status="生成地形与山脉";float sea=builder.seaLevelRatio;
                var task=Task.Run(()=>PlanetTerrainGenerator.Generate(config,sea,land));while(!task.IsCompleted)yield return null;if(task.IsFaulted)throw task.Exception;data=task.Result;
            }
            Progress=terrain ? .58f : .2f;Status="计算温度、湿度与气候";
            var climate=PlanetClimateMap.Instance;var climateConfig=JsonUtility.FromJson<PlanetClimateConfig>(JsonUtility.ToJson(PlanetClimateSystem.Instance.config));
            int resolution=climate.mapResolution,climateSeed=config.seed;var climateData=data;
            var climateTask=Task.Run(()=>new PlanetClimateModel(climateData,climateConfig,climateSeed).Generate(resolution,temp-climateConfig.baseTemperatureReference,humidity-climateConfig.baseHumidityReference));
            while(!climateTask.IsCompleted)yield return null;if(climateTask.IsFaulted)throw climateTask.Exception;
            Progress=.85f;Status="更新画面与地表碰撞";yield return null;
            if(terrain){builder.seed=config.seed;builder.continentRatio=land;builder.landforms=config.landforms;builder.ApplyGeneratedData(data);StationaryBodyRest.Clear();}
            environment.BaseTemperature=temp;environment.BaseHumidity=humidity;ClimateDataHub.BaseTemperature=temp;ClimateDataHub.BaseHumidity=humidity;climate.ApplyGeneratedClimate(climateTask.Result);
            foreach(var body in _bodies)if(body.entity!=null)
            {
                Vector3 position=body.position;
                if(terrain&&body.relocate){float radius=builder.radius+PlanetTerrainSampler.SampleHeight(data,builder.transform.InverseTransformDirection(body.direction))*builder.heightScale;position=builder.transform.position+body.direction*(radius+body.altitude);}
                body.entity.transform.position=position;if(body.body!=null)body.body.position=position;EntityNeighborhood.ObservePosition(body.entity.Genes);
            }
            resources?.Restore(builder);
            Physics.SyncTransforms();Progress=.96f;Status="保存参数并恢复原世界";yield return null;
            if(world?.Current!=null)world.SaveNow(_running);
            Progress=1;Status="更新完成";yield return null;
            Debug.Log("[世界参数] 原世界更新完成："+kind+"，实体="+EntityRegistry.All.Count);
        }
        private void Finish()
        {
            if(!Busy)return;
            foreach(var state in _bodies)if(state.body!=null&&!state.body.isKinematic){state.body.linearVelocity=state.velocity;state.body.angularVelocity=state.angular;}
            _bodies.Clear();_presentation?.Dispose();_presentation=null;
            GameInputGate.Blocked=_blocked;WorldRuntimeGate.Loading=_loading;Busy=false;if(_running&&WorldRuntimeGate.Ready&&ServiceLocator.TryGet<SimulationClock>(out var clock))clock.Play();
        }

        private void OnDisable(){StopAllCoroutines();Finish();}
        private void OnDestroy(){if(Instance==this)Instance=null;}
    }
    public static class WorldLandformEdits
    {
        public static string Kind(string id)=>id=="landforms"?"landforms":null;
        public static void Apply(PlanetTerrainConfig config,string kind,float value){if(kind=="landforms")
            {config.landforms??=new LandformSettings();config.landforms.enabled=value>0;
                // ★ 适配：旧档只在玩家明确启用时升级算法；读档和其他参数修改继续使用旧版本。
                if(value>0){if(config.landforms.version<2){var defaults=new LandformSettings();if(config.landforms.basinTransition<=0){config.landforms.basinTransition=defaults.basinTransition;config.landforms.basinThreshold=defaults.basinThreshold;}if(config.landforms.lakeMeshSubdivisions<1)config.landforms.lakeMeshSubdivisions=defaults.lakeMeshSubdivisions;}
                    // ★ 适配：显式升级时补齐旧档没有的第三版字段；普通读取继续使用原算法及原地形。
                    if(config.landforms.version<3){var defaults=new LandformSettings();config.landforms.riverWidthExponent=defaults.riverWidthExponent;config.landforms.riverMaximumWidth=defaults.riverMaximumWidth;config.landforms.channelSmoothing=defaults.channelSmoothing;config.landforms.basinCount=defaults.basinCount;config.landforms.basinCandidates=defaults.basinCandidates;config.landforms.basinRadius=defaults.basinRadius;config.landforms.basinLakeDepth=defaults.basinLakeDepth;config.landforms.basinMaximumRelief=defaults.basinMaximumRelief;config.landforms.basinSeparation=defaults.basinSeparation;}
                    if(config.landforms.version<3){var defaults=new LandformSettings();config.landforms.riverCurveSegments=defaults.riverCurveSegments;config.landforms.riverCurveStrength=defaults.riverCurveStrength;config.landforms.basinAspectRatio=defaults.basinAspectRatio;config.landforms.basinIrregularity=defaults.basinIrregularity;
                        // ★ 修复（防Bug回归）：旧档自定义宽河/深湖阈值不能因新默认上限而成为无效配置。
                        config.landforms.riverMaximumWidth=Mathf.Max(config.landforms.riverMaximumWidth,config.landforms.riverHalfWidth);config.landforms.basinLakeDepth=Mathf.Max(config.landforms.basinLakeDepth,config.landforms.lakeMinimumDepth+defaults.basinLakeDepth-defaults.lakeMinimumDepth);}
                    // ★ 适配：显式更新水系才启用加深河床；普通读档保留原地形。
                    if(config.landforms.version<4)config.landforms.riverDepthMultiplier=new LandformSettings().riverDepthMultiplier;
                    if(config.landforms.version<5)
                    {
                        var defaults=new LandformSettings();
                        config.landforms.oceanDepthMultiplier=defaults.oceanDepthMultiplier;
                        config.landforms.oceanShelfStart=defaults.oceanShelfStart;
                        config.landforms.lakeDepthMultiplier=defaults.lakeDepthMultiplier;
                    }
                    config.landforms.version=LandformSettings.CurrentVersion;}}}
    }
}
