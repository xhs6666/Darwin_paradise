using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class PopulationSample {public float time;public int members,infants,adults,elders;}
    [Serializable] public sealed class PopulationEvent {public float time;public string text;}
    [Serializable] public sealed class PopulationGeneCount {public string id;public int carried,expressed;}
    [Serializable] public sealed class PopulationRecord
    {
        // ★ 新增：实体配色默认独立；旧存档缺少该字段时维持原本外观。
        public string id,name,mode,parentId,color;public bool originKnown,visible=true,syncEntityColor;
        // ★ 新增：-1表示沿用外部全局默认值，旧档不因缺少字段而变成完全透明。
        public float rangeOpacity=-1;
        public float firstSeen,lastSeen;public int current,peak,lastLiving,increases,decreases;
        public int infants,adults,elders;
        public List<PopulationSample> samples=new List<PopulationSample>();
        public List<PopulationEvent> events=new List<PopulationEvent>();
        public List<PopulationGeneCount> genes=new List<PopulationGeneCount>();
    }
    [Serializable] public sealed class PopulationArchiveState {public int version=1;public List<PopulationRecord> groups=new List<PopulationRecord>();}
    [Serializable] public sealed class PopulationArchiveSettings
    {
        public float refreshSeconds=.5f,sampleGameSeconds=10;public int maximumSamples=512,maximumEvents=256;
        private static PopulationArchiveSettings _current;
        public static PopulationArchiveSettings Current
        {
            get
            {
                if(_current!=null)return _current;var asset=Resources.Load<TextAsset>("Entities/population_archive");if(asset==null)throw new InvalidDataException("缺少 Entities/population_archive");
                var value=JsonUtility.FromJson<PopulationArchiveSettings>(asset.text);
                if(value==null||!float.IsFinite(value.refreshSeconds)||value.refreshSeconds<=0||!float.IsFinite(value.sampleGameSeconds)||value.sampleGameSeconds<=0||value.maximumSamples<4||value.maximumEvents<4)throw new InvalidDataException("种群历史记录配置无效");return _current=value;
            }
        }
    }
    // ★ 新增：档案独立于存活实体；灭绝后保留历史，旧档从首次观察建档，不编造之前的出生或谱系。
    public static class PopulationArchive
    {
        private static readonly Dictionary<string,PopulationRecord> Records=new Dictionary<string,PopulationRecord>();
        public static long Revision {get;private set;}
        public static long DisplayRevision {get;private set;}
        public static IEnumerable<PopulationRecord> All=>Records.Values;
        public static float Now=>ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameTime:0;
        public static PopulationRecord Find(string id)=>id!=null&&Records.TryGetValue(id,out var record)?record:null;
        public static PopulationRecord Register(string id,string mode,string name,bool known=false)
        {
            if(string.IsNullOrEmpty(id))return null;if(Records.TryGetValue(id,out var record))return record;
            record=new PopulationRecord{id=id,mode=mode??"",name=string.IsNullOrWhiteSpace(name)?"种群 "+id.Substring(0,Math.Min(6,id.Length)):name,firstSeen=Now,lastSeen=Now,originKnown=known};Records.Add(id,record);
            Event(record,known?"种群成立":"首次观察；此前历史未记录");Revision++;return record;
        }
        private static void Event(PopulationRecord record,string text)
        {
            record.events.Add(new PopulationEvent{time=Now,text=text});while(record.events.Count>PopulationArchiveSettings.Current.maximumEvents)record.events.RemoveAt(0);
        }
        public static void RecordFounding(string parent,string child,string mode,string name,string reason="繁殖个体离群建立新种群")
        {
            if(WorldAuthority.IsReplica||string.IsNullOrEmpty(parent)||string.IsNullOrEmpty(child)||parent==child)return;
            var source=Register(parent,mode,"原种群");var result=Register(child,mode,name,true);
            if(!string.IsNullOrEmpty(result.parentId))return;result.parentId=parent;result.originKnown=true;
            Event(source,reason+"："+result.name);Event(result,"由种群「"+source.name+"」衍生建立");Revision++;
        }
        public static List<DarwinEntity> Members(string id)=>EntityRegistry.All.Where(EcologyQueries.Alive).Where(e=>e.GetComponent<PopulationBehavior>() is PopulationBehavior p&&p.Active&&p.GroupId==id).ToList();
        public static void Observe()
        {
            if(WorldAuthority.IsReplica||WorldRuntimeGate.Loading)return;
            var groups=EntityRegistry.All.Where(EcologyQueries.Alive).Select(e=>e.GetComponent<PopulationBehavior>()).Where(p=>p!=null&&p.Active&&!string.IsNullOrEmpty(p.GroupId)).GroupBy(p=>p.GroupId).ToArray();var seen=new HashSet<string>();
            foreach(var group in groups)
            {
                seen.Add(group.Key);var first=group.First();var entities=group.Select(p=>p.GetComponent<DarwinEntity>()).ToList();
                var record=Register(group.Key,first.Mode,(first.Leader??first).GetComponent<DarwinEntity>().DisplayName);
                int count=entities.Count;bool changed=count!=record.current;
                if(changed){int delta=count-record.current;if(record.samples.Count>0){record.increases+=Mathf.Max(0,delta);record.decreases+=Mathf.Max(0,-delta);Event(record,"成员数 "+record.current+" → "+count+"（含出生、死亡及迁移）");}record.current=count;Revision++;}
                record.lastSeen=Now;record.peak=Mathf.Max(record.peak,count);record.mode=first.Mode;
                if(changed||record.samples.Count==0||Now-record.samples[record.samples.Count-1].time>=PopulationArchiveSettings.Current.sampleGameSeconds)
                {
                    record.infants=entities.Count(e=>e.Genes.CurrentAge==AgeState.Infant);record.adults=entities.Count(e=>e.Genes.CurrentAge==AgeState.Adult);record.elders=count-record.infants-record.adults;record.lastLiving=count;
                    var genes=new Dictionary<string,PopulationGeneCount>();foreach(var entity in entities)
                    {
                        var active=new HashSet<string>(entity.Genes.Evaluation.Genes.Where(g=>g.IsActive).Select(g=>g.Definition.Id));
                        foreach(string id in entity.Genes.Genome.Genes.Select(g=>g.DefinitionId).Distinct()){if(!genes.TryGetValue(id,out var value))genes[id]=value=new PopulationGeneCount{id=id};value.carried++;if(active.Contains(id))value.expressed++;}
                    }
                    record.genes=genes.Values.OrderBy(g=>g.id).ToList();Sample(record,count);Revision++;
                }
            }
            foreach(var record in Records.Values.Where(r=>r.current>0&&!seen.Contains(r.id)).ToArray())
            {record.decreases+=record.current;record.current=0;Event(record,"种群灭绝或全部成员离开");Sample(record,0);Revision++;}
        }
        private static void Sample(PopulationRecord record,int count)
        {
            record.samples.Add(new PopulationSample{time=Now,members=count,infants=count==0?0:record.infants,adults=count==0?0:record.adults,elders=count==0?0:record.elders});
            if(record.samples.Count>PopulationArchiveSettings.Current.maximumSamples)
            {
                // ★ 适配：保留首尾并压缩中间采样，长期世界不会无限增长；时间跨度不因容量限制消失。
                var compact=new List<PopulationSample>{record.samples[0]};for(int i=2;i<record.samples.Count-1;i+=2)compact.Add(record.samples[i]);compact.Add(record.samples[record.samples.Count-1]);record.samples=compact;
            }
        }
        public static bool SetDisplay(string id,string color,bool visible,bool? syncEntityColor=null,float? rangeOpacity=null)
        {
            if(WorldAuthority.IsReplica||!WorldAuthority.CanEdit||Find(id) is not PopulationRecord record||!ColorUtility.TryParseHtmlString(color,out var parsed)||rangeOpacity.HasValue&&(!float.IsFinite(rangeOpacity.Value)||rangeOpacity<0||rangeOpacity>1))return false;
            record.color="#"+ColorUtility.ToHtmlStringRGB(parsed);record.visible=visible;if(syncEntityColor.HasValue)record.syncEntityColor=syncEntityColor.Value;if(rangeOpacity.HasValue)record.rangeOpacity=rangeOpacity.Value;Revision++;DisplayRevision++;return true;
        }
        public static float OpacityFor(string id)=>Find(id) is PopulationRecord record&&record.rangeOpacity>=0?record.rangeOpacity:GameplayTuning.Current.populationFillOpacity;
        public static bool TryColor(string id,out Color color){color=default;return Find(id) is PopulationRecord record&&!string.IsNullOrEmpty(record.color)&&ColorUtility.TryParseHtmlString(record.color,out color);}
        public static bool IsVisible(string id)=>Find(id)?.visible??true;
        public static PopulationArchiveState Capture(){if(WorldRuntimeGate.Ready&&!WorldAuthority.IsReplica)Observe();return JsonUtility.FromJson<PopulationArchiveState>(JsonUtility.ToJson(new PopulationArchiveState{groups=Records.Values.OrderBy(r=>r.id).ToList()}));}
        public static void Validate(PopulationArchiveState state)
        {
            if(state==null)return;if(state.version!=1||state.groups==null)throw new InvalidDataException("种群档案版本无效");var ids=new HashSet<string>();
            foreach(var r in state.groups)
            {
                if(r!=null&&(!float.IsFinite(r.rangeOpacity)||r.rangeOpacity!= -1&&(r.rangeOpacity<0||r.rangeOpacity>1)))throw new InvalidDataException("种群范围不透明度无效");
                if(r==null||!Guid.TryParseExact(r.id,"N",out _)||!ids.Add(r.id)||string.IsNullOrWhiteSpace(r.name)||r.current<0||r.lastLiving<0||r.peak<r.current||!float.IsFinite(r.firstSeen)||r.firstSeen<0||!float.IsFinite(r.lastSeen)||r.lastSeen<r.firstSeen||r.samples==null||r.events==null||r.genes==null||!string.IsNullOrEmpty(r.color)&&!ColorUtility.TryParseHtmlString(r.color,out _))throw new InvalidDataException("种群档案内容无效");
                float last=-1;foreach(var s in r.samples){if(s==null||!float.IsFinite(s.time)||s.time<last||s.time<0||s.members<0||s.infants<0||s.adults<0||s.elders<0||s.infants+s.adults+s.elders!=s.members)throw new InvalidDataException("种群历史采样无效");last=s.time;}
                if(r.events.Any(e=>e==null||!float.IsFinite(e.time)||e.time<0||string.IsNullOrWhiteSpace(e.text))||r.genes.Any(g=>g==null||string.IsNullOrWhiteSpace(g.id)||g.carried<0||g.expressed<0||g.expressed>g.carried||g.carried>r.lastLiving))throw new InvalidDataException("种群事件或基因计数无效");
            }
            var byId=state.groups.ToDictionary(r=>r.id);foreach(var record in state.groups){var path=new HashSet<string>{record.id};var current=record;while(!string.IsNullOrEmpty(current.parentId)){if(!path.Add(current.parentId)||!byId.TryGetValue(current.parentId,out current))throw new InvalidDataException("种群谱系有循环或缺失来源");}}
        }
        public static void Restore(PopulationArchiveState state)
        {
            Validate(state);Records.Clear();if(state!=null)foreach(var record in JsonUtility.FromJson<PopulationArchiveState>(JsonUtility.ToJson(state)).groups)Records.Add(record.id,record);Revision++;DisplayRevision++;
        }
        public static void Clear(){Records.Clear();Revision++;DisplayRevision++;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Clear();
    }
    // ★ 新增：记录不依赖第二页或详情窗口是否打开，暂停不生成重复历史点。
    public sealed class PopulationArchiveObserver:MonoBehaviour
    {
        private float _next;
        public static void Ensure(){if(UnityEngine.Object.FindFirstObjectByType<PopulationArchiveObserver>()==null)new GameObject("种群档案记录").AddComponent<PopulationArchiveObserver>();}
        private void Update(){if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||Time.unscaledTime<_next)return;_next=Time.unscaledTime+PopulationArchiveSettings.Current.refreshSeconds;PopulationArchive.Observe();}
    }
}
