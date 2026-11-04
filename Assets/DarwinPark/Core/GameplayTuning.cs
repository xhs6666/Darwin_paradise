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
        private static GameplayTuning _current;
        public static string FilePath=>Path.Combine(GamePaths.Root,"config","gameplay.json");
        public static GameplayTuning Current {get{if(_current==null)Reload();return _current;}}
        public static GameplayTuning Parse(string json)
        {
            var value=JsonUtility.FromJson<GameplayTuning>(json);
            if(value==null||value.version!=1)throw new InvalidDataException("游戏数值配置版本无效");
            foreach(var field in typeof(GameplayTuning).GetFields())if(field.FieldType==typeof(float))
            {float n=(float)field.GetValue(value);if(float.IsNaN(n)||float.IsInfinity(n)||n<0)throw new InvalidDataException("数值必须为非负有限数："+field.Name);}
            if(value.manualHitboxScale<1||value.supplyActivityRadiusRatio<=0)throw new InvalidDataException("判定框倍率至少为1，补给活动半径倍率必须大于0");
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
                _current=Parse(File.ReadAllText(FilePath));
            }
            catch(Exception ex){_current=fallback;Debug.LogWarning("[游戏数值] "+FilePath+" 无法读取，使用内置配置："+ex.Message);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>_current=null;
    }
}
