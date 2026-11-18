using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：生产资格来自独立自养基因，移动或混合异养不再隐式取消光合/矿物能力。
    public static class ProducerClassification
    {
        public static bool IsProducer(EntityGeneController genes)=>genes!=null&&(GeneCapabilities.Active(genes,BiologySettings.Current.photoGene)!=null||GeneCapabilities.Active(genes,BiologySettings.Current.mineralGene)!=null)&&OptionalComponent.Read<EggIncubation>(genes)?.Incubating!=true;
        public static void Remove(EntityGeneController genes){}
        public static void Clear(){}
    }
}
