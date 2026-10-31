using System;
using System.IO;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 新增：日志容量、排队与写盘频率配置化，避免测试时大量输出拖慢游戏或无限占用磁盘。
    [Serializable]
    public sealed class RuntimeLogSettings
    {
        public int version,maximumFileBytes,retainedFiles,queuedEntries,maximumEntryCharacters,flushIntervalMilliseconds,shutdownTimeoutMilliseconds;
        public bool enabled;
        public string directoryName;
        public void Validate()
        {
            if(version!=1||string.IsNullOrWhiteSpace(directoryName)||directoryName!=Path.GetFileName(directoryName)||directoryName=="."||directoryName==".."||
                maximumEntryCharacters<64||maximumFileBytes<(long)maximumEntryCharacters*4+256||retainedFiles<2||queuedEntries<8||flushIntervalMilliseconds<10||shutdownTimeoutMilliseconds<100)
                throw new InvalidDataException("运行日志配置无效");
        }
        public static RuntimeLogSettings Load()
        {
            var asset=Resources.Load<TextAsset>("runtime_logging");if(asset==null)throw new InvalidDataException("缺少 Resources/runtime_logging.json，无法启用文件日志");
            var settings=JsonUtility.FromJson<RuntimeLogSettings>(asset.text);if(settings==null)throw new InvalidDataException("运行日志配置为空");settings.Validate();return settings;
        }
    }
}
