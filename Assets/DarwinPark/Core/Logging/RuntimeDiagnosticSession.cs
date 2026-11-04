using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace DarwinPark.Core
{
    // ★ 新增：独立于Unity主线程的心跳与紧急错误文件；主线程卡死时仍能保存最后状态及停顿时间。
    public sealed class RuntimeDiagnosticSession : IDisposable
    {
        private readonly RuntimeDiagnosticSettings _settings;
        private readonly Func<string> _logFile;
        private readonly string _nativeLog;
        private readonly FileStream _active;
        private readonly ManualResetEventSlim _stop=new ManualResetEventSlim();
        private readonly object _disk=new object(),_critical=new object();
        private readonly Thread _worker;
        private long _heartbeat=Stopwatch.GetTimestamp(),_window;
        private int _windowCount,_suppressed,_closed,_fatal;
        private bool _clean;
        private string _state="{}",_failure,_lastError="",_reason="";
        public string DirectoryPath {get;}
        public string Failure=>Volatile.Read(ref _failure);
        public int RecoveredSessions {get;}
        public RuntimeDiagnosticSession(string root,RuntimeDiagnosticSettings settings,string header,Func<string> logFile,string nativeLog)
        {
            settings.Validate();_settings=settings;_logFile=logFile;_nativeLog=nativeLog;Directory.CreateDirectory(root);
            foreach(string old in DiagnosticFiles.Runs(root))
            {
                if(File.Exists(Path.Combine(old,"completed.txt"))||File.Exists(Path.Combine(old,"abnormal-exit.txt"))||!DiagnosticFiles.IsInactive(old))continue;
                DiagnosticFiles.AtomicText(Path.Combine(old,"abnormal-exit.txt"),"发现时间 UTC="+DateTime.UtcNow.ToString("O")+
                    "\n上次会话没有正常关闭标记。可能为崩溃、强制结束、断电或日志自身故障；此标记不确定原因。请查看critical、health、state以及Unity原生崩溃报告。\n");RecoveredSessions++;
            }
            DirectoryPath=Path.Combine(root,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryPath);
            _active=new FileStream(Path.Combine(DirectoryPath,"active.lock"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
            try
            {
                DiagnosticFiles.AtomicText(Path.Combine(DirectoryPath,"session.txt"),"格式=1\n开始UTC="+DateTime.UtcNow.ToString("O")+"\n进程="+Process.GetCurrentProcess().Id+"\n"+header);
                DiagnosticFiles.RetainRuns(root,settings.retainedSessions);
                Checkpoint();
                _worker=new Thread(Loop){IsBackground=true,Name="DarwinPark.Diagnostics"};_worker.Start();
            }
            catch{_active.Dispose();throw;}
        }
        public void Pulse(string state=null)
        {
            Interlocked.Exchange(ref _heartbeat,Stopwatch.GetTimestamp());
            if(state!=null)Volatile.Write(ref _state,state);
        }
        public void RecordCritical(string message,string stack,bool fatal=false)
        {
            if(Volatile.Read(ref _closed)!=0)return;
            if(fatal)Interlocked.Exchange(ref _fatal,1);
            try
            {
                lock(_critical)
                {
                    // ★ 修复（防Bug回归）：错误风暴必须限流且留计数；真正未处理异常始终越过限流写入。
                    long window=Stopwatch.GetTimestamp()/Stopwatch.Frequency;
                    if(window!=_window){_window=window;_windowCount=0;}
                    if(++_windowCount>_settings.criticalPerSecond&&!fatal){Interlocked.Increment(ref _suppressed);return;}
                    string detail=(message??"")+"\n"+(stack??"");int limit=_settings.criticalBytes/4;
                    if(detail.Length>limit)detail=detail.Substring(0,limit)+"…[已截断]";
                    _lastError=DateTimeOffset.Now.ToString("O")+" [线程 "+Thread.CurrentThread.ManagedThreadId+"] "+detail;
                    string file=Path.Combine(DirectoryPath,"critical.log");
                    if(File.Exists(file)&&new FileInfo(file).Length+Encoding.UTF8.GetByteCount(_lastError)+2>_settings.criticalBytes)
                    {string previous=Path.Combine(DirectoryPath,"critical-previous.log");if(File.Exists(previous))File.Delete(previous);File.Move(file,previous);}
                    using(var stream=new FileStream(file,FileMode.Append,FileAccess.Write,FileShare.Read))
                    {byte[] bytes=DiagnosticFiles.Utf8.GetBytes(_lastError+"\n");stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                }
            }
            catch(Exception ex){Volatile.Write(ref _failure,"紧急日志："+ex.Message);}
        }
        public void Checkpoint()
        {
            lock(_disk)
            {
                DiagnosticFiles.AtomicText(Path.Combine(DirectoryPath,"state.json"),Volatile.Read(ref _state));
                double age=(Stopwatch.GetTimestamp()-Interlocked.Read(ref _heartbeat))*1000.0/Stopwatch.Frequency;
                DiagnosticFiles.AtomicText(Path.Combine(DirectoryPath,"health.txt"),"写入UTC="+DateTime.UtcNow.ToString("O")+
                        "\n主线程心跳间隔ms="+age.ToString("F0",System.Globalization.CultureInfo.InvariantCulture)+
                        "\n疑似主线程停顿="+(age>=_settings.stallMilliseconds)+"\n进程工作集bytes="+DiagnosticMemory.WorkingSetBytes()+
                        "\n托管内存bytes="+GC.GetTotalMemory(false)+"\n紧急日志限流数="+Volatile.Read(ref _suppressed)+
                        "\n诊断写盘故障="+Failure+"\n最近错误="+Volatile.Read(ref _lastError));
                CopyTail(_logFile?.Invoke(),"events-tail.log");CopyTail(_nativeLog,"player-tail.log");
            }
        }
        private void CopyTail(string source,string name)
        {
            if(string.IsNullOrEmpty(source)||!File.Exists(source))return;
            try{DiagnosticFiles.AtomicText(Path.Combine(DirectoryPath,name),DiagnosticFiles.Utf8.GetString(DiagnosticFiles.Tail(source,_settings.tailBytes)));}
            catch(IOException ex){Volatile.Write(ref _failure,"日志尾部："+ex.Message);}catch(UnauthorizedAccessException ex){Volatile.Write(ref _failure,"日志尾部："+ex.Message);}
        }
        private void Loop()
        {
            try
            {
                while(!_stop.Wait(_settings.snapshotMilliseconds))
                {try{Checkpoint();}catch(Exception ex){Volatile.Write(ref _failure,"诊断快照："+ex.Message);}}
                Checkpoint();
                if(_clean&&Volatile.Read(ref _fatal)==0&&Failure==null)
                    DiagnosticFiles.AtomicText(Path.Combine(DirectoryPath,"completed.txt"),DateTime.UtcNow.ToString("O")+"\n"+_reason);
            }
            catch(Exception ex){Volatile.Write(ref _failure,"诊断结束："+ex.Message);}
            finally{_active.Dispose();}
        }
        public void Close(bool clean,string reason)
        {
            if(Interlocked.Exchange(ref _closed,1)!=0)return;
            _clean=clean;_reason=reason;_stop.Set();
            if(!_worker.Join(_settings.shutdownMilliseconds))Volatile.Write(ref _failure,"诊断写盘结束等待超时");
        }
        public void Dispose()=>Close(true,"日志系统正常关闭");
    }
}
