namespace DarwinPark.Entities.Ecology
{
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;


    public readonly struct EntitySighting
    {
        public readonly DarwinEntity entity;
        private readonly Vector3 _worldPosition;
        private readonly Transform _planet;
        public readonly Vector3 localPosition;
        public Vector3 position=>_planet!=null?_planet.TransformPoint(localPosition):_worldPosition;
        public readonly float sampledAt;
        public readonly long coordinateRevision;
        public EntitySighting(DarwinEntity target,Vector3 point,float time)
            :this(new EntityCoordinate(target,point),time){}
        internal EntitySighting(EntityCoordinate coordinate,float time)
        {entity=coordinate.entity;_worldPosition=coordinate.position;_planet=EcologyQueries.Planet!=null?EcologyQueries.Planet.transform:null;localPosition=coordinate.localPosition;sampledAt=time;coordinateRevision=coordinate.revision;}
    }
    // ✦ 修复（Bug回归）：共享的是实际坐标索引，每个观察者只接收自己视野筛选后的快照。
    public static class EntityPerceptionHub
    {
        private sealed class Inbox
        {
            public int revision=-1;
            public float next,range,interval,lastReal=float.NegativeInfinity;
            public bool attention,queued;
            public LinkedListNode<EntityGeneController> pendingNode;
            public readonly LinkedList<DarwinEntity> memoryOrder=new();
            public readonly Dictionary<DarwinEntity,LinkedListNode<DarwinEntity>> memoryNodes=new();
            public readonly Stack<LinkedListNode<DarwinEntity>> nodePool=new();
            public readonly List<EntityCoordinate> candidates=new();
            public readonly List<EntitySighting> sightings=new();
            public readonly List<DarwinEntity> expired=new();
            public readonly Dictionary<DarwinEntity,EntitySighting> byTarget=new(),memory=new();
        }
        private static readonly Dictionary<EntityGeneController,Inbox> Inboxes=new();
        private static readonly LinkedList<EntityGeneController> Pending=new();
        private static int _budgetFrame=-1,_scans;
        private static double _usedMilliseconds;
        public static long Searches {get;private set;}
        public static long Delivered {get;private set;}
        public static long CandidateVisits {get;private set;}
        public static int PendingScans=>Pending.Count;
        private static float Now=>ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameTime:Time.time;
        private static Inbox Get(EntityGeneController genes)
        {
            if(!Inboxes.TryGetValue(genes,out var inbox)){inbox=new Inbox();Inboxes.Add(genes,inbox);}
            if(inbox.revision!=genes.LogicRevision)
            {
                PerceptionSubscriptions.Forget(genes,inbox.memory.Keys);PerceptionSubscriptions.Forget(genes,inbox.byTarget.Keys);
                inbox.revision=genes.LogicRevision;inbox.next=float.NegativeInfinity;inbox.lastReal=float.NegativeInfinity;
                inbox.sightings.Clear();inbox.byTarget.Clear();inbox.memory.Clear();
                foreach(var node in inbox.memoryNodes.Values)inbox.nodePool.Push(node);inbox.memoryOrder.Clear();inbox.memoryNodes.Clear();
                var settings=ObservationSettings.Current;var observation=EcologyQueries.Active(genes,settings.geneId);
                bool hasGene=false;foreach(var gene in genes.Genome.Genes)if(gene.DefinitionId==settings.geneId){hasGene=true;break;}
                inbox.attention=observation?.State?.StateId==settings.attentionState;
                inbox.range=hasGene?(observation!=null?Mathf.Max(0,genes.GetAttribute(settings.rangeAttribute))*(inbox.attention?settings.attentionRangeMultiplier:settings.wideRangeMultiplier):0):0;
                inbox.interval=hasGene?(inbox.attention?settings.attentionRefreshSeconds:settings.wideRefreshSeconds):SimulationOptimization.Current.decisionSeconds;
            }
            return inbox;
        }
        public static float Range(EntityGeneController genes)=>genes!=null?Get(genes).range:0;
        public static float Range(EntityGeneController genes,string attribute)=>attribute=="senseRange"?Range(genes):Mathf.Min(Range(genes),Mathf.Max(0,genes.GetAttribute(attribute)));
        // 解析球体遮挡，不为每个候选调用物理射线；距离/隐蔽仍使用既有规则。
        private static bool Visible(EntityGeneController genes,DarwinEntity target,Vector3 point,float range)
        {
            if(!EcologyMotion.Detectable(target,genes.transform.position,range,genes.GetComponent<DarwinEntity>()))return false;
            var planet=EcologyQueries.Planet;if(planet==null||planet.Data==null)return true;
            var a=planet.transform.InverseTransformPoint(genes.transform.position);
            var b=planet.transform.InverseTransformPoint(point);var delta=b-a;
            float t=Mathf.Clamp01(-Vector3.Dot(a,delta)/Mathf.Max(1e-12f,delta.sqrMagnitude));
            return (a+delta*t).sqrMagnitude>=planet.Data.Radius*planet.Data.Radius;
        }
        // Bounded LRU: dense visibility cannot turn memory eviction into a per-target linear scan.
        private static void ForgetMemory(EntityGeneController genes,Inbox inbox,DarwinEntity target)
        {
            inbox.memory.Remove(target);
            if(inbox.memoryNodes.Remove(target,out var node)){inbox.memoryOrder.Remove(node);inbox.nodePool.Push(node);}
            if(!inbox.byTarget.ContainsKey(target))PerceptionSubscriptions.Detach(genes,target);
        }
        private static void Remember(EntityGeneController genes,Inbox inbox,EntitySighting sighting)
        {
            if(inbox.memoryNodes.TryGetValue(sighting.entity,out var node))inbox.memoryOrder.Remove(node);
            else
            {
                if(inbox.memory.Count>=ObservationSettings.Current.maximumRememberedTargets)
                {
                    var oldest=inbox.memoryOrder.First.Value;
                    ForgetMemory(genes,inbox,oldest);
                }
                node=inbox.nodePool.Count>0?inbox.nodePool.Pop():new LinkedListNode<DarwinEntity>(sighting.entity);
                node.Value=sighting.entity;inbox.memoryNodes[sighting.entity]=node;
            }
            inbox.memoryOrder.AddLast(node);inbox.memory[sighting.entity]=sighting;
        }
        private static void Scan(EntityGeneController genes,Inbox inbox)
        {
            using var measured=PerformanceTimings.Measure(PerformanceTimings.Area.Perception);
            inbox.expired.Clear();
            foreach(var pair in inbox.memory)if(pair.Key==null||Now-pair.Value.sampledAt>ObservationSettings.Current.memorySeconds)inbox.expired.Add(pair.Key);
            foreach(var target in inbox.expired)ForgetMemory(genes,inbox,target);
            inbox.expired.Clear();foreach(var target in inbox.byTarget.Keys)if(!inbox.memory.ContainsKey(target))PerceptionSubscriptions.Detach(genes,target);
            inbox.sightings.Clear();inbox.byTarget.Clear();
            float now=Now;EntityCoordinateGrid.Query(genes.transform.position,inbox.range,inbox.candidates);Searches++;
            var self=genes.GetComponent<DarwinEntity>();
            foreach(var coordinate in inbox.candidates)
            {
                var entity=coordinate.entity;CandidateVisits++;
                if(entity==self||!Visible(genes,entity,coordinate.position,inbox.range))continue;
                var sighting=new EntitySighting(coordinate,now);
                inbox.sightings.Add(sighting);inbox.byTarget[entity]=sighting;Remember(genes,inbox,sighting);
                PerceptionSubscriptions.Attach(genes,entity);Delivered++;
            }
            inbox.candidates.Clear();inbox.lastReal=Time.unscaledTime;
            // 固定相位使同期出生的观察者在后续周期错峰；不会延迟受伤时的定向 Notice。
            float phase=(unchecked((uint)genes.GetInstanceID())%997)/997f;
            float cycle=(Mathf.Floor(now/inbox.interval-phase)+1+phase)*inbox.interval;
            inbox.next=Mathf.Max(now+inbox.interval*.5f,cycle);
        }
        private static void Pump()
        {
            if(_budgetFrame!=Time.frameCount){_budgetFrame=Time.frameCount;_scans=0;_usedMilliseconds=0;}
            var settings=ObservationSettings.Current;
            while(Pending.Count>0&&_scans<settings.perceptionScansPerFrame&&_usedMilliseconds<settings.perceptionBudgetMilliseconds)
            {
                var genes=Pending.First.Value;Pending.RemoveFirst();
                if(genes==null||!genes.gameObject.activeInHierarchy||!Inboxes.TryGetValue(genes,out var inbox))continue;
                inbox=Get(genes);inbox.queued=false;inbox.pendingNode=null;double began=Time.realtimeSinceStartupAsDouble;
                Scan(genes,inbox);_usedMilliseconds+=(Time.realtimeSinceStartupAsDouble-began)*1000;_scans++;
            }
        }
        public static IReadOnlyList<EntitySighting> Read(EntityGeneController genes)
        {
            var inbox=Get(genes);
            if(!inbox.queued&&Now>=inbox.next&&Time.unscaledTime-inbox.lastReal>=ObservationSettings.Current.minimumRealRefreshSeconds)
            {inbox.queued=true;inbox.pendingNode=Pending.AddLast(genes);}
            Pump();return inbox.sightings;
        }
        public static bool TryLocate(EntityGeneController genes,DarwinEntity target,out Vector3 point,float maximumRange=float.PositiveInfinity)
        {
            point=default;if(target==null||!target.isActiveAndEnabled)return false;
            Read(genes);var inbox=Get(genes);
            if(!inbox.byTarget.TryGetValue(target,out var sighting)||Now-sighting.sampledAt>ObservationSettings.Current.memorySeconds)return false;
            // ✦ 修复（Bug回归）：跟踪只返回已投递快照，集中观察也不能逐帧读取目标真实坐标绕过预算。
            // ★ 适配：平方距离比较复用同一个有效感知范围。
            float range = Mathf.Min(inbox.range, maximumRange);
            if ((sighting.position - genes.transform.position).sqrMagnitude > range * range) return false;
            point=sighting.position;return true;
        }
        public static bool TryRecall(EntityGeneController genes,DarwinEntity target,out EntitySighting sighting)
        {
            sighting=default;if(target==null||!Inboxes.TryGetValue(genes,out var inbox)||!inbox.memory.TryGetValue(target,out sighting))return false;
            if(Now-sighting.sampledAt<=ObservationSettings.Current.memorySeconds)return true;
            ForgetMemory(genes,inbox,target);sighting=default;return false;
        }
        public static void Notice(EntityGeneController genes,DarwinEntity target)
        {
            if(target==null||!EntityCoordinateGrid.TryGet(target,out var coordinate))return;
            var inbox=Get(genes);if(inbox.range<=0||!Visible(genes,target,coordinate.position,inbox.range))return;
            var sighting=new EntitySighting(coordinate,Now);
            if(!inbox.byTarget.ContainsKey(target))inbox.sightings.Add(sighting);
            else for(int i=0;i<inbox.sightings.Count;i++)if(inbox.sightings[i].entity==target){inbox.sightings[i]=sighting;break;}
            inbox.byTarget[target]=sighting;Remember(genes,inbox,sighting);PerceptionSubscriptions.Attach(genes,target);
        }
        public static void Remove(EntityGeneController genes)
        {
            if(ReferenceEquals(genes,null))return;
            if(Inboxes.TryGetValue(genes,out var own)){PerceptionSubscriptions.Forget(genes,own.memory.Keys);PerceptionSubscriptions.Forget(genes,own.byTarget.Keys);if(own.pendingNode!=null)Pending.Remove(own.pendingNode);}
            Inboxes.Remove(genes);
            var target=genes.GetComponent<DarwinEntity>();if(target==null)return;
            var observers=PerceptionSubscriptions.Take(target);if(observers==null)return;
            foreach(var observer in observers)if(Inboxes.TryGetValue(observer,out var inbox))
            {inbox.byTarget.Remove(target);ForgetMemory(observer,inbox,target);for(int i=inbox.sightings.Count-1;i>=0;i--)if(inbox.sightings[i].entity==target)inbox.sightings.RemoveAt(i);}
        }
        public static void Clear()
        {Inboxes.Clear();Pending.Clear();PerceptionSubscriptions.Clear();EntityCoordinateGrid.Clear();_budgetFrame=-1;Searches=Delivered=CandidateVisits=0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}

namespace DarwinPark.Entities.Ecology
{
using System.Collections.Generic;
using UnityEngine;

    // ★ 新增：死亡注销只通知真正看过目标的观察者，不扫描每个实体的收件箱。
    public static class PerceptionSubscriptions
    {
        private static readonly Dictionary<DarwinEntity,HashSet<EntityGeneController>> Observers=new();
        public static void Attach(EntityGeneController observer,DarwinEntity target){if(!Observers.TryGetValue(target,out var set)){set=new();Observers.Add(target,set);}set.Add(observer);}
        public static void Detach(EntityGeneController observer,DarwinEntity target)
        {if(!ReferenceEquals(target,null)&&Observers.TryGetValue(target,out var set)){set.Remove(observer);if(set.Count==0)Observers.Remove(target);}}
        public static void Forget(EntityGeneController observer,IEnumerable<DarwinEntity> targets){foreach(var target in targets)if(Observers.TryGetValue(target,out var set)){set.Remove(observer);if(set.Count==0)Observers.Remove(target);}}
        public static HashSet<EntityGeneController> Take(DarwinEntity target){if(Observers.Remove(target,out var set))return set;return null;}
        public static void Clear()=>Observers.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
