using System;
using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：新世界地貌配置随地形参数存档；旧档缺此节点时保留原地形。
    [Serializable]public sealed class LandformSettings
    {
        public bool enabled=true;public int version=1,longitudeCells=256,latitudeCells=128;
        public float plateauStrength=.9f,plateauFrequency=2.3f,plateauHeight=.25f,plateauThreshold=.45f,plateauTransition=.12f,basinStrength=.06f;
        public float riverCatchment=38,riverHalfWidth=.8f,riverBedDepth=1.5f,riverSurfaceInset=.25f,lakeMinimumDepth=.8f,bankWidth=2.2f;
        public float moistureDistance=18,moistureBonus=12;public Color waterColor=new(.08f,.40f,.55f,1);
    }
}
