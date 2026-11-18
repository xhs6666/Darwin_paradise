namespace DarwinPark.UI
{
using System;
using System.Collections.Generic;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using UnityEngine;


    [Serializable] public sealed class LanPlayer
    {public string id,name,entity;public bool administrator;}
    [Serializable] public sealed class LanCommand
    {
        public string kind,entity,gene,name,target,resource,parameter;
        public float value,scale;public bool enabled,landOnly,running,attack,jump,rts;
        public int count;public Vector3 point,direction,aim;public Genome genome;
        public string[] selection;public RtsOrderKind order;
        public List<SavedGeneDefinition> definitions;public GeneTreeDocument tree;
        public RandomSpawnRecipe recipe;public PopulationMaintenanceRule maintenanceRule;
        public PartOverride bodyPart;public EntityActivityPlanSave activityPlan;
    }
    [Serializable] public sealed class LanActor
    {public EntitySaveState state;public float suppression;}
    [Serializable] public sealed class LanFrame
    {
        public long sequence;public float time,scale,sun;public bool running;
        public WorldGameSettings gameSettings;
        public PopulationArchiveState population;
        public PopulationMaintenanceState maintenance;
        // ★ 适配：低频同步生态配置，客户端显示与房主应用的默认项一致。
        public string ecologyDefaults;
        public Quaternion rotation;public List<LanActor> actors=new List<LanActor>();
        public List<ColonyResourceSave> resources=new List<ColonyResourceSave>();
        public bool randomBusy;public int randomSpawned,randomRequested;public string randomStatus;
        public float[] ore;public int oreResolution;public float oreTimer;
    }
    [Serializable] public sealed class LanMessage
    {
        public int version=DarwinPark.ServerCore.ServerCompatibility.WireVersion;public string type,id,text,entity;public bool cheats;
        public DarwinPark.ServerCore.ServerCompatibility compatibility;
        public long worldRevision;
        public List<LanPlayer> players;public LanCommand command;public LanFrame frame;public WorldSaveData world;
    }
}

namespace DarwinPark.UI
{
using System.IO;
using DarwinPark.ServerCore;
using UnityEngine;


    // ✦ 新增：发行信息随二进制打包，不允许运维配置放宽协议检查。
    public static class LanRelease
    {
        private static ServerCompatibility _current;
        public static ServerCompatibility Current
        {
            get
            {
                if(_current != null) return _current;
                var asset = Resources.Load<TextAsset>("Entities/server_release");
                if(asset == null) throw new InvalidDataException("Missing Entities/server_release");
                var release = JsonUtility.FromJson<ServerCompatibility>(asset.text);
                if(release == null) throw new InvalidDataException("Invalid server release manifest");
                release.Validate(); return _current = release;
            }
        }
    }
}

namespace DarwinPark.UI
{
using System;
using System.IO;
using UnityEngine;


    // ★ 新增：局域网端口、队列和指令规模有限且可配置，错误配置不能静默启动无限制监听。
    [Serializable] public sealed class LanSettings
    {
        public float worldLoadTimeout=180;
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
