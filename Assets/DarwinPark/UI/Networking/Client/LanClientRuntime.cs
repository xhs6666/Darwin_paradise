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
using static UnityEngine.Object;

namespace DarwinPark.UI
{
    // ✦ 新增：客户端副本与输入适配器，只应用服务端结果和发送意图。
    internal sealed class LanClientRuntime:ILanClientRuntime
    {
        private readonly LanSession session;
        public string DisconnectReason {get;private set;}
        private bool _remoteLoading,_welcomed;private long _receivedSequence=-1;
        private float _lastInput;private bool _pendingJump,_pendingAttack;
        private long _loadingRevision;private LanMessage _pendingWorld;
        private WorldSessionManager _world=>session._world;
        private LanConnection _server=>session._server;
        private bool IsClient=>session.IsClient;
        private List<LanPlayer> _players=>session._players;
        private string OwnedEntityId {get=>session.OwnedEntityId;set=>session.OwnedEntityId=value;}
        private string LocalId {get=>session.LocalId;set=>session.LocalId=value;}
        private string Status {get=>session.Status;set=>session.Status=value;}
        private bool Cheats {get=>session.Cheats;set=>session.Cheats=value;}
        private void PublishAuthority()=>session.PublishAuthority();
        private void SendCommand(LanCommand command)=>session.SendCommand(command);
        public LanClientRuntime(LanSession owner){session=owner;}
        public void Reset(){_welcomed=false;_remoteLoading=false;_receivedSequence=-1;_pendingJump=_pendingAttack=false;DisconnectReason=null;_loadingRevision=0;_pendingWorld=null;}
        public void OnLoaded()
        {
            if(IsClient)
            {
                if(_pendingWorld!=null){var latest=_pendingWorld;_pendingWorld=null;OpenWorld(latest);return;}
                foreach(var entity in EntityRegistry.All)LanReplicaActor.Freeze(entity);_remoteLoading=false;_receivedSequence=-1;
                SendMessage(_server,new LanMessage{type="ready",worldRevision=_loadingRevision});Status="已进入局域网，选择一个实体开始游玩";
                if(!string.IsNullOrEmpty(OwnedEntityId)&&EcologyIdentity.Find(OwnedEntityId) is DarwinEntity selected)EntityViewController.Instance.EnterView(selected);
            }
        }
        private void ApplyFrame(LanFrame frame)
        {
            if(frame==null||frame.sequence<=_receivedSequence)return;_receivedSequence=frame.sequence;
            // ★ 适配：轻量设置随常规帧同步，不因更改上限重新加载整个客户端世界。
            if(frame.gameSettings!=null&&frame.gameSettings.version==1){EntityPopulationLimit.UseSnapshot(frame.gameSettings);if(_world.Current!=null)_world.Current.gameSettings=EntityPopulationLimit.Capture();}
            // ★ 适配：服务端档案只在低频帧出现，空字段不能清空已有历史与颜色。
            // ★ 适配：配置与现有低频档案一起恢复；副本只显示，建群保障仍由房主执行。
            if(!string.IsNullOrWhiteSpace(frame.ecologyDefaults))EcologyDefaults.UseSnapshot(frame.ecologyDefaults);
            if(frame.population!=null)PopulationArchive.Restore(frame.population);
            ServiceLocator.Get<SimulationClock>().ApplyRemoteState(frame.time,frame.scale,frame.running);FindFirstObjectByType<SunSystem>().OrbitAngle=frame.sun;EcologyQueries.Planet.transform.rotation=frame.rotation;
            var ids=new HashSet<string>();foreach(var incoming in frame.actors)
            {
                var state=incoming.state;string id=state.ecology.id;ids.Add(id);var entity=EcologyIdentity.Find(id);
                if(entity==null){entity=EntitySpawnCapture.Create(state.genome,state.position,state.name);if(entity==null)throw new InvalidOperationException("Replica entity creation failed");state.Restore(entity);}
                (entity.GetComponent<LanReplicaActor>()??entity.gameObject.AddComponent<LanReplicaActor>()).Apply(incoming);
            }
            foreach(var entity in EntityRegistry.All.ToArray())if(!ids.Contains(EcologyIdentity.Of(entity))){entity.gameObject.SetActive(false);Destroy(entity.gameObject);}
            var resources=new HashSet<string>();foreach(var saved in frame.resources)
            {
                resources.Add(saved.id);var resource=ColonyResource.Find(saved.id);if(resource==null)ColonyResource.Create(saved);else{resource.State.energy=saved.energy;resource.State.group=saved.group;resource.State.claim=saved.claim;resource.CarryTo(EcologyQueries.ToWorld(saved.local));}
            }
            foreach(var resource in ColonyResource.All.ToArray())if(!resources.Contains(resource.State.id))resource.Remove();
            RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject).ApplyRemoteProgress(frame.randomBusy,frame.randomSpawned,frame.randomRequested,frame.randomStatus);
            // ★ 适配：副本只接收规则供界面展示，自动补充和繁殖调节仍只由房主运行。
            if(frame.maintenance!=null)PopulationMaintenance.Restore(frame.maintenance);
            // ★ 修复（防Bug回归）：Unity JSON会把未发送的null数组还原为空数组，不能据此创建0×0矿图纹理。
            if(frame.oreResolution>0&&frame.ore!=null&&frame.ore.Length==(long)frame.oreResolution*frame.oreResolution){PlanetOreSystem.Instance.RestoreAmounts(frame.ore,frame.oreResolution);PlanetOreSystem.Instance.RegenTimer=frame.oreTimer;}
        }
        public void Tick()
        {
            if(IsClient&&WorldRuntimeGate.Ready&&!_remoteLoading&&!GameInputGate.Blocked&&!WorldAuthority.RtsMode&&EntityViewController.Instance?.Target is DarwinEntity local)
            {
                // ★ 修复（防Bug回归）：客户端发给房主的跳跃也必须使用改键后的输入。
                var manual=local.GetComponent<EntityManualControl>();_pendingAttack|=manual.ConsumeAttack()||manual.AttackHeld;_pendingJump|=ControlBindings.Down(ControlAction.Jump);
                if(Time.unscaledTime-_lastInput>=LanSettings.Current.inputInterval)
                {_lastInput=Time.unscaledTime;SendCommand(new LanCommand{kind="input",direction=manual.MoveDirection,aim=Camera.main.transform.forward,attack=_pendingAttack,jump=_pendingJump});_pendingJump=_pendingAttack=false;}
            }
        }
        public void Handle(LanConnection connection,LanMessage message)
        {
            if(connection!=_server)return;
            if(message.type=="reject"){DisconnectReason=message.text;connection.Dispose();return;}
            if(message.version!=ServerCompatibility.WireVersion){DisconnectReason="服务端通信协议不兼容";connection.Dispose();return;}
            if(message.type=="welcome")
            {
                string reason=LanRelease.Current.Reject(message.compatibility);
                if(reason!=null){DisconnectReason=reason;connection.Dispose();return;}
                _welcomed=true;LocalId=message.id;Cheats=message.cheats;PublishAuthority();Status="正在加载服务端世界…";
            }
            else if(!_welcomed){DisconnectReason="服务端尚未完成版本握手";connection.Dispose();return;}
            else if(message.type=="roster"){_players.Clear();if(message.players!=null)_players.AddRange(message.players);Cheats=message.cheats;PublishAuthority();}
            else if(message.type=="world")
            {if(message.worldRevision<=_loadingRevision)return;_remoteLoading=true;if(_world.Busy)_pendingWorld=message;else OpenWorld(message);}
            else if(message.type=="selected")
            {OwnedEntityId=message.entity??"";WorldAuthority.RtsMode=false;if(string.IsNullOrEmpty(OwnedEntityId))EntityViewController.Instance?.ExitView();else if(EcologyIdentity.Find(OwnedEntityId) is DarwinEntity entity){if(FindFirstObjectByType<GameMainMenu>()?.Visible!=true)GameInputGate.Blocked=false;EntityViewController.Instance.EnterView(entity);}}
            else if(message.type=="frame"&&!_remoteLoading&&WorldRuntimeGate.Ready)ApplyFrame(message.frame);
            else if(message.type=="result")Status=message.text;
        }
        private void OpenWorld(LanMessage message)
        {
            if(message.world==null)throw new ArgumentException("Missing world snapshot");
            LanRelease.Current.RequireSave(message.world.version);_loadingRevision=message.worldRevision;
            _remoteLoading=true;_receivedSequence=-1;EntityViewController.Instance?.ExitView();_world.Open(message.world);
        }
    }
}
