using System;
using System.IO;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：场景Awake之前接收Unity所有线程的输出；正常版和开发版均自动保存，不要求用户打开调试模式。
    public static class RuntimeFileLog
    {
        private static SessionLogWriter _writer;
        private static string _startupFailure;
        public static string DirectoryPath=>_writer?.DirectoryPath??"";
        public static string CurrentFilePath=>_writer?.CurrentFilePath??"";
        public static string Failure=>_writer?.Failure??_startupFailure;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()=>Shutdown("重置运行会话");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            try
            {
                var settings=RuntimeLogSettings.Load();
                StartSession(settings,Path.Combine(GamePaths.Root,settings.directoryName),Path.Combine(Application.persistentDataPath,settings.directoryName));
                if(_writer==null)return;
                // ★ 适配：普通日志之外单独保存异常和心跳，避免进程突然消失只剩Console输出。
                var activeWriter=_writer;RuntimeCrashDiagnostics.Start(()=>activeWriter.CurrentFilePath);
                Debug.Log("[日志] 文件："+CurrentFilePath);
                Debug.Log("[运行] 游戏="+Application.productName+"，版本="+Application.version+"，Unity="+Application.unityVersion+"，平台="+Application.platform+"，编辑器="+Application.isEditor);
                Debug.Log("[系统] "+SystemInfo.operatingSystem+"，CPU="+SystemInfo.processorType+"，内存MB="+SystemInfo.systemMemorySize+"，GPU="+SystemInfo.graphicsDeviceName+"，图形接口="+SystemInfo.graphicsDeviceType);
                Debug.Log("[路径] 世界存档="+GamePaths.Saves+"，自定义基因="+GamePaths.Mods);
                if(Application.isPlaying){var host=new GameObject("RuntimeFileLog");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<RuntimeLogLifecycle>().Bind(_writer);}
            }
            catch(Exception ex){_startupFailure=ex.ToString();Debug.LogError("[日志] 文件日志未能启用："+ex.Message);}
        }
        public static void StartSession(RuntimeLogSettings settings,string preferredDirectory,string fallbackDirectory)
        {
            Shutdown("切换日志会话");_startupFailure=null;if(!settings.enabled)return;
            _writer=new SessionLogWriter(settings,preferredDirectory,fallbackDirectory);
            Application.logMessageReceivedThreaded+=Receive;
            if(_writer.FallbackReason!=null)Debug.LogWarning("[日志] 游戏目录不可写，已保存到 "+DirectoryPath+"；原因："+_writer.FallbackReason);
            Debug.Log("[日志] 会话开始");
        }
        private static void Receive(string message,string stack,LogType type)
        {
            _writer?.Write(message,stack,type);
            RuntimeCrashDiagnostics.Receive(message,stack,type);
        }
        public static bool Flush()=>_writer?.Flush()??false;
        public static void Shutdown(string reason="正常关闭")
        {
            Application.logMessageReceivedThreaded-=Receive;
            var writer=_writer;_writer=null;if(writer==null){RuntimeCrashDiagnostics.Stop(reason);return;}
            writer.Write("[日志] 会话结束："+reason,"",LogType.Log);writer.Dispose();
            // ★ 适配：先排空普通日志，再关闭诊断心跳；正常关闭标记不能早于末尾输出。
            RuntimeCrashDiagnostics.Stop(reason);
        }
        internal static void ShutdownIfCurrent(SessionLogWriter writer){if(ReferenceEquals(_writer,writer))Shutdown();}
        public static void OpenDirectory()
        {
            if(string.IsNullOrEmpty(DirectoryPath))return;
            try{Application.OpenURL(new Uri(DirectoryPath+Path.DirectorySeparatorChar).AbsoluteUri);}catch(Exception ex){Debug.LogWarning("[日志] 打开日志目录失败："+ex.Message);}
        }
    }
}
