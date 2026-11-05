using System;
using System.IO;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：安装目录中可直接修改的全局数值；重启后同时作用于新旧世界，不覆盖世界保存的基因配置。
    [Serializable] public sealed class GameplayTuning
    {
        public int version=1;
        public float baseMetabolismMultiplier=1, movementMetabolismMultiplier=1;
        public float repairFoodPerHealth=.5f, starvationDamagePerSecond=1;
        public float manualAimPadding=.85f, manualHitboxScale=1.5f, manualAttackReachBonus=.35f;
        public float supplyActivityRadiusRatio=1;
        public float colonyActivityRadiusMultiplier=.35f, populationFillOpacity=.22f;
        // ★ 新增：成年且无合适配偶的连续等待天数，外部配置可热更新。
        public float parthenogenesisWaitDays=5;
        private static GameplayTuning _current;
        private static long _stamp;private static float _nextPoll;
        public static string FilePath=>Path.Combine(GamePaths.Root,"config","gameplay.json");
        public static GameplayTuning Current {get{if(_current==null)Reload();return _current;}}
        public static GameplayTuning Parse(string json)
        {
            var value=JsonUtility.FromJson<GameplayTuning>(CommentedJson.Strip(json));
            if(value==null||value.version!=1)throw new InvalidDataException("游戏数值配置版本无效");
            foreach(var field in typeof(GameplayTuning).GetFields())if(field.FieldType==typeof(float))
            {float n=(float)field.GetValue(value);if(float.IsNaN(n)||float.IsInfinity(n)||n<0)throw new InvalidDataException("数值必须为非负有限数："+field.Name);}
            if(value.manualHitboxScale<1||value.supplyActivityRadiusRatio<=0||value.colonyActivityRadiusMultiplier<=0||value.populationFillOpacity>1)throw new InvalidDataException("判定框倍率至少为1，活动半径倍率必须大于0，透明度须在0到1之间");
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
