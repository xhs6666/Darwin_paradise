using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarwinPark.ServerCore;
using UnityEngine;
using CompressionLevel=System.IO.Compression.CompressionLevel;

namespace DarwinPark.EditorTools
{
    // ★ 新增：项目直接产出“核心 ZIP + 启动命令”，依赖在核心内部，启动后按哈希生成不可变运行目录。
    public static class ServerCorePackaging
    {
        private static string Hash(string path)
        {using var stream=File.OpenRead(path);using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static string Quote(string value)
        {
            var text=new StringBuilder("\"");
            foreach(char c in value){if(c=='"'||c=='\\')text.Append('\\').Append(c);else if(char.IsControl(c))text.Append("\\u").Append(((int)c).ToString("x4"));else text.Append(c);}
            return text.Append('"').ToString();
        }
        private static IEnumerable<string> RuntimeFiles(string root)
        {
            var pending=new Stack<string>();pending.Push(root);
            while(pending.Count>0)
            {
                string directory=pending.Pop();
                if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("Core directory must not be a link: "+directory);
                foreach(string child in Directory.GetDirectories(directory))
                {
                    // ★ 适配：Burst 明确标注 DoNotShip 的调试副本不是运行依赖；D3D12、Mono 和 Data 保守保留。
                    if(Path.GetFileName(child).EndsWith("_BurstDebugInformation_DoNotShip",StringComparison.OrdinalIgnoreCase))continue;
                    pending.Push(child);
                }
                foreach(string file in Directory.GetFiles(directory))
                {if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Core file must not be a link: "+file);yield return file;}
            }
        }
        public static string Create(string source,string destination,string target="windows-headless")
        {
            source=Path.GetFullPath(source);destination=Path.GetFullPath(destination);
            if(destination.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||destination==source)throw new IOException("Distribution must be outside the raw build");
            if(Directory.Exists(destination)&&Directory.EnumerateFileSystemEntries(destination).Any())throw new IOException("Distribution output must be empty: "+destination);
            foreach(string required in new[]{"DarwinServer.exe","UnityPlayer.dll","Console.ps1","Stop.ps1","server.example.json","release.json"})if(!File.Exists(Path.Combine(source,required)))throw new IOException("Incomplete core: "+required);
            if(!Directory.Exists(Path.Combine(source,"DarwinServer_Data")))throw new IOException("Missing DarwinServer_Data");
            string releaseText=File.ReadAllText(Path.Combine(source,"release.json"));JsonUtility.FromJson<ServerCompatibility>(releaseText).Validate();
            var files=RuntimeFiles(source).Select(path=>new{path,name=Path.GetRelativePath(source,path).Replace('\\','/'),size=new FileInfo(path).Length,hash=Hash(path)}).OrderBy(file=>file.name,StringComparer.Ordinal).ToArray();
            var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in files)if(!unique.Add(file.name)||file.name=="manifest.json")throw new IOException("Duplicate/reserved core path: "+file.name);
            var manifest=new StringBuilder("{\"schema\":1,\"product\":\"DarwinParadise.Server\",\"target\":").Append(Quote(target)).Append(",\"entry\":\"DarwinServer.exe\",\"release\":").Append(releaseText.Trim()).Append(",\"files\":{");
            for(int i=0;i<files.Length;i++){var file=files[i];if(i>0)manifest.Append(',');manifest.Append(Quote(file.name)).Append(":{\"size\":").Append(file.size).Append(",\"sha256\":").Append(Quote(file.hash)).Append('}');}
            manifest.Append("}}");Directory.CreateDirectory(destination);
            string core=Path.Combine(destination,"DarwinCore.zip");
            using(var stream=new FileStream(core,FileMode.CreateNew))
            using(var archive=new ZipArchive(stream,ZipArchiveMode.Create))
            {
                using(var writer=new StreamWriter(archive.CreateEntry("manifest.json").Open(),new UTF8Encoding(false)))writer.Write(manifest.ToString());
                foreach(var file in files){var entry=archive.CreateEntry(file.name,CompressionLevel.Optimal);using var output=entry.Open();using var input=File.OpenRead(file.path);input.CopyTo(output);}
            }
            string command=File.ReadAllText("Server/PortableStart.cmd").TrimEnd()+"\r\n"+File.ReadAllText("Server/Bootstrap.ps1");
            File.WriteAllText(Path.Combine(destination,"Start.cmd"),command,new UTF8Encoding(false));
            // ★ 新增：下载包解压后恰好两个入口文件；校验清单位于核心内部，不要求用户手动管理依赖目录。
            string bundle=destination+".zip";
            using(var stream=new FileStream(bundle,FileMode.CreateNew))
            using(var archive=new ZipArchive(stream,ZipArchiveMode.Create))
                foreach(string name in new[]{"DarwinCore.zip","Start.cmd"}){var entry=archive.CreateEntry(name,CompressionLevel.NoCompression);using var output=entry.Open();using var input=File.OpenRead(Path.Combine(destination,name));input.CopyTo(output);}
            Debug.Log("[ServerCorePackage] "+bundle+" coreSHA256="+Hash(core)+" files="+files.Length);return bundle;
        }
    }
}
