using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：删除存档实际移出列表并完整归档，保留附件/备份；不在 UI 层执行目录操作。
    public static class WorldSaveManagement
    {
        [Serializable] private sealed class Identity {public string id;}
        private static bool Same(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
        internal static void RejectLink(string path)
        {if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("存档路径不能是目录链接："+path);}
        public static string Archive(string path,string activeId=null)=>ArchiveFrom(DarwinPark.Core.GamePaths.Saves,path,activeId);
        public static string ArchiveFrom(string root,string path,string activeId=null)
        {
            root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);path=Path.GetFullPath(path);
            string id=CheckTarget(root,path,activeId,out bool directory);string parent=Path.GetDirectoryName(path);
            var sources=new List<string>();if(directory)sources.Add(parent);else AddFiles(path,sources);
            // ★ 适配：旧单文件世界新建的局部模板保存在标识目录，归档时须随世界一起移动。
            if(!directory&&Guid.TryParseExact(id,"N",out _)){string sidecar=Path.Combine(root,id);if(Directory.Exists(sidecar)&&!File.Exists(Path.Combine(sidecar,"world.json"))){RejectLink(sidecar);sources.Add(sidecar);}}
            // ★ 修复（防Bug回归）：旧单文件和目录存档可能同时存在；一起归档，防止删除后旧副本重新出现在列表。
            if(!string.IsNullOrEmpty(id))foreach(string legacy in Directory.GetFiles(root,"*.json"))
            {
                if(Same(legacy,path))continue;RejectLink(legacy);
                try{if(Same(JsonUtility.FromJson<Identity>(File.ReadAllText(legacy))?.id,id))AddFiles(legacy,sources);}catch(ArgumentException){}
            }
            string archiveRoot=Path.Combine(root,".deleted");if(Directory.Exists(archiveRoot))RejectLink(archiveRoot);
            string destination=Path.Combine(archiveRoot,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
            string prefix=root+Path.DirectorySeparatorChar;
            // ★ 修复（防Bug回归）：在移动前逐个校验绝对路径；源和归档目标必须都位于同一存档根目录内。
            foreach(string source in sources)if(!Path.GetFullPath(source).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("存档路径越界");
            if(!Path.GetFullPath(destination).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("归档路径越界");
            Directory.CreateDirectory(destination);var moved=new List<(string source,string target,bool directory)>();
            try
            {
                foreach(string source in sources)
                {
                    RejectLink(source);string target=Path.Combine(destination,Path.GetFileName(source));bool isDirectory=Directory.Exists(source);
                    if(isDirectory)Directory.Move(source,target);else File.Move(source,target);moved.Add((source,target,isDirectory));
                }
            }
            catch(Exception error)
            {
                // ★ 修复（防Bug回归）：多文件归档中途失败，尽力退回已移动项；不以失败提示掩盖半删除。
                var rollback=new List<Exception>();for(int i=moved.Count-1;i>=0;i--)try{var item=moved[i];if(item.directory)Directory.Move(item.target,item.source);else File.Move(item.target,item.source);}catch(Exception failure){rollback.Add(failure);}
                if(rollback.Count>0)throw new IOException("删除未完成，部分文件保留于："+destination,new AggregateException(rollback));
                throw new IOException("删除未完成，原存档已保留："+error.Message,error);
            }
            return destination;
        }
        // ★ 适配：备份和删除共用世界路径边界及使用中保护，不各自维护一套容易偏离的检查。
        internal static string CheckTarget(string root,string path,string activeId,out bool directory)
        {
            string parent=Path.GetDirectoryName(path);directory=Same(Path.GetFileName(path),"world.json")&&Same(Path.GetDirectoryName(parent),root);
            if(directory&&(Same(Path.GetFileName(parent),".deleted")||Same(Path.GetFileName(parent),".backups")))throw new IOException("备份目录不是世界存档");
            if(!directory&&!(Same(parent,root)&&Same(Path.GetExtension(path),".json")))throw new IOException("只能操作存档列表中的世界文件");
            RejectLink(root);if(directory)RejectLink(parent);RejectLink(path);
            string id=null;try{id=JsonUtility.FromJson<Identity>(File.ReadAllText(path))?.id;}catch(ArgumentException){}
            if(!string.IsNullOrEmpty(activeId)&&(Same(activeId,id)||Same(activeId,Path.GetFileNameWithoutExtension(directory?parent:path))))throw new InvalidOperationException("请先保存并退出该世界，再操作其存档");
            return id;
        }
        private static void AddFiles(string file,List<string> files)
        {foreach(string suffix in new[]{"",".bak",".tmp"}){string path=file+suffix;if(File.Exists(path)&&!files.Contains(path))files.Add(path);}}
    }
}
