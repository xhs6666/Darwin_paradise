using System;
using System.IO;
using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：旧地形参数不隐式改变几何，新世界/显式保存的新节点才启用地貌。
    public static class LandformSaveCompatibility
    {
        [Serializable]private sealed class Envelope{public LandformSettings landforms=null;public LandNavigationSettings navigation=null;}
        public static void Validate(string json)
        {
            if(json.Contains("\"navigation\""))
            {
                var nav=JsonUtility.FromJson<Envelope>(json)?.navigation;if(nav==null||nav.longitudeCells<32||nav.longitudeCells>512||nav.latitudeCells<16||nav.latitudeCells>256||nav.expansionsPerFrame<1||nav.expansionsPerFrame>8192||nav.routeCacheCount<1||nav.routeCacheCount>8192)throw new InvalidDataException("陆地寻路配置无效");
                foreach(var field in typeof(LandNavigationSettings).GetFields())if(field.FieldType==typeof(float)){float n=(float)field.GetValue(nav);if(!float.IsFinite(n)||n<0)throw new InvalidDataException("寻路参数须为非负有限数："+field.Name);}
                if(nav.maximumSlopeDegrees>85||nav.segmentStep<=0||nav.pathMilliseconds<=0||nav.pathMilliseconds>10||nav.waypointArrival<=0)throw new InvalidDataException("寻路精度/时间预算无效");
            }
            if(!json.Contains("\"landforms\""))return;var value=JsonUtility.FromJson<Envelope>(json)?.landforms;if(value==null)throw new InvalidDataException("地貌配置为空");
            if((value.version<1||value.version>LandformSettings.CurrentVersion)||value.longitudeCells<32||value.longitudeCells>512||value.latitudeCells<16||value.latitudeCells>256)throw new InvalidDataException("地貌版本或水系分辨率无效");
            foreach(var field in typeof(LandformSettings).GetFields())if(field.FieldType==typeof(float)){float n=(float)field.GetValue(value);if(!float.IsFinite(n)||n<0)throw new InvalidDataException("地貌参数须为非负有限数："+field.Name);}
            if(value.riverCatchment<=0||value.riverHalfWidth<=0||value.riverBedDepth<=0||value.bankWidth<value.riverHalfWidth||value.plateauStrength>1||value.plateauThreshold>=1||value.plateauTransition<=0||value.version>=2&&(value.basinThreshold>1||value.basinTransition<=0||value.lakeMeshSubdivisions<1||value.lakeMeshSubdivisions>8))throw new InvalidDataException("河道或高原配置无效");
        }
        public static void Restore(PlanetTerrainMeshBuilder builder,string json)
        {if(!json.Contains("\"landforms\""))builder.landforms=new LandformSettings{enabled=false};if(!json.Contains("\"navigation\""))builder.navigation=new LandNavigationSettings();}
    }
}
