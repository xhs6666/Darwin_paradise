using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DarwinPark.Core
{
    // ★ 修复（防Bug回归）：部分Windows Mono的Process.WorkingSet64恒为0，用系统计数避免把内存不足误报为零占用。
    public static class DiagnosticMemory
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)] private struct Counters
        {
            public uint size,pageFaults;
            public UIntPtr peakWorkingSet,workingSet,peakPagedPool,pagedPool,peakNonPagedPool,nonPagedPool,pageFile,peakPageFile;
        }
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("psapi.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessMemoryInfo(IntPtr process,ref Counters counters,uint size);
#endif
        public static string WorkingSetBytes()
        {
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                var counters=new Counters{size=(uint)Marshal.SizeOf(typeof(Counters))};
                if(GetProcessMemoryInfo(GetCurrentProcess(),ref counters,counters.size)&&counters.workingSet.ToUInt64()>0)return counters.workingSet.ToUInt64().ToString();
#endif
                using(var process=Process.GetCurrentProcess()){process.Refresh();return process.WorkingSet64>0?process.WorkingSet64.ToString():"不可用";}
            }
            catch(Exception ex){return "不可用："+ex.GetType().Name;}
        }
    }
}
