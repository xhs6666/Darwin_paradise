using System;
using System.IO;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：优化节奏独立配置；只改变查询与物理采样频率，不改变营养/寿命累计时间。
    [Serializable] public sealed class SimulationOptimization
    {
        public int version=1,workerQueueLimit=12,expressionCacheLimit=256,maximumResourceCells=4096;
        // ★ 新增：仅云/大气代理使用低模；可通过外部配置回退旧网格作对照，世界重载时应用。
        public bool weatherProxyMeshes=true;public int weatherProxySubdivisions=16;
        public bool taskQueue=true,expressionCache=true,localCompetition=true,singletonKernel=true,resourceInstancing=true;
        public float groundProbeMoveTolerance=.1f,groundProbeVerticalSpeed=.1f;
        // ★ 新增：接地直接查询可独立回退；角度和速度阈值仅跳过近乎相同的物理写入。
        public bool directGroundQueries=true;public float groundNormalAngleDegrees=.15f,groundVelocityEpsilon=.0001f;
        public float decisionSeconds=.25f,mateSearchSeconds=.5f,groundProbeSeconds=.2f,resourceCellSize=32,visualRefreshSeconds=.25f,lightDistance=80;
        private static SimulationOptimization _current;private static long _stamp;private static float _poll;
        public static string FilePath=>Path.Combine(GamePaths.Root,"config","simulation_optimization.json");
        public static SimulationOptimization Current {get{if(_current==null)Reload();return _current;}}
        public static SimulationOptimization Parse(string text)
        {
            var value=JsonUtility.FromJson<SimulationOptimization>(CommentedJson.Strip(text));
            if(value!=null&&(value.weatherProxySubdivisions<4||value.weatherProxySubdivisions>128))throw new InvalidDataException("天气代理细分必须为4～128");
            if(value!=null&&(value.groundNormalAngleDegrees>1||value.groundVelocityEpsilon>.01f))throw new InvalidDataException("接地写入阈值超出有效范围");
            if(value==null||value.version!=1||value.workerQueueLimit<1||value.workerQueueLimit>128||value.expressionCacheLimit<1||value.expressionCacheLimit>4096||value.maximumResourceCells<1||value.maximumResourceCells>1000000)throw new InvalidDataException("计算优化配置版本或数量无效");
            foreach(var field in typeof(SimulationOptimization).GetFields())if(field.FieldType==typeof(float)){float n=(float)field.GetValue(value);if(!float.IsFinite(n)||n<=0)throw new InvalidDataException("计算优化参数必须为正有限数："+field.Name);}
            return value;
        }
        public static void Reload()
        {
            var asset=Resources.Load<TextAsset>("simulation_optimization");if(asset==null)throw new InvalidDataException("缺少 simulation_optimization");
            var fallback=Parse(asset.text);
            try{if(!File.Exists(FilePath)){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));File.WriteAllText(FilePath,asset.text,new System.Text.UTF8Encoding(false));}_current=Parse(File.ReadAllText(FilePath));_stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;}
            catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException){if(_current==null)_current=fallback;Debug.LogWarning("[计算优化] 保留有效配置："+ex.Message);}
        }
        // ★ 新增：全局菜单已有的单一轮询入口调用，不能让每名实体访问磁盘。
        public static void Poll()
        {
            if(Time.unscaledTime<_poll)return;_poll=Time.unscaledTime+2;
            if(_current==null){Reload();return;}
            try{long stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;if(stamp!=_stamp){_stamp=stamp;Reload();}}catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException){Debug.LogWarning("[计算优化] 检查配置失败："+ex.Message);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_current=null;_stamp=0;_poll=0;}
    }
}
