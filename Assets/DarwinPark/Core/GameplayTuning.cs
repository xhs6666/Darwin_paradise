using System;
using System.IO;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：安装目录中可直接修改的全局数值；重启后同时作用于新旧世界，不覆盖世界保存的基因配置。
    [Serializable] public sealed class GameplayTuning
    {
        public int version=1;
        public float baseMetabolismMultiplier=.5f, movementMetabolismMultiplier=.5f;
        public float repairFoodPerHealth=.5f, starvationDamagePerSecond=1;
        public float manualAimPadding=.85f, manualHitboxScale=1.5f, manualAttackReachBonus=.35f;
        public float supplyActivityRadiusRatio=1;
        public float colonyActivityRadiusMultiplier=.35f, populationFillOpacity=.22f;
        // ★ 新增：成年且无合适配偶的连续等待天数，外部配置可热更新。
        public float parthenogenesisWaitDays=5;
        // ★ 新增：收支一起放慢；补给使用吃饱恢复线，避免阈值附近反复折返。
        public float nutritionGainMultiplier=.5f, memberFeedResume=9.5f, queenReserveBuffer=10;
        // ★ 修复（防Bug回归）：专用补给缺失或身边已有备用块时允许跨用途进食，仍保护活着的外群与王后最低口粮。
        public bool colonyShareBackupFood=true;
        public float colonyEmergencyFeedThreshold=3;
        // ★ 新增：相近同基因静止实体共享基础结算；受伤/移动立即退出，繁殖仍逐个计算。
        public bool mergeStationaryEntities=true;
        // ★ 新增：错开空间/基因批次，消除大量孤立组同一帧结算的峰值；不改变累计模拟时间。
        public bool mergeStaggerBatches=true;
        public int mergePhaseBuckets=16;
        public float mergeRadius=6, mergeInterval=.5f, mergeQuietSeconds=2, mergeMaximumMembers=32;
        public float mergeGroupsPerFrame=128, mutationAttemptsPerFrame=16, mergedMutationFraction=.125f;
        // ★ 新增：按全部活体数量确定每轮名额；每帧限额只分摊队列，不丢弃剩余名单。
        public bool mutationRoundsEnabled=true;
        public float mutationPopulationFraction=.1f,mutationRoundSeconds=30;
        // ★ 适配：0定时抽样，1原生命阶段；共用变异总开关。
        public int mutationMode=0,corpseCleanupPerFrame=64;
        public float stageMutationProbability=.3f,stageEnvironmentBoost=.25f,corpseCleanupSeconds=300;
        public bool corpseCleanupEnabled=false;
        // ★ 新增：比例调节默认关闭；启用后只抑制超额繁殖，不直接生成实体。
        public bool populationRatioControlEnabled=false;
        public float multipleBirthMaximum=5;
        public float colonyBirthReservedSlots=2, colonySuccessorPopulation=3;
        public float mergeMaximumCatchupSeconds=5;
        public float mergeFoodTolerance=.25f, mergeReserveTolerance=2, mergeWakeDistance=.1f;
        private static GameplayTuning _current;
        private static long _stamp;private static float _nextPoll;
        public static string FilePath=>Path.Combine(GamePaths.Root,"config","gameplay.json");
        public static GameplayTuning Current {get{if(_current==null)Reload();return _current;}}
        public static GameplayTuning Parse(string json)
        {
            var value=JsonUtility.FromJson<GameplayTuning>(CommentedJson.Strip(json));
            if(value==null||value.version!=1)throw new InvalidDataException("游戏数值配置版本无效");
            if(value.mutationMode<0||value.mutationMode>1||value.stageMutationProbability>1||value.stageEnvironmentBoost>1||value.corpseCleanupSeconds<1||value.corpseCleanupSeconds>86400||value.corpseCleanupPerFrame<1||value.corpseCleanupPerFrame>4096)throw new InvalidDataException("变异模式、阶段概率或尸体清理周期/预算无效");
            if(value.mergePhaseBuckets<1||value.mergePhaseBuckets>256)throw new InvalidDataException("合并错峰批次数必须为1到256");
            if(value.colonyEmergencyFeedThreshold>10)throw new InvalidDataException("紧急补给饱食线必须为0到10");
            if(value.mutationPopulationFraction>1||value.mutationRoundSeconds<1||value.mutationRoundSeconds>86400)throw new InvalidDataException("全局变异比例须为0到1，轮次周期须为1到86400模拟秒");
            foreach(var field in typeof(GameplayTuning).GetFields())if(field.FieldType==typeof(float))
            {float n=(float)field.GetValue(value);if(float.IsNaN(n)||float.IsInfinity(n)||n<0)throw new InvalidDataException("数值必须为非负有限数："+field.Name);}
            if(value.manualHitboxScale<1||value.supplyActivityRadiusRatio<=0||value.colonyActivityRadiusMultiplier<=0||value.populationFillOpacity>1)throw new InvalidDataException("判定框倍率至少为1，活动半径倍率必须大于0，透明度须在0到1之间");
            if(value.mergeMaximumCatchupSeconds<value.mergeInterval||value.mergeMaximumCatchupSeconds>30||value.colonyBirthReservedSlots>32||value.colonySuccessorPopulation<1||value.colonySuccessorPopulation>100000||value.mergeFoodTolerance<.001f||value.mergeFoodTolerance>10||value.mergeReserveTolerance<.001f||value.mergeWakeDistance<=0||value.memberFeedResume>10||value.mergeRadius<=0||value.mergeInterval<.1f||value.mergeInterval>2||value.mergeMaximumMembers<1||value.mergeMaximumMembers>128||value.mergeGroupsPerFrame<1||value.mergeGroupsPerFrame>100000||value.mutationAttemptsPerFrame<1||value.mutationAttemptsPerFrame>100000||value.mergedMutationFraction>1||value.multipleBirthMaximum<1||value.multipleBirthMaximum>16)throw new InvalidDataException("任务或合并计算数值超出有效范围");
            return value;
        }
        public static void Reload()
        {
            var asset=Resources.Load<TextAsset>("gameplay_tuning");
            if(asset==null)throw new InvalidDataException("缺少 gameplay_tuning 默认配置");
            var fallback=Parse(asset.text);
            try
            {
                if(!File.Exists(FilePath)){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));File.WriteAllText(FilePath,asset.text,new System.Text.UTF8Encoding(false));}
                _current=Parse(File.ReadAllText(FilePath));_stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;
            }
            catch(Exception ex){if(_current==null)_current=fallback;Debug.LogWarning("[游戏数值] "+FilePath+" 无法读取，保留上次有效配置："+ex.Message);}
        }
        public static void Poll()
        {
            // ★ 适配：编辑器和发布版均检测外部文件变化；不在属性热路径逐实体访问磁盘。
            if(Time.unscaledTime<_nextPoll)return;_nextPoll=Time.unscaledTime+2;
            try{long stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;if(stamp!=_stamp){_stamp=stamp;Reload();}}catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException){Debug.LogWarning("[游戏数值] 检查外部配置失败："+ex.Message);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset(){_current=null;_stamp=0;_nextPoll=0;}
    }
}
