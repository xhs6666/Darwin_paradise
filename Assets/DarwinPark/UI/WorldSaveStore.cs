using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.UI
{
    [Serializable] public class SavedGeneDefinition {public string id,json;}
    [Serializable] public sealed class WorldSaveData
    {
        public int version=1,seed,oreResolution;
        public string id,name,createdUtc,savedUtc,terrainParameters,environmentParameters;
        public float gameTime,timeScale=1,sunAngle,oreTimer;
        public bool running=true;
        public Vector3 cameraPosition;public Quaternion cameraRotation,planetRotation;
        public float cameraDistance;
        public float[] ore;
        public List<EntitySaveState> entities=new List<EntitySaveState>();
        public List<SavedGeneDefinition> definitions=new List<SavedGeneDefinition>();
        public GeneTreeDocument tree=new GeneTreeDocument();
    }
    public sealed class WorldSaveEntry {public string path,name,error;public WorldSaveData data;}
    // ★ 新增：同目录临时文件+原子替换，失败保留原档和备份，加载先完整校验再清场。
    public static class WorldSaveStore
    {
        public static string DefaultName(int seed)=>DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")+"_"+seed;
        // ★ 新增：文字种子使用稳定哈希，不能使用跨进程/平台会变化的 string.GetHashCode。
        public static int SeedFromText(string text)
        {
            if(string.IsNullOrWhiteSpace(text))return 0;
            text=text.Trim();if(int.TryParse(text,out int value))return value;
            unchecked {uint hash=2166136261;foreach(char c in text){hash^=c;hash*=16777619;}return (int)hash;}
        }
        public static string FileFor(string id)
        {
            if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("存档标识无效");
            return Path.Combine(GamePaths.Saves,id+".json");
        }
        public static List<WorldSaveEntry> List()
        {
            var list=new List<WorldSaveEntry>();if(!Directory.Exists(GamePaths.Saves))return list;
            foreach(var file in Directory.GetFiles(GamePaths.Saves,"*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                var entry=new WorldSaveEntry{path=file,name=Path.GetFileNameWithoutExtension(file)};
                try{entry.data=Read(file);entry.name=entry.data.name;}catch(Exception ex){entry.error=ex.Message;Debug.LogWarning("[存档] "+file+"："+ex.Message);}list.Add(entry);
            }
            return list;
        }
        public static WorldSaveData Read(string path){var data=JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(path,Encoding.UTF8));Validate(data);return data;}
        public static void Validate(WorldSaveData data)
        {
            if(data==null||data.version!=1||!Guid.TryParseExact(data.id,"N",out _)||string.IsNullOrWhiteSpace(data.name)||string.IsNullOrWhiteSpace(data.terrainParameters)||string.IsNullOrWhiteSpace(data.environmentParameters)||data.entities==null||data.definitions==null||data.definitions.Any(d=>d==null||string.IsNullOrEmpty(d.id)))throw new InvalidDataException("存档格式或版本无效");
            if(float.IsNaN(data.gameTime)||float.IsInfinity(data.gameTime)||data.gameTime<0)throw new InvalidDataException("存档时间无效");
            if(float.IsNaN(data.timeScale)||float.IsInfinity(data.timeScale)||data.timeScale<0)throw new InvalidDataException("存档时间倍率无效");
            // ★ 修复（防Bug回归）：在清理当前世界之前校验参数和基因 JSON，损坏存档不会先破坏现场。
            var terrain=JsonUtility.FromJson<TerrainSaveValidation>(data.terrainParameters);
            if(terrain==null||terrain.subdivisions<2||terrain.subdivisions>1024||!Finite(terrain.radius)||terrain.radius<=0||!Finite(terrain.heightScale)||!Finite(terrain.continentRatio)||terrain.continentRatio<=0||terrain.continentRatio>=1)throw new InvalidDataException("地形参数无效");
            var environment=JsonUtility.FromJson<EnvironmentSaveValidation>(data.environmentParameters);
            if(environment==null||!Finite(environment.BaseTemperature)||!Finite(environment.BaseHumidity))throw new InvalidDataException("环境参数无效");
            var catalog=data.definitions.ToDictionary(d=>d.id,d=>d.json);if(catalog.Count==0)throw new InvalidDataException("存档基因配置为空");GeneTreeStorage.Validate(data.tree,catalog);
            foreach(var definition in catalog){var gene=JsonUtility.FromJson<GeneDefinitionJson>(definition.Value);if(gene==null||gene.id!=definition.Key||!Enum.TryParse<GeneCategory>(gene.category,true,out _))throw new InvalidDataException("基因配置无效："+definition.Key);}
            foreach(var entity in data.entities)
            {
                if(entity.genome==null||entity.genome.Genes==null||entity.stats==null||entity.reproduction==null||entity.traits==null)throw new InvalidDataException("实体存档不完整");
                if(entity.genome.Genes.Any(g=>!catalog.ContainsKey(g.DefinitionId)))throw new InvalidDataException("实体引用缺失基因");
            }
            if(data.ore!=null&&(data.oreResolution<1||data.ore.Length!=(long)data.oreResolution*data.oreResolution))throw new InvalidDataException("矿脉数据不完整");
        }
        [Serializable] private sealed class TerrainSaveValidation {public int subdivisions=320;public float radius=300,heightScale=90,continentRatio=.5f;}
        [Serializable] private sealed class EnvironmentSaveValidation {public float BaseTemperature=20,BaseHumidity=44;}
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static void Write(WorldSaveData data,string overridePath=null)
        {
            Validate(data);data.savedUtc=DateTime.UtcNow.ToString("O");string path=overridePath??FileFor(data.id);
            Directory.CreateDirectory(Path.GetDirectoryName(path));string text=JsonUtility.ToJson(data);string temporary=path+".tmp";
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false))){writer.Write(text);writer.Flush();stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
        }
    }
}
