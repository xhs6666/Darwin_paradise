using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：任意线程只做有界排队；单独写盘线程处理刷新、轮转和保留策略，不在每帧主线程同步写文件。
    public sealed class SessionLogWriter : IDisposable
    {
        private sealed class Entry
        {public string text;public bool important;public ManualResetEventSlim barrier;}
        private readonly RuntimeLogSettings _settings;
        private readonly BlockingCollection<Entry> _queue;
        private readonly Thread _thread;
        private readonly string _session;
        private StreamWriter _stream;private long _bytes;private int _part,_stopped,_dropped;
        private string _file,_failure;
        public string DirectoryPath {get;}
        public string CurrentFilePath=>Volatile.Read(ref _file);
        public string Failure=>Volatile.Read(ref _failure);
        public string FallbackReason {get;}
        public SessionLogWriter(RuntimeLogSettings settings,string preferredDirectory,string fallbackDirectory)
        {
            settings.Validate();_settings=settings;
            _session="session-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+System.Diagnostics.Process.GetCurrentProcess().Id+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            DirectoryPath=Path.GetFullPath(preferredDirectory);
            try{OpenPart();}
            catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException)
            {
                // ★ 修复（防Bug回归）：游戏目录只读时改用用户数据目录，记录原因且不妨碍游戏启动。
                FallbackReason=ex.Message;DirectoryPath=Path.GetFullPath(fallbackDirectory);OpenPart();
            }
            _queue=new BlockingCollection<Entry>(settings.queuedEntries);
            _thread=new Thread(WriteLoop){IsBackground=true,Name="DarwinPark.FileLog"};_thread.Start();
        }
        public void Write(string message,string stack,LogType type)
        {
            if(Volatile.Read(ref _stopped)!=0||Failure!=null)return;
            string detail=message??"";
            if(type!=LogType.Log&&!string.IsNullOrWhiteSpace(stack))detail+="\n"+stack;
            if(detail.Length>_settings.maximumEntryCharacters)detail=detail.Substring(0,_settings.maximumEntryCharacters)+"…[已截断]";
            var entry=new Entry{text=DateTimeOffset.Now.ToString("O")+" ["+type+"] [线程 "+Thread.CurrentThread.ManagedThreadId+"] "+detail,important=type==LogType.Error||type==LogType.Exception||type==LogType.Assert};
            try{if(!_queue.TryAdd(entry))Interlocked.Increment(ref _dropped);}catch(InvalidOperationException){ }
        }
        public bool Flush()
        {
            if(Volatile.Read(ref _stopped)!=0)return Failure==null;
            // 不在超时后Dispose信号，后台仍可能需要设置它；托管信号随队列项回收。
            var signal=new ManualResetEventSlim();
            try{return _queue.TryAdd(new Entry{barrier=signal},_settings.shutdownTimeoutMilliseconds)&&signal.Wait(_settings.shutdownTimeoutMilliseconds)&&Failure==null;}
            catch(InvalidOperationException){return false;}
        }
        private void WriteLoop()
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                while(!_queue.IsCompleted)
                {
                    if(_queue.TryTake(out var entry,_settings.flushIntervalMilliseconds))
                    {
                        if(entry.barrier!=null){_stream.Flush();entry.barrier.Set();}
                        else{Append(entry.text);if(entry.important)_stream.Flush();}
                    }
                    int dropped=Interlocked.Exchange(ref _dropped,0);
                    if(dropped>0)Append(DateTimeOffset.Now.ToString("O")+" [Warning] [日志] 队列已满，省略 "+dropped+" 条消息");
                    if(timer.ElapsedMilliseconds>=_settings.flushIntervalMilliseconds){_stream.Flush();timer.Restart();}
                }
                _stream.Flush();
            }
            catch(Exception ex)
            {
                // ★ 修复（防Bug回归）：日志自身失败不能再Debug.Log造成递归，也不能从后台访问Unity对象。
                Volatile.Write(ref _failure,ex.Message);
            }
            finally
            {
                try{_stream?.Dispose();}catch(Exception ex){Volatile.Write(ref _failure,ex.Message);}
                while(_queue.TryTake(out var remaining))remaining.barrier?.Set();
            }
        }
        private void Append(string text)
        {
            long size=Encoding.UTF8.GetByteCount(text)+Encoding.UTF8.GetByteCount(Environment.NewLine);
            if(_bytes+size>_settings.maximumFileBytes&&_bytes>0){_stream.Dispose();_part++;OpenPart();}
            _stream.WriteLine(text);_bytes+=size;
        }
        private void OpenPart()
        {
            Directory.CreateDirectory(DirectoryPath);string file=Path.Combine(DirectoryPath,_session+"-"+_part.ToString("D3")+".log");
            _stream=new StreamWriter(new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));_bytes=0;Volatile.Write(ref _file,file);
            // ★ 新增：只管理本功能的session日志，其他文件不删；其他游戏实例仍打开的日志由系统共享锁保护。
            try
            {
                foreach(var old in new DirectoryInfo(DirectoryPath).GetFiles("session-*.log").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(_settings.retainedFiles))
                    if(old.FullName!=file)try{old.Delete();}catch(IOException){ }catch(UnauthorizedAccessException){ }
            }
            catch(IOException){ }catch(UnauthorizedAccessException){ }
            try{File.WriteAllText(Path.Combine(DirectoryPath,"latest-log.txt"),file,new UTF8Encoding(false));}catch(IOException){ }catch(UnauthorizedAccessException){ }
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref _stopped,1)!=0)return;_queue.CompleteAdding();
            // ★ 修复（防Bug回归）：先排空再关文件；慢磁盘退出等待有上限，不能永久阻塞关闭游戏。
            if(!_thread.Join(_settings.shutdownTimeoutMilliseconds))Volatile.Write(ref _failure,"等待日志写盘超时");
        }
    }
}
