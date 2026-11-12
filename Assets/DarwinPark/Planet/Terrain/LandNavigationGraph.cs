using System;
using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Planet
{
    [Serializable]public sealed class LandNavigationSettings
    {
        public int longitudeCells=256,latitudeCells=128,expansionsPerFrame=512,routeCacheCount=512;
        public float shoreClearance=.2f,maximumSlopeDegrees=50,segmentStep=1.5f,pathMilliseconds=1.5f,repathSeconds=.6f,failureRetrySeconds=2,waypointArrival=1.5f;
    }
    // ★ 新增：地形装载时一次建立陆地连通图；路径查询只用数组，不反复查询场景和碰撞体。
    public sealed class LandNavigationGraph
    {
        public readonly LandNavigationSettings Settings;public readonly PlanetTerrainData Terrain;public readonly int Width,Height;public readonly Vector3[] Points,Directions;public readonly bool[] Walkable;public readonly int[] Regions;private readonly byte[] _edges;
        public static readonly int[] DX={-1,0,1,-1,1,-1,0,1},DY={-1,-1,-1,0,0,1,1,1};
        public LandNavigationGraph(PlanetTerrainData data,LandNavigationSettings settings)
        {
            Terrain=data;Settings=settings;Width=Mathf.Clamp(settings.longitudeCells,32,512);Height=Mathf.Clamp(settings.latitudeCells,16,256);int n=Width*Height;Points=new Vector3[n];Directions=new Vector3[n];Walkable=new bool[n];Regions=new int[n];_edges=new byte[n];
            for(int i=0;i<n;i++){var dir=PlanetTerrainSampler.DirectionFromUV((i%Width+.5f)/Width,(i/Width+.5f)/Height);Directions[i]=dir;Points[i]=dir*(data.Radius+SurfaceHeight(dir)*data.HeightScale);Walkable[i]=Dry(dir);}
            for(int i=0;i<n;i++)if(Walkable[i])for(int edge=0;edge<8;edge++){int next=Neighbour(i,edge);if(next<0||!Walkable[next])continue;if(Segment(Directions[i],Directions[next]))_edges[i]|=(byte)(1<<edge);}
            int region=0;var queue=new Queue<int>();for(int i=0;i<n;i++)if(Walkable[i]&&Regions[i]==0){Regions[i]=++region;queue.Enqueue(i);while(queue.Count>0){int node=queue.Dequeue();for(int e=0;e<8;e++)if(Linked(node,e)){int next=Neighbour(node,e);if(Regions[next]==0){Regions[next]=region;queue.Enqueue(next);}}}}
        }
        // ★ 适配：真实生成数据用三角形高度；仅无网格的工具/测试数据回退到高度图。
        public float SurfaceHeight(Vector3 direction)=>RadialTerrainSurface.TrySampleLocal(Terrain,direction,out var hit)?(hit.point.magnitude-Terrain.Radius)/Mathf.Max(.001f,Terrain.HeightScale):PlanetTerrainSampler.SampleHeight(Terrain,direction);
        public bool Dry(Vector3 direction)
        {float height=SurfaceHeight(direction),water=Terrain.Hydrology?.Water(direction)??Terrain.SeaLevel;return (height-water)*Terrain.HeightScale>Settings.shoreClearance;}
        public bool Segment(Vector3 a,Vector3 b)
        {
            float distance=Vector3.Angle(a,b)*Mathf.Deg2Rad*(Terrain.Radius+Terrain.SeaLevel*Terrain.HeightScale);int steps=Mathf.Max(1,Mathf.CeilToInt(distance/Mathf.Max(.2f,Terrain.Hydrology!=null?Mathf.Min(Settings.segmentStep,Terrain.Hydrology.Settings.riverHalfWidth):Settings.segmentStep)));float previous=SurfaceHeight(a);
            for(int i=0;i<=steps;i++){var p=Vector3.Slerp(a,b,i/(float)steps).normalized;if(!Dry(p))return false;float h=SurfaceHeight(p);if(i>0&&Mathf.Atan2(Mathf.Abs(h-previous)*Terrain.HeightScale,Mathf.Max(.001f,distance/steps))*Mathf.Rad2Deg>Settings.maximumSlopeDegrees)return false;previous=h;}return true;
        }
        public int Cell(Vector3 direction){var uv=PlanetTerrainSampler.DirectionToUV(direction);return Mathf.Min(Height-1,(int)(uv.y*Height))*Width+Mathf.Min(Width-1,(int)(uv.x*Width));}
        public int Closest(Vector3 direction)
        {int cell=Cell(direction);if(Walkable[cell]&&Segment(direction,Directions[cell]))return cell;int best=-1;float distance=float.PositiveInfinity;int x=cell%Width,y=cell/Width;for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){if(y+dy<0||y+dy>=Height)continue;int i=(y+dy)*Width+(x+dx+Width)%Width;if(!Walkable[i])continue;float d=(Directions[i]-direction).sqrMagnitude;if(d<distance&&Segment(direction,Directions[i])){best=i;distance=d;}}return best;}
        public bool Connected(Vector3 a,Vector3 b){int start=Closest(a),goal=Closest(b);return start>=0&&goal>=0&&Regions[start]==Regions[goal];}
        public bool Linked(int cell,int edge)=>(_edges[cell]&(1<<edge))!=0;
        public int Neighbour(int cell,int edge){int y=cell/Width+DY[edge];return y<0||y>=Height?-1:y*Width+(cell%Width+DX[edge]+Width)%Width;}
    }
}
