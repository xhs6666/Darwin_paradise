using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace DarwinPark.Core
{
    [Serializable]public sealed class PerformanceHotspot
    {public string script,path,method,algorithm;public long calls;public double inclusivePerFrame,selfPerFrame,p95Self,maximumSelfFrame,maximumCall,framePercent;}
    [Serializable]public sealed class PerformanceScriptCost
    {public string script,path;public long measuredCalls;public double selfPerFrame,p95Self,maximumSelfFrame,framePercent;}
    // ★ 新增：仅主动采样时按真实调用栈扣除子范围，定位脚本/方法自身开销；不把尚未覆盖的代码伪装成零耗时。
    public static class RuntimeCostBreakdown
    {
        private readonly struct Key
        {public readonly PerformanceTimings.Area area;public readonly string file,method;public Key(PerformanceTimings.Area a,string f,string m){area=a;file=f;method=m;}}
        private sealed class Keys:IEqualityComparer<Key>
        {public bool Equals(Key a,Key b)=>a.area==b.area&&ReferenceEquals(a.file,b.file)&&ReferenceEquals(a.method,b.method);public int GetHashCode(Key k)=>HashCode.Combine((int)k.area,RuntimeHelpers.GetHashCode(k.file),RuntimeHelpers.GetHashCode(k.method));}
        private sealed class Entry
        {public string path,method,algorithm;public long calls,total,self,frameTotal,frameSelf,maximumCall;public long[] selfFrames;}
        private struct Frame {public Entry entry;public long children;}
        private static readonly Dictionary<Key,Entry> Entries=new(new Keys());private static readonly Frame[] Stack=new Frame[256];
        private static int _depth,_capacity,_frames,_thread;public static int DroppedScopes{get;private set;}
        internal static void Begin(int capacity){Entries.Clear();Array.Clear(Stack,0,Stack.Length);_depth=_frames=0;_capacity=capacity;DroppedScopes=0;_thread=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        internal static int Enter(PerformanceTimings.Area area,string file,string method)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=_thread)return -1;
            if(_depth==Stack.Length){DroppedScopes++;return -1;}var key=new Key(area,file,method);
            if(!Entries.TryGetValue(key,out var entry))
            {string path=file.Replace('\\','/');int assets=path.IndexOf("Assets/",StringComparison.Ordinal);if(assets>=0)path=path.Substring(assets);entry=new Entry{path=path,method=method,algorithm=area.ToString(),selfFrames=new long[_capacity]};Entries.Add(key,entry);}
            int token=_depth++;Stack[token]=new Frame{entry=entry};return token;
        }
        internal static void Leave(int token,long ticks)
        {
            if(token<0)return;if(token!=_depth-1){DroppedScopes++;return;}var frame=Stack[token];Stack[token]=default;_depth--;
            var entry=frame.entry;long self=Math.Max(0,ticks-frame.children);entry.calls++;entry.total+=ticks;entry.self+=self;entry.frameTotal+=ticks;entry.frameSelf+=self;entry.maximumCall=Math.Max(entry.maximumCall,ticks);
            if(_depth>0)Stack[_depth-1].children+=ticks;
        }
        internal static void RecordFrame(){foreach(var entry in Entries.Values){if(_frames<_capacity)entry.selfFrames[_frames]=entry.frameSelf;entry.frameSelf=entry.frameTotal=0;}_frames++;}
        private static double Ms(long ticks)=>ticks*1000d/Stopwatch.Frequency;
        private static double Percentile(long[] values,int used){var copy=new long[used];Array.Copy(values,copy,used);Array.Sort(copy);return used==0?0:Ms(copy[Math.Min(used-1,(int)(used*.95))]);}
        internal static void Finish(PerformanceReport report,double frameMean)
        {
            int frames=Math.Max(1,_frames),used=Math.Min(_frames,_capacity);var scripts=new Dictionary<string,(long calls,long total,long[] frames)>();
            foreach(var entry in Entries.Values)
            {
                double self=Ms(entry.self)/frames;report.hotspots.Add(new PerformanceHotspot{script=System.IO.Path.GetFileName(entry.path),path=entry.path,method=entry.method,algorithm=entry.algorithm,calls=entry.calls,inclusivePerFrame=Ms(entry.total)/frames,selfPerFrame=self,p95Self=Percentile(entry.selfFrames,used),maximumSelfFrame=used==0?0:Ms(entry.selfFrames.Take(used).Max()),maximumCall=Ms(entry.maximumCall),framePercent=frameMean>0?self/frameMean*100:0});
                if(!scripts.TryGetValue(entry.path,out var script))script=(0,0,new long[used]);script.calls+=entry.calls;script.total+=entry.self;for(int i=0;i<used;i++)script.frames[i]+=entry.selfFrames[i];scripts[entry.path]=script;
            }
            report.hotspots.Sort((a,b)=>b.selfPerFrame.CompareTo(a.selfPerFrame));
            foreach(var pair in scripts){double self=Ms(pair.Value.total)/frames;report.scripts.Add(new PerformanceScriptCost{script=System.IO.Path.GetFileName(pair.Key),path=pair.Key,measuredCalls=pair.Value.calls,selfPerFrame=self,p95Self=Percentile(pair.Value.frames,used),maximumSelfFrame=used==0?0:Ms(pair.Value.frames.Max()),framePercent=frameMean>0?self/frameMean*100:0});}
            report.scripts.Sort((a,b)=>b.selfPerFrame.CompareTo(a.selfPerFrame));report.droppedDetailScopes=DroppedScopes;
            report.coverageNote="脚本/方法排行只统计已覆盖的主线程计时范围。自身耗时扣除已计时子调用；包含耗时不能相加。未覆盖脚本、引擎、等待及采样本身仍计入实际帧耗时。";
        }
        internal static void Clear(){Entries.Clear();Array.Clear(Stack,0,Stack.Length);_depth=0;}
    }
}
