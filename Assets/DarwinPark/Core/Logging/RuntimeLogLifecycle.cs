using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：退出、后台挂起时主动刷新；禁用域重载重复播放仍可成对注销，旧对象不能关闭新会话。
    public sealed class RuntimeLogLifecycle : MonoBehaviour
    {
        private SessionLogWriter _writer;private bool _reported;
        public void Bind(SessionLogWriter writer)=>_writer=writer;
        private void Update()
        {
            if(!_reported&&_writer?.Failure!=null){_reported=true;Debug.LogWarning("[日志] 文件写入已停止："+_writer.Failure+"；Unity原始日志仍可使用。");}
        }
        private void OnApplicationPause(bool paused){if(paused){Debug.Log("[运行] 应用转入后台");RuntimeFileLog.Flush();}}
        private void OnApplicationQuit(){Debug.Log("[运行] 正常退出");RuntimeFileLog.Flush();}
        private void OnDestroy()=>RuntimeFileLog.ShutdownIfCurrent(_writer);
    }
}
