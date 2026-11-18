using System;
using System.Collections;
using System.IO;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.UI
{
    // ✦ 新增：独立开服入口。二进制和数据目录分离，更新前备份，失败不另建世界覆盖旧档。
    [DefaultExecutionOrder(-2000)]
    public sealed class DedicatedServerBootstrap : MonoBehaviour
    {
        [Serializable] public sealed class Settings
        {
            public int version=1,port=47777,seed=78031,tickRate=30;
            public float autoSaveSeconds=120,startupTimeout=300,land=.5f,temperature=20,humidity=44;
            public string worldId="",worldName="Darwin Paradise Server";
            public bool cheats=false;
            public void Validate()
            {
                if(version!=1||port<1||port>65535||tickRate<1||tickRate>240||!Positive(autoSaveSeconds)||!Positive(startupTimeout)||!Finite(land)||land<=0||land>=1||!Finite(temperature)||!Finite(humidity)||humidity<0||humidity>100||string.IsNullOrWhiteSpace(worldName)||!string.IsNullOrEmpty(worldId)&&!Guid.TryParseExact(worldId,"N",out _))
                    throw new InvalidDataException("Invalid server.json configuration");
            }
            private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
            private static bool Positive(float n)=>Finite(n)&&n>0;
        }
        [Serializable] private sealed class WorldPointer {public string worldId;}
        private Settings _settings;private WorldSessionManager _world;private FileStream _lease;
        private DedicatedServerHealth _health;
        private bool _started,_stopping;private double _since;
        private string Pointer=>Path.Combine(GamePaths.Root,"server-world.json");
        private string StopRequest=>Path.Combine(GamePaths.Root,"stop.request");
        private void Awake()
        {
            WorldRuntimeGate.Managed=true;WorldRuntimeGate.Ready=false;GameInputGate.Blocked=false;
            Application.runInBackground=true;
        }
        private IEnumerator Start()
        {
            if(!DedicatedServerMode.Enabled){Debug.LogError("Dedicated server entry requires server build");enabled=false;yield break;}
            // 等待场景中的服务注册，不依赖菜单 Awake 或屏幕渲染。
            yield return null;
            try
            {
                Directory.CreateDirectory(GamePaths.Root);
                _lease=new FileStream(Path.Combine(GamePaths.Root,"server.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
                // 🔧 修复原因：停服到备份期间拒绝外部手工开服，防止新进程与离线备份同时写数据。
                string guard=Path.Combine(GamePaths.Root,"update.guard");
                if(File.Exists(guard)&&File.ReadAllText(guard)!=DedicatedServerMode.Argument("-server-update-id"))
                    throw new InvalidOperationException("Server data is reserved by an update transaction");
                // ✅ 新增：取得数据租约后才发布启动状态，避免第二个进程覆盖在线服的健康凭证。
                _health=new DedicatedServerHealth();_health.Write("starting");
                string config=DedicatedServerMode.Argument("-server-config")??Path.Combine(GamePaths.Root,"server.json");
                _settings=JsonUtility.FromJson<Settings>(File.ReadAllText(config));if(_settings==null)throw new InvalidDataException("Empty server configuration");_settings.Validate();
                LanRelease.Current.Validate();QualitySettings.vSyncCount=0;Application.targetFrameRate=_settings.tickRate;
                _world=FindFirstObjectByType<WorldSessionManager>();if(_world==null)throw new InvalidOperationException("Missing world session");
                _world.autoSaveInterval=_settings.autoSaveSeconds;_world.Failed+=Fail;_world.Loaded+=Loaded;
                LanSession.Ensure(_world.gameObject);_since=Time.realtimeSinceStartupAsDouble;
                if(File.Exists(StopRequest))throw new InvalidOperationException("Remove stale stop.request before starting server");
                string id=_settings.worldId;
                if(string.IsNullOrEmpty(id)&&File.Exists(Pointer))
                {
                    var pointer=JsonUtility.FromJson<WorldPointer>(File.ReadAllText(Pointer));
                    id=pointer?.worldId;if(string.IsNullOrEmpty(id))throw new InvalidDataException("Invalid server-world.json; refusing to create replacement world");
                }
                if(!string.IsNullOrEmpty(id))
                {
                    string path=WorldSaveStore.FileFor(id);var data=WorldSaveStore.Read(path);
                    // 复制整个世界目录，防止更新后 .bak 被自动保存轮换覆盖。
                    string backup=Path.Combine(GamePaths.Root,"backups",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff")+"-"+id);
                    CopyDirectory(Path.GetDirectoryName(path),backup);
                    _world.Open(data);
                }
                else _world.Create(_settings.worldName,_settings.seed,_settings.land,_settings.temperature,_settings.humidity);
            }
            catch(Exception ex){Fail(ex.ToString());}
        }
        private static void CopyDirectory(string source,string target)
        {
            Directory.CreateDirectory(target);
            foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),false);
            foreach(string directory in Directory.GetDirectories(source))CopyDirectory(directory,Path.Combine(target,Path.GetFileName(directory)));
        }
        private void Loaded()
        {
            if(_started)return;
            try
            {
                string temp=Pointer+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(new WorldPointer{worldId=_world.Current.id}));
                if(File.Exists(Pointer))File.Replace(temp,Pointer,Pointer+".bak");else File.Move(temp,Pointer);
                if(!LanSession.Instance.StartHost(_settings.port,_settings.cheats))throw new InvalidOperationException(LanSession.Instance.Status);
                var clock=ServiceLocator.Get<SimulationClock>();if(_world.Current.running)clock.Play();else clock.Pause();
                _health.Write("ready",LanSession.Instance.Port,_world.Current.id);
                _started=true;Debug.Log("[Server] READY port="+LanSession.Instance.Port+" world="+_world.Current.id+" release="+LanRelease.Current.release);
            }
            catch(Exception ex){Fail(ex.ToString());}
        }
        private void Update()
        {
            if(_settings==null||_stopping)return;
            if(!_started){if(Time.realtimeSinceStartupAsDouble-_since>_settings.startupTimeout)Fail("World startup timed out");return;}
            if(File.Exists(StopRequest)&&!_world.Busy)
            {
                // 🔧 修复原因：退出前写保存成功凭证，更新器必须同时等到数据租约释放才能备份。
                try{_world.SaveNow();LanSession.Instance.CloseHost();File.Delete(StopRequest);_health.Write("stopped");_stopping=true;Application.Quit(0);}
                catch(Exception ex){Debug.LogError("[Server] Stop/save failed: "+ex);_stopping=true;}
            }
        }
        private void Fail(string error){_stopping=true;Debug.LogError("[Server] STARTUP FAILED: "+error);Application.Quit(1);}
        private void OnDestroy(){if(_world!=null){_world.Failed-=Fail;_world.Loaded-=Loaded;}_lease?.Dispose();}
    }
}
