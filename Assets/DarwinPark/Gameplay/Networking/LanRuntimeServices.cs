using System;
using DarwinPark.ServerCore;
using UnityEngine;

namespace DarwinPark.UI
{
    // ✦ 新增：会话依赖接口，权威实现由服务端程序集注入，避免 UI 反向引用服务端形成依赖环。
    public interface ILanServerRuntime : IDisposable
    {
        bool Listening {get;}
        int Port {get;}
        int MemberCount {get;}
        bool StartHost(int port,bool cheats);
        void CloseHost();
        void SetCheats(bool enabled);
        void Accept(LanConnection connection);
        bool Disconnect(LanConnection connection);
        bool Handle(LanConnection connection,LanMessage message);
        void Tick();
        bool AssignLocal(string id);
        void ProcessLocalCommand(LanCommand command);
        bool ApplyLocalRts(LanCommand command);
    }
    public interface ILanClientRuntime
    {
        string DisconnectReason {get;}
        void Reset();void OnLoaded();void Tick();void Handle(LanConnection connection,LanMessage message);
    }
    public static class LanRuntimeServices
    {
        public static Func<LanSession,ILanServerRuntime> CreateServer;
        public static Func<LanSession,ILanClientRuntime> CreateClient;
        public static Func<LanCommand,LanSession,bool> ApplyAuthority;
        public static bool Apply(LanCommand command,LanSession session)
        {
            if(ApplyAuthority==null)throw new InvalidOperationException("DarwinPark.Server.Unity runtime is not installed");
            return ApplyAuthority(command,session);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){CreateServer=null;CreateClient=null;ApplyAuthority=null;}
    }
}
