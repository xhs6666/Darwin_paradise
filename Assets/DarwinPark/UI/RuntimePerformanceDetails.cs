using System;
using System.Globalization;
using System.Linq;
using System.Text;
using DarwinPark.Core;
using UnityEngine;
namespace DarwinPark.UI
{
    // ★ 新增：同一份JSON明细生成中文可读报告和设置页排行；按自身耗时排序，不能把父子包含项相加。
    public static class RuntimePerformanceDetails
    {
        private static int _tab;private static bool _all;
        private static string N(double number)=>number.ToString("F3",CultureInfo.InvariantCulture);
        public static string Summary(PerformanceReport report)
        {var scripts=report.scripts.Take(3).Select(s=>s.script+" "+s.selfPerFrame.ToString("F1")+"ms");return report.scripts.Count>0?"已覆盖脚本自身耗时/帧：\n"+string.Join("\n",scripts):"本次没有记录到脚本明细，可在游戏运行时重新采样。";}
        public static string Markdown(PerformanceReport report)
        {
            var b=new StringBuilder("# 游戏性能采样\n\n");b.AppendLine(report.context).AppendLine().AppendLine(report.coverageNote).AppendLine();
            foreach(var metric in report.metrics.Where(m=>m.available&&(m.name is "Frame elapsed" or "Main Thread" or "GPU Frame Time")))b.AppendLine(metric.name+"：平均 "+N(metric.mean)+" ms，P95 "+N(metric.p95)+" ms，最大 "+N(metric.maximum)+" ms。");
            b.AppendLine("\n## 脚本自身开销排行\n\n| 脚本 | 平均ms/帧 | P95 | 最大帧 | 帧占比 | 已计时调用 |\n| --- | ---: | ---: | ---: | ---: | ---: |");
            foreach(var s in report.scripts)b.AppendLine($"| {s.path} | {N(s.selfPerFrame)} | {N(s.p95Self)} | {N(s.maximumSelfFrame)} | {N(s.framePercent)}% | {s.measuredCalls} |");
            b.AppendLine("\n## 方法与算法排行\n\n| 方法 | 算法范围 | 自身ms/帧 | 包含ms/帧 | P95自身 | 最大单次 | 调用 |\n| --- | --- | ---: | ---: | ---: | ---: | ---: |");
            foreach(var s in report.hotspots)b.AppendLine($"| {s.path}::{s.method} | {s.algorithm} | {N(s.selfPerFrame)} | {N(s.inclusivePerFrame)} | {N(s.p95Self)} | {N(s.maximumCall)} | {s.calls} |");
            b.AppendLine("\n## 引擎分项（包含关系，不能直接相加）\n");foreach(var m in report.metrics.Where(m=>m.source=="engine"))b.AppendLine(m.name+"："+(m.available?$"{N(m.mean)} {m.unit}，最大 {N(m.maximum)}":"本平台未提供有效数据"));
            return b.ToString();
        }
        public static void Draw(PerformanceReport report)
        {
            GUILayout.Label("具体开销（按自身耗时排序）");GUILayout.BeginHorizontal();string[] labels={"脚本","方法 / 算法","引擎分项"};for(int i=0;i<labels.Length;i++)if(GUILayout.Button(labels[i],i==_tab?RuntimeMenuTheme.Primary:GUI.skin.button,GUILayout.Height(30)))_tab=i;GUILayout.EndHorizontal();
            var wrap=new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true};GUILayout.Label(report.coverageNote,wrap);int limit=_all?int.MaxValue:20;
            if(_tab==0)foreach(var s in report.scripts.Take(limit)){GUILayout.Label(s.script+"："+N(s.selfPerFrame)+" ms/帧  ("+s.framePercent.ToString("F1")+"%)",wrap);GUILayout.Label(s.path+"\nP95 "+N(s.p95Self)+" · 最大帧 "+N(s.maximumSelfFrame)+" · 已计时调用 "+s.measuredCalls,wrap);}
            else if(_tab==1)foreach(var s in report.hotspots.Take(limit)){GUILayout.Label(s.script+" :: "+s.method,wrap);GUILayout.Label(s.algorithm+" · 自身 "+N(s.selfPerFrame)+" · 包含 "+N(s.inclusivePerFrame)+" ms/帧\n最大单次 "+N(s.maximumCall)+" ms · 调用 "+s.calls+"\n"+s.path,wrap);}
            else foreach(var m in report.metrics.Where(m=>m.source=="engine"))GUILayout.Label(m.name+"："+(m.available?N(m.mean)+" "+m.unit:"未提供数据"),wrap);
            if(RuntimeMenuTheme.Button(_all?"仅显示前20项":"显示全部记录",null,false,28))_all=!_all;
        }
    }
}
