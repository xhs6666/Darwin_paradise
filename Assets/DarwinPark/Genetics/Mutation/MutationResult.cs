using System.Collections.Generic;

namespace DarwinPark.Genetics.Mutation
{
    // ★ 适配：突变类型与返回结果是同一个数据约定，合并维护。
    public enum MutationType
    {
        Error = 0,
        Duplicate = 1,
        Deletion = 2,
        Insertion = 3
    }

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