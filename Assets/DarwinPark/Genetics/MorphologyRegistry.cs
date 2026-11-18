using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DarwinPark.Genetics
{
    // ★ 新增：一次装载并校验部件/形体注册表；拒绝缺少模型定义、循环父子关系与无限部件展开。
    public static class MorphologyRegistry
    {
        private static Dictionary<string,PartDefinition> _parts;private static Dictionary<string,BodyDefinition> _bodies;
        private static MorphologySnapshot _snapshot;
        // ★ 新增：共享网格绑定注册表实例，切换世界或配置快照后不能串用旧模型。
        public static object Identity {get{Ensure();return _settings;}}
        public static PartCatalog Settings {get{Ensure();return _settings;}}private static PartCatalog _settings;
        private static T Read<T>(string path)
        {
            if(_snapshot!=null){object value=typeof(T)==typeof(PartCatalog)?_snapshot.settings:typeof(T)==typeof(PartDefinition)?_snapshot.parts.Find(p=>p.id==Path.GetFileName(path)):(object)_snapshot.bodies.Find(b=>b.id==Path.GetFileName(path));if(value==null)throw new InvalidDataException("形体快照缺少："+path);return (T)value;}
            string external=Path.Combine(DarwinPark.Core.GamePaths.Mods,"BodyPartsV2",path+".json");
            if(File.Exists(external))return JsonUtility.FromJson<T>(File.ReadAllText(external));
            var asset=Resources.Load<TextAsset>(path);if(asset==null)throw new InvalidDataException("缺少形体配置："+path);return JsonUtility.FromJson<T>(asset.text);
        }
        private static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
        public static void Ensure()
        {
            if(_parts!=null)return;var settings=Read<PartCatalog>("Morphology/registry");
            if(settings.version!=2||settings.maximumParts<1||settings.maximumParts>64||!float.IsFinite(settings.refreshMilliseconds)||settings.refreshMilliseconds<=0||!float.IsFinite(settings.farDistance)||settings.farDistance<=0)throw new InvalidDataException("形体注册表版本或预算无效");
            var efficiencies=new HashSet<string>();var parts=new Dictionary<string,PartDefinition>();var bodies=new Dictionary<string,BodyDefinition>();
            foreach(string id in settings.parts){if(!System.Text.RegularExpressions.Regex.IsMatch(id??"","^[A-Za-z0-9_-]+$"))throw new InvalidDataException("部件ID无效");var part=Read<PartDefinition>("Morphology/Parts/"+id);if(part.id!=id||parts.ContainsKey(id)||!ColorUtility.TryParseHtmlString(part.color,out part.ParsedColor)||!ColorUtility.TryParseHtmlString(part.efficientColor,out part.ParsedEfficientColor)||!Finite(part.minimumScale)||!Finite(part.maximumScale)||part.minimumScale.x<=0||part.minimumScale.y<=0||part.minimumScale.z<=0||part.maximumScale.x<part.minimumScale.x||part.maximumScale.y<part.minimumScale.y||part.maximumScale.z<part.minimumScale.z||!float.IsFinite(part.efficiencyMaximum)||part.efficiencyMaximum<=0)throw new InvalidDataException("部件定义无效："+id);if(string.IsNullOrEmpty(part.modelResource)&&part.primitive!="sphere"&&part.primitive!="cube"&&part.primitive!="capsule")throw new InvalidDataException("部件模型类型无效："+id);parts.Add(id,part);if(!string.IsNullOrEmpty(part.efficiencyAttribute))efficiencies.Add(part.efficiencyAttribute);}
            foreach(string id in settings.bodies)
            {
                if(!System.Text.RegularExpressions.Regex.IsMatch(id??"","^[A-Za-z0-9_-]+$"))throw new InvalidDataException("形体ID无效");var body=Read<BodyDefinition>("Morphology/Bodies/"+id);if(body.id!=id||body.version!=2||body.slots==null||body.slots.Count>settings.maximumParts)throw new InvalidDataException("实体形体无效："+id);
                body.SlotsById=new Dictionary<string,BodySlot>();foreach(var slot in body.slots){if(string.IsNullOrEmpty(slot.id)||body.SlotsById.ContainsKey(slot.id)||!parts.ContainsKey(slot.partId)||!Finite(slot.position)||!Finite(slot.rotation)||!Finite(slot.scale)||!float.IsFinite(slot.surfaceOffset)||slot.attachment=="surface"&&slot.position.sqrMagnitude<1e-8f||slot.id!="body"&&string.IsNullOrEmpty(slot.parent)||slot.scale.x<=0||slot.scale.y<=0||slot.scale.z<=0||slot.id==slot.parent||!string.IsNullOrEmpty(slot.parent)&&!body.SlotsById.ContainsKey(slot.parent)||slot.attachment!="socket"&&slot.attachment!="surface")throw new InvalidDataException("部位必须有序、有限且无父子环："+slot.id);body.SlotsById.Add(slot.id,slot);}if(!body.SlotsById.ContainsKey("body"))throw new InvalidDataException("缺少body根部位");bodies.Add(id,body);
            }
            if(!parts.ContainsKey(settings.simplifiedPartId))throw new InvalidDataException("简化部件未注册");settings.EfficiencyAttributes=new string[efficiencies.Count];efficiencies.CopyTo(settings.EfficiencyAttributes);_settings=settings;_parts=parts;_bodies=bodies;
        }
        public static PartDefinition Part(string id){Ensure();return _parts.TryGetValue(id,out var part)?part:throw new InvalidDataException("未注册部件："+id);}
        public static BodyDefinition Body(string id){Ensure();return _bodies.TryGetValue(id,out var body)?body:throw new InvalidDataException("未注册形体："+id);}
        // ★ 新增：形体/部件定义按世界只保存一份，不在每个活体重复写大量JSON，也不依赖后续资源文件变动。
        public static string Capture(){Ensure();return JsonUtility.ToJson(new MorphologySnapshot{settings=_settings,parts=new List<PartDefinition>(_parts.Values),bodies=new List<BodyDefinition>(_bodies.Values)});}
        public static string CaptureDefaults()
        {
            var source=_snapshot;var parts=_parts;var bodies=_bodies;var settings=_settings;try{_snapshot=null;_parts=null;_bodies=null;_settings=null;return Capture();}finally{_snapshot=source;_parts=parts;_bodies=bodies;_settings=settings;}
        }
        public static void UseSnapshot(string raw)
        {
            var next=JsonUtility.FromJson<MorphologySnapshot>(raw);if(next?.version!=2||next.settings==null||next.parts==null||next.bodies==null)throw new InvalidDataException("形体快照版本或列表无效");
            var source=_snapshot;var parts=_parts;var bodies=_bodies;var settings=_settings;try{_snapshot=next;_parts=null;_bodies=null;_settings=null;Ensure();}catch{_snapshot=source;_parts=parts;_bodies=bodies;_settings=settings;throw;}
        }
        public static void ValidateSnapshot(string raw)
        {var source=_snapshot;var parts=_parts;var bodies=_bodies;var settings=_settings;try{UseSnapshot(raw);}finally{_snapshot=source;_parts=parts;_bodies=bodies;_settings=settings;}}
        public static void ValidateGenome(Genome genome,BodyDefinition definition=null)
        {
            if(genome?.Genes==null||genome.BodyOverrides==null)throw new InvalidDataException("实体形体数据缺失");var body=definition??Body(genome.BodyPlanId);var seen=new HashSet<string>();foreach(var change in genome.BodyOverrides)if(change==null||!seen.Add(change.slotId)||!body.slots.Exists(s=>s.id==change.slotId)||!Finite(change.scale)||!Finite(change.position)||!Finite(change.rotation)||change.position.sqrMagnitude>64||Mathf.Abs(change.rotation.x)>360||Mathf.Abs(change.rotation.y)>360||Mathf.Abs(change.rotation.z)>360||change.scale.x<=0||change.scale.y<=0||change.scale.z<=0||change.scale.x>16||change.scale.y>16||change.scale.z>16)throw new InvalidDataException("实体部位参数无效");
        }
        public static void Clear(){_snapshot=null;_parts=null;_bodies=null;_settings=null;}
        public static void ValidateGene(GeneDefinitionJson gene)
        {
            Ensure();if(gene.schemaVersion!=2)throw new InvalidDataException("不支持旧版基因定义："+gene.id);
            if(!float.IsFinite(gene.bodyColorWeight)||gene.bodyColorWeight<0||gene.bodyColorWeight>1)throw new InvalidDataException("基因配色权重无效");
            if(gene.partBindings==null||gene.partModifiers==null||gene.requiresGenes==null)throw new InvalidDataException("部位规则不能为空："+gene.id);
            if(System.Enum.TryParse<GeneCategory>(gene.category,true,out var category)&&category==GeneCategory.Reproduction&&gene.partBindings.Count!=0)throw new InvalidDataException("生殖类基因不生成器官："+gene.id);
            foreach(var binding in gene.partBindings){Part(binding.partId);if(string.IsNullOrEmpty(binding.slotId))throw new InvalidDataException("部位缺少槽位");}
            foreach(var modifier in gene.partModifiers)if(modifier.parameter!="scaleX"&&modifier.parameter!="scaleY"&&modifier.parameter!="scaleZ"||!float.IsFinite(modifier.minimum)||!float.IsFinite(modifier.maximum)||modifier.minimum<=0||modifier.maximum<modifier.minimum||!float.IsFinite(modifier.rangeMin)||!float.IsFinite(modifier.rangeMax)||modifier.rangeMax<=modifier.rangeMin)throw new InvalidDataException("形体参数曲线无效："+gene.id);
            if(gene.explicitState&&(string.IsNullOrEmpty(gene.defaultStateId)||!gene.states.Exists(s=>s.stateId==gene.defaultStateId)))throw new InvalidDataException("离散基因缺少默认状态："+gene.id);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
