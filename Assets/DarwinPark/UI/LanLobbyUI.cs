using System.Linq;
using System.Net;
using System.Net.Sockets;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：同一游戏提供房主与客户端入口；成员先选实体，世界编辑控件由权限入口隐藏，不以隐藏代替服务端验证。
    public sealed class LanLobbyUI : MonoBehaviour
    {
        public string Address="127.0.0.1",PlayerName="成员";
        private string _port,_addresses;private bool _options,_cheats;private Vector2 _scroll;
        private string PortText {get=>_port??(_port=LanSettings.Current.port.ToString());set=>_port=value;}
        public void DrawJoin()
        {
            var session=LanSession.Instance;GUILayout.Label("管理员在世界中按 Esc，选择对局域网开放。",RuntimeMenuTheme.Muted);
            GUILayout.Label("管理员的局域网 IP");Address=GUILayout.TextField(Address,128);GUILayout.Label("端口");PortText=GUILayout.TextField(PortText,5);GUILayout.Label("玩家名称");PlayerName=GUILayout.TextField(PlayerName,24);
            GUI.enabled=!session.Online;if(RuntimeMenuTheme.Button("加入游戏","network",true,46)&&int.TryParse(PortText,out int port))session.Join(Address,port,PlayerName);GUI.enabled=true;
            GUILayout.Label(session.Status,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
        }
        public void DrawPause()
        {
            var session=LanSession.Instance;
            if(!session.Online)
            {
                if(RuntimeMenuTheme.Button("对局域网开放","network",false,42))_options=!_options;
                if(_options)
                {
                    GUILayout.BeginHorizontal();GUILayout.Label("端口",GUILayout.Width(55));PortText=GUILayout.TextField(PortText,5);GUILayout.EndHorizontal();_cheats=GUILayout.Toggle(_cheats,"作弊模式：成员也可编辑与放置");
                    if(RuntimeMenuTheme.Button("开始开放",null,true,38)&&int.TryParse(PortText,out int port)){if(session.StartHost(port,_cheats)&&ServiceLocator.TryGet<SimulationClock>(out var clock))clock.Play();}
                }
            }
            else if(session.IsHost)
            {
                GUILayout.Label("管理员 · 成员 "+session.MemberCount+" · 端口 "+session.Port,RuntimeMenuTheme.Muted);
                if(_addresses==null)try{_addresses=string.Join(" / ",Dns.GetHostAddresses(Dns.GetHostName()).Where(a=>a.AddressFamily==AddressFamily.InterNetwork).Select(a=>a.ToString()));}catch(SocketException){_addresses="请查看系统网络设置";}
                GUILayout.Label("局域网 IP："+_addresses,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
                bool cheats=GUILayout.Toggle(session.Cheats,"作弊模式：所有成员拥有世界编辑权限");if(cheats!=session.Cheats)session.SetCheats(cheats);
                if(RuntimeMenuTheme.Button("关闭局域网",null,false,36))session.CloseHost();
            }
            else
            {GUILayout.Label(session.Cheats?"成员 · 作弊模式已开启":"成员 · 仅可操控选择的实体",RuntimeMenuTheme.Muted);if(RuntimeMenuTheme.Button("离开局域网",null,false,38))session.LeaveToMenu();}
            if(!string.IsNullOrEmpty(session.Status))GUILayout.Label(session.Status,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
        }
        private void Update()
        {
            var session=LanSession.Instance;if(session==null||!session.IsClient||!WorldRuntimeGate.Ready||FindFirstObjectByType<GameMainMenu>()?.Visible==true)return;
            if(string.IsNullOrEmpty(session.OwnedEntityId)){GameInputGate.Blocked=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        private void OnGUI()
        {
            var session=LanSession.Instance;if(session==null||!session.IsClient||!WorldRuntimeGate.Ready||!string.IsNullOrEmpty(session.OwnedEntityId)||FindFirstObjectByType<GameMainMenu>()?.Visible==true)return;
            GUI.depth=-800;RuntimeMenuTheme.Apply();float width=Mathf.Min(500,Screen.width-24),height=Mathf.Min(600,Screen.height-32);
            GUILayout.BeginArea(new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height),GUI.skin.box);GUILayout.Label("选择实体游玩",RuntimeMenuTheme.Title);GUILayout.Label("普通成员进入时选择一次，实体死亡后可重新选择。",RuntimeMenuTheme.Muted);
            _scroll=GUILayout.BeginScrollView(_scroll);
            foreach(var entity in EntityRegistry.All.Where(EcologyQueries.Alive).ToArray())
            {
                string id=EcologyIdentity.Of(entity);bool occupied=session.Players.Any(p=>p.entity==id&&p.id!=session.LocalId);var ant=entity.GetComponent<AntColonyMember>();
                GUI.enabled=!occupied;string role=ant!=null&&ant.Active&&ant.Role!=AntRole.None?" · "+AntColonySettings.Current.Rule(ant.Role).label:"";
                if(GUILayout.Button(entity.DisplayName+role+(occupied?"（已占用）":""),GUILayout.Height(38)))session.RequestEntity(id);GUI.enabled=true;
            }
            GUILayout.EndScrollView();GUILayout.Label(session.Status,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});GUILayout.EndArea();
        }
    }
}
