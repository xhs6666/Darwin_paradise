using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarwinPark.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    // ★ 新增：编辑器编译无法发现Editor专用API误入Player，必须执行真正的非开发版Windows构建。
    public static class PlayerBuildLogRegression
    {
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/PlayerBuildLogReview");
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);Passed.Add(text);Debug.Log("[PlayerBuildLogReview] "+text);}
        public static void RunBatch()
        {
            Directory.CreateDirectory(Folder);Passed.Clear();File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;
            try
            {
                VerifyLogging();
                // ★ 修复（防Bug回归）：Shader.Find的字符串引用不会阻止构建裁剪，必须有Resources内的实际材质依赖。
                var materials=Resources.LoadAll<Material>("RuntimeMaterials");Check(materials.Length==3&&materials.All(m=>m.shader!=null&&m.shader.isSupported),"太阳、星空与发光能量块材质都有可打包的Shader引用");
                var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();Check(scenes.Length>0&&scenes.All(File.Exists),"发布场景有效");
                string target=Path.Combine(Folder,"Windows","DarwinParadise.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=target,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                ExportMessages(report);
                File.WriteAllText(Path.Combine(Folder,"build-summary.txt"),"Result="+report.summary.result+"\nErrors="+report.summary.totalErrors+"\nWarnings="+report.summary.totalWarnings+"\nSize="+report.summary.totalSize+"\nPath="+target,new UTF8Encoding(false));
                Check(report.summary.result==BuildResult.Succeeded&&report.summary.totalErrors==0,"真实Windows非开发版Player构建成功");
            }
            catch(Exception ex){error=ex.ToString();Debug.LogException(ex);}
            finally{RuntimeFileLog.Shutdown("日志回归结束");}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed),new UTF8Encoding(false));
            Debug.Log("[PlayerBuildLogReview] "+(error==null?"PASS":"FAIL"));EditorApplication.Exit(error==null?0:1);
        }
        public static void InspectLastReportBatch()
        {
            var report=BuildReport.GetLatestReport();if(report==null)throw new InvalidOperationException("缺少上次构建报告");ExportMessages(report);EditorApplication.Exit(0);
        }
        private static void ExportMessages(BuildReport report)
        {
            // ★ 新增：保留构建器的真实告警明细，不能把脚本0警告误写成整个Player构建0警告。
            File.WriteAllText(Path.Combine(Folder,"build-messages.txt"),string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.type+": "+m.content)),new UTF8Encoding(false));
        }
        private static RuntimeLogSettings Settings()=>new RuntimeLogSettings{version=1,enabled=true,directoryName="logs",maximumFileBytes=1048576,retainedFiles=3,queuedEntries=1024,maximumEntryCharacters=1024,flushIntervalMilliseconds=20,shutdownTimeoutMilliseconds=2000};
        private static byte[] ReadActive(string path)
        {
            // ★ 适配：仍在写入的日志读取端要允许共享写，Mono默认ReadAllText只共享读会把正常文件锁误报成故障。
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            using(var copy=new MemoryStream()){input.CopyTo(copy);return copy.ToArray();}
        }
        private static void VerifyLogging()
        {
            var configured=RuntimeLogSettings.Load();Check(configured.enabled&&configured.maximumFileBytes>0,"日志JSON配置可加载并校验");
            string root=Path.Combine(Folder,"LogTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            var settings=Settings();string capture=Path.Combine(root,"capture");RuntimeFileLog.StartSession(settings,capture,capture);string first=RuntimeFileLog.CurrentFilePath;
            const string marker="日志自测-中文-";Debug.Log(marker+"普通输出");Debug.LogWarning(marker+"预期警告");
            // ★ 新增：这里的Error/Exception是隔离工程主动生成的预期测试数据，不属于游戏故障。
            Debug.LogError(marker+"预期错误");try{throw new InvalidOperationException(marker+"预期异常");}catch(Exception ex){Debug.LogException(ex);}
            Task.Run(()=>{for(int i=0;i<32;i++)Debug.Log(marker+"后台-"+i.ToString("D2"));}).GetAwaiter().GetResult();
            Check(RuntimeFileLog.Flush(),"普通输出和工作线程输出可以主动刷新");string text=Encoding.UTF8.GetString(ReadActive(first));
            Check(text.Contains(marker+"普通输出")&&text.Contains(marker+"预期警告")&&text.Contains(marker+"预期错误")&&text.Contains(marker+"预期异常")&&text.Contains(nameof(VerifyLogging)),"捕获Unity全部级别及异常堆栈");
            Check(Enumerable.Range(0,32).All(i=>text.Contains(marker+"后台-"+i.ToString("D2"))),"32条真实后台线程Unity日志全部写入");
            Check(text.Contains("[线程 ")&&text.Contains("[Warning]")&&text.Contains("[Error]")&&text.Contains("[Exception]"),"包含时间、级别和线程信息");
            var bytes=ReadActive(first);new UTF8Encoding(false,true).GetString(bytes);Check(!(bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191),"日志UTF-8无BOM且中文可读");
            RuntimeFileLog.StartSession(settings,capture,capture);string second=RuntimeFileLog.CurrentFilePath;Debug.Log(marker+"不重复订阅");RuntimeFileLog.Shutdown("预期退出");
            string latest=File.ReadAllText(second);Check(first!=second&&latest.Split(new[]{marker+"不重复订阅"},StringSplitOptions.None).Length==2&&latest.Contains("会话结束"),"重开会话文件独立、只订阅一次、退出排空尾部");
            long length=new FileInfo(second).Length;Debug.Log(marker+"关闭后不捕获");Check(new FileInfo(second).Length==length,"关闭日志后注销Unity事件");
            using(new FileStream(second,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(true,"关闭后日志文件锁已释放");
            string blocked=Path.Combine(root,"not-a-directory");File.WriteAllText(blocked,"existing file");string fallback=Path.Combine(root,"fallback");
            using(var writer=new SessionLogWriter(settings,blocked,fallback)){writer.Write("fallback-tail","",LogType.Log);Check(writer.DirectoryPath==fallback&&writer.FallbackReason!=null&&writer.Flush(),"游戏目录不可写时改用用户目录且说明原因");}
            string rotate=Path.Combine(root,"rotate");settings=Settings();settings.maximumEntryCharacters=128;settings.maximumFileBytes=1024;
            using(var writer=new SessionLogWriter(settings,rotate,rotate)){for(int i=0;i<40;i++)writer.Write(i.ToString("D2")+new string('a',220),"",LogType.Log);writer.Write("rotation-final","",LogType.Log);Check(writer.Flush(),"轮转后后台写入仍正常");}
            var files=Directory.GetFiles(rotate,"session-*.log");Check(files.Length==settings.retainedFiles&&files.All(f=>new FileInfo(f).Length<=settings.maximumFileBytes),"日志轮转文件大小及保留数量有界");
            string rotated=string.Join("\n",files.Select(File.ReadAllText));Check(rotated.Contains("rotation-final")&&rotated.Contains("已截断")&&File.Exists(File.ReadAllText(Path.Combine(rotate,"latest-log.txt"))),"过长消息截断、最终输出和最新日志指针有效");
            settings.enabled=false;string disabled=Path.Combine(root,"disabled");RuntimeFileLog.StartSession(settings,disabled,disabled);Check(!Directory.Exists(disabled)&&string.IsNullOrEmpty(RuntimeFileLog.CurrentFilePath),"禁用日志时不创建文件");
        }
    }
}
