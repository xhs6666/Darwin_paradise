using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditorInternal;
using UnityEditor.Profiling;
using UnityEngine;
namespace DarwinPark.EditorTools
{
    // ★ 新增：只在独立测试工程录制普通CPU层级，按实际样本自耗时与分配定位未计时的主线程，不以包含项相加猜测根因。
    internal static class UserFrameHierarchyAudit
    {
        [Serializable]private sealed class Row{public int frame;public string path;public float total,self,bytes,calls;}
        [Serializable]private sealed class Data{public List<Row> rows=new();}
        internal static void Start(){ProfilerDriver.enabled=true;}
        internal static void Finish(string folder)
        {
            int last=ProfilerDriver.lastFrameIndex;ProfilerDriver.enabled=false;var data=new Data();
            for(int frame=Math.Max(ProfilerDriver.firstFrameIndex,last-7);frame<=last;frame++)
            {
                using var view=ProfilerDriver.GetHierarchyFrameDataView(frame,0,HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,HierarchyFrameDataView.columnTotalTime,false);
                if(!view.valid)continue;var queue=new Queue<(int id,string path)>();queue.Enqueue((view.GetRootItemID(),""));
                while(queue.Count>0)
                {
                    var item=queue.Dequeue();string path=item.path+"/"+view.GetItemName(item.id);var row=new Row{frame=frame,path=path,total=view.GetItemColumnDataAsFloat(item.id,HierarchyFrameDataView.columnTotalTime),self=view.GetItemColumnDataAsFloat(item.id,HierarchyFrameDataView.columnSelfTime),bytes=view.GetItemColumnDataAsFloat(item.id,HierarchyFrameDataView.columnGcMemory),calls=view.GetItemColumnDataAsFloat(item.id,HierarchyFrameDataView.columnCalls)};data.rows.Add(row);
                    var children=new List<int>();view.GetItemChildren(item.id,children);foreach(int child in children)queue.Enqueue((child,path));
                }
            }
            if(data.rows.Count==0)throw new InvalidOperationException("CPU层级无有效帧");File.WriteAllText(Path.Combine(folder,"cpu-hierarchy.json"),JsonUtility.ToJson(data,true));
            File.WriteAllLines(Path.Combine(folder,"cpu-top.txt"),data.rows.OrderByDescending(v=>v.self).Take(60).Select(v=>$"frame={v.frame} self={v.self:F3} total={v.total:F3} bytes={v.bytes:F0} calls={v.calls:F0} {v.path}"));
        }
    }
}
