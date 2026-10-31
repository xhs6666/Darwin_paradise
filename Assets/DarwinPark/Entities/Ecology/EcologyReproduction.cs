using System.Linq;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    public static class EcologyReproduction
    {
        // ★ 新增：卵生后代先作为可攻击的独立卵存在，孵化前不进入普通生命周期。
        public static void Spawn(EntityGeneController parent,Genome genome,Vector3 position)
        {
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner?.Invoke(genome,position,"");
            var child=EntityRegistry.All.FirstOrDefault(e=>!before.Contains(e));if(child==null)return;
            // ★ 适配：出生立即固定分工与种群标记，早于幼年变异和任何 AI 帧。
            AntColonyMember.OnBirth(parent,child);
            if(EcologyQueries.Active(parent,EcologySettings.Load().oviparousGene)==null)return;
            var egg=child.GetComponent<EggIncubation>()??child.gameObject.AddComponent<EggIncubation>();
            egg.Begin(EcologySettings.Load().incubationSeconds/Mathf.Max(.01f,parent.GetAttribute("hatchSpeed")));
        }
    }
}
