using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DarwinPark.Core
{
    // ★ 新增：共享读取、原子替换和有界导出只处理诊断目录，不遍历存档、账号或命令行。
    public static class DiagnosticFiles
    {
        public static readonly Encoding Utf8=new UTF8Encoding(false);
        public static void AtomicText(string file,string text)
        {
            string temp=file+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.Read))
            {var bytes=Utf8.GetBytes(text??"");stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
        }
        public static byte[] Tail(string file,int maximum)
        {
            using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            {
                int count=(int)Math.Min(stream.Length,maximum);bool trimmed=stream.Length>count;stream.Seek(-count,SeekOrigin.End);
                var bytes=new byte[count];int read=0,n;while(read<count&&(n=stream.Read(bytes,read,count-read))>0)read+=n;
                // ★ 修复（防Bug回归）：从完整下一行开始，避免截在中文UTF-8编码中间。
                int start=trimmed?Array.IndexOf(bytes,(byte)'\n',0,read)+1:0;
                return bytes.Skip(start).Take(read-start).ToArray();
            }
        }
        public static bool IsOwnedRun(string directory)=>Path.GetFileName(directory).StartsWith("run-",StringComparison.Ordinal)&&
            Guid.TryParseExact(Path.GetFileName(directory).Substring(4),"N",out _)&&File.Exists(Path.Combine(directory,"session.txt"));
        public static bool IsInactive(string directory)
        {
            try{using(new FileStream(Path.Combine(directory,"active.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))return true;}
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
        }
        public static string[] Runs(string root)=>Directory.Exists(root)?Directory.GetDirectories(root,"run-*")
            .Where(d=>(File.GetAttributes(d)&FileAttributes.ReparsePoint)==0&&IsOwnedRun(d)).OrderByDescending(Directory.GetCreationTimeUtc).ToArray():Array.Empty<string>();
        public static void RetainRuns(string root,int retained)
        {
            foreach(string old in Runs(root).Skip(retained))
                if(IsInactive(old))try{Directory.Delete(old,true);}catch(IOException){ }catch(UnauthorizedAccessException){ }
        }
        public static string Export(string root,string nativeDirectory,RuntimeDiagnosticSettings settings)
        {
            string exportRoot=Path.Combine(root,"exports");Directory.CreateDirectory(exportRoot);
            string output=Path.Combine(exportRoot,"diagnostics-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".zip");
            string temporary=output+".tmp";long remaining=settings.exportMaximumBytes;var notes=new StringBuilder();
            try
            {
                using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))
                {
                    foreach(string run in Runs(root).Take(settings.retainedSessions))
                    {
                        // 白名单禁止把锁文件、临时文件和意外放入目录的存档打包。
                        foreach(string name in new[]{"session.txt","state.json","health.txt","critical.log","critical-previous.log","events-tail.log","player-tail.log","completed.txt","abnormal-exit.txt"})
                            Add(zip,Path.Combine(run,name),Path.GetFileName(run)+"/"+name,ref remaining,notes);
                    }
                    // 原生转储仅为同一游戏崩溃目录中的近期候选，不能据此断言属于当前实例。
                    try
                    {
                        if(!string.IsNullOrEmpty(nativeDirectory)&&Directory.Exists(nativeDirectory))
                        {
                            var candidates=NativeFiles(nativeDirectory,settings.nativeSearchDepth).Where(f=>File.GetLastWriteTimeUtc(f)>=DateTime.UtcNow.AddHours(-settings.nativeLookbackHours))
                                .OrderByDescending(File.GetLastWriteTimeUtc).Take(settings.nativeMaximumFiles);
                            foreach(string file in candidates)Add(zip,file,"native-candidates/"+Path.GetRelativePath(nativeDirectory,file).Replace('\\','/'),ref remaining,notes);
                        }
                    }
                    catch(IOException ex){notes.AppendLine("原生报告目录读取失败："+ex.Message);}
                    catch(UnauthorizedAccessException ex){notes.AppendLine("原生报告目录读取失败："+ex.Message);}
                    var entry=zip.CreateEntry("README.txt");using(var writer=new StreamWriter(entry.Open(),Utf8))writer.Write(
                        "诊断包：请同时提供复现步骤、发生时间和游戏版本。\nabnormal-exit 只表示未完成正常关闭，也可能是强制结束或断电，不代表已确定崩溃原因。\n"+
                        "state 为最近主线程采样，health 为后台心跳。卡死时两者时间差有助定位。critical 为独立刷盘的错误/异常；events 为最近游戏日志；player 为发布版Unity原始日志尾部。\n"+
                        "native-candidates 如存在，是最近"+settings.nativeLookbackHours+"小时同游戏目录的候选报告；编辑器不自动采集Editor.log，以避免混入Unity授权信息。没有转储不代表没有崩溃。\n"+
                        "未打包存档或完整基因数据。文件仍可能含本机路径/玩家命名/网络地址，分享前可检查。\n"+notes);
                }
                File.Move(temporary,output);
                foreach(var old in new DirectoryInfo(exportRoot).GetFiles("diagnostics-*.zip").OrderByDescending(f=>f.CreationTimeUtc).Skip(settings.retainedExports))
                    try{old.Delete();}catch(IOException){ }catch(UnauthorizedAccessException){ }
                return output;
            }
            catch{try{File.Delete(temporary);}catch(IOException){ }throw;}
        }
        private static System.Collections.Generic.IEnumerable<string> NativeFiles(string root,int depth)
        {
            if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)yield break;
            foreach(string file in Directory.GetFiles(root))if((File.GetAttributes(file)&FileAttributes.ReparsePoint)==0&&
                (Path.GetExtension(file).Equals(".dmp",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(file).Equals(".log",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(file)=="error.txt"))yield return file;
            if(depth>0)foreach(string child in Directory.GetDirectories(root))foreach(string file in NativeFiles(child,depth-1))yield return file;
        }
        private static void Add(ZipArchive zip,string file,string name,ref long remaining,StringBuilder notes)
        {
            if(!File.Exists(file))return;
            try
            {
                using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                {
                    long length=input.Length;if(length>remaining){notes.AppendLine("预算不足，未包含："+name+" ("+length+" bytes)");return;}
                    using(var output=zip.CreateEntry(name,CompressionLevel.Fastest).Open())
                    {
                        var buffer=new byte[8192];long left=length;while(left>0){int n=input.Read(buffer,0,(int)Math.Min(buffer.Length,left));if(n==0)break;output.Write(buffer,0,n);left-=n;}
                    }
                    remaining-=length;
                }
            }
            catch(IOException ex){notes.AppendLine("读取失败："+name+"："+ex.Message);}catch(UnauthorizedAccessException ex){notes.AppendLine("读取失败："+name+"："+ex.Message);}
        }
    }
}
