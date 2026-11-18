using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace DarwinPark.Genetics.Mutation
{
    [Serializable] public sealed class JointMutationGroup {public string id;public string[] genes=Array.Empty<string>();public float maximumDelta=10,stateChangeChance=.05f;}
    [Serializable] public sealed class JointMutationSettings {public int version=2;public float chance=.15f;public JointMutationGroup[] groups=Array.Empty<JointMutationGroup>();}
    // ★ 新增：联合作用不同于联合变异；事件先准备整组候选，全部合法才一次提交，不创建半套基因或部位。
    public static class JointGeneMutation
    {
        private static JointMutationSettings _settings;
        public static JointMutationSettings Settings
        {get{if(_settings!=null)return _settings;var asset=Resources.Load<TextAsset>("Genetics/joint_mutations");if(asset==null)throw new InvalidDataException("缺少Genetics/joint_mutations");var settings=JsonUtility.FromJson<JointMutationSettings>(asset.text);if(settings?.version!=2||!float.IsFinite(settings.chance)||settings.chance<0||settings.chance>1||settings.groups==null)throw new InvalidDataException("联合变异配置无效");foreach(var group in settings.groups)if(group.genes==null||group.genes.Length<2||group.genes.Distinct().Count()!=group.genes.Length||!float.IsFinite(group.maximumDelta)||group.maximumDelta<=0||group.maximumDelta>100||!float.IsFinite(group.stateChangeChance)||group.stateChangeChance<0||group.stateChangeChance>1)throw new InvalidDataException("联合变异组无效："+group.id);return _settings=settings;}}
        public static MutationResult Apply(Genome genome,JointMutationGroup group)
        {
            if(genome==null||group?.genes==null||group.genes.Length<2||group.genes.Distinct().Count()!=group.genes.Length||!float.IsFinite(group.maximumDelta)||group.maximumDelta<=0||group.maximumDelta>100||!float.IsFinite(group.stateChangeChance)||group.stateChangeChance<0||group.stateChangeChance>1)return MutationResult.Fail("联合变异组为空");
            var candidate=new List<(GeneInstance gene,float value,string state)>();float shift=UnityEngine.Random.Range(-group.maximumDelta,group.maximumDelta);
            foreach(string id in group.genes)
            {
                var source=genome.Genes.FirstOrDefault(g=>g.Enabled&&g.DefinitionId==id);if(source==null)return MutationResult.Fail("联合变异缺少成员："+id);
                var definition=GeneDatabase.Get(id);if(definition==null)return MutationResult.Fail("联合变异定义不存在");string state=source.StateId;
                if(definition.ExplicitState&&UnityEngine.Random.value<group.stateChangeChance){var available=definition.States.Where(s=>!source.MissingStateIds.Contains(s.StateId)).ToArray();if(available.Length==0)return MutationResult.Fail("联合变异无合法状态");state=available[UnityEngine.Random.Range(0,available.Length)].StateId;}
                if(definition.ExplicitState&&!definition.States.Exists(s=>s.StateId==state))return MutationResult.Fail("联合变异状态无效");float value=Mathf.Clamp(source.Value+shift,0,100);if(!float.IsFinite(value))return MutationResult.Fail("联合变异数值无效");candidate.Add((source,value,state));
            }
            foreach(var item in candidate){item.gene.Value=item.value;item.gene.StateId=item.state;}
            return MutationResult.Ok(MutationType.Error,"联合变异："+group.id,candidate.Select(c=>c.gene).ToArray());
        }
        public static MutationResult Try(Genome genome)
        {
            var settings=Settings;if(UnityEngine.Random.value>=settings.chance)return null;
            var groups=settings.groups.Where(group=>group.genes.All(id=>genome.Genes.Exists(g=>g.Enabled&&g.DefinitionId==id))).ToArray();return groups.Length==0?null:Apply(genome,groups[UnityEngine.Random.Range(0,groups.Length)]);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>_settings=null;
    }
}
