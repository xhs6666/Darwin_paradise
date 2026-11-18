using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ✦ 新增：执行真实实体的登记、同帧移动、死亡、视野预算与千实体增量索引回归。
    public static class GlobalCoordinateRegression
    {
        private static readonly List<GameObject> Objects=new();
        private static List<string> _passed;
        private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_passed.Add(message);}
        private static DarwinEntity Spawn(Vector3 position)
        {
            var go=new GameObject("CoordinateFixture");Objects.Add(go);go.transform.position=position;
            go.AddComponent<SphereCollider>();var stats=go.AddComponent<EntityStats>();
            if(!stats.TryGet("health",out _))typeof(EntityStats).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stats,null);
            var genes=go.AddComponent<EntityGeneController>();genes.enabled=false;genes.Genome=new Genome();genes.Recalculate();
            var entity=go.AddComponent<DarwinEntity>();
            if(entity.Genes==null)typeof(DarwinEntity).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(entity,null);
            EntityRegistry.Register(entity);EntityNeighborhood.Changed(genes);return entity;
        }
        private static void EnableObservation(DarwinEntity entity,ObservationSettings settings,float range)
        {
            entity.Genes.Genome.Add(GeneInstance.Create(settings.geneId,75,0));
            entity.Genes.Recalculate();
            entity.Genes.Evaluation.Attributes[settings.rangeAttribute]=range;
        }
        internal static void ForceScan(EntityGeneController genes)
        {
            var inbox=typeof(EntityPerceptionHub).GetMethod("Get",Static).Invoke(null,new object[]{genes});
            inbox.GetType().GetField("next").SetValue(inbox,float.NegativeInfinity);
            inbox.GetType().GetField("lastReal").SetValue(inbox,float.NegativeInfinity);
            typeof(EntityPerceptionHub).GetField("_budgetFrame",Static).SetValue(null,-1);
            EntityPerceptionHub.Read(genes);
        }
        public static void Validate(PlanetTerrainMeshBuilder planet,List<string> passed)
        {
            _passed=passed;
            var settingsField=typeof(ObservationSettings).GetField("_current",Static);
            var original=ObservationSettings.Current;
            var settings=JsonUtility.FromJson<ObservationSettings>(JsonUtility.ToJson(original));
            settings.perceptionBudgetMilliseconds=1000;settings.perceptionScansPerFrame=2;settingsField.SetValue(null,settings);
            try
            {
                var a=Spawn(Vector3.forward*116);var b=Spawn(new Vector3(1,0,116));
                var query=new List<EntityCoordinate>();EntityCoordinateGrid.Query(a.transform.position,5,query);
                Check(query.Exists(c=>c.entity==a)&&query.Exists(c=>c.entity==b),"All entities share the same spatial index");
                var late=new GameObject("LateStatsFixture");Objects.Add(late);late.transform.position=new Vector3(5000,5000,5000);late.AddComponent<SphereCollider>();
                var lateGenes=late.AddComponent<EntityGeneController>();lateGenes.enabled=false;lateGenes.Genome=new Genome();lateGenes.Recalculate();
                var lateEntity=late.AddComponent<DarwinEntity>();
                if(lateEntity.Genes==null)typeof(DarwinEntity).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(lateEntity,null);
                EntityRegistry.Register(lateEntity);EntityNeighborhood.Synchronize();
                var lateStats=late.AddComponent<EntityStats>();
                if(!lateStats.TryGet("health",out _))typeof(EntityStats).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(lateStats,null);
                EntityCoordinateGrid.Query(late.transform.position,1,query);
                Check(query.Exists(c=>c.entity==lateEntity),"Registration before Stats creation is repaired without a full rescan");
                int builds=EntityCoordinateGrid.Builds;long buckets=EntityCoordinateGrid.BucketMoves;
                b.transform.position+=Vector3.right*.125f;EntityNeighborhood.ObservePosition(b.Genes);
                Check(EntityCoordinateGrid.TryGet(b,out var snapshot)&&snapshot.position==b.transform.position,"Explicit movement updates shared coordinates in the same frame");
                Check(EntityCoordinateGrid.Builds==builds&&EntityCoordinateGrid.BucketMoves==buckets,"Movement inside a cell neither rebuilds the index nor moves bucket members");
                b.transform.position=new Vector3(200,0,116);EntityNeighborhood.ObservePosition(b.Genes);EntityCoordinateGrid.Query(a.transform.position,5,query);
                Check(!query.Exists(c=>c.entity==b),"Cross-cell movement removes the old spatial membership");
                b.GetComponent<EntityStats>().Modify("health",-100,false);
                Check(!EntityCoordinateGrid.TryGet(b,out _),"Zero health immediately removes a live spatial record");
                b.GetComponent<EntityStats>().Modify("health",100,false);
                Check(EntityCoordinateGrid.TryGet(b,out _),"Restored health reinserts the spatial record");
                // ★ 适配：观察范围来自有效探测基因，测试裸实体不能只注入属性冒充探测能力。
                EnableObservation(a,settings,10);
                b.transform.position=new Vector3(1,0,116);EntityNeighborhood.ObservePosition(b.Genes);
                EntityPerceptionHub.Clear();ForceScan(a.Genes);
                Check(EntityPerceptionHub.TryLocate(a.Genes,b,out var observed),"An observer receives a nearby visible target");
                b.transform.position+=Vector3.right;EntityNeighborhood.ObservePosition(b.Genes);
                Check(EntityPerceptionHub.TryLocate(a.Genes,b,out var remembered)&&remembered==observed,"Tracking returns the observed snapshot rather than hidden live coordinates");
                EntityPerceptionHub.Notice(a.Genes,b);
                Check(EntityPerceptionHub.TryLocate(a.Genes,b,out var noticed)&&noticed==b.transform.position,"A visible directed notice updates the existing sighting");
                b.transform.position=new Vector3(200,0,116);EntityNeighborhood.ObservePosition(b.Genes);ForceScan(a.Genes);
                Check(!EntityPerceptionHub.TryLocate(a.Genes,b,out _)&&EntityPerceptionHub.TryRecall(a.Genes,b,out var memory)&&memory.position==noticed,"Leaving sight retains a separate last-known location");
                b.gameObject.SetActive(false);EntityRegistry.Unregister(b);
                Check(!EntityPerceptionHub.TryRecall(a.Genes,b,out _),"Unregistration removes subscriptions and remembered target references");
                var c=Spawn(new Vector3(2,0,116));var d=Spawn(new Vector3(3,0,116));
                EnableObservation(c,settings,10);EnableObservation(d,settings,10);
                EntityPerceptionHub.Clear();EntityPerceptionHub.Read(a.Genes);EntityPerceptionHub.Read(c.Genes);EntityPerceptionHub.Read(d.Genes);
                Check(EntityPerceptionHub.Searches==2&&EntityPerceptionHub.PendingScans==1,"Observation scans obey a shared frame budget");
                EntityPerceptionHub.Remove(d.Genes);
                Check(EntityPerceptionHub.PendingScans==0,"Removing a queued observer releases its pending request immediately");
                EntityPerceptionHub.Read(d.Genes);
                typeof(EntityPerceptionHub).GetField("_budgetFrame",Static).SetValue(null,-1);EntityPerceptionHub.Read(a.Genes);
                Check(EntityPerceptionHub.Searches==3&&EntityPerceptionHub.PendingScans==0,"Queued observers are processed in FIFO order");
                a.Genes.Evaluation.Attributes["senseRange"]=1000;c.transform.position=-Vector3.forward*116;EntityNeighborhood.ObservePosition(c.Genes);
                EntityPerceptionHub.Clear();ForceScan(a.Genes);
                Check(!EntityPerceptionHub.TryLocate(a.Genes,c,out _),"The planet blocks observations through its opposite hemisphere");
                c.transform.position=new Vector3(2,0,116);EntityNeighborhood.ObservePosition(c.Genes);
                Spawn(new Vector3(4,0,116));settings.maximumRememberedTargets=2;ForceScan(a.Genes);
                var inbox=typeof(EntityPerceptionHub).GetMethod("Get",Static).Invoke(null,new object[]{a.Genes});
                var memories=(Dictionary<DarwinEntity,EntitySighting>)inbox.GetType().GetField("memory").GetValue(inbox);
                Check(memories.Count==2,"Dense observations keep a bounded memory cache");
                EntityPerceptionHub.Remove(a.Genes);
                var subscriptions=(System.Collections.IDictionary)typeof(PerceptionSubscriptions).GetField("Observers",Static).GetValue(null);
                Check(subscriptions.Count==0,"Evicted visible targets leave no subscriptions after observer removal");

                var crowd=new List<DarwinEntity>();
                for(int i=0;i<1000;i++)crowd.Add(Spawn(new Vector3((i%32)*128+1,(i/32)*128+1,1000)));
                EntityCoordinateGrid.Query(crowd[0].transform.position,2,query);
                builds=EntityCoordinateGrid.Builds;buckets=EntityCoordinateGrid.BucketMoves;
                var watch=Stopwatch.StartNew();
                foreach(var entity in crowd){entity.transform.position+=Vector3.right*.125f;EntityNeighborhood.ObservePosition(entity.Genes);}
                watch.Stop();double movement=watch.Elapsed.TotalMilliseconds;
                long movementBucketMoves=EntityCoordinateGrid.BucketMoves-buckets;int movementRebuilds=EntityCoordinateGrid.Builds-builds;
                Check(EntityCoordinateGrid.Builds==builds&&EntityCoordinateGrid.BucketMoves==buckets,"One thousand moving entities do not trigger full rebuilds or same-cell rebucketing");
                long visits=EntityCoordinateGrid.CandidateVisits;
                EntityCoordinateGrid.Query(crowd[400].transform.position,2,query);
                long localVisits=EntityCoordinateGrid.CandidateVisits-visits;
                Check(query.Count==1&&query[0].entity==crowd[400]&&localVisits<10,"A local query visits nearby buckets rather than all one thousand entities");
                for(int i=0;i<30;i++)
                {
                    var center=crowd[(i*31)%crowd.Count].transform.position;float radius=30+i*7;
                    EntityCoordinateGrid.Query(center,radius,query);int expected=0;
                    foreach(var entity in crowd)if((entity.transform.position-center).sqrMagnitude<=radius*radius)expected++;
                    Check(query.Count==expected,"Spatial query matches an exact distance reference");
                }
                var before=crowd[400].transform.position;
                planet.transform.position=new Vector3(20,-15,30);planet.transform.rotation=Quaternion.Euler(0,90,0);
                foreach(var entity in crowd)entity.transform.position=planet.transform.TransformPoint(entity.transform.position);
                EntityCoordinateGrid.Query(planet.transform.TransformPoint(before),2,query);
                Check(query.Count==1&&query[0].entity==crowd[400]&&EntityCoordinateGrid.Builds==builds,"Rigid planet rotation preserves local queries without rebuilding the spatial index");
                Directory.CreateDirectory("Artifacts/GeographyNavigationReview");
                File.WriteAllText("Artifacts/GeographyNavigationReview/index-metrics.txt",$"Entities=1000\nSameCellMovementMilliseconds={movement:F3}\nFullRebuildsDuringMovement={movementRebuilds}\nBucketMovesDuringMovement={movementBucketMoves}\nLocalCandidateVisits={localVisits}\n");
            }
            finally
            {
                foreach(var go in Objects)if(go!=null){var entity=go.GetComponent<DarwinEntity>();if(entity!=null)EntityRegistry.Unregister(entity);Object.DestroyImmediate(go);}
                Objects.Clear();EntityPerceptionHub.Clear();settingsField.SetValue(null,original);
                planet.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            }
        }
    }
}
