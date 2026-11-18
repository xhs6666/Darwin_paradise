using System;
using System.IO;

namespace DarwinPark.Core
{
    // ✦ 新增：专服由独立构建确定，普通客户端不能用启动参数冒充专服。
    public static class DedicatedServerMode
    {
        public static bool Enabled
        {
            get
            {
#if UNITY_SERVER || DARWIN_DEDICATED_SERVER
                return true;
#else
                return false;
#endif
            }
        }
        public static string Argument(string key)
        {
            var args=System.Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length;i++)if(args[i]==key)
            {
                if(i+1==args.Length||args[i+1].StartsWith("-",StringComparison.Ordinal))throw new ArgumentException("Missing value for "+key);
                return args[i+1];
            }
            return null;
        }
        public static string DataRoot(string fallback)
        {
            string path=Enabled?Argument("-server-data"):null;
            return Path.GetFullPath(path??fallback);
        }
    }
}
