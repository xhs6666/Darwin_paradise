namespace DarwinPark.UI
{
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;


    // ✦ 新增：只读文本格式化，不创建控件或修改实体。
    internal static class EntityInfoText
    {
        public static string BaseInfo(DarwinEntity entity)
        {
            var genes=entity.GetComponent<EntityGeneController>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<color=#4EE5B5><b>【基础信息】</b></color>");
            var stats = entity.GetComponent<EntityStats>();
            if (stats != null)
            {
                if (stats.TryGet("health", out var hp)) sb.AppendLine($"生命值: <color=#FF6B6B>{hp:F1}</color>");
                if (stats.TryGet("nutrient", out var nut)) sb.AppendLine($"饱食度: <color=#FFD166>{nut:F1}</color>");
                // ★ 删除这一行: if (stats.TryGet("storedNutrient", out var stored)) sb.AppendLine($"储存营养: <color=#FFD166>{stored:F1}</color>");
                if (stats.TryGet("size", out var size)) sb.AppendLine($"体型: <color=#4EE5B5>{size:F2}</color>");
            }

            // ★ 新增：读取实体自带状态显示在基础信息里
            var geneCtrl = genes;
            if (geneCtrl != null)
            {
                string ageStr = "幼年";
                if (geneCtrl.CurrentAge == AgeState.Adult) ageStr = "成年";
                else if (geneCtrl.CurrentAge == AgeState.Elder) ageStr = "老年";
                sb.AppendLine($"生命阶段: <color=#4EE5B5>{ageStr}</color>");
            }

            if (genes.IsGestating) sb.AppendLine("<color=#4EE5B5>特殊状态: 孕育后代中...</color>");
            // ★ 适配：实际活动状态读取独立组件，不再始终显示自由活动。
            sb.AppendLine("当前状态: "+EntityStatePresentation.Current(entity));

            if (PlanetClimateSystem.Instance != null)
            {
                var climate = PlanetClimateSystem.Instance.GetClimateAt(entity.transform.position);
                sb.AppendLine($"当前温度: {climate.Temperature:F1}°C | 湿度: {climate.Humidity:F1}%");
            }

            // ★ 适配：显示温度/溺水环境伤害状态，便于验证新基因是否生效。
            var hazard = entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>();
            if (hazard != null)
                sb.AppendLine($"环境状态: {hazard.GetStatusLine()}");

            sb.AppendLine("<color=#294A5B>──────────────────────────────</color>");

            return sb.ToString();
        }
    }
}

namespace DarwinPark.UI
{
using System.Linq;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;


    // ★ 新增：分工状态与普通基因状态分开呈现，显示与战斗识别一致的种群标记。
    public static class ColonyStatusPresentation
    {
        public static bool TryCurrent(DarwinEntity entity,out string text)
        {
            text=null;var member=entity.GetComponent<AntColonyMember>();if(member==null||!member.Active||member.Role==AntRole.None)return false;
            string activity=member.Activity switch {ColonyActivity.Patrol=>"巡逻",ColonyActivity.Hunt=>"捕猎",ColonyActivity.Collect=>"收集",ColonyActivity.Transport=>"运输",ColonyActivity.Feed=>"前往补给",ColonyActivity.Reproduce=>entity.Genes.IsGestating?"孕育":"准备繁殖",ColonyActivity.Founding=>"建立新种群",ColonyActivity.Young=>"育幼",ColonyActivity.Mate=>"陪伴王后",_=>"休息"};
            // ★ 适配：活动状态在前、固定分工附在括号中，与普通实体状态保持同一阅读顺序。
            if(member.Activity==ColonyActivity.Rest)activity=DarwinPark.Genetics.EntityStateRegistry.Current.Get(DarwinPark.Genetics.EntityStateRegistry.Current.defaultStateId).displayName;
            text=activity+"（"+AntColonySettings.Current.Rule(member.Role).label+"）";return true;
        }
        public static string Summary(DarwinEntity entity)
        {
            var member=entity.GetComponent<AntColonyMember>();if(member==null||!member.Active||member.Role==AntRole.None)return "";
            string marker=member.GroupId.Length>=6?member.GroupId.Substring(0,6):member.GroupId;
            TryCurrent(entity,out string current);
            return "蚁群 "+marker+" · "+member.Group.Count()+" 成员\n"+current+"\n士兵 "+member.Group.Count(m=>m.Role==AntRole.Soldier)+"  工匠 "+member.Group.Count(m=>m.Role==AntRole.Worker)+"  繁殖 "+member.Group.Count(m=>m.Role==AntRole.Reproductive);
        }
    }
}

namespace DarwinPark.UI
{
using System.Linq;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;


    // ★ 新增：运行活动状态与基因倾向分别展示，注册表名称始终是唯一文案来源。
    public static class EntityStatePresentation
    {
        public static string Current(DarwinEntity entity)
        {
            // ★ 适配：蚁群优先显示实际分工活动，避免工匠运输仍被标为普通巡逻。
            if(ColonyStatusPresentation.TryCurrent(entity,out string colony))return NutritionStatusPresentation.Append(entity,colony);
            var controller=entity.GetComponent<EntityStateController>();
            return NutritionStatusPresentation.Append(entity,controller!=null?controller.Current.displayName:EntityStateRegistry.Current.Get(EntityStateRegistry.Current.defaultStateId).displayName);
        }
        public static string GeneTags(GeneDefinition gene)
        {
            if(gene.EntityStateTags.Count==0)return "";
            return "实体状态标签："+string.Join(" / ",gene.EntityStateTags.Select(id=>EntityStateRegistry.Current.Get(id).displayName));
        }
    }
}

namespace DarwinPark.UI
{
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;


    public static class NutritionStatusPresentation
    {
        public static string Append(DarwinEntity entity,string current)
        {
            var replica=entity.GetComponent<NutritionReplicaPresentation>();
            bool attached=replica!=null?replica.attached:entity.GetComponent<NutritionParasite>()?.Host!=null;
            int count=replica!=null?replica.parasites:NutritionParasite.CountOn(entity);
            int eggs=replica!=null?replica.foreignEggs:entity.GetComponent<EggHostPregnancy>()?.Count??0;
            if(attached)current="寄生中";
            if(count>0)current+="（被寄生）";
            if(eggs>0)current+="（卵寄生孕育 "+eggs+"）";
            return current;
        }
    }
}
