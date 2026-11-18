namespace DarwinPark.UI
{
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using UnityEngine;

    [Serializable] public class SavedGeneDefinition {public string id,json;}
    [Serializable] public sealed class WorldSaveData
    {
        public int version=2,seed,oreResolution;
        public int contentEpoch;
        public string savedWithRelease;
        public string morphology;
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
        // ★ 新增：维持目标及生成来源独立保存，旧档缺字段不启用自动生成。
        public PopulationMaintenanceState maintenance;
        public DarwinPark.Entities.Simulation.MutationRoundSave mutationRounds;
        public DarwinPark.Entities.Simulation.CorpseCleanupSave corpseCleanup;
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
        public static WorldSaveData Read(string path){var data=WorldSaveSections.Read(path);Validate(data);return data;}
        public static void Validate(WorldSaveData data)
        {
            if(data!=null)LanRelease.Current.RequireWorld(data.version,data.contentEpoch);
            if(data==null||data.version!=2||!Guid.TryParseExact(data.id,"N",out _)||string.IsNullOrWhiteSpace(data.name)||string.IsNullOrWhiteSpace(data.terrainParameters)||string.IsNullOrWhiteSpace(data.environmentParameters)||data.entities==null||data.definitions==null||data.definitions.Any(d=>d==null||string.IsNullOrEmpty(d.id)))throw new InvalidDataException("存档格式或版本无效");
            MorphologyRegistry.ValidateSnapshot(data.morphology);
            // ★ 适配：新快照在清场前完成检查；旧档缺少 colony/ant 字段仍可读取。
            data.gameSettings=WorldGameSettings.Normalize(data.gameSettings);
            // ★ 适配：旧档的生态配置在清场前迁移/校验，规则列表仍按旧档恢复，不自动替换。
            data.colony=DarwinPark.Entities.Ecology.EcologyDefaults.PrepareSnapshot(data.colony);
            DarwinPark.Entities.Ecology.ColonyWorldState.Validate(data.colony);
            foreach(var entity in data.entities)DarwinPark.Entities.Ecology.ColonyWorldState.ValidateMember(entity?.ecology?.ant);
            foreach(var entity in data.entities)DarwinPark.Entities.Ecology.ColonyWorldState.ValidateMotion(entity?.ecology);
            if(float.IsNaN(data.gameTime)||float.IsInfinity(data.gameTime)||data.gameTime<0)throw new InvalidDataException("存档时间无效");
            if(float.IsNaN(data.timeScale)||float.IsInfinity(data.timeScale)||data.timeScale<0)throw new InvalidDataException("存档时间倍率无效");
            // ★ 适配：加载前校验全局变异计时和剩余名单，损坏快照不能先清理现场。
            DarwinPark.Entities.Simulation.MutationRoundController.Validate(data.mutationRounds);
            // ★ 适配：删除名单与剩余时间必须在清场前校验。
            DarwinPark.Entities.Simulation.CorpseCleanupController.Validate(data.corpseCleanup);
            // ★ 修复（防Bug回归）：在清理当前世界之前校验参数和基因 JSON，损坏存档不会先破坏现场。
            // ★ 适配：有界验证在清场前执行，旧档缺地貌字段仍可加载。
            DarwinPark.Planet.LandformSaveCompatibility.Validate(data.terrainParameters);
            var terrain=JsonUtility.FromJson<TerrainSaveValidation>(data.terrainParameters);
            if(terrain==null||terrain.subdivisions<2||terrain.subdivisions>1024||!Finite(terrain.radius)||terrain.radius<=0||!Finite(terrain.heightScale)||!Finite(terrain.continentRatio)||terrain.continentRatio<=0||terrain.continentRatio>=1)throw new InvalidDataException("地形参数无效");
            var environment=JsonUtility.FromJson<EnvironmentSaveValidation>(data.environmentParameters);
            if(environment==null||!Finite(environment.BaseTemperature)||!Finite(environment.BaseHumidity))throw new InvalidDataException("环境参数无效");
            var catalog=data.definitions.ToDictionary(d=>d.id,d=>d.json);if(catalog.Count==0)throw new InvalidDataException("存档基因配置为空");GeneTreeStorage.Validate(data.tree,catalog);
            PopulationMaintenance.Validate(data.maintenance,catalog.Keys);
            // ★ 适配：默认生态模板中的基因也在清场前核对，错误外部配置不能损坏当前世界。
            DarwinPark.Entities.Ecology.EcologyDefaults.ValidateCatalog(data.colony?.ecologyDefaults,catalog.Keys);
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
                // ★ 适配：坏胎数在清场前拒绝，旧档缺省单胎。
                if(!Finite(entity.pendingSimulationTime)||entity.pendingSimulationTime<0)throw new InvalidDataException("实体合并计算时间无效");
                foreach(var reproduction in entity.reproduction)DarwinPark.Entities.Logic.MultipleBirthPolicy.Validate(reproduction?.litter);
                if(entity.reproduction.Any(r=>r==null||!Finite(r.mateWait)||r.mateWait<0||!Finite(r.maintenanceWait)||r.maintenanceWait<0||r.parthenogenetic&&r.partner?.Genes?.Count>0))throw new InvalidDataException("繁殖等待状态无效");
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
            Validate(data);data.contentEpoch=LanRelease.Current.contentEpoch;data.savedWithRelease=LanRelease.Current.release;data.savedUtc=DateTime.UtcNow.ToString("O");string path=overridePath??FileFor(data.id);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            if(string.Equals(Path.GetFileName(path),"world.json",StringComparison.OrdinalIgnoreCase))
                WorldSaveSections.Write(data,path);
            else WorldSaveSections.WriteAtomic(path,data);
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

namespace DarwinPark.UI
{
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Genetics;
using UnityEngine;


    // ★ 新增：清场前独立校验状态注册表和基因标签，坏配置不能先破坏玩家现场。
    public static class EntityStateSaveValidation
    {
        public static void Validate(WorldSaveData data)
        {
            var registry=EntityStateRegistry.Parse(string.IsNullOrWhiteSpace(data.entityStateRegistry)?EntityStateRegistry.ReadConfiguredJson():data.entityStateRegistry);
            var catalog=new Dictionary<string,GeneDefinitionJson>();
            foreach(var entry in data.definitions)
            {
                var gene=JsonUtility.FromJson<GeneDefinitionJson>(entry.json);
                MorphologyRegistry.ValidateGene(gene);GeneEntityStateTags.Validate(gene,registry);catalog.Add(entry.id,gene);
            }
            foreach(var condition in registry.states.SelectMany(s=>s.entryAny.Concat(s.transitions.SelectMany(t=>t.any))).SelectMany(r=>r.all))
                if(condition.kind=="nearby_gene_state"&&(!catalog.TryGetValue(condition.geneId,out var gene)||!gene.states.Any(s=>s.stateId==condition.geneStateId)))
                    throw new InvalidDataException("实体状态条件引用缺失基因或基因状态："+condition.geneId+" / "+condition.geneStateId);
            var shapes=JsonUtility.FromJson<MorphologySnapshot>(data.morphology).bodies.ToDictionary(b=>b.id);
            foreach(var entity in data.entities)
            {
                if(entity?.genome==null||!shapes.TryGetValue(entity.genome.BodyPlanId,out var shape))throw new InvalidDataException("实体形体未注册");
                MorphologyRegistry.ValidateGenome(entity.genome,shape);
                foreach(var instance in entity.genome.Genes)
                {
                    if(instance==null||!catalog.TryGetValue(instance.DefinitionId,out var definition)||definition.explicitState&&!definition.states.Exists(s=>s.stateId==instance.StateId)||definition.partBindings.Any(b=>!shape.slots.Exists(s=>s.id==b.slotId)))throw new InvalidDataException("实体模式或部位引用无效");
                }
            }
            foreach(var entity in data.entities)
                if(!string.IsNullOrEmpty(entity?.ecology?.entityStateId)&&registry.Get(entity.ecology.entityStateId)==null)throw new InvalidDataException("存档实体状态未注册："+entity.ecology.entityStateId);
        }
    }
}

namespace DarwinPark.UI
{
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using UnityEngine;


    // ★ 新增：旧长孕育和长休养必须随新表达规则一起迁移，避免恢复生长后先老死再到生产时间。
    public static class EcologyLegacySaveUpgrade
    {
        public static int Apply(WorldSaveData world,IEnumerable<DarwinEntity> entities)
        {
            if(!string.IsNullOrWhiteSpace(world.ecologyBalanceSettings))return 0;
            var settings=EcologyBalanceSettings.Current;
            var saved=world.entities.SelectMany(e=>e.reproduction).ToLookup(r=>r.gene);int changed=0;
            foreach(var entity in entities)
            {
                if(entity==null)continue;
                var genes=entity.Genes;var stats=entity.GetComponent<EntityStats>();
                if(stats==null||!stats.TryGet(settings.sizeAttribute,out float size))continue;
                size=Mathf.Max(settings.minimumReproductionSize,size);
                foreach(var gene in genes.Genome.Genes)
                {
                    if(!(genes.GetLogic(gene) is ReproductionGeneLogic logic))continue;
                    var state=logic.CaptureState();float priorGestation=state.gestation,priorCooldown=state.cooldown;
                    state.gestation=Mathf.Min(state.gestation,logic.CalculateGestationTime(size));
                    state.cooldown=Mathf.Min(state.cooldown,EcologyReproductionTiming.CooldownSeconds(genes));
                    if(state.gestation==priorGestation&&state.cooldown==priorCooldown)continue;
                    logic.RestoreState(state);changed++;
                    // ★ 适配：内存中的世界快照同步收纳，加载重试时不能重新使用旧长计时；磁盘仍只由正常保存链路写入。
                    foreach(var record in saved[state.gene]){record.gestation=state.gestation;record.cooldown=state.cooldown;}
                }
            }
            world.ecologyBalanceSettings=EcologyBalanceSettings.CurrentJson;
            if(changed>0)Debug.Log("[生态存档] 已收纳 "+changed+" 项旧孕育/休养计时，并登记世界规则快照。");
            return changed;
        }
    }
}

namespace DarwinPark.UI
{
    // ★ 新增：历史缺失只能按默认值补回；仅生成新世界副本，不在普通读档中静默改写遗传数据。
    public static class WorldBaseGeneRepair
    {
        public static WorldSaveData CreateCopy(WorldSaveData source,out int repairedEntities,out int addedGenes)
        {
            if(source==null)throw new System.ArgumentNullException(nameof(source));
            var copy=UnityEngine.JsonUtility.FromJson<WorldSaveData>(UnityEngine.JsonUtility.ToJson(source));
            var asset=UnityEngine.Resources.Load<UnityEngine.TextAsset>("Entities/entity_default");
            if(asset==null)throw new System.IO.InvalidDataException("缺少默认实体配置");
            var defaults=UnityEngine.JsonUtility.FromJson<DarwinPark.Genetics.EntityGeneData>(asset.text);
            var protectedIds=DarwinPark.Genetics.Mutation.GeneMutationSettings.CreateDefault().ProtectedGeneIds;
            var entries=new System.Collections.Generic.Dictionary<string,DarwinPark.Genetics.GeneInstanceData>();
            foreach(string id in protectedIds)
            {
                var entry=defaults.genes.Find(g=>g.definitionId==id);
                if(entry==null||!copy.definitions.Exists(d=>d.id==id))throw new System.IO.InvalidDataException("缺少待恢复基础基因配置："+id);
                entries.Add(id,entry);
            }
            repairedEntities=addedGenes=0;
            foreach(var entity in copy.entities)
            {
                int count=Repair(entity.genome,entries);if(count>0)repairedEntities++;addedGenes+=count;
                // ★ 修复（防Bug回归）：固定分工会在重算时还原锁定基因组；仅补实体主表会立即再次丢失修复结果。
                var ant=entity.ecology?.ant;
                if(ant!=null)
                {
                    if(!string.IsNullOrEmpty(ant.lockedGenome))
                    {var locked=UnityEngine.JsonUtility.FromJson<DarwinPark.Genetics.Genome>(ant.lockedGenome);Repair(locked,entries);ant.lockedGenome=UnityEngine.JsonUtility.ToJson(locked);}
                    if(ant.partnerGenome?.Genes?.Count>0)Repair(ant.partnerGenome,entries);
                }
                foreach(var reproduction in entity.reproduction)if(reproduction.partner?.Genes?.Count>0)Repair(reproduction.partner,entries);
            }
            RepairBuiltinSpawnSources(copy,entries);
            copy.id=System.Guid.NewGuid().ToString("N");copy.name=source.name+"（基础基因修复副本）";copy.savedUtc=System.DateTime.UtcNow.ToString("O");
            WorldSaveStore.Validate(copy);return copy;
        }
        // ★ 修复（防Bug回归）：旧内置模板和维持规则会继续生成同类缺失实体。只修复副本里的已知内置来源。
        private static void RepairBuiltinSpawnSources(WorldSaveData copy,
            System.Collections.Generic.Dictionary<string,DarwinPark.Genetics.GeneInstanceData> defaults)
        {
            var asset=UnityEngine.Resources.Load<UnityEngine.TextAsset>("Entities/ecology_defaults");
            if(asset==null)throw new System.IO.InvalidDataException("缺少内置生态模板");
            var builtin=DarwinPark.Entities.Ecology.EcologyDefaults.Parse(asset.text);
            if(!string.IsNullOrWhiteSpace(copy.colony?.ecologyDefaults))
            {
                var saved=DarwinPark.Entities.Ecology.EcologyDefaults.Parse(copy.colony.ecologyDefaults);
                foreach(var template in saved.templates)
                {
                    var match=System.Array.Find(builtin.templates,item=>item.id==template.id);
                    if(match==null)continue;
                    var genes=new System.Collections.Generic.List<DarwinPark.Entities.Ecology.EcologyDefaultGene>(template.genes);
                    foreach(var entry in defaults)
                    {
                        if(genes.Exists(gene=>gene.id==entry.Key))continue;
                        var stock=System.Array.Find(match.genes,gene=>gene.id==entry.Key);
                        genes.Add(stock??new DarwinPark.Entities.Ecology.EcologyDefaultGene
                            {id=entry.Key,value=entry.Value.value,tier=entry.Value.tier});
                    }
                    template.genes=genes.ToArray();
                }
                copy.colony.ecologyDefaults=UnityEngine.JsonUtility.ToJson(saved);
            }

            if(copy.maintenance?.rules==null)return;
            foreach(var rule in copy.maintenance.rules)
            {
                string templateId=rule.id switch
                {
                    DarwinPark.Entities.Ecology.EcologyDefaults.ProducerRuleId=>builtin.producerTemplate,
                    DarwinPark.Entities.Ecology.EcologyDefaults.MobileRuleId=>builtin.mobileTemplate,
                    _=>null
                };
                var genome=rule.source?.template;
                if(templateId==null||genome==null)continue;
                var stock=System.Array.Find(builtin.templates,template=>template.id==templateId);
                foreach(var entry in defaults)
                {
                    if(genome.HasDefinition(entry.Key))continue;
                    var gene=System.Array.Find(stock.genes,item=>item.id==entry.Key);
                    genome.Add(DarwinPark.Genetics.GeneInstance.Create(entry.Key,
                        gene?.value??entry.Value.value,gene?.tier??entry.Value.tier));
                }
            }
        }
        private static int Repair(DarwinPark.Genetics.Genome genome,System.Collections.Generic.Dictionary<string,DarwinPark.Genetics.GeneInstanceData> defaults)
        {
            if(genome?.Genes==null)throw new System.IO.InvalidDataException("待恢复基因组无效");int added=0;
            foreach(var pair in defaults)if(!genome.HasDefinition(pair.Key))
            {var gene=pair.Value;genome.Add(DarwinPark.Genetics.GeneInstance.Create(gene.definitionId,gene.value,gene.tier));added++;}
            return added;
        }
    }
}
