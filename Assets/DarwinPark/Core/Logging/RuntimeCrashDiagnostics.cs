using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarwinPark.Core
{
    // ★ 新增：Unity只在主线程采样；线程异常处理器只能访问纯托管会话，不能查询场景或显卡API。
    public static class RuntimeCrashDiagnostics
    {
        private static RuntimeDiagnosticSession _session;
        private static RuntimeDiagnosticSettings _settings;
        public static Func<Snapshot> SampleState;
        private static string _nativeFolder="",_root="";
        private static double _sampleAt;
        private static float _worstFrame;
        private static string _startupFailure="";
        public static string DirectoryPath=>_session?.DirectoryPath??"";
        public static string Failure=>_session?.Failure??_startupFailure;
        public static int PreviousAbnormalSessions=>_session?.RecoveredSessions??0;
        [Serializable] public sealed class Snapshot
        {
            public string utc,scene,worldId,worldName,loadingStatus,saveStatus,networkRole,fileLogFailure;
            public int frame,entities,members;
            public bool ready,loading,running,focused;
            public float lastFrameMilliseconds,worstFrameMilliseconds,configuredSpeed,actualSpeed,gameTime;
            public double pendingSimulationSeconds;
            public long unityAllocatedBytes,unityReservedBytes;
        }
        public static void Start(Func<string> logFile=null)
        {
            Stop("重置诊断会话");_startupFailure="";
            try
            {
                _settings=RuntimeDiagnosticSettings.Load();if(!_settings.enabled)return;
                // 错误/异常总保留脚本调用栈，普通输出不打开Full堆栈，避免额外性能开销。
                foreach(var type in new[]{LogType.Error,LogType.Exception,LogType.Assert})Application.SetStackTraceLogType(type,StackTraceLogType.ScriptOnly);
                _root=Path.Combine(RuntimeFileLog.DirectoryPath,"diagnostics");_nativeFolder="";
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                _nativeFolder=UnityEngine.Windows.CrashReporting.crashReportFolder;
#endif
                string header="游戏="+Application.productName+"\n版本="+Application.version+"\n构建GUID="+Application.buildGUID+"\nUnity="+Application.unityVersion+"\n编辑器="+Application.isEditor+
                    "\n系统="+SystemInfo.operatingSystem+"\nCPU="+SystemInfo.processorType+"\n内存MB="+SystemInfo.systemMemorySize+
                    "\nGPU="+SystemInfo.graphicsDeviceName+"\n图形API="+SystemInfo.graphicsDeviceType+"\nUnity日志="+Application.consoleLogPath+"\n原生崩溃目录="+_nativeFolder+
                    "\n游戏日志="+RuntimeFileLog.CurrentFilePath+"\n";
                string initialLog=RuntimeFileLog.CurrentFilePath;
                _session=new RuntimeDiagnosticSession(_root,_settings,header,logFile??(()=>initialLog),Application.isEditor?null:Application.consoleLogPath);
                _sampleAt=0;_worstFrame=0;
                AppDomain.CurrentDomain.UnhandledException+=Unhandled;TaskScheduler.UnobservedTaskException+=Unobserved;
                Tick();
                Debug.Log("[诊断] 崩溃前状态与独立错误记录："+DirectoryPath);
                if(PreviousAbnormalSessions>0)Debug.LogWarning("[诊断] 发现 "+PreviousAbnormalSessions+" 次未正常结束的会话，已标记abnormal-exit.txt；请导出诊断包排查，标记本身不代表已确定原因。");
            }
            catch(Exception ex){_startupFailure=ex.ToString();Debug.LogError("[诊断] 启动失败："+ex.Message);}
        }
        public static void Receive(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_session?.RecordCritical("["+type+"] "+message,stack);}
        public static void Tick()
        {
            var session=_session;if(session==null)return;session.Pulse();
            _worstFrame=Mathf.Max(_worstFrame,Time.unscaledDeltaTime*1000);
            double now=Time.realtimeSinceStartupAsDouble;if(now<_sampleAt)return;_sampleAt=now+_settings.sampleMilliseconds/1000.0;
            ServiceLocator.TryGet<SimulationClock>(out var clock);
            var state=SampleState?.Invoke()??new Snapshot();
            state.utc=DateTime.UtcNow.ToString("O");state.scene=SceneManager.GetActiveScene().name;state.frame=Time.frameCount;
            state.fileLogFailure=RuntimeFileLog.Failure;
            state.ready=WorldRuntimeGate.Ready;state.loading|=WorldRuntimeGate.Loading;state.running=clock!=null&&clock.IsRunning;
            state.focused=Application.isFocused;state.lastFrameMilliseconds=Time.unscaledDeltaTime*1000;state.worstFrameMilliseconds=_worstFrame;
            state.configuredSpeed=clock?.ConfiguredTimeScale??0;state.actualSpeed=clock?.MotionTimeScale??0;state.gameTime=clock?.GameTime??0;
            state.pendingSimulationSeconds=clock?.PendingGameTime??0;
            state.unityAllocatedBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            state.unityReservedBytes=UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
            session.Pulse(JsonUtility.ToJson(state,true));_worstFrame=0;
        }
        private static void Unhandled(object sender,UnhandledExceptionEventArgs args)
        {
            _session?.RecordCritical("[未处理异常] terminating="+args.IsTerminating,args.ExceptionObject?.ToString(),args.IsTerminating);
            RuntimeFileLog.Flush();
        }
        private static void Unobserved(object sender,UnobservedTaskExceptionEventArgs args)
        {
            _session?.RecordCritical("[未观察的Task异常]",args.Exception.ToString());
            // 不SetObserved，不改变游戏原本的异常传播策略。
        }
        public static Task<string> ExportAsync()
        {
            if(_session==null)return Task.FromException<string>(new InvalidOperationException("诊断系统未启用："+Failure));
            var session=_session;var settings=_settings;string root=_root,native=_nativeFolder;
            return Task.Run(()=>{RuntimeFileLog.Flush();session.Checkpoint();return DiagnosticFiles.Export(root,native,settings);});
        }
        public static void Stop(string reason)
        {
            AppDomain.CurrentDomain.UnhandledException-=Unhandled;TaskScheduler.UnobservedTaskException-=Unobserved;
            var session=_session;_session=null;session?.Close(true,reason);
        }
    }
}
