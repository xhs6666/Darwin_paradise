using System;
using System.IO;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：诊断频率和磁盘预算独立配置，不能为排查卡顿引入逐帧写盘。
    [Serializable] public sealed class RuntimeDiagnosticSettings
    {
        public int version=1;
        public bool enabled=true;
        public int snapshotMilliseconds=5000, sampleMilliseconds=1000, stallMilliseconds=15000;
        public int shutdownMilliseconds=3000;
        public int retainedSessions=8, criticalBytes=524288, criticalPerSecond=8;
        public int tailBytes=262144, retainedExports=3, exportMaximumBytes=67108864, nativeMaximumFiles=16;
        public int nativeLookbackHours=48,nativeSearchDepth=2;
        public void Validate()
        {
            if(version!=1||snapshotMilliseconds<100||sampleMilliseconds<100||shutdownMilliseconds<100||stallMilliseconds<snapshotMilliseconds*2L||
               retainedSessions<2||criticalBytes<4096||criticalPerSecond<1||tailBytes<1024||retainedExports<1||
               exportMaximumBytes<tailBytes*4L||nativeMaximumFiles<1||nativeLookbackHours<1||nativeLookbackHours>8760||nativeSearchDepth<0||nativeSearchDepth>8)throw new InvalidDataException("崩溃诊断配置无效");
        }
        public static RuntimeDiagnosticSettings Load()
        {
            var asset=Resources.Load<TextAsset>("runtime_diagnostics");
            if(asset==null)throw new InvalidDataException("缺少 Resources/runtime_diagnostics.json");
            var settings=JsonUtility.FromJson<RuntimeDiagnosticSettings>(asset.text);
            if(settings==null)throw new InvalidDataException("崩溃诊断配置为空");settings.Validate();return settings;
        }
    }
}
