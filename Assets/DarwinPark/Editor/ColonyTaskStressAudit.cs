using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEditor;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    // ★ 新增：测试专用战斗归因和补给隔离，不修改发布版的伤害/种群强度。
    public static class ColonyTaskStressAudit
    {
        private const string Key="DarwinPark.ColonyTaskStress";
        private static readonly HashSet<DarwinEntity> Seen=new();private static readonly Dictionary<DarwinEntity,float> Health=new();
        private static bool _started,_queenSeen;private static int _attacks;private static float _time;
        private static string Folder=>Path.GetFullPath("Artifacts/ColonyTaskStressReview");
        public static void RunDiagnosticBatch(){SessionState.SetInt(Key,1);ColonyContinuityRegression.RunBatch();}
        public static void RunFeedingBatch(){SessionState.SetInt(Key,2);ColonyContinuityRegression.RunBatch();}
        private static void Write(string text)=>File.AppendAllText(Path.Combine(Folder,"events.txt"),text+"\n");
        private static string Values(DarwinEntity entity){var stats=entity.GetComponent<EntityStats>();stats.TryGet("health",out var hp);stats.TryGet("nutrient",out var food);stats.TryGet("storedNutrient",out var reserve);return $"hp={hp} food={food} reserve={reserve} life={entity.GetComponent<EntityLifeCycle>()?.Progress}";}
        public static void BeforeTick(DarwinEntity entity,float time)
        {
            int mode=SessionState.GetInt(Key,0);if(mode==0)return;_time=time;
            if(!_started){_started=true;Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"events.txt"),"mode="+mode+"\n");}
            if(Seen.Add(entity))
            {
                if(mode==2)entity.Genes.OnEvaluationChanged+=SuppressAttack;
                var member=entity.GetComponent<AntColonyMember>();
                if(member!=null&&member.Active&&member.Role==AntRole.Queen)
                {
                    _queenSeen=true;EntityCombat.Ensure(entity.gameObject).Attacked+=(attacker,damage)=>{_attacks++;Write($"t={_time} queen={entity.DisplayName} attackedBy={attacker?.DisplayName} damage={damage} {Values(entity)}");};
                }
            }
            if(mode==2)SuppressAttack(entity.Genes.Evaluation);
            var queen=entity.GetComponent<AntColonyMember>();if(queen==null||!queen.Active||queen.Role!=AntRole.Queen)return;
            var stats=entity.GetComponent<EntityStats>();stats.TryGet("health",out var hp);
            if(Health.TryGetValue(entity,out var previous)&&hp<previous)Write($"t={time} queenHealthDrop={previous}->{hp} {Values(entity)}");Health[entity]=hp;
        }
        private static void SuppressAttack(GenomeEvaluation evaluation){if(evaluation!=null)evaluation.Attributes["attack"]=0;}
        public static bool StopRequested()
        {
            if(SessionState.GetInt(Key,0)!=1||!_queenSeen||AntColonyMember.All.Any(m=>m.Role==AntRole.Queen))return false;
            Write("DIAGNOSTIC STOP: no queen; recorded attacks="+_attacks);return true;
        }
        public static void Finish()
        {
            if(SessionState.GetInt(Key,0)!=0&&_started)Write("FINISH mode="+SessionState.GetInt(Key,0)+" attacks="+_attacks);
            foreach(var entity in Seen)if(entity!=null&&entity.Genes!=null)entity.Genes.OnEvaluationChanged-=SuppressAttack;
            Seen.Clear();Health.Clear();_started=_queenSeen=false;_attacks=0;SessionState.SetInt(Key,0);
        }
    }
}
