using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：隔离项目中验证世界模板分区、旧资料显式导入、完整备份与新副本恢复，不操作真实用户资料。
    public static class WorldStorageRegression
    {
        [Serializable]private sealed class Record{public string a,b,nameA,nameB;}
        private static readonly List<string> Passed=new();private static string Folder=>Path.GetFullPath("Artifacts/WorldStorageReview");
        private static string Hash(string file){using var input=File.OpenRead(file);using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(input));}
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Passed.Add(message);}
        private static void Reject(Action action,string message){bool rejected=false;try{action();}catch(IOException){rejected=true;}catch(InvalidOperationException){rejected=true;}Check(rejected,message);}
        public static void RunBatch()
        {
            Directory.CreateDirectory(Folder);
            try
            {
                var data=WorldSaveStore.Read("Artifacts/WaterHazardReview/world-input.json");string inputHash=Hash("Artifacts/WaterHazardReview/world-input.json");
                var record=new Record{a=Guid.NewGuid().ToString("N"),b=Guid.NewGuid().ToString("N"),nameA="A-"+Guid.NewGuid().ToString("N"),nameB="B-"+Guid.NewGuid().ToString("N")};
                data.id=record.a;WorldSaveStore.Write(data);var other=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(data));other.id=record.b;WorldSaveStore.Write(other);
                var go=new GameObject("TemplateStorageFixture");go.AddComponent<SphereCollider>();var genes=go.AddComponent<EntityGeneController>();genes.Genome=GeneMutator.CreateBaseGenome();var actor=go.AddComponent<DarwinEntity>();
                EntityTemplateManager.UseWorld(record.a);actor.DisplayName=record.nameA;EntityTemplateManager.SaveTemplate(actor);string fileA=EntityTemplateStore.FilePath;string templateA=File.ReadAllText(fileA);
                Check(EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameA)&&fileA.Contains(record.a),"模板写入当前世界目录");
                EntityTemplateManager.UseWorld(record.b);Check(EntityTemplateManager.Templates.Count==0,"新世界不继承另一个世界的模板");actor.DisplayName=record.nameB;EntityTemplateManager.SaveTemplate(actor);
                Check(EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameB)&&!EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameA),"第二个世界拥有独立模板集合");
                EntityTemplateManager.UseWorld(record.a);Check(EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameA)&&!EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameB),"切回原世界恢复原模板且不串档");
                string path=WorldSaveStore.FileFor(record.a),hash=Hash(path),attachment=Path.Combine(Path.GetDirectoryName(path),"attachment.bin");File.WriteAllText(attachment,"complete attachment");
                Reject(()=>WorldSaveBackups.Create(path,record.a),"使用中的世界不能离线备份");
                string backup=WorldSaveBackups.Create(path);Check(File.Exists(Path.Combine(backup,"world","templates","entities.json"))&&File.Exists(Path.Combine(backup,"world","attachment.bin")),"备份包含当前世界模板及附件");
                Check(Hash(Path.Combine(backup,"world","world.json"))==hash&&Hash(path)==hash,"备份保留世界字节且不改原存档");
                Check(WorldSaveBackups.Latest(path,record.a)==backup,"最新备份查询找到完整发布的备份");
                using(var held=new FileStream(attachment,FileMode.Open,FileAccess.Read,FileShare.None))Reject(()=>WorldSaveBackups.Create(path),"附件复制中途失败时不发布不完整备份");
                Check(WorldSaveBackups.Latest(path,record.a)==backup&&Hash(path)==hash,"失败备份不覆盖最新有效备份或原档");
                var restored=WorldSaveBackups.RestoreCopy(backup);Check(restored.id!=record.a&&!restored.running&&restored.entities.Count==data.entities.Count&&restored.seed==data.seed&&restored.gameTime==data.gameTime,"恢复产生新标识并保留实体、种子和日期，默认暂停");
                EntityTemplateManager.UseWorld(restored.id);Check(EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameA)&&!EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameB),"恢复副本携带原世界模板并继续独立保存");
                Check(Hash(path)==hash&&WorldSaveStore.List().All(e=>!e.path.Contains(".backups")),"恢复不覆盖原世界，备份和半成品不进入世界列表");
                File.AppendAllText(Path.Combine(backup,"world","templates","entities.json"),"tamper");Reject(()=>WorldSaveBackups.RestoreCopy(backup),"校验发现模板附件损坏时拒绝恢复");
                // ★ 新增：旧单文件世界的本地模板也必须一起备份、归档和恢复，不能遗留跨档旁路文件。
                var legacyWorld=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(data));legacyWorld.id=Guid.NewGuid().ToString("N");string legacyPath=Path.Combine(GamePaths.Saves,"legacy-"+legacyWorld.id+".json");WorldSaveStore.Write(legacyWorld,legacyPath);
                string legacyTemplate=EntityTemplateStore.ForWorld(legacyWorld.id);Directory.CreateDirectory(Path.GetDirectoryName(legacyTemplate));File.WriteAllText(legacyTemplate,templateA);
                string legacyBackup=WorldSaveBackups.Create(legacyPath);Check(File.ReadAllText(Path.Combine(legacyBackup,"world","templates","entities.json"))==templateA,"旧单文件世界备份包含标识目录内模板");
                string archived=WorldSaveManagement.Archive(legacyPath);Check(!File.Exists(legacyPath)&&!File.Exists(legacyTemplate)&&File.Exists(Path.Combine(archived,legacyWorld.id,"templates","entities.json")),"旧单文件世界归档时模板附件同步移走");
                var legacyRestored=WorldSaveBackups.RestoreCopy(legacyBackup);Check(File.ReadAllText(EntityTemplateStore.ForWorld(legacyRestored.id))==templateA,"原世界归档后独立备份仍可恢复模板副本");
                Check(Hash(path)==hash&&inputHash==Hash("Artifacts/WaterHazardReview/world-input.json"),"全部操作保留原世界与测试输入");
                File.WriteAllText(Path.Combine(Folder,"scope-fixture.json"),JsonUtility.ToJson(record));Object.DestroyImmediate(go);EntityTemplateManager.UseWorld(null);Check(EntityTemplateManager.Templates.Count==0,"退出世界后不遗留上一世界模板");
                File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));EditorApplication.Exit(0);
            }
            catch(Exception error){File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL\n"+error+"\n"+string.Join("\n",Passed));Debug.LogException(error);EditorApplication.Exit(1);}
        }
        public static void VerifyAfterRestart()
        {
            try
            {
                var record=JsonUtility.FromJson<Record>(File.ReadAllText(Path.Combine(Folder,"scope-fixture.json")));Check(EntityTemplateManager.Templates.Count==0,"独立进程启动时不挂载全局模板");
                EntityTemplateManager.UseWorld(record.a);Check(EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameA)&&!EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameB),"独立进程按世界 A 标识恢复模板");
                EntityTemplateManager.UseWorld(record.b);Check(EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameB)&&!EntityTemplateManager.Templates.Any(t=>t.templateName==record.nameA),"独立进程按世界 B 标识恢复模板");
                File.WriteAllText(Path.Combine(Folder,"restart.txt"),"PASS\n"+string.Join("\n",Passed));EditorApplication.Exit(0);
            }
            catch(Exception error){File.WriteAllText(Path.Combine(Folder,"restart.txt"),"FAIL\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
