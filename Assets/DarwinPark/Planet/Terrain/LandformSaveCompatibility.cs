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
            // ★ 新增：限制新版生成预算与宽度，拒绝会导致无界循环或河床/水面失配的存档参数。
            // ★ 适配：拒绝零深度或无界倍率，保证渲染、碰撞和游泳采样共用有效河床。
            if(value.version>=5&&(value.oceanDepthMultiplier<1||value.oceanDepthMultiplier>4||
                value.oceanShelfStart<=0||value.oceanShelfStart>=1||value.lakeDepthMultiplier<1||value.lakeDepthMultiplier>4))
                throw new InvalidDataException("海床或湖床深度参数无效");
            if(value.version>=4&&(value.riverDepthMultiplier<1||value.riverDepthMultiplier>8))throw new InvalidDataException("河床深度倍率无效");
            if(value.version>=3&&(value.riverMaximumWidth<value.riverHalfWidth||value.riverWidthExponent>1||value.channelSmoothing>.5f||value.basinCount<0||value.basinCount>128||value.basinCandidates<value.basinCount||value.basinCandidates>16384||value.basinRadius<=0||value.basinLakeDepth<=value.lakeMinimumDepth||value.basinSeparation<2))throw new InvalidDataException("新版水系或盆地配置无效");
            if(value.version>=3&&(value.riverCurveSegments<1||value.riverCurveSegments>8||value.riverCurveStrength>.4f))throw new InvalidDataException("河道曲线细分或弯曲强度无效");
            if(value.version>=3&&(value.basinAspectRatio<.25f||value.basinAspectRatio>1||value.basinIrregularity>.4f))throw new InvalidDataException("盆地长宽比或岸线扰动无效");
        }
        public static void Restore(PlanetTerrainMeshBuilder builder,string json)
        {if(!json.Contains("\"landforms\""))builder.landforms=new LandformSettings{enabled=false};if(!json.Contains("\"navigation\""))builder.navigation=new LandNavigationSettings();}
    }
}
