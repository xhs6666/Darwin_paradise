using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarwinPark.Entities;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    // ★ 新增：所有移动/删除测试仅操作隔离夹具，校验完整附件、旧副本、使用中保护和取消行为。
    public static class SaveDetailsRegression
    {
        private static readonly List<string> Passed=new();
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Passed.Add(message);}
        private static string Hash(string file){using var stream=File.OpenRead(file);using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(stream));}
        private static void Reject(Action action,string message){bool failed=false;try{action();}catch(IOException){failed=true;}catch(InvalidOperationException){failed=true;}Check(failed,message);}
        public static void RunBatch()
        {
            string evidence=Path.GetFullPath("Artifacts/SaveDetailsReview");Directory.CreateDirectory(evidence);
            try
            {
                var source=WorldSaveStore.Read("Artifacts/WaterHazardReview/world-input.json");string sourceHash=Hash("Artifacts/WaterHazardReview/world-input.json");
                string root=Path.Combine(evidence,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
                string folder=Path.Combine(root,source.id);Directory.CreateDirectory(folder);string path=Path.Combine(folder,"world.json");WorldSaveStore.Write(source,path);
                string legacy=Path.Combine(root,source.id+".json");File.Copy(path,legacy);File.Copy(path,legacy+".bak");File.Copy(path,legacy+".tmp");File.Copy(path,path+".bak");File.WriteAllText(Path.Combine(folder,"attachment.txt"),"complete world attachment");
                var neighbor=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(source));neighbor.id=Guid.NewGuid().ToString("N");string neighborPath=Path.Combine(root,neighbor.id,"world.json");WorldSaveStore.Write(neighbor,neighborPath);string neighborHash=Hash(neighborPath);
                var entry=new WorldSaveEntry{path=path,name=source.name,data=source};var details=new WorldSaveDetailsPage();details.Open(entry);
                Check(WorldSaveDetailsPage.Describe(entry).Contains("实体数量："+source.entities.Count),"详情实体数量来自真实存档快照");
                var summaryData=new WorldSaveData{gameTime=49*3600+65,entities=new List<EntitySaveState>{new(){age=AgeState.Infant},new(){age=AgeState.Adult,gestating=true},new(){age=AgeState.Elder,favorite=true}}};
                string summary=WorldSaveDetailsPage.Describe(new WorldSaveEntry{path=path,data=summaryData});
                Check(summary.Contains("幼体 1   成年 1   老年 1")&&summary.Contains("孕育中：1   收藏：1")&&summary.Contains("49:01:05"),"年龄/孕育/收藏统计和超过24小时的游戏时间正确");
                Reject(()=>details.ConfirmDelete(),"未经确认不能删除");
                details.RequestDelete();details.CancelConfirmation();Check(!details.Confirming&&File.Exists(path),"取消确认保留存档");
                Reject(()=>WorldSaveManagement.ArchiveFrom(root,path,source.id),"正在使用的世界禁止删除");Check(File.Exists(path),"使用中保护未移动任何世界文件");
                string outside=Path.Combine(evidence,"outside.json");File.WriteAllText(outside,"do not move");Reject(()=>WorldSaveManagement.ArchiveFrom(root,outside),"拒绝存档根目录外的目标");Check(File.Exists(outside),"边界拒绝保留外部文件");
                // Unity 在 OnGUI 之外将 Event.type 读为 Ignore，文件测试不冒充真实窗口鼠标验收。
                using(var held=new FileStream(legacy+".bak",FileMode.Open,FileAccess.Read,FileShare.Read))Reject(()=>WorldSaveManagement.ArchiveFrom(root,path),"旧备份被占用时中止删除");
                Check(File.Exists(path)&&File.Exists(legacy)&&File.Exists(legacy+".bak"),"多文件移动中途失败回滚，原目录及旧副本仍完整可读");
                string oldHash=Hash(path);string archive=WorldSaveManagement.ArchiveFrom(root,path);
                Check(!File.Exists(path)&&File.Exists(Path.Combine(archive,source.id,"world.json"))&&Hash(Path.Combine(archive,source.id,"world.json"))==oldHash,"存档从列表源移除，完整内容归档保持逐字节一致");
                Check(File.Exists(Path.Combine(archive,source.id,"world.json.bak"))&&File.ReadAllText(Path.Combine(archive,source.id,"attachment.txt"))=="complete world attachment","目录附件及备份完整保留");
                Check(!File.Exists(legacy)&&!File.Exists(legacy+".bak")&&!File.Exists(legacy+".tmp")&&File.Exists(Path.Combine(archive,source.id+".json.bak")),"旧单文件重复档及备份一并归档，不会删除后复现");
                Check(WorldSaveStore.ListFrom(root).Count==1&&Hash(neighborPath)==neighborHash,"刷新列表不显示恢复副本且不影响其他世界");
                string corrupt=Path.Combine(root,"damaged.json");File.WriteAllText(corrupt,"not-json");details.Open(new WorldSaveEntry{path=corrupt,name="损坏夹具"});Check(details.Entry.error!=null&&WorldSaveDetailsPage.Describe(details.Entry).Contains("无法读取"),"损坏档仍能打开详情查看错误");
                string removed=WorldSaveManagement.ArchiveFrom(root,corrupt);Check(!File.Exists(corrupt)&&File.Exists(Path.Combine(removed,"damaged.json")),"损坏存档可以安全移出列表");
                string single=Path.Combine(root,"legacy-only.json");File.Copy(neighborPath,single);string singleArchive=WorldSaveManagement.ArchiveFrom(root,single);Check(File.Exists(Path.Combine(singleArchive,"legacy-only.json"))&&File.Exists(neighborPath),"旧单文件可单独删除且不移动其他目录");
                Check(sourceHash==Hash("Artifacts/WaterHazardReview/world-input.json"),"所有测试均未改写原始输入存档");
                var confirmation=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(source));confirmation.id=Guid.NewGuid().ToString("N");string confirmationPath=WorldSaveStore.FileFor(confirmation.id);WorldSaveStore.Write(confirmation);
                details.Open(new WorldSaveEntry{path=confirmationPath,name=confirmation.name});details.RequestDelete();string confirmedArchive=details.ConfirmDelete();
                Check(details.Entry==null&&!details.Confirming&&!File.Exists(confirmationPath)&&File.Exists(Path.Combine(confirmedArchive,confirmation.id,"world.json")),"详情页二次确认通过正式归档入口完成删除并清理选中状态");
                File.WriteAllText(Path.Combine(evidence,"result.txt"),"PASS\n"+string.Join("\n",Passed));EditorApplication.Exit(0);
            }
            catch(Exception error){File.WriteAllText(Path.Combine(evidence,"result.txt"),"FAIL\n"+error+"\n"+string.Join("\n",Passed));Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
