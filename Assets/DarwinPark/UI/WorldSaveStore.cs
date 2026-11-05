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
        // ★ 适配：状态阈值/迁移随世界保存，后续修改 mods 不会静默改写旧世界的规则。
        public string entityStateRegistry;
        // ★ 适配：代谢、生产、体型与繁殖周期采用同一世界快照，读档不会混用另一套平衡数值。
        public string ecologyBalanceSettings;
        // ★ 适配：蚁群世界物件独立于存活实体保存，尸体和黄色能量块不会读档消失。
        public DarwinPark.Entities.Ecology.ColonyWorldState colony;
        // ★ 适配：存档内设置独立于全局偏好，旧档缺字段按版本标记补默认值。
        public WorldGameSettings gameSettings;
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
            // ★ 修复（防Bug回归）：世界使用稳定标识作为独立目录，后续可并列放入截图、分区和扩展数据。
            return Path.Combine(GamePaths.Saves,id,"world.json");
        }
        public static List<WorldSaveEntry> List()
        {
            return ListFrom(GamePaths.Saves);
        }
        // ★ 适配：同时发现新目录和旧单文件；同一世界优先显示新格式，损坏文件仍明确列出。
        public static List<WorldSaveEntry> ListFrom(string root)
        {
            var list=new List<WorldSaveEntry>();if(!Directory.Exists(root))return list;
            var files=Directory.GetDirectories(root).Select(d=>Path.Combine(d,"world.json")).Where(File.Exists)
                .Concat(Directory.GetFiles(root,"*.json"));
            var seen=new HashSet<string>();
            foreach(var file in files)
            {
                var entry=new WorldSaveEntry{path=file,name=Path.GetFileNameWithoutExtension(file)};
                try{entry.data=Read(file);entry.name=entry.data.name;if(!seen.Add(entry.data.id))continue;}catch(Exception ex){entry.error=ex.Message;Debug.LogWarning("[存档] "+file+"："+ex.Message);}list.Add(entry);
            }
            return list.OrderByDescending(e=>File.GetLastWriteTimeUtc(e.path)).ToList();
        }
        public static WorldSaveData Read(string path){var data=JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(path,Encoding.UTF8));Validate(data);return data;}
        public static void Validate(WorldSaveData data)
        {
            if(data==null||data.version!=1||!Guid.TryParseExact(data.id,"N",out _)||string.IsNullOrWhiteSpace(data.name)||string.IsNullOrWhiteSpace(data.terrainParameters)||string.IsNullOrWhiteSpace(data.environmentParameters)||data.entities==null||data.definitions==null||data.definitions.Any(d=>d==null||string.IsNullOrEmpty(d.id)))throw new InvalidDataException("存档格式或版本无效");
            // ★ 适配：新快照在清场前完成检查；旧档缺少 colony/ant 字段仍可读取。
            data.gameSettings=WorldGameSettings.Normalize(data.gameSettings);
            DarwinPark.Entities.Ecology.ColonyWorldState.Validate(data.colony);
            foreach(var entity in data.entities)DarwinPark.Entities.Ecology.ColonyWorldState.ValidateMember(entity?.ecology?.ant);
            foreach(var entity in data.entities)DarwinPark.Entities.Ecology.ColonyWorldState.ValidateMotion(entity?.ecology);
            if(float.IsNaN(data.gameTime)||float.IsInfinity(data.gameTime)||data.gameTime<0)throw new InvalidDataException("存档时间无效");
            if(float.IsNaN(data.timeScale)||float.IsInfinity(data.timeScale)||data.timeScale<0)throw new InvalidDataException("存档时间倍率无效");
            // ★ 修复（防Bug回归）：在清理当前世界之前校验参数和基因 JSON，损坏存档不会先破坏现场。
            var terrain=JsonUtility.FromJson<TerrainSaveValidation>(data.terrainParameters);
            if(terrain==null||terrain.subdivisions<2||terrain.subdivisions>1024||!Finite(terrain.radius)||terrain.radius<=0||!Finite(terrain.heightScale)||!Finite(terrain.continentRatio)||terrain.continentRatio<=0||terrain.continentRatio>=1)throw new InvalidDataException("地形参数无效");
            var environment=JsonUtility.FromJson<EnvironmentSaveValidation>(data.environmentParameters);
            if(environment==null||!Finite(environment.BaseTemperature)||!Finite(environment.BaseHumidity))throw new InvalidDataException("环境参数无效");
            var catalog=data.definitions.ToDictionary(d=>d.id,d=>d.json);if(catalog.Count==0)throw new InvalidDataException("存档基因配置为空");GeneTreeStorage.Validate(data.tree,catalog);
            // ★ 修复（防Bug回归）：已知旧关系迁移会同步修正父基因，必须写回快照；否则文件里的父定义与已迁移的出错连线不一致。
            foreach(var definition in data.definitions)definition.json=catalog[definition.id];
            // ★ 适配：新增营养定义成对写回快照，不能只更新树节点却遗漏基因JSON。
            foreach(var entry in catalog)if(data.definitions.All(d=>d.id!=entry.Key))data.definitions.Add(new SavedGeneDefinition{id=entry.Key,json=entry.Value});
            foreach(var definition in catalog){var gene=JsonUtility.FromJson<GeneDefinitionJson>(definition.Value);if(gene==null||gene.id!=definition.Key||!Enum.TryParse<GeneCategory>(gene.category,true,out _))throw new InvalidDataException("基因配置无效："+definition.Key);}
            EntityStateSaveValidation.Validate(data);
            // ★ 适配：先校验世界生态快照，禁止清场后才发现坏平衡参数；旧世界使用待迁移的当前配置。
            DarwinPark.Entities.Ecology.EcologyBalanceSettings.Parse(string.IsNullOrWhiteSpace(data.ecologyBalanceSettings)?DarwinPark.Entities.Ecology.EcologyBalanceSettings.ReadConfiguredJson():data.ecologyBalanceSettings);
            foreach(var entity in data.entities)
            {
                // ★ 适配：兼容新增生态字段之前的存档；有内容但没有有效标识的记录仍作为损坏拒绝。
                if(entity?.ecology?.IsEmpty==true)entity.ecology=null;
                if(entity.genome==null||entity.genome.Genes==null||entity.stats==null||entity.reproduction==null||entity.traits==null)throw new InvalidDataException("实体存档不完整");
                // ★ 适配：新增等待计时与孤雌孕育标记在清场前检查；旧档缺字段按0/false兼容。
                if(entity.reproduction.Any(r=>r==null||!Finite(r.mateWait)||r.mateWait<0||r.parthenogenetic&&r.partner?.Genes?.Count>0))throw new InvalidDataException("孤雌生殖状态无效");
                if(entity.genome.Genes.Any(g=>!catalog.ContainsKey(g.DefinitionId)))throw new InvalidDataException("实体引用缺失基因");
                // ★ 适配：外来卵不能略过后代基因校验；寄生宿主标识独立校验。
                DarwinPark.Entities.Ecology.NutritionSaveValidation.Validate(entity.ecology,catalog.Keys);
                if(entity.ecology!=null&&(!Guid.TryParseExact(entity.ecology.id,"N",out _)||!Finite(entity.ecology.evictionRemaining)||!Finite(entity.ecology.commandRemaining)||!Finite(entity.ecology.incubationRemaining)))throw new InvalidDataException("生态状态或实体标识无效");
            }
            var ids=data.entities.Where(e=>e.ecology!=null).Select(e=>e.ecology.id).ToList();
            if(ids.Count!=ids.Distinct().Count())throw new InvalidDataException("实体持久化标识重复");
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
            // ★ 适配：新档完整落盘后才收纳旧档；迁移失败不删除原文件，也不掩盖本次保存成功。
            if(overridePath==null)ArchiveLegacy(GamePaths.Saves,data.id);
        }
        public static void ArchiveLegacy(string root,string id)
        {
            if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("存档标识无效");
            string folder=Path.Combine(root,id),legacy=Path.Combine(root,id+".json");
            if(!File.Exists(Path.Combine(folder,"world.json")))return;
            try
            {
                foreach(string suffix in new[]{"",".bak"})
                {
                    string source=legacy+suffix,target=Path.Combine(folder,"legacy.json"+suffix);
                    if(File.Exists(source)&&!File.Exists(target))File.Move(source,target);
                }
            }
            catch(IOException ex){Debug.LogWarning("[存档] 新存档已保存，旧档保留在原处："+ex.Message);}
            catch(UnauthorizedAccessException ex){Debug.LogWarning("[存档] 新存档已保存，旧档保留在原处："+ex.Message);}
        }
    }
}
