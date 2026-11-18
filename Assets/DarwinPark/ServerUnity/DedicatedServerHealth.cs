using System;
using System.Diagnostics;
using System.IO;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.UI
{
    // ✅ 新增：独立更新器只接受本次启动的健康凭证，旧日志或旧 READY 不能放行更新。
    internal sealed class DedicatedServerHealth
    {
        [Serializable] private sealed class Receipt
        {
            public string state, runId, release, worldId;
            public int pid, port;
        }
        private readonly string _runId = DedicatedServerMode.Argument("-server-run-id") ?? Guid.NewGuid().ToString("N");

        public void Write(string state, int port = 0, string worldId = "")
        {
            string path = Path.Combine(GamePaths.Root, "server-status.json");
            string temporary = path + ".tmp";
            var receipt = new Receipt
            {
                state = state, runId = _runId, pid = Process.GetCurrentProcess().Id,
                release = LanRelease.Current.release, port = port, worldId = worldId
            };
            File.WriteAllText(temporary, JsonUtility.ToJson(receipt));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
