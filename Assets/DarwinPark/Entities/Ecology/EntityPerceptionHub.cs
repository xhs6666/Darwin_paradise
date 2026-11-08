using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：坐标信息只投递给视野内的观察者；广域状态保留采样坐标，注意状态只对已知目标实时读共享坐标。
    public readonly struct EntitySighting
    {
        public readonly DarwinEntity entity;public readonly Vector3 position;public readonly float sampledAt;
        public EntitySighting(DarwinEntity target,Vector3 point,float time){entity=target;position=point;sampledAt=time;}
    }
    public static class EntityPerceptionHub
    {
        private sealed class Inbox
        {
            public int revision=-1,frame=-1;public long membership=-1;public float next,range,interval;public bool attention;
            public readonly List<EntityCoordinate> candidates=new();public readonly List<EntitySighting> sightings=new();
            public readonly Dictionary<DarwinEntity,EntitySighting> byTarget=new();
        }
        private static readonly Dictionary<EntityGeneController,Inbox> Inboxes=new();
        public static long Searches {get;private set;}public static long Delivered {get;private set;}public static long CandidateVisits {get;private set;}
        private static float Now=>ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameTime:Time.time;
        private static Inbox Get(EntityGeneController genes)
        {
            if(!Inboxes.TryGetValue(genes,out var inbox)){inbox=new Inbox();Inboxes.Add(genes,inbox);}
            if(inbox.revision!=genes.LogicRevision)
            {
                inbox.revision=genes.LogicRevision;inbox.frame=-1;inbox.next=float.NegativeInfinity;inbox.sightings.Clear();inbox.byTarget.Clear();
                var settings=ObservationSettings.Current;var observation=EcologyQueries.Active(genes,settings.geneId);
                bool hasGene=false;foreach(var gene in genes.Genome.Genes)if(gene.DefinitionId==settings.geneId){hasGene=true;break;}
                inbox.attention=observation?.State?.StateId==settings.attentionState;
                inbox.range=hasGene?(observation!=null?Mathf.Max(0,genes.GetAttribute(settings.rangeAttribute))*(inbox.attention?settings.attentionRangeMultiplier:settings.wideRangeMultiplier):0):Mathf.Max(0,genes.GetAttribute("senseRange"));
                inbox.interval=hasGene?(inbox.attention?settings.attentionRefreshSeconds:settings.wideRefreshSeconds):0;
            }
            return inbox;
        }
        public static float Range(EntityGeneController genes)=>genes!=null?Get(genes).range:0;
        public static float Range(EntityGeneController genes,string attribute)=>attribute=="senseRange"?Range(genes):Mathf.Min(Range(genes),Mathf.Max(0,genes.GetAttribute(attribute)));
        public static IReadOnlyList<EntitySighting> Read(EntityGeneController genes)
        {
            var inbox=Get(genes);float now=Now;
            if(inbox.membership==EntityRegistry.MembershipRevision&&(inbox.frame==Time.frameCount||now<inbox.next))return inbox.sightings;
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.Perception);
            inbox.membership=EntityRegistry.MembershipRevision;inbox.frame=Time.frameCount;inbox.next=now+inbox.interval;inbox.sightings.Clear();inbox.byTarget.Clear();
            EntityCoordinateGrid.Query(genes.transform.position,inbox.range,inbox.candidates);Searches++;var self=genes.GetComponent<DarwinEntity>();
            foreach(var coordinate in inbox.candidates)
            {
                var entity=coordinate.entity;CandidateVisits++;if(entity==self||!EcologyMotion.Detectable(entity,genes.transform.position,inbox.range,self))continue;
                var sighting=new EntitySighting(entity,coordinate.position,now);inbox.sightings.Add(sighting);inbox.byTarget[entity]=sighting;Delivered++;
            }
            inbox.candidates.Clear();return inbox.sightings;
        }
        public static bool TryLocate(EntityGeneController genes,DarwinEntity target,out Vector3 point,float maximumRange=float.PositiveInfinity)
        {
            point=default;if(target==null)return false;Read(genes);var inbox=Get(genes);
            if(!inbox.byTarget.TryGetValue(target,out var sighting)||!EntityNeighborhood.TryPosition(target,out var current)||!EcologyMotion.Detectable(target,genes.transform.position,Mathf.Min(inbox.range,maximumRange),genes.GetComponent<DarwinEntity>()))return false;
            point=inbox.attention||inbox.interval==0?current:sighting.position;return true;
        }
        public static void Notice(EntityGeneController genes,DarwinEntity target)
        {
            // ★ 适配：真实受击是一个新观察事件；不必等待下一轮全视野搜索才定位近处攻击者。
            if(target==null)return;Read(genes);var inbox=Get(genes);
            if(inbox.range<=0||!EntityNeighborhood.TryPosition(target,out var point)||!EcologyMotion.Detectable(target,genes.transform.position,inbox.range,genes.GetComponent<DarwinEntity>()))return;
            var sighting=new EntitySighting(target,point,Now);if(!inbox.byTarget.ContainsKey(target))inbox.sightings.Add(sighting);inbox.byTarget[target]=sighting;
        }
        public static void Remove(EntityGeneController genes)
        {
            if(ReferenceEquals(genes,null))return;Inboxes.Remove(genes);
            // 注销目标从所有收件箱释放，不能让死亡/换世界后的引用一直被保留。
            var target=genes.GetComponent<DarwinEntity>();if(target==null)return;
            foreach(var inbox in Inboxes.Values){inbox.byTarget.Remove(target);for(int i=inbox.sightings.Count-1;i>=0;i--)if(inbox.sightings[i].entity==target)inbox.sightings.RemoveAt(i);}
        }
        public static void Clear(){Inboxes.Clear();EntityCoordinateGrid.Clear();Searches=Delivered=CandidateVisits=0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
