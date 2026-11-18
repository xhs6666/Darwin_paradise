using System;
using DarwinPark.ServerCore;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：房主唯一权威；身份来自已接受的连接，不能从客户端报文提升管理员；同一实体只能分配给一名玩家。
    [DefaultExecutionOrder(300)]
    public sealed class LanSession : MonoBehaviour
    {
        public static LanSession Instance {get;private set;}
        public bool IsHost=>_host!=null&&_host.Listening;
        public bool IsClient=>_server!=null;
        public bool Online=>IsHost||IsClient||_connecting;
        public bool Cheats {get;internal set;}
        public bool CanManage=>!Online||IsHost||Cheats;
        public string LocalId {get;internal set;}="host";
        public string OwnedEntityId {get;internal set;}="";
        public string Status {get;internal set;}="";
        public int Port=>_host?.Port??0;
        public int RejectedCommands {get;internal set;}
        public int MemberCount=>_host?.MemberCount??0;
        public IReadOnlyList<LanPlayer> Players=>_players;
        internal readonly List<LanPlayer> _players=new List<LanPlayer>();
        private readonly ConcurrentQueue<(LanConnection connection,string text)> _incoming=new ConcurrentQueue<(LanConnection,string)>();
        private readonly ConcurrentQueue<LanConnection> _accepted=new ConcurrentQueue<LanConnection>(),_disconnected=new ConcurrentQueue<LanConnection>();
        private readonly ConcurrentQueue<Action> _main=new ConcurrentQueue<Action>();
        internal LanConnection _server;internal WorldSessionManager _world;
        private ILanServerRuntime _host;private ILanClientRuntime _replica;
        private TcpClient _joiningClient;private int _joinVersion;
        private bool _connecting,_leaving,_quitting;
        public static LanSession Ensure(GameObject host)=>Instance!=null?Instance:host.GetComponent<LanSession>()??host.AddComponent<LanSession>();
        private void Awake()
        {
            if(LanRuntimeServices.CreateServer==null)throw new InvalidOperationException("Missing DarwinPark.Server.Unity registration");
            _host=LanRuntimeServices.CreateServer(this);_replica=DedicatedServerMode.Enabled?null:LanRuntimeServices.CreateClient?.Invoke(this);
            if(!DedicatedServerMode.Enabled&&_replica==null)throw new InvalidOperationException("Missing client presentation registration");
            Instance=this;_world=GetComponent<WorldSessionManager>()??FindFirstObjectByType<WorldSessionManager>();_world.Loaded+=OnLoaded;
            if(DedicatedServerMode.Enabled)return;
            GameplayPresentation.AttachSession?.Invoke(this);
        }
        internal void PublishAuthority()
        {WorldAuthority.Online=Online;WorldAuthority.IsReplica=IsClient||_connecting;WorldAuthority.CanEdit=CanManage;WorldAuthority.RequestTime=(running,scale)=>SubmitEdit(new LanCommand{kind="time",running=running,scale=scale});}
        // ✦ 补充：公共会话只编排生命周期，权威计算与副本显示在独立类型中。
        public bool StartHost(int port=-1,bool cheats=false)=>_host.StartHost(port,cheats);
        public void CloseHost()=>_host.CloseHost();
        public void SetCheats(bool enabled)=>_host.SetCheats(enabled);
        private void OnLoaded()=>_replica?.OnLoaded();
        internal void Accept(LanConnection connection)=>_accepted.Enqueue(connection);
        internal void ReceiveClosed(LanConnection connection)=>_disconnected.Enqueue(connection);
        internal void ReceivePacket(LanConnection connection,string text)=>Receive(connection,text);
        public void Join(string address,int port,string name)
        {
            if(_replica==null)throw new InvalidOperationException("Dedicated server cannot join a remote world");
            if(Online)return;if(port<1||port>65535||string.IsNullOrWhiteSpace(address)){Status="请输入有效地址和端口";return;}
            _replica?.Reset();_connecting=true;_leaving=false;Status="正在连接管理员…";PublishAuthority();var settings=LanSettings.Current;
            // ★ 新增：连接、身份、权限与关键指令可追踪，不记录每帧输入、快照或完整基因报文。
            Debug.Log("[联机] 连接地址="+address.Trim()+"，端口="+port);
            // ★ 修复（防Bug回归）：取消后旧异步连接不可覆盖新房间，也不能在场景销毁后留下未关闭的套接字。
            int version=Interlocked.Increment(ref _joinVersion);var client=new TcpClient();_joiningClient=client;
            _=Task.Run(async()=>
            {
                try
                {
                    var task=client.ConnectAsync(address.Trim(),port);if(await Task.WhenAny(task,Task.Delay(TimeSpan.FromSeconds(settings.connectionTimeout)))!=task)throw new TimeoutException("连接超时");await task;
                    var connection=new LanConnection(client,settings.maximumPacketBytes,settings.maximumQueuedPackets,Receive,c=>_disconnected.Enqueue(c));
                    if(version!=Volatile.Read(ref _joinVersion)){connection.Dispose();return;}
                    _main.Enqueue(()=>{if(!_connecting||version!=_joinVersion){connection.Dispose();return;}_joiningClient=null;_server=connection;_connecting=false;PublishAuthority();connection.Start();SendMessage(connection,new LanMessage{type="join",text=(name??"成员").Trim(),compatibility=LanRelease.Current});});
                }
                catch(Exception ex){client.Close();_main.Enqueue(()=>{if(version!=_joinVersion)return;_joiningClient=null;_connecting=false;Status="连接失败："+ex.Message;PublishAuthority();Debug.LogWarning("[联机] "+Status);});}
            });
        }
        private void Receive(LanConnection connection,string text)
        {if(_incoming.Count>=LanSettingsQueueLimit){connection.Dispose();return;}_incoming.Enqueue((connection,text));}
        private int LanSettingsQueueLimit=128;
        internal static void SendMessage(LanConnection connection,LanMessage message)=>connection.Send(JsonUtility.ToJson(message));
        public void SendCommand(LanCommand command)
        {if(IsClient)SendMessage(_server,new LanMessage{type="command",command=command});else if(IsHost)_host.ProcessLocalCommand(command);}
        public bool SubmitEdit(LanCommand command)
        {
            if(!CanManage){Status="只有管理员或开启作弊的成员可以修改世界";return false;}
            if(IsClient){SendCommand(command);return true;}return LanRuntimeServices.Apply(command,this);
        }
        public bool AllowView(DarwinEntity entity)
        {
            if(!Online)return true;string id=EcologyIdentity.Of(entity);
            if(IsClient){if(OwnedEntityId!=id){SendCommand(new LanCommand{kind="choose",entity=id});return false;}SetRtsMode(false);return true;}
            return _host.AssignLocal(id);
        }
        public void RequestEntity(string id)
        {if(IsClient)SendCommand(new LanCommand{kind="choose",entity=id});else if(_host.AssignLocal(id)&&EcologyIdentity.Find(id) is DarwinEntity entity)GameplayPresentation.EnterView?.Invoke(entity);}
        public bool CanCommand(DarwinEntity entity)
        {if(CanManage)return true;return QueenControls(OwnedEntityId,entity);}
        internal static bool QueenControls(string owned,DarwinEntity entity)
        {
            var queen=EcologyIdentity.Find(owned)?.GetComponent<AntColonyMember>();var ant=entity!=null?entity.GetComponent<AntColonyMember>():null;
            return queen!=null&&queen.Active&&queen.Role==AntRole.Queen&&ant!=null&&ant.Active&&ant.GroupId==queen.GroupId;
        }
        public bool CanUseRts=>CanManage||QueenControls(OwnedEntityId,EcologyIdentity.Find(OwnedEntityId));
        public void SetRtsMode(bool active)
        {
            if(active&&!CanUseRts){Status="只有管理员或王后可以使用RTS";return;}WorldAuthority.RtsMode=active;
            if(active)GameplayPresentation.ExitView?.Invoke();
            if(IsClient)SendCommand(new LanCommand{kind="mode",rts=active});
        }
        public bool IssueRts(string[] selection,RtsOrderKind kind,Vector3 point,DarwinEntity target=null,ColonyResource resource=null)
        {
            var command=new LanCommand{kind="rts",selection=selection,order=kind,point=point,target=target!=null?EcologyIdentity.Of(target):null,resource=resource?.State.id};
            if(IsClient){SendCommand(command);return true;}return _host.ApplyLocalRts(command);
        }
        public static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        public static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private void Update()
        {
            LanSettingsQueueLimit=LanSettings.Current.maximumQueuedPackets;
            while(_main.TryDequeue(out var action))action();
            while(_accepted.TryDequeue(out var connection))
            {
                _host.Accept(connection);
            }
            int processed=0;while(processed++<64&&_incoming.TryDequeue(out var packet))
            {
                try{var message=JsonUtility.FromJson<LanMessage>(packet.text);if(message==null)throw new ArgumentException("联机消息为空");Handle(packet.connection,message);}
                catch(Exception ex){Status="网络消息被拒绝："+ex.Message;Debug.LogWarning("[局域网] "+Status);RejectedCommands++;}
            }
            while(_disconnected.TryDequeue(out var closed))
            {
                if(_host.Disconnect(closed))continue;
                if(closed==_server&&!_leaving){LeaveToMenu(string.IsNullOrEmpty(_replica?.DisconnectReason)?"服务端已关闭对局或连接中断":_replica?.DisconnectReason);return;}
            }
            _host.Tick();
            _replica?.Tick();
        }
        private void Handle(LanConnection connection,LanMessage message)
        {
            if(!_host.Handle(connection,message))_replica?.Handle(connection,message);
        }
        public void LeaveToMenu(string reason="已离开局域网")
        {
            _leaving=true;bool replica=IsClient;CancelConnecting();_server?.Dispose();_server=null;
            if(IsHost)CloseHost();
            if(replica){WorldAuthority.IsReplica=true;_world.ReturnToMenu(false);_replica?.Reset();}
            OwnedEntityId="";_players.Clear();Cheats=false;WorldAuthority.Reset();Status=reason;Debug.Log("[联机] "+reason);GameplayPresentation.ShowNetworkMain?.Invoke(reason);_leaving=false;
        }
        private void CancelConnecting(){Interlocked.Increment(ref _joinVersion);_connecting=false;_joiningClient?.Close();_joiningClient=null;}
        private void OnApplicationQuit(){_quitting=true;CancelConnecting();_server?.Dispose();_host?.Dispose();}
        private void OnDestroy(){CancelConnecting();if(_world!=null)_world.Loaded-=OnLoaded;_server?.Dispose();_host?.Dispose();if(Instance==this)Instance=null;if(!_quitting)WorldAuthority.Reset();}
    }
}
