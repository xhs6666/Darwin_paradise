using System;
using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：新世界地貌配置随地形参数存档；旧档缺此节点时保留原地形。
    [Serializable]public sealed class LandformSettings
    {
        public const int CurrentVersion=6;
        public bool enabled=true;public int version=CurrentVersion,longitudeCells=256,latitudeCells=128;
        // ★ 新增：第二版宽盆地遮罩；旧档version=1仍走原计算，只有显式升级才改变地形。
        public float basinThreshold=.52f,basinTransition=.16f;
        public int lakeMeshSubdivisions=3;
        public float plateauStrength=.9f,plateauFrequency=2.3f,plateauHeight=.25f,plateauThreshold=.45f,plateauTransition=.12f,basinStrength=.06f;
        public float riverCatchment=38,riverHalfWidth=.8f,riverBedDepth=1.5f,riverSurfaceInset=.25f,lakeMinimumDepth=.8f,bankWidth=2.2f;
        public float moistureDistance=18,moistureBonus=12;public Color waterColor=new(.08f,.40f,.55f,1);
        // ★ 新增：第三版按汇水量加宽河道，并在内陆缓坡雕刻可蓄水盆地；全部参数随世界存档。
        // ★ 新增：第四版加深实际河床，独立倍率保留旧版种子结果；旧世界须通过现有地貌更新流程应用。
        public float riverDepthMultiplier=3;
        // ★ 新增：第五版保留浅水陆架，并在离岸推进到深水区时逐渐加深海盆。
        public float oceanDepthMultiplier=1.65f,oceanShelfStart=.87f,lakeDepthMultiplier=1.7f;
        public float riverWidthExponent=.38f,riverMaximumWidth=2.6f,channelSmoothing=.32f;
        public int riverCurveSegments=4;public float riverCurveStrength=.24f;
        public int basinCount=14,basinCandidates=1024;
        public float basinRadius=18,basinLakeDepth=4,basinMaximumRelief=6,basinSeparation=3;
        public float basinAspectRatio=.65f,basinIrregularity=.16f;
    }
}
