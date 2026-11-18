using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：仅缓存纯表达数值，不共享个体GeneInstance、属性表或行为计时器。阶段/体型处理仍逐实体执行。
    public static class GeneExpressionCache
    {
        private sealed class Value {public int index;public GeneDefinition definition;public GeneStateDefinition state;public float suppression,strength,environment,role;public bool gestating,missingRequirements;public Dictionary<string,float> attributes;}
        private sealed class Snapshot {public readonly List<Value> values=new();public Dictionary<string,float> attributes;}
        private static readonly Dictionary<string,Snapshot> Values=new();private static readonly Queue<string> Order=new();private static readonly StringBuilder Key=new();
        public static long Hits {get;private set;}public static long Misses {get;private set;}public static int Count=>Values.Count;
        private static void Text(string text){Key.Append(text?.Length??-1).Append(':').Append(text).Append(';');}
        public static GenomeEvaluation Resolve(Genome genome,bool gestating,float suppression,string exempt,IReadOnlyDictionary<string,float> roles)
        {
            if(!SimulationOptimization.Current.expressionCache||genome==null)return GeneValueResolver.ResolveUncached(genome,gestating,suppression,exempt,roles);
            GeneDatabase.EnsureLoaded();Key.Clear();Key.Append(gestating).Append(':').Append(suppression.ToString("R",CultureInfo.InvariantCulture));Text(exempt);
            foreach(var gene in genome.Genes){Text(gene.DefinitionId);Text(gene.StateId);Key.Append(gene.Value.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(gene.CopyStrength.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(gene.Tier).Append(':').Append(gene.Enabled).Append(':');if(gene.MissingStateIds!=null)foreach(var id in gene.MissingStateIds)Text(id);Key.Append('|');float role=0;roles?.TryGetValue(gene.DefinitionId,out role);Key.Append(role.ToString("R",CultureInfo.InvariantCulture)).Append(';');}
            string key=Key.ToString();
            if(Values.TryGetValue(key,out var cached))
            {
                Hits++;var result=new GenomeEvaluation{Attributes=new Dictionary<string,float>(cached.attributes)};
                foreach(var value in cached.values)result.Genes.Add(new GeneEvaluation{Gene=genome.Genes[value.index],GenomeIndex=value.index,Definition=value.definition,State=value.state,MissingRequirements=value.missingRequirements,SuppressionAmount=value.suppression,EffectiveStrength=value.strength,EnvironmentSuppression=value.environment,RoleSuppression=value.role,GestatingAtEvaluation=value.gestating,AttributeContributions=new Dictionary<string,float>(value.attributes)});
                return result;
            }
            Misses++;var evaluated=GeneValueResolver.ResolveUncached(genome,gestating,suppression,exempt,roles);var snapshot=new Snapshot{attributes=new Dictionary<string,float>(evaluated.Attributes)};
            foreach(var value in evaluated.Genes)snapshot.values.Add(new Value{index=value.GenomeIndex,definition=value.Definition,state=value.State,missingRequirements=value.MissingRequirements,suppression=value.SuppressionAmount,strength=value.EffectiveStrength,environment=value.EnvironmentSuppression,role=value.RoleSuppression,gestating=value.GestatingAtEvaluation,attributes=new Dictionary<string,float>(value.AttributeContributions)});
            int limit=SimulationOptimization.Current.expressionCacheLimit;while(Order.Count>=limit)Values.Remove(Order.Dequeue());Values[key]=snapshot;Order.Enqueue(key);return evaluated;
        }
        public static void Clear(){Values.Clear();Order.Clear();Key.Clear();Hits=Misses=0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
