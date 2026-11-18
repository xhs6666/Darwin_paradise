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

using static DarwinPark.UI.LanSession;

namespace DarwinPark.UI
{
    // ✦ 新增：Unity 权威适配器；禁止读取本地鼠标、键盘、相机或操作界面。
    internal sealed class LanServerRuntime : ILanServerRuntime
    {
        internal sealed class Peer
        {public LanConnection connection;public LanPlayer player;public ServerAdmission admission;public bool rts;public float inputTime,window;public int commands;}
        private readonly LanSession session;
        public LanListener Listener {get;private set;}
        public bool Listening=>Listener!=null;
        public int Port=>Listener?.Port??0;
        public int MemberCount=>_peers.Count;
        private bool IsHost=>Listening;
        private bool Online=>session.Online;
        private WorldSessionManager _world=>session._world;
        private List<LanPlayer> _players=>session._players;
        private bool Cheats {get=>session.Cheats;set=>session.Cheats=value;}
        private string LocalId {get=>session.LocalId;set=>session.LocalId=value;}
        private string OwnedEntityId {get=>session.OwnedEntityId;set=>session.OwnedEntityId=value;}
        private string Status {get=>session.Status;set=>session.Status=value;}
        private int RejectedCommands {get=>session.RejectedCommands;set=>session.RejectedCommands=value;}
        private void PublishAuthority()=>session.PublishAuthority();
        private readonly Dictionary<string,Peer> _peers=new Dictionary<string,Peer>();
        private float _lastFrame,_lastConfig,_lastOre;private long _sequence;private string _configuration;
        public LanServerRuntime(LanSession owner){session=owner;}
        public bool AssignLocal(string id)=>Assign(null,id);
        public void ProcessLocalCommand(LanCommand command)=>ProcessCommand(null,command);
        public bool ApplyLocalRts(LanCommand command)=>ApplyRts(null,command);
        public bool StartHost(int port=-1,bool cheats=false)
        {
            if(Online||!WorldRuntimeGate.Ready||_world.Current==null){Status="请先进入一个单人世界";return false;}
            try
            {
                LanRelease.Current.RequireWorld(_world.Current.version,_world.Current.contentEpoch);
                var settings=LanSettings.Current;Listener=new LanListener(port<0?settings.port:port,client=>
                {
                    var connection=new LanConnection(client,settings.maximumPacketBytes,settings.maximumQueuedPackets,session.ReceivePacket,session.ReceiveClosed);session.Accept(connection);
                });Cheats=cheats;LocalId="host";_configuration=LanWorldSnapshot.Configuration();Status="已对局域网开放，端口 "+Port;PublishAuthority();Roster();Debug.Log("[联机] 房主开放端口="+Port+"，作弊="+Cheats);return true;
            }
            catch(Exception ex)when(ex is SocketException||ex is InvalidOperationException){Listener?.Dispose();Listener=null;Status="开放失败："+ex.Message;Debug.LogWarning("[联机] "+Status);return false;}
        }
        public void SetCheats(bool enabled)
        {if(!IsHost)return;Cheats=enabled;PublishAuthority();Roster();Status=enabled?"作弊模式已开启":"作弊模式已关闭";Debug.Log("[联机] "+Status);}
        public bool Assign(Peer peer,string id)
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
        public void ProcessCommand(Peer peer,LanCommand command)
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
            bool applied=LanEditExecutor.Apply(command,session);Reply(peer,(applied?"操作已应用：":"操作被拒绝：")+command.kind,applied);
        }
        public bool ApplyRts(Peer peer,LanCommand command)
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
        private void Reply(Peer peer,string text,bool success)
        {Status=text;if(!success)RejectedCommands++;if(success)Debug.Log("[联机] player="+(peer?.player.id??"host")+"，"+text);else Debug.LogWarning("[联机] 拒绝 player="+(peer?.player.id??"host")+"，"+text);if(peer!=null)SendMessage(peer.connection,new LanMessage{type="result",text=text});}
        private void Roster()
        {
            _players.Clear();_players.Add(new LanPlayer{id="host",name="管理员",administrator=true,entity=OwnedEntityId});_players.AddRange(_peers.Values.Where(p=>p.admission.Joined).Select(p=>p.player));
            var message=new LanMessage{type="roster",players=_players,cheats=Cheats};foreach(var peer in _peers.Values)if(peer.admission.Joined)SendMessage(peer.connection,message);
        }
        private void SendWorld(Peer peer)
        {peer.admission.Loading(Time.unscaledTimeAsDouble);SendMessage(peer.connection,new LanMessage{type="world",world=LanWorldSnapshot.Capture(_world),worldRevision=peer.admission.WorldRevision});}
        public void CloseHost()
        {
            if(!IsHost)return;foreach(var peer in _peers.Values){EcologyIdentity.Find(peer.player.entity)?.GetComponent<EntityManualControl>()?.Release();peer.connection.Dispose();}_peers.Clear();Listener.Dispose();Listener=null;_players.Clear();Cheats=false;OwnedEntityId="";WorldAuthority.Reset();Status="局域网已关闭，继续单人游戏";Debug.Log("[联机] "+Status);
        }
        public void Tick()
        {
            foreach(var peer in _peers.Values)
                if(peer.admission.Expired(Time.unscaledTimeAsDouble,LanSettings.Current.connectionTimeout,LanSettings.Current.worldLoadTimeout))peer.connection.Dispose();
            if(IsHost&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading&&!_world.Busy)
            {
                bool roster=false;foreach(var peer in _peers.Values)
                {
                    var entity=EcologyIdentity.Find(peer.player.entity);
                    if(!string.IsNullOrEmpty(peer.player.entity)&&!EcologyQueries.Alive(entity)){peer.player.entity="";peer.rts=false;SendMessage(peer.connection,new LanMessage{type="selected",entity=""});roster=true;}
                    if(entity!=null&&Time.unscaledTime-peer.inputTime>LanSettings.Current.inputTimeout){entity.GetComponent<EntityManualControl>()?.SetInput(Vector3.zero,false);entity.GetComponent<EntityManualControl>()?.ClearAttack();}
                }
                if(!string.IsNullOrEmpty(OwnedEntityId)&&!EcologyQueries.Alive(EcologyIdentity.Find(OwnedEntityId))){OwnedEntityId="";roster=true;}if(roster)Roster();
                if(Time.unscaledTime-_lastConfig>1){_lastConfig=Time.unscaledTime;string config=LanWorldSnapshot.Configuration();if(config!=_configuration){_configuration=config;foreach(var peer in _peers.Values)if(peer.admission.Joined)SendWorld(peer);}}
                if(_peers.Values.Any(p=>p.admission.Ready)&&Time.unscaledTime-_lastFrame>=LanSettings.Current.snapshotInterval){_lastFrame=Time.unscaledTime;bool ore=Time.unscaledTime-_lastOre>=LanSettings.Current.oreSnapshotInterval;if(ore)_lastOre=Time.unscaledTime;var json=JsonUtility.ToJson(new LanMessage{type="frame",frame=LanWorldSnapshot.Frame(++_sequence,ore)});foreach(var peer in _peers.Values)if(peer.admission.Ready)peer.connection.Send(json);}
            }
        }
        public bool Handle(LanConnection connection,LanMessage message)
        {
            if(IsHost&&_peers.TryGetValue(connection.Id,out var peer))
            {
                if(peer.admission.Rejected)return true;
                if(message.version!=ServerCompatibility.WireVersion){Reject(peer,"通信协议不兼容，请更新客户端");return true;}
                if(message.type=="join")
                {
                    string reason=peer.admission.Join(LanRelease.Current,message.compatibility,Time.unscaledTimeAsDouble);
                    if(reason!=null){Reject(peer,reason);return true;}
                    if(_world.Current.version<message.compatibility.minimumSave||_world.Current.version>message.compatibility.maximumSave){Reject(peer,"客户端不支持当前世界存档格式");return true;}
                    peer.player.name=string.IsNullOrWhiteSpace(message.text)?"成员":message.text.Substring(0,Mathf.Min(24,message.text.Length));
                    SendMessage(connection,new LanMessage{type="welcome",id=connection.Id,cheats=Cheats,compatibility=LanRelease.Current});Roster();SendWorld(peer);
                }
                else if(message.type=="ready"&&peer.admission.AcknowledgeWorld(message.worldRevision)){}
                else if(message.type=="command"&&peer.admission.CanCommand&&WorldRuntimeGate.Ready){WorldAuthority.RemoteCommand=true;try{ProcessCommand(peer,message.command);}finally{WorldAuthority.RemoteCommand=false;}}
                else Reply(peer,"不允许客户端上传此类状态",false);return true;
            }
            return false;
        }
        public void Accept(LanConnection connection)
        {
            if(!Listening||_peers.Count>=LanSettings.Current.maximumMembers){connection.Dispose();return;}
            var peer=new Peer{connection=connection,player=new LanPlayer{id=connection.Id,name="成员",entity=""},window=Time.unscaledTime,admission=new ServerAdmission(Time.unscaledTimeAsDouble)};
            _peers.Add(connection.Id,peer);connection.Start();
        }
        public bool Disconnect(LanConnection connection)
        {
            if(!_peers.TryGetValue(connection.Id,out var peer))return false;
            EcologyIdentity.Find(peer.player.entity)?.GetComponent<EntityManualControl>()?.Release();
            _peers.Remove(connection.Id);if(Listening)Roster();return true;
        }
        public void Dispose(){foreach(var peer in _peers.Values)peer.connection.Dispose();_peers.Clear();Listener?.Dispose();Listener=null;}
        private void Reject(Peer peer,string reason)
        {
            peer.admission.Block();RejectedCommands++;peer.connection.SendAndClose(JsonUtility.ToJson(new LanMessage{type="reject",text=reason,compatibility=LanRelease.Current}));
        }
    }
}
