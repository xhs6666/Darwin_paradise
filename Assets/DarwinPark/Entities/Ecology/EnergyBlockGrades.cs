using System;
using System.IO;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class EnergyBlockGrade {public string name,color;public float minimumEnergy,scale=1;}
    [Serializable] public sealed class EnergyBlockGradeTable {public EnergyBlockGrade[] levels;}
    // ★ 新增：等级按真实剩余能量确定，合并/吸收/读档都自动更新，不凭等级凭空补能量。
    public static class EnergyBlockGrades
    {
        private static EnergyBlockGradeTable _table;
        public static EnergyBlockGradeTable Table
        {
            get
            {
                if(_table!=null)return _table;
                Reload();return _table;
            }
        }
        public static string FilePath=>Path.Combine(GamePaths.Root,"config","energy_levels.json");
        public static void Reload()
        {
            var asset=Resources.Load<TextAsset>("Entities/energy_levels");
            if(asset==null)throw new InvalidDataException("缺少能量块等级配置 Entities/energy_levels");
            var fallback=Parse(asset.text);
            // ★ 适配：外部可调分级能量与颜色，错误配置告警并保留上次有效值。
            try
            {
                if(!File.Exists(FilePath)){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));File.WriteAllText(FilePath,asset.text,new System.Text.UTF8Encoding(false));}
                _table=Parse(File.ReadAllText(FilePath));
            }
            catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException)
            {if(_table==null)_table=fallback;Debug.LogWarning("[能量块等级] 保留有效配置："+ex.Message);}
        }
        public static EnergyBlockGradeTable Parse(string json)
        {
            var table=JsonUtility.FromJson<EnergyBlockGradeTable>(CommentedJson.Strip(json));
            if(table?.levels==null||table.levels.Length!=5)throw new InvalidDataException("能量块必须有五个等级");
            float previous=-1;
            foreach(var level in table.levels)
            {
                if(level==null||string.IsNullOrEmpty(level.name)||!ColorUtility.TryParseHtmlString(level.color,out _)||!float.IsFinite(level.minimumEnergy)||level.minimumEnergy<=previous||!float.IsFinite(level.scale)||level.scale<=0)throw new InvalidDataException("能量块等级颜色、能量或大小无效");
                previous=level.minimumEnergy;
            }
            if(table.levels[0].minimumEnergy!=0)throw new InvalidDataException("最低等级须从零开始");
            return table;
        }
        public static int Level(float energy)
        {int level=0;while(level+1<Table.levels.Length&&energy>=Table.levels[level+1].minimumEnergy)level++;return level;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>_table=null;
    }
}
