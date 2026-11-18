using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.Entities
{
    // ★ 新增：部位修改通过权威端校验并一次提交，固定分工仍遵守其遗传锁定。
    public static class EntityBodyEditor
    {
        public static bool Apply(DarwinEntity entity,PartOverride part,bool reset)
        {
            if(!Ecology.EcologyQueries.Alive(entity)||part==null||DarwinPark.Core.OptionalComponent.Read<Ecology.AntColonyMember>(entity)?.Frozen==true)return false;
            var genome=entity.Genes.Genome;var body=MorphologyRegistry.Body(genome.BodyPlanId);
            if(!body.slots.Exists(s=>s.id==part.slotId))return false;
            var candidate=new Genome{BodyPlanId=genome.BodyPlanId,Genes=genome.Genes,BodyOverrides=new System.Collections.Generic.List<PartOverride>(genome.BodyOverrides)};
            candidate.BodyOverrides.RemoveAll(p=>p.slotId==part.slotId);
            if(!reset)candidate.BodyOverrides.Add(new PartOverride{slotId=part.slotId,scale=part.scale,position=part.position,rotation=part.rotation});
            try{MorphologyRegistry.ValidateGenome(candidate);}catch(System.IO.InvalidDataException){return false;}
            genome.BodyOverrides=candidate.BodyOverrides;entity.Genes.Recalculate();return true;
        }
    }
}
