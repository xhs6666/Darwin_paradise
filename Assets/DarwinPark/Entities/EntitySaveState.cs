using System;
using System.Collections.Generic;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.Entities
{
    [Serializable] public class StatSaveValue {public string id;public float value,max;}
    [Serializable] public class ReproductionSaveState {public string gene;public float gestation,cooldown;public Genome partner;}
    // ★ 新增：实体快照保留遗传实例、生命周期与孕育，而非读档时重新出生。
    [Serializable] public class EntitySaveState
    {
        public string name;public Vector3 position,scale;public Quaternion rotation;
        public Genome genome;public AgeState age;public float lifeProgress,mutationChance,energy;
        public int mutatedStages,stageChecks;public bool gestating,favorite;
        public List<string> traits=new List<string>();public List<StatSaveValue> stats=new List<StatSaveValue>();
        public List<ReproductionSaveState> reproduction=new List<ReproductionSaveState>();
        public static EntitySaveState Capture(DarwinEntity entity)
        {
            var genes=entity.Genes;var state=new EntitySaveState{name=entity.DisplayName,position=entity.transform.position,rotation=entity.transform.rotation,scale=entity.transform.localScale,
                genome=JsonUtility.FromJson<Genome>(JsonUtility.ToJson(genes.Genome)),age=genes.CurrentAge,lifeProgress=entity.GetComponent<EntityLifeCycle>()?.Progress??0,
                mutationChance=genes.mutationChance,mutatedStages=genes.MutatedStageMask,stageChecks=genes.StageMutationChecks,gestating=genes.IsGestating,energy=entity.Energy,
                traits=new List<string>(entity.Traits),stats=entity.GetComponent<EntityStats>().CaptureValues()};
            foreach(var gene in genes.Genome.Genes)if(genes.GetLogic(gene) is ReproductionGeneLogic reproduction)state.reproduction.Add(reproduction.CaptureState());
            return state;
        }
        public void Restore(DarwinEntity entity)
        {
            entity.transform.SetPositionAndRotation(position,rotation);entity.transform.localScale=scale;entity.DisplayName=name;
            var genes=entity.Genes;genes.mutationChance=mutationChance;genes.IsGestating=gestating;genes.RestoreStageState(age,mutatedStages,stageChecks);
            entity.GetComponent<EntityLifeCycle>().RestoreProgress(lifeProgress);
            genes.Recalculate();entity.GetComponent<EntityStats>().RestoreValues(stats);
            foreach(var entry in reproduction){var gene=genes.Genome.Find(entry.gene);if(genes.GetLogic(gene) is ReproductionGeneLogic logic)logic.RestoreState(entry);}
            foreach(var trait in traits)entity.EnableTrait(trait);entity.AddEnergy(energy-entity.Energy);
        }
    }
}
