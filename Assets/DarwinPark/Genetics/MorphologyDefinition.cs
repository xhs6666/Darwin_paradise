using System;
using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Genetics
{
    // ★ 新增：纯数据形体协议，逻辑服务端不依赖模型、材质或表现程序集。
    [Serializable] public sealed class PartBinding {public string slotId,partId,stateId,requireGene,forbidGene;}
    // ★ 新增：参数规则也支持组合条件，让移动型受光部件收紧而不改变其营养能力。
    [Serializable] public sealed class PartModifier {public string slotId,parameter="scaleY",attributeId,requireGene,forbidGene;public float minimum=.8f,maximum=1.2f,rangeMin,rangeMax=100;}
    // ★ 新增：个体部件可编辑形状、身体坐标偏移及朝向，仍随基因组和模板持久化。
    [Serializable] public sealed class PartOverride {public string slotId;public Vector3 scale=Vector3.one,position,rotation;}
    [Serializable] public sealed class BodySlot {public string id,displayName,parent,partId;public string attachment="socket";public Vector3 position,rotation,scale=Vector3.one;public float surfaceOffset;}
    [Serializable] public sealed class BodyDefinition {public int version=2;public string id;public List<BodySlot> slots=new(); internal Dictionary<string,BodySlot> SlotsById;}
    [Serializable] public sealed class PartDefinition {public string id,primitive="sphere",modelResource="",color="#AABBAA",efficiencyAttribute="";public string efficientColor="#126B20"; internal Color ParsedColor, ParsedEfficientColor;public float efficiencyMaximum=15;public Vector3 minimumScale=new(.05f,.05f,.05f),maximumScale=new(4,4,4);}
    [Serializable] public sealed class PartCatalog {public int version=2,maximumParts=24;public string simplifiedPartId="body_basic";public float refreshMilliseconds=2,farDistance=100;public List<string> parts=new(),bodies=new(); internal string[] EfficiencyAttributes;}
    [Serializable] public sealed class MorphologySnapshot {public int version=2;public PartCatalog settings;public List<PartDefinition> parts=new();public List<BodyDefinition> bodies=new();}
    [Serializable] public sealed class ExpressedPart {public string slotId,partId;public Vector3 position,scale;public Quaternion rotation;public Color color;}

    [Serializable] public sealed class SavedBodyComposition
    {
        public long Key;
        public List<ExpressedPart> Parts;

        public bool IsValid(HashSet<string> partIds, int maximum)
        {
            if(Parts==null||Parts.Count==0||Parts.Count>maximum)return false;
            var slots=new HashSet<string>();
            foreach(var part in Parts)
            {
                if(part==null||string.IsNullOrEmpty(part.slotId)||!slots.Add(part.slotId)||!partIds.Contains(part.partId)||
                    !Finite(part.position)||!Finite(part.scale)||part.scale.x<=0||part.scale.y<=0||part.scale.z<=0||
                    !Finite(new Vector3(part.rotation.x,part.rotation.y,part.rotation.z))||!float.IsFinite(part.rotation.w)||
                    !Finite(new Vector3(part.color.r,part.color.g,part.color.b))||!float.IsFinite(part.color.a))return false;
            }
            return slots.Contains("body");
        }

        private static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
    }
}
