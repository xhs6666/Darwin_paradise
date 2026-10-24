using System.Collections.Generic;
using System.Linq;

namespace DarwinPark.Genetics
{
    [System.Serializable]
    public class Genome
    {
        public List<GeneInstance> Genes = new();

        public GeneInstance Find(string instanceId) =>
            Genes.FirstOrDefault(g => g.InstanceId == instanceId);

        public IEnumerable<GeneInstance> FindByDefinition(string defId) =>
            Genes.Where(g => g.DefinitionId == defId);

        public bool HasDefinition(string defId) => Genes.Any(g => g.DefinitionId == defId);

        public GeneInstance Add(GeneInstance gene)
        {
            Genes.Add(gene);
            return gene;
        }

        public bool Remove(string instanceId)
        {
            var g = Find(instanceId);
            return g != null && Genes.Remove(g);
        }
    }
}
