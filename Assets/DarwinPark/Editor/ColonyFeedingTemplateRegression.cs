using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    // ★ 新增：繁忙分工必须真正切换补给且能量守恒；模板另起编辑器进程验收，不能只检查静态列表。
    public static class ColonyFeedingTemplateRegression
    {
        private static string Folder=>Path.GetFullPath("Artifacts/ColonyFeedingTemplateReview");
        private static readonly List<string> Passed=new List<string>();
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);Passed.Add(message);}
        private static float Food(AntColonyMember member)
        {var stats=member.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);return food+reserve;}
        private static void Fill(AntColonyMember member,float value)
        {var stats=member.GetComponent<EntityStats>();stats.TryGet("nutrient",out float old);stats.Modify("nutrient",value-old,false);stats.TryGet("storedNutrient",out old);stats.Modify("storedNutrient",-old,false);}
        public static void Verify(AntColonyMember queen,AntColonyMember worker,AntColonyMember soldier,AntColonyMember young,ColonyResource nursery,ColonyResource supply)
        {
            Directory.CreateDirectory(Folder);Passed.Clear();
            var members=new[]{queen,worker,soldier,young};var positions=members.Select(m=>m.transform.position).ToArray();
            var stats=members.Select(m=>m.GetComponent<EntityStats>().CaptureValues()).ToArray();
            var states=members.Select(m=>m.Capture()).ToArray();float nurseryEnergy=nursery.State.energy,supplyEnergy=supply.State.energy;
            nursery.State.energy=supply.State.energy=1000;
            try
            {
                foreach(var member in new[]{queen,young})
                {
                    Fill(member,0);member.transform.position=supply.transform.position;
                    member.Destination(out var point,out var target,out _);
                    // ★ 适配：业务要求进入可实际进食的半径，不再要求多个身体共用资源中心点。
                    Check(target==null&&EcologyQueries.Distance(point,nursery.transform.position)<=AntColonySettings.Current.feedRadius,"饥饿"+member.Role+"主动走向育幼能量块");
                    member.transform.position=nursery.transform.position;float energy=nursery.State.energy;member.Advance(.1f);
                    Check(Food(member)>0&&Mathf.Abs(energy-nursery.State.energy-Food(member))<.001f,"育幼能量经实际分工更新进入"+member.Role+"且守恒");
                }
                // ★ 新增：先到的幼体仍能进食，但不能吃光减速王后尚未抵达的最后口粮。
                Fill(queen,0);Fill(young,0);young.transform.position=nursery.transform.position;
                nursery.State.energy=AntColonySettings.Current.queenFeedThreshold+AntColonySettings.Current.memberFeedThreshold;
                ColonyResource.Feed(young,1);ColonyResource.Feed(young,1);
                Check(Food(young)>0&&nursery.State.energy>=AntColonySettings.Current.queenFeedThreshold,"幼体先抵达时保留饥饿王后的最低口粮");
                queen.transform.position=nursery.transform.position;ColonyResource.Feed(queen,.1f);
                Check(Food(queen)>0,"王后抵达后可领取预留口粮");nursery.State.energy=1000;
                // 存在猎物时，饥饿指令必须比猎物优先，不能只验证空闲士兵。
                Fill(soldier,0);typeof(AntColonyMember).GetField("HuntTarget",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(soldier,worker.Entity);
                soldier.Destination(out var feedingPoint,out var feedingTarget,out _);
                Check(feedingTarget==null&&EcologyQueries.Distance(feedingPoint,supply.transform.position)<=AntColonySettings.Current.feedRadius,"已有追猎目标时士兵先前往补给");
                typeof(AntColonyMember).GetField("HuntTarget",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(soldier,null);
                var corpse=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=worker.GroupId,kind=ColonyResourceKind.Corpse,energy=12,local=EcologyQueries.ToLocal(worker.transform.position),scale=Vector3.one});
                Fill(worker,10);worker.State.resourceTarget=corpse.State.id;worker.State.scan=10;worker.Advance(.1f);
                Check(worker.State.carriedResource==corpse.State.id,"补给测试建立真实尸体运输");
                worker.State.deliveryPoint=EcologyQueries.ToLocal(nursery.transform.position);Fill(worker,0);
                worker.transform.position=positions[2];worker.Advance(.1f);worker.Destination(out feedingPoint,out _,out _);
                Check(worker.Activity==ColonyActivity.Feed&&EcologyQueries.Distance(feedingPoint,supply.transform.position)<=AntColonySettings.Current.feedRadius&&worker.State.carriedResource==corpse.State.id&&corpse.State.claim==worker.Id,"饥饿工匠暂停运输去补给并保留独占货物");
                worker.transform.position=supply.transform.position;worker.Advance(1);worker.Advance(1);
                Check(Food(worker)>AntColonySettings.Current.memberFeedThreshold,"繁忙工匠通过实际分工更新吃饱");
                worker.Destination(out var delivery,out _,out _);Check(delivery==EcologyQueries.ToWorld(worker.State.deliveryPoint),"工匠吃饱后自动恢复原运输目的地");
                worker.transform.position=delivery;worker.Advance(.1f);Check(ColonyResource.Find(corpse.State.id)==null&&string.IsNullOrEmpty(worker.State.carriedResource),"补给后运输完成且尸体仅转换一次");
                // 持续结算代谢和补给，验证不只是单次手动调用 Feed 成功。
                Fill(soldier,0);soldier.transform.position=supply.transform.position;
                var vitals=soldier.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>();var advance=typeof(DarwinPark.Entities.Vitals.EntityVitals).GetMethod("AdvanceVitals",BindingFlags.NonPublic|BindingFlags.Instance);
                soldier.GetComponent<EntityStats>().TryGet("health",out float health);
                for(int i=0;i<1200;i++){soldier.Advance(.1f);advance.Invoke(vitals,new object[]{.1f});}
                soldier.GetComponent<EntityStats>().TryGet("health",out float afterHealth);
                Check(Food(soldier)>0&&afterHealth>=health,"士兵营养基因受抑仍连续120秒吸收补给且不因饥饿掉血");
                // ★ 适配：灭绝种群的资源允许回收，外群保护必须用实际存活的拥有者验收。
                Fill(soldier,0);string queenGroup=queen.GroupId,foreignGroup=Guid.NewGuid().ToString("N");
                queen.Population.JoinColony(foreignGroup,true);supply.State.group=foreignGroup;
                ColonyResource.Feed(soldier,1);Check(Food(soldier)==0,"不能吸收存活外群的能量块");
                queen.Population.JoinColony(queenGroup,true);supply.State.group=worker.GroupId;
                var savedName=worker.Entity.DisplayName;string name="模板重启验证-"+Guid.NewGuid().ToString("N");worker.Entity.DisplayName=name;
                EntityTemplateManager.SaveTemplate(worker.Entity);EntityTemplateManager.SaveTemplate(worker.Entity);worker.Entity.DisplayName=savedName;
                Check(File.Exists(EntityTemplateStore.FilePath)&&EntityTemplateManager.Templates.Count(t=>t.templateName==name)==1,"模板实际落盘且同名保存不重复");
                File.WriteAllText(Path.Combine(Folder,"fixture.json"),JsonUtility.ToJson(EntityTemplateManager.Templates.Single(t=>t.templateName==name)));
                VerifyStoreRecovery();
                File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));
                Debug.Log("[ColonyFeedingTemplateReview] PASS: "+Passed.Count+" checks");
            }
            finally
            {
                for(int i=0;i<members.Length;i++){members[i].transform.position=positions[i];members[i].GetComponent<EntityStats>().RestoreValues(stats[i]);members[i].Restore(states[i]);}
                nursery.State.energy=nurseryEnergy;supply.State.energy=supplyEnergy;
            }
        }
        private static void VerifyStoreRecovery()
        {
            string path=Path.Combine(Folder,"store-recovery.json");foreach(string suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
            var item=new EntityTemplateData{templateName="备份验证",genes=new List<GeneInstanceData>{new GeneInstanceData{definitionId="gene_population",value=20,tier=1}}};
            EntityTemplateStore.Save(new List<EntityTemplateData>{item},path);item.genes[0].value=21;EntityTemplateStore.Save(new List<EntityTemplateData>{item},path);
            File.WriteAllText(path,"{invalid");Check(EntityTemplateStore.Load(path).Single().genes[0].value==20,"主模板文件损坏可读取有效备份");
            EntityTemplateStore.Save(EntityTemplateStore.Load(path),path);Check(EntityTemplateStore.Load(path).Count==1&&EntityTemplateStore.Load(path+".bak").Count==1,"备份恢复后再次保存保留有效备份");
        }
        public static void VerifyAfterRestart()
        {
            try
            {
                var expected=JsonUtility.FromJson<EntityTemplateData>(File.ReadAllText(Path.Combine(Folder,"fixture.json")));
                var actual=EntityTemplateManager.Templates.Single(t=>t.templateName==expected.templateName);
                if(JsonUtility.ToJson(expected)!=JsonUtility.ToJson(actual))throw new InvalidOperationException("重启后模板基因或名称变化");
                File.WriteAllText(Path.Combine(Folder,"restart.txt"),"PASS: 独立Unity进程从默认模板路径恢复名称、图标及全部基因数值/层级");
                // 只移除本次测试的唯一命名模板，保留其他模板。
                EntityTemplateStore.Save(EntityTemplateManager.Templates.Where(t=>t.templateName!=expected.templateName).ToList());
                Debug.Log("[TemplateRestartReview] PASS");EditorApplication.Exit(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(Folder,"restart.txt"),"FAIL\n"+ex);Debug.LogException(ex);EditorApplication.Exit(1);}
        }
    }
}
