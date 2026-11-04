using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class ColonyWorldState
    {
        public int version=1;public string settings;
        public List<ColonyResourceSave> resources=new List<ColonyResourceSave>();
        public bool IsEmpty=>version==0&&string.IsNullOrEmpty(settings)&&(resources==null||resources.Count==0);
        public static ColonyWorldState Capture()=>new ColonyWorldState{settings=AntColonySettings.CurrentJson,resources=ColonyResource.All.Select(r=>r.Capture()).ToList()};
        public static void Validate(ColonyWorldState state)
        {
            if(state==null||state.IsEmpty)return;
            if(state.version!=1||state.resources==null)throw new InvalidDataException("蚁群世界快照版本或资源列表无效");
            if(!string.IsNullOrEmpty(state.settings))AntColonySettings.Parse(state.settings);
            var ids=new HashSet<string>();
            foreach(var resource in state.resources)
                if(resource==null||!Guid.TryParseExact(resource.id,"N",out _)||!ids.Add(resource.id)||!(resource.kind==ColonyResourceKind.Corpse&&string.IsNullOrEmpty(resource.group))&&!Guid.TryParseExact(resource.group,"N",out _)||!Enum.IsDefined(typeof(ColonyResourceKind),resource.kind)||float.IsNaN(resource.energy)||float.IsInfinity(resource.energy)||resource.energy<=0||!float.IsFinite(resource.sourceSize)||resource.sourceSize<0||!Finite(resource.local)||!Finite(resource.scale))
                    throw new InvalidDataException("蚁群资源标识、位置或能量无效");
        }
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
        public static void Prepare(ColonyWorldState state){Validate(state);ColonyResource.ClearWorld();AntColonySettings.UseSnapshot(state?.settings);}
        public static void Restore(ColonyWorldState state)
        {
            if(state?.resources!=null)foreach(var resource in state.resources)ColonyResource.Create(JsonUtility.FromJson<ColonyResourceSave>(JsonUtility.ToJson(resource)));
            foreach(var member in AntColonyMember.All.ToArray())AntColonyWorker.Resolve(member);
        }
        public static void Clear(){ColonyResource.ClearWorld();AntColonySettings.Clear();}
        public static void ValidateMember(AntColonyMemberSave state)
        {
            if(state==null||!state.HasData)return;
            // ★ 修复（防Bug回归）：数字形式的未定义角色和非有限运输坐标也必须在清场前拒绝，不能等到恢复角色时崩溃。
            if(!Enum.TryParse<AntRole>(state.role,out var role)||!Enum.IsDefined(typeof(AntRole),role)||role==AntRole.None||state.supplyPoints==null||state.supplyPoints.Any(p=>!Finite(p))||!Finite(state.foundingPoint)||!Finite(state.deliveryPoint))throw new InvalidDataException("蚁群分工、运输位置或补给点无效");
            foreach(float value in new[]{state.scan,state.patrolRemaining,state.restRemaining,state.circleAngle,state.reproductionTimer,state.reproductionCooldown})
                if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("蚁群计时无效");
            if(!string.IsNullOrEmpty(state.lockedGenome))ValidateGenome(JsonUtility.FromJson<Genetics.Genome>(state.lockedGenome));
            if(state.partnerGenome!=null&&state.partnerGenome.Genes?.Count>0)ValidateGenome(state.partnerGenome);
        }
        private static void ValidateGenome(Genetics.Genome genome)
        {
            if(genome?.Genes==null||genome.Genes.Count==0||genome.Genes.Any(g=>g==null||string.IsNullOrEmpty(g.DefinitionId)||float.IsNaN(g.Value)||float.IsInfinity(g.Value)||g.MissingStateIds==null))throw new InvalidDataException("蚁群固定或配偶基因快照无效");
        }
        public static void ValidateMotion(EcologySaveState state)
        {
            if(state==null)return;
            if(state.rts!=null&&(!Enum.IsDefined(typeof(RtsOrderKind),state.rts.kind)||!Finite(state.rts.point)))throw new InvalidDataException("RTS命令或目标位置无效");
            // ★ 修复（防Bug回归）：持久化的巡逻和反击计时同样需要有限值校验，否则坏档会让休息或攻击永久不结束。
            foreach(float value in new[]{state.patrol?.rest??0,state.patrol?.active??0,state.combat?.remaining??0})
                if(float.IsNaN(value)||float.IsInfinity(value)||value<0)throw new InvalidDataException("巡逻或反击计时无效");
        }
    }
}
