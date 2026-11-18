using System;

namespace DarwinPark.ServerCore
{
    // ✦ 新增：协议、内容纪元与存档格式独立演进；补丁版本不作为强制断线依据。
    [Serializable] public sealed class ServerCompatibility
    {
        public const int WireVersion = 2;
        public int protocol, contentEpoch, minimumSave, maximumSave;
        public string release;

        public void Validate()
        {
            if(protocol != WireVersion || contentEpoch < 1 || minimumSave < 1 || maximumSave < minimumSave || string.IsNullOrWhiteSpace(release))
                throw new ArgumentException("Invalid server release manifest");
        }

        public string Reject(ServerCompatibility remote)
        {
            Validate();
            if(remote == null) return "缺少版本握手，请更新客户端";
            if(remote.minimumSave<1||remote.maximumSave<remote.minimumSave)return "客户端存档版本范围无效";
            if(remote.protocol != protocol) return "通信协议不兼容，请更新客户端与服务端";
            if(remote.contentEpoch != contentEpoch) return "游戏内容版本不兼容，请安装同一内容纪元的版本";
            if(remote.minimumSave > maximumSave || remote.maximumSave < minimumSave) return "存档格式范围不兼容";
            if(string.IsNullOrWhiteSpace(remote.release)) return "缺少客户端发行版本";
            return null;
        }

        public void RequireSave(int version)
        {
            Validate();
            if(version < minimumSave || version > maximumSave)
                throw new InvalidOperationException("Unsupported save format " + version + "; supported " + minimumSave + ".." + maximumSave);
        }
        public void RequireWorld(int saveVersion,int savedContentEpoch)
        {
            RequireSave(saveVersion);
            if(savedContentEpoch!=contentEpoch)
                throw new InvalidOperationException("存档内容版本不兼容："+savedContentEpoch+"；当前版本："+contentEpoch+"。本版本不提供旧存档迁移，原文件保留。");
        }
    }

    // ✦ 新增：显式入服状态机，未握手/未加载世界的连接不能执行命令。
    public sealed class ServerAdmission
    {
        public bool Joined { get; private set; }
        public bool Ready { get; private set; }
        public bool Rejected { get; private set; }
        public double Since { get; private set; }
        public long WorldRevision {get;private set;}
        public ServerAdmission(double now) { Since = now; }
        public string Join(ServerCompatibility local, ServerCompatibility remote, double now)
        {
            if(Joined || Rejected) return "重复入服握手";
            string reason = local.Reject(remote);
            if(reason != null) { Rejected = true; return reason; }
            Joined = true; Since = now; return null;
        }
        public bool AcknowledgeWorld(long revision)
        {
            if(!Joined || Rejected || WorldRevision==0 || revision!=WorldRevision) return false;
            Ready = true; return true;
        }
        public void Loading(double now) { Ready = false; Since = now; WorldRevision++; }
        public bool Expired(double now, double handshakeTimeout, double loadingTimeout)
            => !Ready && now - Since > (Joined ? loadingTimeout : handshakeTimeout);
        public bool CanCommand => Joined && Ready && !Rejected;
        public void Block(){Rejected=true;Ready=false;}
    }
}
