using System.Collections.Generic;
namespace DarwinPark.Genetics
{
    // ★ 新增：每个存档保留自身基因配置，安装或修改 mods 不会悄悄改写旧世界。
    public static class GeneRuntimeProfile
    {
        public static Dictionary<string,string> Catalog;
        public static GeneTreeDocument Tree;
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        // ★ 适配：离开世界必须一起释放状态规则快照，下一世界不能串用上个世界的规则。
        public static void Clear(){Catalog=null;Tree=null;EntityStateRegistry.Clear();}
    }
}
