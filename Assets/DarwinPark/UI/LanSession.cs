using System;
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
        private sealed class Peer
        {public LanConnection connection;public LanPlayer player;public bool ready,rts,joined;public float inputTime,window;public int commands;}
        public static LanSession Instance {get;private set;}
        public bool IsHost=>_listener!=null;
        public bool IsClient=>_server!=null;
        public bool Online=>IsHost||IsClient||_connecting;
        public bool Cheats {get;private set;}
        public bool CanManage=>!Online||IsHost||Cheats;
        public string LocalId {get;private set;}="host";
        public string OwnedEntityId {get;private set;}="";
        public string Status {get;private set;}="";
        public int Port=>_listener?.Port??0;
        public int RejectedCommands {get;private set;}
        public int MemberCount=>_peers.Count;
        public IReadOnlyList<LanPlayer> Players=>_players;
        private readonly List<LanPlayer> _players=new List<LanPlayer>();
        private readonly Dictionary<string,Peer> _peers=new Dictionary<string,Peer>();
        private readonly ConcurrentQueue<(LanConnection connection,string text)> _incoming=new ConcurrentQueue<(LanConnection,string)>();
        private readonly ConcurrentQueue<LanConnection> _accepted=new ConcurrentQueue<LanConnection>(),_disconnected=new ConcurrentQueue<LanConnection>();
        private readonly ConcurrentQueue<Action> _main=new ConcurrentQueue<Action>();
        private LanListener _listener;private LanConnection _server;private WorldSessionManager _world;
        private TcpClient _joiningClient;private int _joinVersion;
        private bool _connecting,_remoteLoading,_leaving,_quitting,_pendingJump,_pendingAttack;
        private float _lastFrame,_lastInput,_lastConfig,_lastOre;private long _sequence,_receivedSequence=-1;
        private string _configuration;
        public static LanSession Ensure(GameObject host)=>Instance!=null?Instance:host.GetComponent<LanSession>()??host.AddComponent<LanSession>();
        private void Awake()
        {
            Instance=this;_world=GetComponent<WorldSessionManager>()??FindFirstObjectByType<WorldSessionManager>();_world.Loaded+=OnLoaded;
            if(GetComponent<LanLobbyUI>()==null)gameObject.AddComponent<LanLobbyUI>();if(GetComponent<RtsSelectionInput>()==null)gameObject.AddComponent<RtsSelectionInput>();
        }
        private void PublishAuthority()
        {WorldAuthority.Online=Online;WorldAuthority.IsReplica=IsClient||_connecting;WorldAuthority.CanEdit=CanManage;WorldAuthority.RequestTime=(running,scale)=>SubmitEdit(new LanCommand{kind="time",running=running,scale=scale});}
        public bool StartHost(int port=-1,bool cheats=false)
        {
            if(Online||!WorldRuntimeGate.Ready||_world.Current==null){Status="请先进入一个单人世界";return false;}
            try
            {
                var settings=LanSettings.Current;_listener=new LanListener(port<0?settings.port:port,client=>
                {
                    var connection=new LanConnection(client,settings.maximumPacketBytes,settings.maximumQueuedPackets,Receive,c=>_disconnected.Enqueue(c));_accepted.Enqueue(connection);
                });Cheats=cheats;LocalId="host";_configuration=LanWorldSnapshot.Configuration();Status="已对局域网开放，端口 "+Port;PublishAuthority();Roster();Debug.Log("[联机] 房主开放端口="+Port+"，作弊="+Cheats);return true;
            }
            catch(Exception ex)when(ex is SocketException||ex is InvalidOperationException){_listener?.Dispose();_listener=null;Status="开放失败："+ex.Message;Debug.LogWarning("[联机] "+Status);return false;}
        }
        public void Join(string address,int port,string name)
        {
            if(Online)return;if(port<1||port>65535||string.IsNullOrWhiteSpace(address)){Status="请输入有效地址和端口";return;}
            _connecting=true;_leaving=false;Status="正在连接管理员…";PublishAuthority();var settings=LanSettings.Current;
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
                    _main.Enqueue(()=>{if(!_connecting||version!=_joinVersion){connection.Dispose();return;}_joiningClient=null;_server=connection;_connecting=false;PublishAuthority();connection.Start();SendMessage(connection,new LanMessage{type="join",text=(name??"成员").Trim()});});
                }
                catch(Exception ex){client.Close();_main.Enqueue(()=>{if(version!=_joinVersion)return;_joiningClient=null;_connecting=false;Status="连接失败："+ex.Message;PublishAuthority();Debug.LogWarning("[联机] "+Status);});}
            });
        }
        private void Receive(LanConnection connection,string text)
        {if(_incoming.Count>=LanSettingsQueueLimit){connection.Dispose();return;}_incoming.Enqueue((connection,text));}
        private int LanSettingsQueueLimit=128;
        private static void SendMessage(LanConnection connection,LanMessage message)=>connection.Send(JsonUtility.ToJson(message));
        public void SendCommand(LanCommand command)
        {if(IsClient)SendMessage(_server,new LanMessage{type="command",command=command});else if(IsHost)ProcessCommand(null,command);}
        public bool SubmitEdit(LanCommand command)
        {
            if(!CanManage){Status="只有管理员或开启作弊的成员可以修改世界";return false;}
            if(IsClient){SendCommand(command);return true;}return LanEditExecutor.Apply(command,this);
        }
        public bool AllowView(DarwinEntity entity)
        {
            if(!Online)return true;string id=EcologyIdentity.Of(entity);
            if(IsClient){if(OwnedEntityId!=id){SendCommand(new LanCommand{kind="choose",entity=id});return false;}SetRtsMode(false);return true;}
            return Assign(null,id);
        }
        public void RequestEntity(string id)
        {if(IsClient)SendCommand(new LanCommand{kind="choose",entity=id});else if(Assign(null,id)&&EcologyIdentity.Find(id) is DarwinEntity entity)EntityViewController.Instance.EnterView(entity);}
        public bool CanCommand(DarwinEntity entity)
        {if(CanManage)return true;return QueenControls(OwnedEntityId,entity);}
        private static bool QueenControls(string owned,DarwinEntity entity)
        {
            var queen=EcologyIdentity.Find(owned)?.GetComponent<AntColonyMember>();var ant=entity!=null?entity.GetComponent<AntColonyMember>():null;
            return queen!=null&&queen.Active&&queen.Role==AntRole.Queen&&ant!=null&&ant.Active&&ant.GroupId==queen.GroupId;
        }
        public bool CanUseRts=>CanManage||QueenControls(OwnedEntityId,EcologyIdentity.Find(OwnedEntityId));
        public void SetRtsMode(bool active)
        {
            if(active&&!CanUseRts){Status="只有管理员或王后可以使用RTS";return;}WorldAuthority.RtsMode=active;
            if(active)EntityViewController.Instance?.ExitView();
            if(IsClient)SendCommand(new LanCommand{kind="mode",rts=active});
        }
        public bool IssueRts(string[] selection,RtsOrderKind kind,Vector3 point,DarwinEntity target=null,ColonyResource resource=null)
        {
            var command=new LanCommand{kind="rts",selection=selection,order=kind,point=point,target=target!=null?EcologyIdentity.Of(target):null,resource=resource?.State.id};
            if(IsClient){SendCommand(command);return true;}return ApplyRts(null,command);
        }
        public void SetCheats(bool enabled)
        {if(!IsHost)return;Cheats=enabled;PublishAuthority();Roster();Status=enabled?"作弊模式已开启":"作弊模式已关闭";Debug.Log("[联机] "+Status);}
        private bool Assign(Peer peer,string id)
        {
            var entity=EcologyIdentity.Find(id);if(!EcologyQueries.Alive(entity)||AntColonyWorker.IsCarried(entity)||entity.GetComponent<EggIncubation>()?.Incubating==true){Reply(peer,"该实体无法操控",false);return false;}
            string previous=peer!=null?peer.player.entity:OwnedEntityId;
            if(peer!=null&&!Cheats&&!string.IsNullOrEmpty(previous)&&previous!=id&&EcologyQueries.Alive(EcologyIdentity.Find(previous))){Reply(peer,"成员只能在进入游戏或实体死亡后选择",false);return false;}
            if(_peers.Values.Any(p=>p!=peer&&p.player.entity==id)||(peer!=null&&OwnedEntityId==id)){Reply(peer,"该实体已由其他玩家操控",false);return false;}
            if(previous!=id)EcologyIdentity.Find(previous)?.GetComponent<EntityManualControl>()?.Release();
            if(peer==null)OwnedEntityId=id;
            else
            {
                peer.player.entity=id;peer.rts=false;peer.inputTime=Time.unscaledTime;
                var manual=entity.GetComponent<EntityManualControl>()??entity.gameObject.AddComponent<EntityManualControl>();manual.BeginControl(EcologyQueries.Planet.transform);
                SendMessage(peer.connection,new LanMessage{type="selected",entity=id});
            }
            Debug.Log("[联机] 分配实体 player="+(peer?.player.id??"host")+"，entity="+id);Roster();return true;
        }
        private void ProcessCommand(Peer peer,LanCommand command)
        {
            if(command==null)return;
            if(peer!=null)
            {
                if(Time.unscaledTime-peer.window>=1){peer.window=Time.unscaledTime;peer.commands=0;}
                if(++peer.commands>LanSettings.Current.maximumCommandsPerSecond){Reply(peer,"指令过于频繁",false);return;}
            }
            if(command.kind=="choose"){Assign(peer,command.entity);return;}
            if(command.kind=="rts"){ApplyRts(peer,command);return;}
            if(command.kind=="mode")
            {if(peer!=null){peer.rts=command.rts;var entity=EcologyIdentity.Find(peer.player.entity);if(command.rts)entity?.GetComponent<EntityManualControl>()?.Release();else if(entity!=null)(entity.GetComponent<EntityManualControl>()??entity.gameObject.AddComponent<EntityManualControl>()).BeginControl(EcologyQueries.Planet.transform);}return;}
            if(command.kind=="input"||command.kind=="release")
            {
                if(peer==null)return;var entity=EcologyIdentity.Find(peer.player.entity);
                if(!EcologyQueries.Alive(entity)||peer.rts||!Finite(command.direction)||!Finite(command.aim)){Reply(peer,"无有效操控目标",false);return;}
                if(command.kind=="release"){entity.GetComponent<EnergyBlockMetabolism>()?.TryRelease(command.aim,out _);return;}
                peer.inputTime=Time.unscaledTime;var manual=entity.GetComponent<EntityManualControl>();if(manual==null)return;
                manual.SetInput(Vector3.ClampMagnitude(command.direction,1),command.attack);if(command.attack)manual.QueueAttack();
                var aim=command.aim.sqrMagnitude>.1f?command.aim.normalized:entity.transform.forward;manual.SetAim(new Ray(entity.transform.position+EcologyQueries.Up(entity.transform.position)*.25f,aim));
                if(command.jump){var jump=entity.GetComponent<EntityJumpMotor>()??entity.gameObject.AddComponent<EntityJumpMotor>();jump.Bind(EcologyQueries.Planet.transform);jump.TryJump(manual.CanMove);}return;
            }
            // ★ 修复（防Bug回归）：作弊与成员身份只读服务端状态；客户端不能伪造管理员、修改自己权限或直接上传世界。
            if(peer!=null&&!Cheats){Reply(peer,"需要管理员权限",false);return;}
            bool applied=LanEditExecutor.Apply(command,this);Reply(peer,(applied?"操作已应用：":"操作被拒绝：")+command.kind,applied);
        }
        private bool ApplyRts(Peer peer,LanCommand command)
        {
            if(command.selection==null||command.selection.Length==0||command.selection.Length>LanSettings.Current.maximumRtsSelection||!Finite(command.point)||!Enum.IsDefined(typeof(RtsOrderKind),command.order)){Reply(peer,"RTS指令无效",false);return false;}
            var entities=command.selection.Distinct().Select(EcologyIdentity.Find).ToArray();string owner=peer?.player.entity??OwnedEntityId;
            if(entities.Any(e=>!EcologyQueries.Alive(e)||(peer!=null&&!Cheats&&!QueenControls(owner,e))||_peers.Values.Any(p=>p!=peer&&p.player.entity==EcologyIdentity.Of(e)))){Reply(peer,"不能指挥此实体或外群",false);return false;}
            var target=EcologyIdentity.Find(command.target);var resource=ColonyResource.Find(command.resource);int applied=0;
            foreach(var entity in entities)
            {
                var point=command.point;
                if(command.order==RtsOrderKind.Move&&entities.Length>1){var up=EcologyQueries.Up(point);var axis=Vector3.Cross(up,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.Cross(up,Vector3.right).normalized;point=EcologyQueries.Surface(point+Quaternion.AngleAxis(applied*137.5f,up)*axis*LanSettings.Current.rtsFormationSpacing*Mathf.Sqrt(applied));}
                var order=RtsOrderController.Ensure(entity);order.arrivalDistance=LanSettings.Current.rtsArrivalDistance;if(order.Issue(command.order,point,target,resource))applied++;
            }
            Reply(peer,"已向 "+applied+" 个实体下达指令",applied>0);return applied>0;
        }
        public static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        public static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private void Reply(Peer peer,string text,bool success)
        {Status=text;if(!success)RejectedCommands++;if(success)Debug.Log("[联机] player="+(peer?.player.id??"host")+"，"+text);else Debug.LogWarning("[联机] 拒绝 player="+(peer?.player.id??"host")+"，"+text);if(peer!=null)SendMessage(peer.connection,new LanMessage{type="result",text=text});}
        private void Roster()
        {
            _players.Clear();_players.Add(new LanPlayer{id="host",name="管理员",administrator=true,entity=OwnedEntityId});_players.AddRange(_peers.Values.Select(p=>p.player));
            var message=new LanMessage{type="roster",players=_players,cheats=Cheats};foreach(var peer in _peers.Values)SendMessage(peer.connection,message);
        }
        private void SendWorld(Peer peer)
        {peer.ready=false;SendMessage(peer.connection,new LanMessage{type="world",world=LanWorldSnapshot.Capture(_world)});}
        private void OnLoaded()
        {
            if(IsClient)
            {
                foreach(var entity in EntityRegistry.All)LanReplicaActor.Freeze(entity);_remoteLoading=false;_receivedSequence=-1;
                SendMessage(_server,new LanMessage{type="ready"});Status="已进入局域网，选择一个实体开始游玩";
                if(!string.IsNullOrEmpty(OwnedEntityId)&&EcologyIdentity.Find(OwnedEntityId) is DarwinEntity selected)EntityViewController.Instance.EnterView(selected);
            }
        }
        private void Update()
        {
            LanSettingsQueueLimit=LanSettings.Current.maximumQueuedPackets;
            while(_main.TryDequeue(out var action))action();
            while(_accepted.TryDequeue(out var connection))
            {
                if(!IsHost||_peers.Count>=LanSettings.Current.maximumMembers){connection.Dispose();continue;}
                var peer=new Peer{connection=connection,player=new LanPlayer{id=connection.Id,name="成员",administrator=false,entity=""},window=Time.unscaledTime};_peers.Add(connection.Id,peer);connection.Start();Debug.Log("[联机] 接受成员连接 player="+connection.Id);
            }
            int processed=0;while(processed++<64&&_incoming.TryDequeue(out var packet))
            {
                try{var message=JsonUtility.FromJson<LanMessage>(packet.text);if(message==null||message.version!=1)throw new ArgumentException("联机版本无效");Handle(packet.connection,message);}
                catch(Exception ex){Status="网络消息被拒绝："+ex.Message;Debug.LogWarning("[局域网] "+Status);RejectedCommands++;}
            }
            while(_disconnected.TryDequeue(out var closed))
            {
                if(_peers.TryGetValue(closed.Id,out var peer)){EcologyIdentity.Find(peer.player.entity)?.GetComponent<EntityManualControl>()?.Release();_peers.Remove(closed.Id);Roster();Debug.Log("[联机] 成员离开 player="+closed.Id);}
                else if(closed==_server&&!_leaving){LeaveToMenu("管理员已关闭对局或连接中断");return;}
            }
            if(IsHost&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading&&!_world.Busy)
            {
                bool roster=false;foreach(var peer in _peers.Values)
                {
                    var entity=EcologyIdentity.Find(peer.player.entity);
                    if(!string.IsNullOrEmpty(peer.player.entity)&&!EcologyQueries.Alive(entity)){peer.player.entity="";peer.rts=false;SendMessage(peer.connection,new LanMessage{type="selected",entity=""});roster=true;}
                    if(entity!=null&&Time.unscaledTime-peer.inputTime>LanSettings.Current.inputTimeout){entity.GetComponent<EntityManualControl>()?.SetInput(Vector3.zero,false);entity.GetComponent<EntityManualControl>()?.ClearAttack();}
                }
                if(!string.IsNullOrEmpty(OwnedEntityId)&&!EcologyQueries.Alive(EcologyIdentity.Find(OwnedEntityId))){OwnedEntityId="";roster=true;}if(roster)Roster();
                if(Time.unscaledTime-_lastConfig>1){_lastConfig=Time.unscaledTime;string config=LanWorldSnapshot.Configuration();if(config!=_configuration){_configuration=config;foreach(var peer in _peers.Values)SendWorld(peer);}}
                if(_peers.Values.Any(p=>p.ready)&&Time.unscaledTime-_lastFrame>=LanSettings.Current.snapshotInterval){_lastFrame=Time.unscaledTime;bool ore=Time.unscaledTime-_lastOre>=LanSettings.Current.oreSnapshotInterval;if(ore)_lastOre=Time.unscaledTime;var json=JsonUtility.ToJson(new LanMessage{type="frame",frame=LanWorldSnapshot.Frame(++_sequence,ore)});foreach(var peer in _peers.Values)if(peer.ready)peer.connection.Send(json);}
            }
            if(IsClient&&WorldRuntimeGate.Ready&&!_remoteLoading&&!GameInputGate.Blocked&&!WorldAuthority.RtsMode&&EntityViewController.Instance?.Target is DarwinEntity local)
            {
                // ★ 修复（防Bug回归）：客户端发给房主的跳跃也必须使用改键后的输入。
                var manual=local.GetComponent<EntityManualControl>();_pendingAttack|=manual.ConsumeAttack()||manual.AttackHeld;_pendingJump|=ControlBindings.Down(ControlAction.Jump);
                if(Time.unscaledTime-_lastInput>=LanSettings.Current.inputInterval)
                {_lastInput=Time.unscaledTime;SendCommand(new LanCommand{kind="input",direction=manual.MoveDirection,aim=Camera.main.transform.forward,attack=_pendingAttack,jump=_pendingJump});_pendingJump=_pendingAttack=false;}
            }
        }
        private void Handle(LanConnection connection,LanMessage message)
        {
            if(IsHost&&_peers.TryGetValue(connection.Id,out var peer))
            {
                if(message.type=="join"&&!peer.joined){peer.joined=true;peer.player.name=string.IsNullOrWhiteSpace(message.text)?"成员":message.text.Substring(0,Mathf.Min(24,message.text.Length));SendMessage(connection,new LanMessage{type="welcome",id=connection.Id,cheats=Cheats});Roster();SendWorld(peer);}
                else if(message.type=="ready")peer.ready=true;
                else if(message.type=="command"&&WorldRuntimeGate.Ready){WorldAuthority.RemoteCommand=true;try{ProcessCommand(peer,message.command);}finally{WorldAuthority.RemoteCommand=false;}}
                else Reply(peer,"不允许客户端上传此类状态",false);return;
            }
            if(connection!=_server)return;
            if(message.type=="welcome"){LocalId=message.id;Cheats=message.cheats;PublishAuthority();Status="正在加载管理员的世界…";Debug.Log("[联机] 成员身份确认 player="+LocalId+"，作弊="+Cheats);}
            else if(message.type=="roster"){_players.Clear();if(message.players!=null)_players.AddRange(message.players);Cheats=message.cheats;PublishAuthority();}
            else if(message.type=="world")
            {_remoteLoading=true;_receivedSequence=-1;EntityViewController.Instance?.ExitView();_world.Open(message.world);}
            else if(message.type=="selected")
            {OwnedEntityId=message.entity??"";WorldAuthority.RtsMode=false;if(string.IsNullOrEmpty(OwnedEntityId))EntityViewController.Instance?.ExitView();else if(EcologyIdentity.Find(OwnedEntityId) is DarwinEntity entity){if(FindFirstObjectByType<GameMainMenu>()?.Visible!=true)GameInputGate.Blocked=false;EntityViewController.Instance.EnterView(entity);}}
            else if(message.type=="frame"&&!_remoteLoading&&WorldRuntimeGate.Ready)ApplyFrame(message.frame);
            else if(message.type=="result")Status=message.text;
        }
        private void ApplyFrame(LanFrame frame)
        {
            if(frame==null||frame.sequence<=_receivedSequence)return;_receivedSequence=frame.sequence;
            // ★ 适配：轻量设置随常规帧同步，不因更改上限重新加载整个客户端世界。
            if(frame.gameSettings!=null&&frame.gameSettings.version==1){EntityPopulationLimit.UseSnapshot(frame.gameSettings);if(_world.Current!=null)_world.Current.gameSettings=EntityPopulationLimit.Capture();}
            // ★ 适配：服务端档案只在低频帧出现，空字段不能清空已有历史与颜色。
            if(frame.population!=null)PopulationArchive.Restore(frame.population);
            ServiceLocator.Get<SimulationClock>().ApplyRemoteState(frame.time,frame.scale,frame.running);FindFirstObjectByType<SunSystem>().OrbitAngle=frame.sun;EcologyQueries.Planet.transform.rotation=frame.rotation;
            var ids=new HashSet<string>();foreach(var incoming in frame.actors)
            {
                var state=incoming.state;string id=state.ecology.id;ids.Add(id);var entity=EcologyIdentity.Find(id);
                if(entity==null){var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(state.genome,state.position,state.name);entity=EntityRegistry.All.Single(e=>!before.Contains(e));state.Restore(entity);}
                (entity.GetComponent<LanReplicaActor>()??entity.gameObject.AddComponent<LanReplicaActor>()).Apply(incoming);
            }
            foreach(var entity in EntityRegistry.All.ToArray())if(!ids.Contains(EcologyIdentity.Of(entity))){entity.gameObject.SetActive(false);Destroy(entity.gameObject);}
            var resources=new HashSet<string>();foreach(var saved in frame.resources)
            {
                resources.Add(saved.id);var resource=ColonyResource.Find(saved.id);if(resource==null)ColonyResource.Create(saved);else{resource.State.energy=saved.energy;resource.State.group=saved.group;resource.State.claim=saved.claim;resource.CarryTo(EcologyQueries.ToWorld(saved.local));}
            }
            foreach(var resource in ColonyResource.All.ToArray())if(!resources.Contains(resource.State.id))resource.Remove();
            RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject).ApplyRemoteProgress(frame.randomBusy,frame.randomSpawned,frame.randomRequested,frame.randomStatus);
            // ★ 修复（防Bug回归）：Unity JSON会把未发送的null数组还原为空数组，不能据此创建0×0矿图纹理。
            if(frame.oreResolution>0&&frame.ore!=null&&frame.ore.Length==(long)frame.oreResolution*frame.oreResolution){PlanetOreSystem.Instance.RestoreAmounts(frame.ore,frame.oreResolution);PlanetOreSystem.Instance.RegenTimer=frame.oreTimer;}
        }
        public void CloseHost()
        {
            if(!IsHost)return;foreach(var peer in _peers.Values){EcologyIdentity.Find(peer.player.entity)?.GetComponent<EntityManualControl>()?.Release();peer.connection.Dispose();}_peers.Clear();_listener.Dispose();_listener=null;_players.Clear();Cheats=false;OwnedEntityId="";WorldAuthority.Reset();Status="局域网已关闭，继续单人游戏";Debug.Log("[联机] "+Status);
        }
        public void LeaveToMenu(string reason="已离开局域网")
        {
            _leaving=true;bool replica=IsClient;CancelConnecting();_server?.Dispose();_server=null;
            if(IsHost)CloseHost();
            if(replica){WorldAuthority.IsReplica=true;_world.ReturnToMenu(false);_remoteLoading=false;}
            OwnedEntityId="";_players.Clear();Cheats=false;WorldAuthority.Reset();Status=reason;Debug.Log("[联机] "+reason);FindFirstObjectByType<GameMainMenu>()?.ShowNetworkMain(reason);_leaving=false;
        }
        private void CancelConnecting(){Interlocked.Increment(ref _joinVersion);_connecting=false;_joiningClient?.Close();_joiningClient=null;}
        private void OnApplicationQuit(){_quitting=true;CancelConnecting();_server?.Dispose();_listener?.Dispose();foreach(var peer in _peers.Values)peer.connection.Dispose();}
        private void OnDestroy(){CancelConnecting();_world.Loaded-=OnLoaded;_server?.Dispose();_listener?.Dispose();foreach(var peer in _peers.Values)peer.connection.Dispose();if(Instance==this)Instance=null;if(!_quitting)WorldAuthority.Reset();}
    }
}
