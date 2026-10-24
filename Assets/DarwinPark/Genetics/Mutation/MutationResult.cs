using System.Collections.Generic;

namespace DarwinPark.Genetics.Mutation
{
    public class MutationResult
    {
        public bool Success;
        public MutationType Type;
        public string Description;
        public List<GeneInstance> Affected = new();

        public static MutationResult Fail(string reason) =>
            new MutationResult { Success = false, Description = reason };

        public static MutationResult Ok(MutationType t, string desc, params GeneInstance[] affected)
        {
            var r = new MutationResult { Success = true, Type = t, Description = desc };
            if (affected != null) r.Affected.AddRange(affected);
            return r;
        }
    }
}