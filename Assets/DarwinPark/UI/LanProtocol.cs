using System;
using System.Collections.Generic;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.UI
{
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
    }
    [Serializable] public sealed class LanActor
    {public EntitySaveState state;public float suppression;}
    [Serializable] public sealed class LanFrame
    {
        public long sequence;public float time,scale,sun;public bool running;
        public WorldGameSettings gameSettings;
        public PopulationArchiveState population;
        public PopulationMaintenanceState maintenance;
        public Quaternion rotation;public List<LanActor> actors=new List<LanActor>();
        public List<ColonyResourceSave> resources=new List<ColonyResourceSave>();
        public bool randomBusy;public int randomSpawned,randomRequested;public string randomStatus;
        public float[] ore;public int oreResolution;public float oreTimer;
    }
    [Serializable] public sealed class LanMessage
    {
        public int version=1;public string type,id,text,entity;public bool cheats;
        public List<LanPlayer> players;public LanCommand command;public LanFrame frame;public WorldSaveData world;
    }
}
