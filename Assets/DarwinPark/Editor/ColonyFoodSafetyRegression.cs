using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    // ★ 新增：验证有粮不吃的类别、远锁和迁居复现，以及真实角色更新的长期进食、索引生命周期与能量守恒。
    public static class ColonyFoodSafetyRegression
    {
        private static readonly List<string> Passed=new();
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);Passed.Add(text);}
        private static void Fill(AntColonyMember member,float food,float reserve=0)
        {var stats=member.GetComponent<EntityStats>();stats.TryGet("nutrient",out var f);stats.TryGet("storedNutrient",out var r);stats.Modify("nutrient",food-f,false);stats.Modify("storedNutrient",reserve-r,false);member.State.plan=new EntityTaskPlanSave();}
        private static float Food(AntColonyMember member)
        {var stats=member.GetComponent<EntityStats>();stats.TryGet("nutrient",out var food);stats.TryGet("storedNutrient",out var reserve);return food+reserve;}
        public static void Verify(AntColonyMember queen,AntColonyMember worker,AntColonyMember soldier,AntColonyMember young)
        {
            Passed.Clear();var tuning=GameplayTuning.Current;bool backup=tuning.colonyShareBackupFood;var members=new[]{queen,worker,soldier,young};string group=queen.GroupId;
            var positions=members.Select(m=>m.transform.position).ToArray();var states=members.Select(m=>m.Capture()).ToArray();var values=members.Select(m=>m.GetComponent<EntityStats>().CaptureValues()).ToArray();var ages=members.Select(m=>m.Genes.CurrentAge).ToArray();
            var originals=ColonyResource.All.ToArray();foreach(var resource in originals)resource.gameObject.SetActive(false);var created=new List<ColonyResource>();
            ColonyResource Make(ColonyResourceKind kind,float energy,Vector3 point,string owner=null)
            {var resource=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=owner??group,kind=kind,energy=energy,local=EcologyQueries.ToLocal(point),scale=Vector3.one});created.Add(resource);return resource;}
            void Clear(){foreach(var resource in created)if(resource!=null)resource.Remove();created.Clear();foreach(var member in members)member.State.plan=new EntityTaskPlanSave();}
            Vector3 here=positions[0];var up=EcologyQueries.Up(here);var tangent=Vector3.Cross(up,Mathf.Abs(up.y)>.9f?Vector3.right:Vector3.up).normalized;
            Vector3 At(float distance)=>EcologyQueries.Surface(here+tangent*distance);
            try
            {
                bool invalidThreshold=false;try{GameplayTuning.Parse("{\"version\":1,\"colonyEmergencyFeedThreshold\":11}");}catch(InvalidDataException){invalidThreshold=true;}Check(invalidThreshold,"拒绝超过饱食容量的紧急补给线");
                tuning.colonyShareBackupFood=true;foreach(var member in members)member.transform.position=here;Fill(queen,10,100);
                var nursery=Make(ColonyResourceKind.Nursery,100,here);Fill(soldier,0);float energy=nursery.State.energy;ColonyResource.Feed(soldier,1);
                Check(Food(soldier)>0&&Mathf.Abs(energy-nursery.State.energy-Food(soldier))<.001f,"成年士兵只有育幼块时实际吸收且能量守恒");
                Fill(worker,0);ColonyResource.Feed(worker,1);Check(Food(worker)>0,"工匠没有成年补给点时可借用育幼块");
                Clear();var supply=Make(ColonyResourceKind.Supply,100,here);Fill(queen,0);ColonyResource.Feed(queen,1);Check(Food(queen)>0,"王后只有成年补给时可以实际进食");
                Fill(young,0);ColonyResource.Feed(young,1);Check(Food(young)>0,"幼体只有成年补给时可以实际进食");
                nursery=Make(ColonyResourceKind.Nursery,100,here);Fill(queen,0);Fill(soldier,0);
                Check(ColonyFeeding.Find(queen,false)==nursery&&ColonyFeeding.Find(soldier,false)==supply,"两类补给均可用时保留各分工的专用点优先");
                Clear();supply=Make(ColonyResourceKind.Supply,100,At(35));var local=Make(ColonyResourceKind.Supply,100,here);Fill(soldier,0);soldier.State.plan=new EntityTaskPlanSave{feeding=true,supply=supply.State.id};energy=local.State.energy;
                ColonyResource.Feed(soldier,1);Check(Food(soldier)>0&&local.State.energy<energy&&supply.State.energy==100,"远处锁定补给不阻止吃身边真实能量块");
                var corpses=new List<ColonyResource>();for(int i=0;i<300;i++)corpses.Add(Make(ColonyResourceKind.Corpse,10,At(60)));
                Check(ColonyFoodSources.Count==2,"300具尸体不进入进食候选索引");
                local.transform.position=At(AntColonySettings.Current.feedRadius+2);local.State.kind=ColonyResourceKind.Nursery;Fill(queen,10,100);Fill(soldier,1);soldier.State.plan=new EntityTaskPlanSave{feeding=true,supply=supply.State.id};
                Check(ColonyFeeding.Find(soldier,false)==local,"饱食危急时解除远处计划，选择进食半径外更近的备用块");
                Fill(soldier,8);soldier.State.plan=new EntityTaskPlanSave{feeding=true,supply=supply.State.id};Check(ColonyFeeding.Find(soldier,false)==supply,"饱食未危急时保持有效远处计划，不反复抢占目标");
                local.transform.position=here;local.State.kind=ColonyResourceKind.Supply;
                long visits=ColonyFoodSources.CandidateVisits;Fill(soldier,0);ColonyFeeding.Find(soldier,false);
                Check(ColonyFoodSources.CandidateVisits-visits<=4,"补给候选访问只随能量块增长，不逐次遍历尸体");
                local.gameObject.SetActive(false);Check(ColonyFoodSources.Count==1,"停用资源立即从能量索引移除");local.gameObject.SetActive(true);Check(ColonyFoodSources.Count==2,"重新启用能量块补回索引且不重复");local.Remove();Check(ColonyFoodSources.Count==1,"耗尽或移除的能量块不保留陈旧索引");
                Clear();nursery=Make(ColonyResourceKind.Nursery,100,here);Fill(queen,10,100);Fill(soldier,0);tuning.colonyShareBackupFood=false;
                Check(ColonyFeeding.Find(soldier,false)==null,"外部开关关闭时可恢复严格专用补给");tuning.colonyShareBackupFood=true;
                Fill(queen,0);Fill(soldier,0);nursery.State.energy=AntColonySettings.Current.queenFeedThreshold+1;ColonyResource.Feed(soldier,10);
                Check(nursery.State.energy>=AntColonySettings.Current.queenFeedThreshold&&Food(soldier)>0,"成年成员借用育幼块仍预留饥饿王后的最低口粮");
                ColonyResource.Feed(queen,1);Check(Food(queen)>0,"王后抵达后可以吃预留口粮");
                Clear();string foreign=Guid.NewGuid().ToString("N");queen.Population.JoinColony(foreign,true);supply=Make(ColonyResourceKind.Supply,100,here,foreign);Fill(soldier,0);ColonyResource.Feed(soldier,1);
                Check(Food(soldier)==0&&supply.State.energy==100,"备用进食不偷取仍有存活成员的外群补给");queen.Population.JoinColony(group,true);
                supply.State.group=Guid.NewGuid().ToString("N");ColonyResource.Feed(soldier,1);Check(Food(soldier)>0&&supply.State.group==group,"废弃补给在真实吸收时领取并恢复归属");
                Clear();supply=Make(ColonyResourceKind.Supply,100,At(15));Fill(queen,0);queen.State.founding=true;queen.State.foundingPoint=EcologyQueries.ToLocal(At(-45));
                queen.Destination(out var destination,out _,out _);Check(EcologyQueries.Distance(destination,supply.transform.position)<=AntColonySettings.Current.feedRadius&&queen.Activity==ColonyActivity.Feed,"迁居中王后先前往可用补给而不是继续饿着赶路");
                Check(EntityTaskPlan.Priority(ColonyActivity.Feed)>EntityTaskPlan.Priority(ColonyActivity.Founding),"补给优先级高于建群迁居");
                var founding=queen.State.foundingPoint;Fill(queen,10,100);queen.Destination(out destination,out _,out _);
                Check(queen.State.founding&&queen.State.foundingPoint==founding&&destination==EcologyQueries.ToWorld(founding),"吃饱后恢复原建群目标，不丢失迁居计划");queen.State.founding=false;
                Clear();supply=Make(ColonyResourceKind.Supply,10000,here);
                foreach(var member in members){member.transform.position=here;Fill(member,0);member.State.reproductionCooldown=10000;member.State.reproductionTimer=10000;}
                var health=members.Select(m=>{m.GetComponent<EntityStats>().TryGet("health",out var hp);return hp;}).ToArray();
                for(int tick=0;tick<1200;tick++)foreach(var member in members){member.Advance(.1f);member.GetComponent<EntityVitals>().AdvanceShared(.1f);}
                Check(members.All(m=>Food(m)>0)&&members.Select((m,i)=>m.GetComponent<EntityStats>().TryGet("health",out var hp)&&hp>=health[i]).All(ok=>ok),"王后幼体工匠士兵连续120秒真实分工和代谢更新均能吃单类补给且不饿死");
                Check(supply.State.energy>0&&supply.State.energy<10000,"连续补给实际扣减有限能量，没有凭空恢复饱食度");
                Clear();Check(ColonyFoodSources.Count==0,"测试资源清场后食物索引不残留");
                string folder=Path.GetFullPath("Artifacts/ColonyFoodSafetyReview");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));
            }
            finally
            {
                Clear();queen.Population.JoinColony(group,true);tuning.colonyShareBackupFood=backup;
                for(int i=0;i<members.Length;i++){members[i].transform.position=positions[i];members[i].Genes.CurrentAge=ages[i];members[i].GetComponent<EntityStats>().RestoreValues(values[i]);members[i].Restore(states[i]);}
                foreach(var resource in originals)if(resource!=null)resource.gameObject.SetActive(true);
            }
        }
    }
}
