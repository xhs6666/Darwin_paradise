using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarwinPark.Core;
using UnityEditor;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    // ★ 新增：只在隔离工程执行故意异常、停顿和故障夹具，不能影响正在运行的用户世界。
    public static class RuntimeDiagnosticsRegression
    {
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/RuntimeDiagnosticsReview");
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Passed.Add(text);}
        private static string Read(string path)=>DiagnosticFiles.Utf8.GetString(DiagnosticFiles.Tail(path,1048576));
        public static void RunBatch()
        {
            Directory.CreateDirectory(Folder);Passed.Clear();string error=null;
            try{Verify();}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);}
            finally{RuntimeFileLog.Shutdown("诊断回归结束");}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed),DiagnosticFiles.Utf8);
            Debug.Log("[RuntimeDiagnosticsReview] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static RuntimeDiagnosticSettings Fast()=>new RuntimeDiagnosticSettings{snapshotMilliseconds=100,sampleMilliseconds=100,stallMilliseconds=300,tailBytes=4096,criticalBytes=4096,criticalPerSecond=4,retainedSessions=3};
        private static void Verify()
        {
            RuntimeDiagnosticSettings.Load().Validate();Check(true,"诊断JSON可加载，所有磁盘和频率预算有效");
            string root=Path.Combine(Folder,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            string logs=Path.Combine(root,"capture");var config=RuntimeLogSettings.Load();RuntimeFileLog.StartSession(config,logs,logs);RuntimeCrashDiagnostics.Start();
            string run=RuntimeCrashDiagnostics.DirectoryPath;
            Debug.Log("诊断回归-普通输出");Debug.LogError("诊断回归-预期错误");
            try{throw new InvalidOperationException("诊断回归-预期异常");}catch(Exception ex){Debug.LogException(ex);}
            Task.Run(()=>Debug.LogError("诊断回归-后台线程错误")).GetAwaiter().GetResult();
            string critical=Read(Path.Combine(run,"critical.log"));
            Check(critical.Contains("预期错误")&&critical.Contains("预期异常")&&critical.Contains(nameof(Verify))&&critical.Contains("后台线程错误"),"主线程与后台异常在回调返回前独立落盘，含真实调用栈");
            typeof(RuntimeCrashDiagnostics).GetMethod("Unobserved",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{null,new UnobservedTaskExceptionEventArgs(new AggregateException("预期Task异常"))});
            Check(Read(Path.Combine(run,"critical.log")).Contains("预期Task异常"),"未观察Task异常处理器写入独立错误文件");
            Check(RuntimeFileLog.Flush(),"普通日志仍正常刷新");
            string zip=RuntimeCrashDiagnostics.ExportAsync().GetAwaiter().GetResult();
            using(var archive=ZipFile.OpenRead(zip))
            {
                Check(archive.Entries.Any(e=>e.Name=="critical.log")&&archive.Entries.Any(e=>e.Name=="state.json")&&archive.Entries.Any(e=>e.Name=="session.txt"),"一键异步导出包含错误、状态、版本环境");
                Check(!archive.Entries.Any(e=>e.Name=="active.lock"||e.Name=="Editor.log"),"导出排除文件锁及可能含Unity授权信息的Editor.log");
                var state=archive.Entries.First(e=>e.Name=="state.json");using(var reader=new StreamReader(state.Open()))Check(reader.ReadToEnd().Contains("pendingSimulationSeconds"),"状态包含帧时间、模拟积压等排查卡顿字段");
            }
            RuntimeFileLog.Shutdown("预期正常退出");Check(File.Exists(Path.Combine(run,"completed.txt")),"正常关闭排空后保存完成标记");
            string fatalCapture=Path.Combine(root,"fatal-capture");RuntimeFileLog.StartSession(config,fatalCapture,fatalCapture);RuntimeCrashDiagnostics.Start();
            string fatalRun=RuntimeCrashDiagnostics.DirectoryPath;
            typeof(RuntimeCrashDiagnostics).GetMethod("Unhandled",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{null,new UnhandledExceptionEventArgs(new InvalidOperationException("终止性异常夹具"),true)});
            RuntimeFileLog.Shutdown("终止性异常后不能误标正常");
            Check(Read(Path.Combine(fatalRun,"critical.log")).Contains("终止性异常夹具")&&!File.Exists(Path.Combine(fatalRun,"completed.txt")),"终止性异常处理器保留错误，后续关闭也不能覆盖为正常退出");
            Check(ulong.TryParse(DiagnosticMemory.WorkingSetBytes(),out var resident)&&resident>0,"Windows Mono可读取真实进程工作集内存");
            string sessions=Path.Combine(root,"sessions"),native=Path.Combine(root,"Player.log");File.WriteAllText(native,"Unity原始输出-中文\n",DiagnosticFiles.Utf8);
            var settings=Fast();var a=new RuntimeDiagnosticSession(sessions,settings,"版本=回归",()=>native,native);a.Pulse("{\"entities\":800}");a.RecordCritical("故意异常","真实堆栈");a.Checkpoint();
            Check(Read(Path.Combine(a.DirectoryPath,"player-tail.log")).Contains("Unity原始输出-中文"),"发布版原始日志可共享读取并保留中文尾部");
            var b=new RuntimeDiagnosticSession(sessions,settings,"并发实例",()=>native,native);
            Check(b.RecoveredSessions==0&&!File.Exists(Path.Combine(a.DirectoryPath,"abnormal-exit.txt")),"其他仍在运行的实例不被误报为崩溃");
            Thread.Sleep(450);a.Checkpoint();string health=Read(Path.Combine(a.DirectoryPath,"health.txt"));
            Check(health.Contains("疑似主线程停顿=True")&&Read(Path.Combine(a.DirectoryPath,"state.json")).Contains("800"),"主线程不再心跳时后台记录疑似卡死及最后实体数");
            a.Pulse();a.Checkpoint();Check(Read(Path.Combine(a.DirectoryPath,"health.txt")).Contains("疑似主线程停顿=False"),"恢复心跳后停顿提示清除");
            for(int i=0;i<100;i++)a.RecordCritical("同秒错误风暴-"+i,"");a.RecordCritical("致命异常越过限流","",true);a.Checkpoint();
            Check(Read(Path.Combine(a.DirectoryPath,"critical.log")).Contains("致命异常越过限流")&&!Read(Path.Combine(a.DirectoryPath,"health.txt")).Contains("紧急日志限流数=0\n"),"错误风暴有界并记录省略数量，致命异常仍保留");
            for(int i=0;i<20;i++)a.RecordCritical("轮转-"+i+new string('中',800),"",true);
            Check(File.Exists(Path.Combine(a.DirectoryPath,"critical-previous.log"))&&new FileInfo(Path.Combine(a.DirectoryPath,"critical.log")).Length<=settings.criticalBytes&&new FileInfo(Path.Combine(a.DirectoryPath,"critical-previous.log")).Length<=settings.criticalBytes,"紧急错误日志轮转且两个分段大小受限");
            a.Close(false,"模拟未正常退出");string old=a.DirectoryPath;
            using(var c=new RuntimeDiagnosticSession(sessions,settings,"重启",()=>native,native))Check(c.RecoveredSessions==1&&File.Exists(Path.Combine(old,"abnormal-exit.txt")),"重启识别未正常结束的上次会话并保留证据");
            b.Dispose();Check(File.Exists(Path.Combine(b.DirectoryPath,"completed.txt")),"并发实例正常退出各自保存标记");
            string unrelated=Path.Combine(sessions,"unrelated");Directory.CreateDirectory(unrelated);File.WriteAllText(Path.Combine(unrelated,"keep.txt"),"保留");
            for(int i=0;i<5;i++)using(var c=new RuntimeDiagnosticSession(sessions,settings,"保留策略",()=>native,native)){ }
            Check(DiagnosticFiles.Runs(sessions).Length==settings.retainedSessions&&File.Exists(Path.Combine(unrelated,"keep.txt")),"只清理本功能拥有的会话且总数量受限");
            string failedRoot=Path.Combine(root,"failed");var failed=new RuntimeDiagnosticSession(failedRoot,settings,"故障测试",()=>native,native);
            File.WriteAllText(Path.Combine(failed.DirectoryPath,"critical.log"),"");File.Delete(Path.Combine(failed.DirectoryPath,"critical.log"));Directory.CreateDirectory(Path.Combine(failed.DirectoryPath,"critical.log"));
            failed.RecordCritical("不可写夹具","");Check(!string.IsNullOrEmpty(failed.Failure),"诊断写盘失败向上层报告，不能假装成功");failed.Dispose();
            string bytesFile=Path.Combine(root,"utf8.log");File.WriteAllText(bytesFile,new string('中',5000)+"\n最后完整中文行\n",DiagnosticFiles.Utf8);
            string tail=new UTF8Encoding(false,true).GetString(DiagnosticFiles.Tail(bytesFile,4096));Check(tail.Contains("最后完整中文行"),"有界尾部读取不会截坏中文编码");
            string nativeRoot=Path.Combine(root,"native");Directory.CreateDirectory(nativeRoot);File.WriteAllText(Path.Combine(nativeRoot,"error.log"),"native candidate");File.WriteAllBytes(Path.Combine(nativeRoot,"crash.dmp"),new byte[1024]);File.WriteAllText(Path.Combine(nativeRoot,"private.bin"),"排除");
            File.WriteAllText(Path.Combine(DiagnosticFiles.Runs(sessions)[0],"world-save.json"),"不可导出");string exported=DiagnosticFiles.Export(sessions,nativeRoot,settings);
            using(var archive=ZipFile.OpenRead(exported))Check(archive.Entries.Any(e=>e.Name=="crash.dmp")&&archive.Entries.Any(e=>e.Name=="error.log")&&!archive.Entries.Any(e=>e.Name=="private.bin"||e.Name=="world-save.json"),"候选原生转储可收集，非白名单文件不导出");
            string large=Path.Combine(nativeRoot,"large.dmp");using(var file=File.Create(large))file.SetLength(settings.exportMaximumBytes+1L);
            string bounded=DiagnosticFiles.Export(sessions,nativeRoot,settings);
            using(var archive=ZipFile.OpenRead(bounded)){using(var reader=new StreamReader(archive.GetEntry("README.txt").Open()))Check(!archive.Entries.Any(e=>e.Name=="large.dmp")&&reader.ReadToEnd().Contains("预算不足"),"超出预算的转储不打包且明确注明遗漏原因");}
            for(int i=0;i<4;i++)DiagnosticFiles.Export(sessions,nativeRoot,settings);
            Check(Directory.GetFiles(Path.Combine(sessions,"exports"),"*.zip").Length==settings.retainedExports,"导出包保留数量受限");
        }
    }
}
