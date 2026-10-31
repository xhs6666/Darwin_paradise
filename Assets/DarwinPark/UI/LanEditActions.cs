using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Environment;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：所有创造操作共用一个入口；客户端只发送意图，服务端再次核验权限后执行，不信任客户端实体状态。
    public static class LanEditActions
    {
        public static bool Submit(LanCommand command)
        {if(!WorldAuthority.CanEdit)return false;var session=LanSession.Instance;return session!=null?session.SubmitEdit(command):LanEditExecutor.Apply(command,null);}
        public static bool SetGene(DarwinEntity entity,string instance,float value)=>Submit(new LanCommand{kind="gene",entity=EcologyIdentity.Of(entity),gene=instance,value=value});
        public static bool AddGene(DarwinEntity entity,string definition)=>Submit(new LanCommand{kind="addGene",entity=EcologyIdentity.Of(entity),gene=definition});
        public static bool Rename(DarwinEntity entity,string name)=>Submit(new LanCommand{kind="rename",entity=EcologyIdentity.Of(entity),name=name});
        public static bool Spawn(Genome genome,Vector3 point)=>Submit(new LanCommand{kind="spawn",genome=genome,point=point});
        public static bool Parameter(string id,float value)=>Submit(new LanCommand{kind="parameter",parameter=id,value=value});
        public static bool Profile(Dictionary<string,string> catalog,GeneTreeDocument tree)=>Submit(new LanCommand{kind="profile",definitions=catalog.Select(p=>new SavedGeneDefinition{id=p.Key,json=p.Value}).ToList(),tree=tree});
    }
    public static class LanEditExecutor
    {
        public static bool Apply(LanCommand command,LanSession session)
        {
            if(command==null)return false;
            var entity=EcologyIdentity.Find(command.entity);
            switch(command.kind)
            {
                case "gene":
                    if(!EcologyQueries.Alive(entity)||!LanSession.Finite(command.value))return false;
                    var gene=entity.Genes.Genome.Find(command.gene);if(gene==null)return false;gene.Value=Mathf.Clamp(command.value,0,100);entity.Genes.Recalculate();return true;
                case "addGene":
                    if(!EcologyQueries.Alive(entity)||entity.Genes.Genome.Genes.Count>=LanSettings.Current.maximumGenomeGenes)return false;var definition=GeneDatabase.All.FirstOrDefault(g=>g.Id==command.gene);if(definition==null)return false;
                    entity.Genes.Genome.Add(GeneInstance.Create(definition.Id,50,definition.Tier));entity.Genes.Recalculate();return true;
                case "rename":
                    if(!EcologyQueries.Alive(entity))return false;entity.DisplayName=string.IsNullOrWhiteSpace(command.name)?entity.Genes.GetRandomName():command.name.Trim().Substring(0,Mathf.Min(command.name.Trim().Length,64));return true;
                case "spawn":
                    if(!LanSession.Finite(command.point)||!ValidGenome(command.genome)||DarwinEntity.Spawner==null)return false;
                    var surfacePoint=EcologyQueries.Surface(command.point);float altitude=Vector3.Dot(command.point-surfacePoint,EcologyQueries.Up(surfacePoint));
                    if(!LanSession.Finite(altitude))return false;DarwinEntity.Spawner(EntityPlacementUtility.CopyGenome(command.genome),surfacePoint+EcologyQueries.Up(surfacePoint)*Mathf.Clamp(altitude,0,EcologyQueries.Planet.heightScale),"");return true;
                case "batch":
                    if(!LanSession.Finite(command.point)||!LanSession.Finite(command.value)||!LanSession.Finite(command.scale)||!ValidGenome(command.genome))return false;
                    var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();if(planet==null)return false;var point=EcologyQueries.Surface(command.point);
                    return EntityPlacementUtility.SpawnBatch(command.genome,point,EcologyQueries.Up(point),Mathf.Clamp(command.value,0,planet.heightScale),Mathf.Clamp(command.count,1,RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject).maxCount),Mathf.Clamp(command.scale,0,LanSettings.Current.rtsPickRadius))>0;
                case "random":
                    return ValidGenome(command.genome)&&RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject).StartBatch(command.genome,command.count,command.landOnly);
                case "randomCancel":RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject).Cancel();return true;
                case "time":
                    if(!LanSession.Finite(command.scale)||command.scale<0||command.scale>LanSettings.Current.maximumTimeScale)return false;var clock=ServiceLocator.Get<SimulationClock>();clock.SetTimeScale(command.scale);if(command.running)clock.Play();else clock.Pause();return true;
                case "parameter":
                    if(!LanSession.Finite(command.value))return false;var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var environment=ServiceLocator.Get<EnvironmentSystem>();
                    if(command.parameter.Contains("seed")){builder.seed=(int)command.value;builder.Build();}
                    else if(command.parameter.Contains("temp")){environment.BaseTemperature=Mathf.Clamp(command.value,-30,60);ClimateDataHub.BaseTemperature=environment.BaseTemperature;}
                    else if(command.parameter.Contains("hum")){environment.BaseHumidity=Mathf.Clamp(command.value,0,100);ClimateDataHub.BaseHumidity=environment.BaseHumidity;}
                    else return false;PlanetClimateMap.Instance?.MarkDirty();return true;
                case "profile":
                    if(command.definitions==null||command.definitions.Count>LanSettings.Current.maximumGeneDefinitions||command.tree==null)return false;var catalog=command.definitions.ToDictionary(d=>d.id,d=>d.json);
                    foreach(var pair in catalog){var parsed=JsonUtility.FromJson<GeneDefinitionJson>(pair.Value);if(!System.Text.RegularExpressions.Regex.IsMatch(pair.Key,"^[A-Za-z0-9_-]+$")||parsed==null||parsed.id!=pair.Key||!System.Enum.TryParse<GeneCategory>(parsed.category,true,out _))return false;}
                    if(catalog.Count==0||EntityRegistry.All.Any(e=>e.Genes.Genome.Genes.Any(g=>!catalog.ContainsKey(g.DefinitionId))))return false;GeneTreeStorage.Validate(command.tree,catalog);
                    GeneRuntimeProfile.Catalog=catalog;GeneRuntimeProfile.Tree=command.tree;GeneDatabase.Reload();GeneDatabase.EnsureLoaded();foreach(var current in EntityRegistry.All)current.Genes.Recalculate();return true;
                case "save":Object.FindFirstObjectByType<WorldSessionManager>().SaveNow();return true;
                default:return false;
            }
        }
        private static bool ValidGenome(Genome genome)
        {return genome==null||genome.Genes!=null&&genome.Genes.Count<=LanSettings.Current.maximumGenomeGenes&&genome.Genes.All(g=>g!=null&&g.MissingStateIds!=null&&LanSession.Finite(g.CopyStrength)&&g.CopyStrength>=0&&LanSession.Finite(g.Value)&&g.Value>=0&&g.Value<=100&&GeneDatabase.All.Any(d=>d.Id==g.DefinitionId));}
    }
}
