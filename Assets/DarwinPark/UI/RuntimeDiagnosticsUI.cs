using System;
using System.Threading.Tasks;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：设置菜单提供诊断包而非新的常驻HUD；压缩/读盘在后台，显示真实成功或失败。
    public static class RuntimeDiagnosticsUI
    {
        private static Task<string> _export;
        public static void Draw()
        {
            if(RuntimeCrashDiagnostics.PreviousAbnormalSessions>0)GUILayout.Label("发现上次未正常结束的记录，可导出诊断包排查。",RuntimeMenuTheme.Muted);
            if(!string.IsNullOrEmpty(RuntimeFileLog.Failure))GUILayout.Label("文件日志故障："+RuntimeFileLog.Failure);
            if(!string.IsNullOrEmpty(RuntimeCrashDiagnostics.Failure))GUILayout.Label("诊断故障："+RuntimeCrashDiagnostics.Failure);
            bool busy=_export!=null&&!_export.IsCompleted;
            bool previous=GUI.enabled;GUI.enabled=previous&&!busy&&!string.IsNullOrEmpty(RuntimeCrashDiagnostics.DirectoryPath);
            if(RuntimeMenuTheme.Button(busy?"正在导出诊断包…":"导出诊断包",null,false,36))_export=RuntimeCrashDiagnostics.ExportAsync();
            GUI.enabled=previous;
            if(_export?.IsFaulted==true)GUILayout.Label("导出失败："+_export.Exception.GetBaseException().Message,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
            else if(_export?.Status==TaskStatus.RanToCompletion)GUILayout.Label("已保存："+_export.Result,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
        }
    }
}
