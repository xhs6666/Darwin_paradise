using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：退出、后台挂起时主动刷新；禁用域重载重复播放仍可成对注销，旧对象不能关闭新会话。
    public sealed class RuntimeLogLifecycle : MonoBehaviour
    {
        private SessionLogWriter _writer;private bool _reported,_diagnosticsReported;
        public void Bind(SessionLogWriter writer)=>_writer=writer;
        private void Update()
        {
            // ★ 适配：主线程仅更新内存心跳，每秒采样；后台线程负责持久化。
            RuntimeCrashDiagnostics.Tick();
            if(!_reported&&_writer?.Failure!=null){_reported=true;Debug.LogWarning("[日志] 文件写入已停止："+_writer.Failure+"；Unity原始日志仍可使用。");}
            // ★ 适配：磁盘满或权限变化时明确提示诊断故障，不能只在导出时才知道快照缺失。
            if(!_diagnosticsReported&&!string.IsNullOrEmpty(RuntimeCrashDiagnostics.Failure)){_diagnosticsReported=true;Debug.LogWarning("[诊断] 写入异常："+RuntimeCrashDiagnostics.Failure);}
        }
        private void OnApplicationPause(bool paused){if(paused){Debug.Log("[运行] 应用转入后台");RuntimeFileLog.Flush();}}
        private void OnApplicationQuit(){Debug.Log("[运行] 正常退出");RuntimeFileLog.Shutdown("收到正常退出事件");}
        private void OnDestroy()=>RuntimeFileLog.ShutdownIfCurrent(_writer);
    }
}
