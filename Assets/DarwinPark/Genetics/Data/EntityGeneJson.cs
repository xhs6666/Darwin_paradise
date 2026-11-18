using System;
using System.Collections.Generic;

namespace DarwinPark.Genetics
{


    [Serializable]
    public class EntityGeneData
    {
        public string entityName;
        public string bodyPlanId="organism_basic";
        public List<PartOverride> bodyOverrides=new();
        public List<string> namePrefixes = new List<string>();
        public List<string> nameSuffixes = new List<string>();
        public List<GeneInstanceData> genes = new List<GeneInstanceData>();
        public Genome ToGenome(){var genome=new Genome{BodyPlanId=bodyPlanId};foreach(var part in bodyOverrides)genome.BodyOverrides.Add(new PartOverride{slotId=part.slotId,scale=part.scale});foreach(var gene in genes)genome.Add(gene.CreateInstance());return genome;}
    }

    [Serializable]
    public class GeneInstanceData
    {
        public string definitionId;
        public float value;
        public int tier;
        public string stateId;
        public bool enabled=true;
        public float copyStrength=1;
        public List<string> missingStateIds=new();
        // ★ 新增：模板、默认实体和放置统一恢复基因状态，不能把禁用、缺失状态或复制强度丢掉。
        public GeneInstance CreateInstance(){var gene=GeneInstance.Create(definitionId,value,tier);if(!string.IsNullOrEmpty(stateId))gene.StateId=stateId;gene.Enabled=enabled;gene.CopyStrength=copyStrength;gene.MissingStateIds=new List<string>(missingStateIds);return gene;}
    }

}
