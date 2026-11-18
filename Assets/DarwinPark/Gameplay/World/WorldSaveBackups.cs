using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：离线完整世界备份，包括存档内模板和附件；完成校验后才发布，恢复始终生成新世界。
    public static class WorldSaveBackups
    {
        [Serializable]private sealed class Item {public string name,hash;public long size;}
        [Serializable]private sealed class Manifest {public int version=1;public string worldId,source,createdUtc;public List<Item> files=new();}
        private static string Hash(string file){using var input=File.OpenRead(file);using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");}
        private static string Root(string root)=>Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        private static string CheckedUnder(string root,string path)
        {path=Path.GetFullPath(path);if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("备份路径越界");return path;}
        public static string Create(string path,string activeId=null)=>CreateFrom(GamePaths.Saves,path,activeId);
        public static string CreateFrom(string root,string path,string activeId=null)
        {
            root=Root(root);path=Path.GetFullPath(path);string id=WorldSaveManagement.CheckTarget(root,path,activeId,out bool directory);
            string backups=Path.Combine(root,".backups");Directory.CreateDirectory(backups);WorldSaveManagement.RejectLink(backups);
            string key=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"-"+Guid.NewGuid().ToString("N"),stage=CheckedUnder(backups,Path.Combine(backups,".partial-"+key)),destination=CheckedUnder(backups,Path.Combine(backups,key));
            string world=Path.Combine(stage,"world");Directory.CreateDirectory(world);
            if(directory)CopyTree(Path.GetDirectoryName(path),world);
            else
            {
                CopyFile(path,Path.Combine(world,"world.json"));foreach(string suffix in new[]{".bak",".tmp"})if(File.Exists(path+suffix))CopyFile(path+suffix,Path.Combine(world,"world.json"+suffix));
                // 旧单文件世界也可能已保存本地模板，归一化备份时一并收纳其附件。
                if(Guid.TryParseExact(id,"N",out _)){string sidecar=Path.Combine(root,id);if(Directory.Exists(sidecar))CopyTree(sidecar,world,true);}
            }
            var manifest=new Manifest{worldId=id,source=path,createdUtc=DateTime.UtcNow.ToString("O")};
            foreach(string file in Files(world))manifest.files.Add(new Item{name=Path.GetRelativePath(world,file).Replace('\\','/'),size=new FileInfo(file).Length,hash=Hash(file)});
            File.WriteAllText(Path.Combine(stage,"backup.json"),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));Verify(stage);
            // ★ 修复（防Bug回归）：源/目标都是已检查的备份根目录子项，半成品不能被最新备份查询选中。
            Directory.Move(stage,destination);return destination;
        }
        private static IEnumerable<string> Files(string root)
        {
            var pending=new Stack<string>();pending.Push(root);
            while(pending.Count>0){string directory=pending.Pop();WorldSaveManagement.RejectLink(directory);foreach(string child in Directory.GetDirectories(directory))pending.Push(child);foreach(string file in Directory.GetFiles(directory)){WorldSaveManagement.RejectLink(file);yield return file;}}
        }
        private static void CopyFile(string source,string target)
        {
            WorldSaveManagement.RejectLink(source);Directory.CreateDirectory(Path.GetDirectoryName(target));
            using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);input.CopyTo(output);output.Flush(true);
        }
        private static void CopyTree(string source,string target,bool skipWorld=false)
        {foreach(string file in Files(source)){string relative=Path.GetRelativePath(source,file);if(skipWorld&&new[]{"world.json","world.json.bak","world.json.tmp"}.Contains(relative))continue;CopyFile(file,Path.Combine(target,relative));}}
        private static Manifest Verify(string directory)
        {
            WorldSaveManagement.RejectLink(directory);var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(directory,"backup.json"),Encoding.UTF8));
            if(manifest==null||manifest.version!=1||manifest.files==null||manifest.files.Count==0)throw new IOException("备份清单无效");
            string world=Path.Combine(directory,"world");var actual=Files(world).ToArray();if(actual.Length!=manifest.files.Count)throw new IOException("备份文件数量不匹配");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in manifest.files)
            {
                string file=CheckedUnder(world,Path.Combine(world,item.name));
                if(!seen.Add(file)||!File.Exists(file)||new FileInfo(file).Length!=item.size||Hash(file)!=item.hash)throw new IOException("备份内容校验失败："+item.name);
            }
            if(!File.Exists(Path.Combine(world,"world.json")))throw new IOException("备份缺少世界存档");return manifest;
        }
        public static string Latest(string path,string id=null)=>LatestFrom(GamePaths.Saves,path,id);
        public static string LatestFrom(string root,string path,string id=null)
        {
            string backups=Path.Combine(Root(root),".backups");if(!Directory.Exists(backups))return null;WorldSaveManagement.RejectLink(backups);
            foreach(string directory in Directory.GetDirectories(backups).Where(d=>!Path.GetFileName(d).StartsWith(".")).OrderByDescending(Path.GetFileName))
            {
                try{WorldSaveManagement.RejectLink(directory);var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(directory,"backup.json")));if(manifest?.version==1&&(!string.IsNullOrEmpty(id)?manifest.worldId==id:string.Equals(manifest?.source,Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)))return directory;}
                catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is ArgumentException){Debug.LogWarning("[备份] 跳过损坏清单："+error.Message);}
            }
            return null;
        }
        public static WorldSaveData RestoreCopy(string backup)=>RestoreCopyTo(GamePaths.Saves,backup);
        public static WorldSaveData RestoreCopyTo(string root,string backup)
        {
            root=Root(root);WorldSaveManagement.RejectLink(root);string backups=Path.Combine(root,".backups");WorldSaveManagement.RejectLink(backups);backup=CheckedUnder(backups,backup);if(!string.Equals(Path.GetDirectoryName(backup),backups,StringComparison.OrdinalIgnoreCase))throw new IOException("只能恢复当前存档目录的完整备份");Verify(backup);
            var data=WorldSaveStore.Read(Path.Combine(backup,"world","world.json"));data.id=Guid.NewGuid().ToString("N");data.name+="（备份恢复副本）";data.running=false;
            string stage=CheckedUnder(backups,Path.Combine(backups,".restore-"+data.id)),destination=CheckedUnder(root,Path.Combine(root,data.id));Directory.CreateDirectory(stage);CopyTree(Path.Combine(backup,"world"),stage);
            WorldSaveStore.Write(data,Path.Combine(stage,"world.json"));
            // ★ 修复（防Bug回归）：发布为新标识目录，永远不覆盖原世界；模板附件随目录一起恢复。
            Directory.Move(stage,destination);return data;
        }
    }
}
