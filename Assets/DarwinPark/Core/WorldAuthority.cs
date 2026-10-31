using System;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：底层只认识模拟/编辑权限，不反向引用网络UI；客户端只展示服务端状态，不运行第二份生态。
    public static class WorldAuthority
    {
        public static bool Online,IsReplica,RtsMode,RemoteCommand;
        public static bool CanEdit=true;
        public static Func<bool,float,bool> RequestTime;
        public static bool InterceptTime(bool running,float scale)
        {
            if(!Online)return false;
            if(!CanEdit)return true;
            if(IsReplica){RequestTime?.Invoke(running,scale);return true;}return false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset(){Online=IsReplica=RtsMode=RemoteCommand=false;CanEdit=true;RequestTime=null;}
    }
}
