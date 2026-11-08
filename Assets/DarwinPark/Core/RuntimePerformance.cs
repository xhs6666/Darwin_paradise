using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：只在主动采样时计时，发布版也能定位真实游戏逻辑；嵌套项目是包含关系，不能相加当成总帧耗时。
    public static class PerformanceTimings
    {
        // ★ 适配：把活体大项继续细分，避免把行动、邻域重建和补给开销都误归因为合并算法。
        public enum Area { Scheduler,Genes,StatePerception,Vitals,Environment,Population,ColonyTasks,GroundAnchor,Gravity,BottomUI,ResourceVisuals,GeneSignature,GeneRecalculation,Nutrition,Reproduction,Locomotion,Neighborhood,Feeding,Formation,Count }
        private static readonly long[] Ticks=new long[(int)Area.Count],Calls=new long[(int)Area.Count];
        public static bool Capturing {get;private set;}
        public readonly struct Scope:IDisposable
        {
            private readonly long _start;private readonly Area _area;
            internal Scope(Area area){_area=area;_start=Capturing?Stopwatch.GetTimestamp():0;}
            public void Dispose(){if(_start!=0&&Capturing){Ticks[(int)_area]+=Stopwatch.GetTimestamp()-_start;Calls[(int)_area]++;}}
        }
        public static Scope Measure(Area area)=>new Scope(area);
        internal static void Begin(){if(Capturing)throw new InvalidOperationException("已有性能采样正在执行");Array.Clear(Ticks,0,Ticks.Length);Array.Clear(Calls,0,Calls.Length);Capturing=true;}
        internal static void End(){Capturing=false;}
        internal static double Take(int area,out long calls){long ticks=Ticks[area];calls=Calls[area];Ticks[area]=Calls[area]=0;return ticks*1000d/Stopwatch.Frequency;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){Capturing=false;Array.Clear(Ticks,0,Ticks.Length);Array.Clear(Calls,0,Calls.Length);}
    }
    [Serializable]public sealed class PerformanceMetric
    {public string name,unit,source;public bool available;public int samples;public long calls;public double mean,p95,maximum;}
    [Serializable]public sealed class PerformanceReport
    {public int version=1,frames;public string context;public bool truncated;public List<PerformanceMetric> metrics=new List<PerformanceMetric>();}
    // ★ 新增：采样期间只写预分配缓冲，结束后才排序/序列化；引擎计数不可用时明确缺省，不把0伪装成零成本。
    public sealed class PerformanceSampleSession:IDisposable
    {
        private readonly double[][] _areas;private readonly long[] _calls;private readonly double[] _frames;
        private readonly List<(string name,ProfilerRecorder recorder)> _engine=new List<(string,ProfilerRecorder)>();private bool _closed;private int _count;
        private static readonly string[] EngineNames={"Main Thread","Render Thread","PlayerLoop","BehaviourUpdate","FixedBehaviourUpdate","Physics.Simulate","Physics.Processing","Physics.FetchResults","GC.Collect","GC Allocated In Frame","Gfx.WaitForPresentOnGfxThread","Gfx.WaitForRenderThread","Gfx.PresentFrame","Gfx.ProcessCommands","WaitForTargetFPS","Draw Calls Count","Triangles Count","SetPass Calls Count"};
        public PerformanceSampleSession(int capacity=8192)
        {
            _frames=new double[capacity];int count=(int)PerformanceTimings.Area.Count;_areas=new double[count][];_calls=new long[count];for(int i=0;i<count;i++)_areas[i]=new double[capacity];
            PerformanceTimings.Begin();
            try{var available=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(available);var names=new HashSet<string>(EngineNames);foreach(var handle in available){var description=ProfilerRecorderHandle.GetDescription(handle);if(names.Remove(description.Name))_engine.Add((description.Name,ProfilerRecorder.StartNew(description.Category,description.Name,capacity,ProfilerRecorderOptions.Default)));}}
            catch{Dispose();throw;}
        }
        public void RecordFrame(double milliseconds)
        {if(_closed)return;for(int i=0;i<_areas.Length;i++){double value=PerformanceTimings.Take(i,out long calls);_calls[i]+=calls;if(_count<_frames.Length)_areas[i][_count]=value;}if(_count<_frames.Length)_frames[_count]=milliseconds;_count++;}
        private static PerformanceMetric Metric(string name,string unit,string source,double[] values,int frames,long calls=0)
        {Array.Sort(values);return new PerformanceMetric{name=name,unit=unit,source=source,available=true,samples=values.Length,calls=calls,mean=values.Sum()/Math.Max(1,frames),p95=values.Length>0?values[Math.Min(values.Length-1,(int)(values.Length*.95))]:0,maximum=values.Length>0?values[values.Length-1]:0};}
        public PerformanceReport Finish(string context)
        {
            if(_closed)throw new ObjectDisposedException(nameof(PerformanceSampleSession));
            var report=new PerformanceReport{frames=_count,context=context,truncated=_count>_frames.Length};int used=Math.Min(_count,_frames.Length);
            report.metrics.Add(Metric("Frame elapsed","ms","wall",_frames.Take(used).ToArray(),used));
            for(int i=0;i<_areas.Length;i++)report.metrics.Add(Metric(((PerformanceTimings.Area)i).ToString(),"ms","game inclusive",_areas[i].Take(used).ToArray(),used,_calls[i]));
            foreach(var entry in _engine){var recorder=entry.recorder;recorder.Stop();bool time=recorder.UnitType==ProfilerMarkerDataUnit.TimeNanoseconds;var samples=recorder.ToArray();var values=samples.Select(s=>s.Value*(time?1e-6:1)).ToArray();var metric=Metric(entry.name,time?"ms":recorder.UnitType.ToString(),"engine",values,Math.Max(1,values.Length));metric.available=recorder.Valid&&values.Length>0;report.metrics.Add(metric);if(recorder.WrappedAround)report.truncated=true;}
            foreach(var name in EngineNames)if(!report.metrics.Exists(m=>m.name==name))report.metrics.Add(new PerformanceMetric{name=name,source="engine",available=false});
            Dispose();return report;
        }
        public void Dispose(){if(_closed)return;_closed=true;foreach(var entry in _engine)entry.recorder.Dispose();PerformanceTimings.End();}
    }
}
