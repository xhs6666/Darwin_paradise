using System;
using System.IO;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：局域网端口、队列和指令规模有限且可配置，错误配置不能静默启动无限制监听。
    [Serializable] public sealed class LanSettings
    {
        public int version,port,maximumMembers,maximumPacketBytes,maximumQueuedPackets,maximumCommandsPerSecond,maximumRtsSelection,maximumGenomeGenes,maximumGeneDefinitions;
        public float snapshotInterval,inputInterval,inputTimeout,connectionTimeout,rtsArrivalDistance,rtsFormationSpacing,rtsPickRadius,maximumTimeScale,oreSnapshotInterval;
        private static LanSettings _current;
        public static LanSettings Current
        {
            get
            {
                if(_current!=null)return _current;var asset=Resources.Load<TextAsset>("Entities/lan_multiplayer");if(asset==null)throw new InvalidDataException("缺少 Entities/lan_multiplayer");
                var value=JsonUtility.FromJson<LanSettings>(asset.text);
                if(value==null||value.version!=1||value.port<1||value.port>65535||value.maximumMembers<1||value.maximumPacketBytes<1024||value.maximumQueuedPackets<1||value.maximumCommandsPerSecond<1||value.maximumRtsSelection<1)throw new InvalidDataException("局域网配置无效");
                foreach(float n in new[]{value.snapshotInterval,value.inputInterval,value.inputTimeout,value.connectionTimeout,value.rtsArrivalDistance,value.rtsFormationSpacing,value.rtsPickRadius,value.maximumTimeScale,value.oreSnapshotInterval})if(float.IsNaN(n)||float.IsInfinity(n)||n<=0)throw new InvalidDataException("局域网参数必须为有限正数");
                return _current=value;
            }
        }
    }
}
