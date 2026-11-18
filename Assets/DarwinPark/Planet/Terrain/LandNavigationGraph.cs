using System;
using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Planet
{
    [Serializable]public sealed class LandNavigationSettings
    {
        public int longitudeCells=256,latitudeCells=128,expansionsPerFrame=512,routeCacheCount=512;
        public float shoreClearance=.2f,maximumSlopeDegrees=50,segmentStep=1.5f,pathMilliseconds=1.5f,repathSeconds=.6f,failureRetrySeconds=2,waypointArrival=1.5f;
        public float routeDeviationTolerance=.25f;
    }
    // ★ 新增：地形装载时一次建立陆地连通图；路径查询只用数组，不反复查询场景和碰撞体。
    public sealed class LandNavigationGraph
    {
        public readonly LandNavigationSettings Settings;public readonly PlanetTerrainData Terrain;public readonly int Width,Height;public readonly Vector3[] Points,Directions;public readonly bool[] Walkable;public readonly int[] Regions;private readonly byte[] _edges;
        private struct ShoreSample
        {
            public Vector3 direction;
            public float ground,water;
            public bool hasGround,hasWater;
        }
        private ShoreSample[] _shoreSamples;
        private readonly float _surfaceRadius,_segmentStep,_slopeLimit,_highestWaterRadius;
        public static readonly int[] DX={-1,0,1,-1,1,-1,0,1},DY={-1,-1,-1,0,0,1,1,1};
        public LandNavigationGraph(PlanetTerrainData data,LandNavigationSettings settings)
        {
            _surfaceRadius=data.Radius+data.SeaLevel*data.HeightScale;
            _highestWaterRadius=data.HeightScale>0
                ? data.Radius+(data.Hydrology?.MaximumWaterLevel??data.SeaLevel)*data.HeightScale+.001f
                : float.PositiveInfinity;
            _segmentStep=Mathf.Max(.2f,data.Hydrology!=null?Mathf.Min(settings.segmentStep,data.Hydrology.Settings.riverHalfWidth):settings.segmentStep);
            _slopeLimit=Mathf.Tan(settings.maximumSlopeDegrees*Mathf.Deg2Rad);
            Terrain=data;Settings=settings;Width=Mathf.Clamp(settings.longitudeCells,32,512);Height=Mathf.Clamp(settings.latitudeCells,16,256);int n=Width*Height;Points=new Vector3[n];Directions=new Vector3[n];Walkable=new bool[n];Regions=new int[n];_edges=new byte[n];
            for(int i=0;i<n;i++){var dir=PlanetTerrainSampler.DirectionFromUV((i%Width+.5f)/Width,(i/Width+.5f)/Height);Directions[i]=dir;float surface=PlanetCoordinate.SurfaceRadius(data,dir);Points[i]=dir*surface;Walkable[i]=HasDryClearance(dir,surface);}
            for(int i=0;i<n;i++)if(Walkable[i])for(int edge=0;edge<8;edge++){int next=Neighbour(i,edge);if(next<0||!Walkable[next])continue;if(Segment(Directions[i],Directions[next]))_edges[i]|=(byte)(1<<edge);}
            int region=0;var queue=new Queue<int>();for(int i=0;i<n;i++)if(Walkable[i]&&Regions[i]==0){Regions[i]=++region;queue.Enqueue(i);while(queue.Count>0){int node=queue.Dequeue();for(int e=0;e<8;e++)if(Linked(node,e)){int next=Neighbour(node,e);if(Regions[next]==0){Regions[next]=region;queue.Enqueue(next);}}}}
            _shoreSamples=new ShoreSample[8192];
        }

        // ★ 适配：只按精确方向缓存实测地形和水位；格子碰撞覆盖，不拿相邻点的水位替代细河。
        private ref ShoreSample Slot(Vector3 direction)
        {
            uint hash=(uint)direction.GetHashCode();
            hash^=hash>>16;hash*=0x7feb352d;hash^=hash>>15;
            ref var slot=ref _shoreSamples[hash&(uint)(_shoreSamples.Length-1)];
            if(!slot.direction.Equals(direction))slot=new ShoreSample{direction=direction};
            return ref slot;
        }

        public float WaterRadius(Vector3 direction)
        {
            if(_shoreSamples==null||Terrain.Hydrology==null||!DarwinPark.Core.SimulationOptimization.Current.shoreSampleReuse)
                return PlanetCoordinate.WaterSurfaceRadius(Terrain,direction);
            ref var slot=ref Slot(direction);
            if(!slot.hasWater)
            {
                slot.water=PlanetCoordinate.WaterSurfaceRadius(Terrain,direction);
                slot.hasWater=true;
            }
            return slot.water;
        }

        private bool DrySample(Vector3 direction,bool cache,out float ground)
        {
            if(!cache)
            {
                ground=PlanetCoordinate.SurfaceRadius(Terrain,direction);
                return HasDryClearance(direction,ground);
            }
            ref var slot=ref Slot(direction);
            if(!slot.hasGround)
            {
                slot.ground=PlanetCoordinate.SurfaceRadius(Terrain,direction);
                slot.hasGround=true;
            }
            ground=slot.ground;
            // ★ 适配：整段仍采样坡度和身体两侧；单个地表样本高过所有水面时无需求出具体水位。
            if (ground-_highestWaterRadius>Settings.shoreClearance) return true;
            if(!slot.hasWater)
            {
                slot.water=Terrain.Hydrology==null?_surfaceRadius:PlanetCoordinate.WaterSurfaceRadius(Terrain,direction);
                slot.hasWater=true;
            }
            return ground-slot.water>Settings.shoreClearance;
        }
        // ★ 适配：真实生成数据用三角形高度；仅无网格的工具/测试数据回退到高度图。
        public float SurfaceHeight(Vector3 direction)=>(PlanetCoordinate.SurfaceRadius(Terrain,direction)-Terrain.Radius)/Mathf.Max(.001f,Terrain.HeightScale);
        public bool Dry(Vector3 direction)
            => DrySample(direction,UseShoreCache,out _);
        private bool UseShoreCache=>_shoreSamples!=null&&DarwinPark.Core.SimulationOptimization.Current.shoreSampleReuse;
        // ★ 适配：真实水位和地形半径只采样一次；即使岸线余量设置为零也不能走入浅水。
        private bool HasDryClearance(Vector3 direction,float ground)
            => ground - _highestWaterRadius > Settings.shoreClearance ||
                ground - WaterRadius(direction) > Settings.shoreClearance;
        public bool Segment(Vector3 a,Vector3 b)
        {
            // ★ 修复（防Bug回归）：物理步的微小夹角用 acos(dot) 会舍入为零，错误放大坡度并清掉启动速度；半角弦长保留短距离精度。
            a=a.normalized;b=b.normalized;
            float distance=2*Mathf.Atan2((a-b).magnitude,(a+b).magnitude)*_surfaceRadius;
            int steps=Mathf.Max(1,Mathf.CeilToInt(distance/_segmentStep));float previous=0;
            float riseLimit = _slopeLimit * Mathf.Max(.001f, distance / steps);
            bool cache=UseShoreCache;
            for (int i = 0; i <= steps; i++)
            {
                var direction = i == 0 ? a : i == steps ? b : Vector3.Slerp(a,b,i/(float)steps);
                if (!DrySample(direction,cache,out float ground) || i > 0 && Mathf.Abs(ground-previous)>riseLimit) return false;
                previous = ground;
            }
            return true;
        }
        public int Cell(Vector3 direction){var uv=PlanetTerrainSampler.DirectionToUV(direction);return Mathf.Min(Height-1,(int)(uv.y*Height))*Width+Mathf.Min(Width-1,(int)(uv.x*Width));}
        public int Closest(Vector3 direction)
        {if(!Dry(direction))return -1;int cell=Cell(direction);if(Walkable[cell]&&Segment(direction,Directions[cell]))return cell;int best=-1;float distance=float.PositiveInfinity;int x=cell%Width,y=cell/Width;for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){if(y+dy<0||y+dy>=Height)continue;int i=(y+dy)*Width+(x+dx+Width)%Width;if(!Walkable[i])continue;float d=(Directions[i]-direction).sqrMagnitude;if(d<distance&&Segment(direction,Directions[i])){best=i;distance=d;}}return best;}
        public bool Connected(Vector3 a,Vector3 b){int start=Closest(a),goal=Closest(b);return start>=0&&goal>=0&&Regions[start]==Regions[goal];}
        public bool Linked(int cell,int edge)=>(_edges[cell]&(1<<edge))!=0;
        public int Neighbour(int cell,int edge){int y=cell/Width+DY[edge];return y<0||y>=Height?-1:y*Width+(cell%Width+DX[edge]+Width)%Width;}
    }
}
