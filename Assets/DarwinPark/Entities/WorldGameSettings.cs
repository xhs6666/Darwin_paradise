using System;
using System.IO;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 新增：每个世界自己的生成上限；旧档缺字段使用资源默认值，0明确表示不限。
    [Serializable] public sealed class WorldGameSettings
    {
        public int version=1,entityLimit;
        public static WorldGameSettings Normalize(WorldGameSettings value)
        {
            if(value==null||value.version==0)
            {
                var asset=Resources.Load<TextAsset>("Entities/world_game_settings");
                if(asset==null)throw new InvalidDataException("缺少 Entities/world_game_settings");
                value=JsonUtility.FromJson<WorldGameSettings>(asset.text);
            }
            if(value==null||value.version!=1||value.entityLimit<0)throw new InvalidDataException("存档实体上限无效");
            return new WorldGameSettings{version=1,entityLimit=value.entityLimit};
        }
    }
    public static class EntityPopulationLimit
    {
        private static WorldGameSettings _current;private static int _restoring;
        public static int Maximum=>_current?.entityLimit??0;
        public static bool CanSpawn=>WorldAuthority.IsReplica||_restoring>0||!WorldRuntimeGate.Loading&&(Maximum==0||EntityRegistry.All.Count<Maximum);
        public static WorldGameSettings Capture()=>WorldGameSettings.Normalize(_current);
        public static void UseSnapshot(WorldGameSettings value)=>_current=WorldGameSettings.Normalize(value);
        // ★ 适配：超出新上限的旧存档必须完整恢复；按单个实体使用作用域，不跨yield持有豁免。
        public static IDisposable RestoreScope(){_restoring++;return new Scope();}
        private sealed class Scope:IDisposable {private bool _disposed;public void Dispose(){if(_disposed)return;_disposed=true;_restoring--;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void Clear(){_current=null;_restoring=0;}
    }
}
