using System;
using System.Collections.Generic;
using System.IO;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    [Serializable]public sealed class CorpseSuppression{public string gene;public float value;}
    // ★ 新增：尸体只保存展示所需生前快照，不创建伪活体，也不保存宿主/繁殖伙伴等重复的整套世界引用。
    [Serializable]public sealed class CorpseEntityRecord
    {
        public int version;public Genome genome;public AgeState age;public float lifeProgress,environmentSuppression;public bool gestating;
        public List<StatSaveValue> stats=new List<StatSaveValue>();public List<CorpseSuppression> roleSuppression=new List<CorpseSuppression>();
        public static CorpseEntityRecord Capture(DarwinEntity entity)
        {
            var genes=entity.Genes;var record=new CorpseEntityRecord{version=1,genome=JsonUtility.FromJson<Genome>(JsonUtility.ToJson(genes.Genome)),age=genes.CurrentAge,lifeProgress=entity.GetComponent<EntityLifeCycle>()?.Progress??0,environmentSuppression=genes.EnvironmentGeneSuppression,gestating=genes.IsGestating,stats=entity.GetComponent<EntityStats>().CaptureValues()};
            var roles=AntColonyMember.ExpressionSuppression(genes);if(roles!=null)foreach(var pair in roles)record.roleSuppression.Add(new CorpseSuppression{gene=pair.Key,value=pair.Value});return record;
        }
        public GenomeEvaluation Evaluate(){var roles=new Dictionary<string,float>();foreach(var pair in roleSuppression)roles[pair.gene]=pair.value;return GeneValueResolver.Resolve(genome,gestating,environmentSuppression,additionalSuppression:roles);}
        // ★ 修复（防Bug回归）：Unity旧档缺少内联对象时可能生成全零空对象，必须与明确的损坏新档区分。
        public static bool LegacyEmpty(CorpseEntityRecord value)=>value!=null&&value.version==0&&(value.genome?.Genes==null||value.genome.Genes.Count==0)&&value.age==AgeState.Infant&&value.lifeProgress==0&&value.environmentSuppression==0&&!value.gestating&&(value.stats==null||value.stats.Count==0)&&(value.roleSuppression==null||value.roleSuppression.Count==0);
        public static CorpseEntityRecord Normalize(CorpseEntityRecord value){if(value==null||LegacyEmpty(value))return null;Validate(value);return value;}
        public static void Validate(CorpseEntityRecord value)
        {
            if(value==null||LegacyEmpty(value))return;if(value.version!=1||value.genome?.Genes==null||!Enum.IsDefined(typeof(AgeState),value.age)||!float.IsFinite(value.lifeProgress)||value.lifeProgress<0||!float.IsFinite(value.environmentSuppression)||value.environmentSuppression<0||value.stats==null||value.roleSuppression==null)throw new InvalidDataException("尸体实体档案无效");
            var ids=new HashSet<string>();foreach(var gene in value.genome.Genes)if(gene==null||string.IsNullOrEmpty(gene.DefinitionId)||!Guid.TryParseExact(gene.InstanceId,"N",out _)||!ids.Add(gene.InstanceId)||!float.IsFinite(gene.Value)||gene.Value<0||gene.Value>100||!float.IsFinite(gene.CopyStrength)||gene.CopyStrength<0||gene.MissingStateIds==null)throw new InvalidDataException("尸体遗传档案无效");
            foreach(var stat in value.stats)if(stat==null||string.IsNullOrEmpty(stat.id)||!float.IsFinite(stat.value)||!float.IsFinite(stat.max)||stat.max<0)throw new InvalidDataException("尸体数值档案无效");
            foreach(var suppression in value.roleSuppression)if(suppression==null||string.IsNullOrEmpty(suppression.gene)||!float.IsFinite(suppression.value)||suppression.value<0)throw new InvalidDataException("尸体抑制档案无效");
        }
    }
}
