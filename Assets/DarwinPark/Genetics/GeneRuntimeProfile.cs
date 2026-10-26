using System.Collections.Generic;
namespace DarwinPark.Genetics
{
    // ★ 新增：每个存档保留自身基因配置，安装或修改 mods 不会悄悄改写旧世界。
    public static class GeneRuntimeProfile
    {
        public static Dictionary<string,string> Catalog;
        public static GeneTreeDocument Tree;
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear(){Catalog=null;Tree=null;}
    }
}
