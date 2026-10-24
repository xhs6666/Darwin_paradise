using System;
using System.Collections.Generic;

namespace DarwinPark.Genetics
{


    [Serializable]
    public class EntityGeneData
    {
        public string entityName;
        public List<string> namePrefixes = new List<string>();
        public List<string> nameSuffixes = new List<string>();
        public List<GeneInstanceData> genes = new List<GeneInstanceData>();
    }

    [Serializable]
    public class GeneInstanceData
    {
        public string definitionId;
        public float value;
        public int tier;
    }

}