using System.IO;
using UnityEngine;
namespace DarwinPark.Core
{
    // ★ 新增：编辑器以工程根目录、发布版以可执行文件目录为基准，save/mods 读写使用同一入口。
    public static class GamePaths
    {
        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        public static string Saves => Path.Combine(Root,"save");
        public static string Mods => Path.Combine(Root,"mods");
    }
    public static class WorldRuntimeGate
    {
        public static bool Managed, Ready, Loading, Recording;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){Managed=Ready=Loading=Recording=false;}
    }
}
