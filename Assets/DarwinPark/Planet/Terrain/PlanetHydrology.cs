using System;
using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：从海洋向内陆优先洪泛，确定每格唯一出流父节点，再反向汇流。
    // 河流依赖汇水量和实际下游连通，不用无出口的随机线条冒充河网。
    public sealed class PlanetHydrology
    {
        public readonly int Width,Height;public readonly float[] Bed,Spill,Accumulation;public readonly int[] Downstream;public readonly bool[] River,Lake;public readonly Vector3[] Directions;
        public readonly LandformSettings Settings;private readonly float _sea,_radius,_scale,_bankWidth;
        public int RiverCells{get;private set;}public int LakeCells{get;private set;}
        private PlanetHydrology(PlanetTerrainData data,LandformSettings settings)
        {
            Settings=settings;Width=Mathf.Clamp(settings.longitudeCells,32,512);Height=Mathf.Clamp(settings.latitudeCells,16,256);int n=Width*Height;_sea=data.SeaLevel;_radius=data.Radius+_sea*data.HeightScale;_scale=Mathf.Max(.001f,data.HeightScale);_bankWidth=Mathf.Max(settings.bankWidth,_radius*1.5f/data.Subdivisions);
            Bed=new float[n];Spill=new float[n];Accumulation=new float[n];Downstream=new int[n];River=new bool[n];Lake=new bool[n];Directions=new Vector3[n];
            for(int i=0;i<n;i++){Directions[i]=PlanetTerrainSampler.DirectionFromUV((i%Width+.5f)/Width,(i/Width+.5f)/Height);Bed[i]=PlanetTerrainSampler.SampleHeight(data,Directions[i]);Downstream[i]=-1;}
        }
        public static PlanetHydrology Build(PlanetTerrainData data,LandformSettings settings)
        {
            var result=new PlanetHydrology(data,settings);var heap=new SurfaceMinHeap();var seen=new bool[result.Bed.Length];var order=new List<int>(seen.Length);
            int lowest=0;for(int i=0;i<seen.Length;i++){if(result.Bed[i]<result.Bed[lowest])lowest=i;if(result.Bed[i]<=data.SeaLevel){seen[i]=true;result.Spill[i]=data.SeaLevel;heap.Push(i,data.SeaLevel);}}
            if(heap.Count==0){seen[lowest]=true;result.Spill[lowest]=result.Bed[lowest];heap.Push(lowest,result.Bed[lowest]);}
            while(heap.Count>0){int cell=heap.Pop();order.Add(cell);int x=cell%result.Width,y=cell/result.Width;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){if(dx==0&&dy==0||y+dy<0||y+dy>=result.Height)continue;int next=(y+dy)*result.Width+(x+dx+result.Width)%result.Width;if(seen[next])continue;seen[next]=true;result.Downstream[next]=cell;result.Spill[next]=Mathf.Max(result.Bed[next],result.Spill[cell]);heap.Push(next,result.Spill[next]);}}
            for(int i=0;i<seen.Length;i++)result.Accumulation[i]=Mathf.Max(.01f,Mathf.Sqrt(1-result.Directions[i].y*result.Directions[i].y));
            for(int i=order.Count-1;i>=0;i--){int cell=order[i],down=result.Downstream[cell];if(down>=0)result.Accumulation[down]+=result.Accumulation[cell];}
            for(int i=0;i<seen.Length;i++){result.Lake[i]=result.Bed[i]>data.SeaLevel&&result.Spill[i]-result.Bed[i]>=settings.lakeMinimumDepth/result._scale;
                result.River[i]=result.Bed[i]>data.SeaLevel&&result.Downstream[i]>=0&&result.Accumulation[i]>=settings.riverCatchment;
                if(result.Lake[i])result.LakeCells++;if(result.River[i])result.RiverCells++;}
            return result;
        }
        public int Cell(Vector3 dir){var uv=PlanetTerrainSampler.DirectionToUV(dir);return Mathf.Min(Height-1,(int)(uv.y*Height))*Width+Mathf.Min(Width-1,(int)(uv.x*Width));}
        public float Water(Vector3 dir)
        {
            int cell=Cell(dir);float water=Lake[cell]?Spill[cell]:_sea;
            int x=cell%Width,y=cell/Width;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {if(y+dy<0||y+dy>=Height)continue;int i=(y+dy)*Width+(x+dx+Width)%Width;if(!River[i])continue;float t;float distance=SegmentDistance(dir,i,out t);if(distance<=Settings.riverHalfWidth)water=Mathf.Max(water,Mathf.Lerp(Spill[i],Spill[Downstream[i]],t)-Settings.riverSurfaceInset/_scale);}
            return water;
        }
        public float SegmentDistance(Vector3 direction,int river,out float t)
        {var a=Directions[river];var b=Directions[Downstream[river]];var delta=b-a;t=Mathf.Clamp01(Vector3.Dot(direction-a,delta)/Mathf.Max(1e-10f,delta.sqrMagnitude));return Vector3.Distance(direction,Vector3.Lerp(a,b,t).normalized)*_radius;}
        public float Carved(Vector3 dir,float original)
        {
            int cell=Cell(dir),x=cell%Width,y=cell/Width;float height=original;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {if(y+dy<0||y+dy>=Height)continue;int i=(y+dy)*Width+(x+dx+Width)%Width;if(!River[i])continue;float t;float d=SegmentDistance(dir,i,out t);float bank=Mathf.Max(Settings.riverHalfWidth,_bankWidth);if(d>bank)continue;
                float bottom=Mathf.Lerp(Spill[i],Spill[Downstream[i]],t)-(Settings.riverSurfaceInset+Settings.riverBedDepth)/_scale;
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(Settings.riverHalfWidth,bank,d));height=Mathf.Min(height,Mathf.Lerp(original,bottom,weight));}
            return Mathf.Max(0,height);
        }
        public float Moisture(Vector3 dir)
        {int cell=Cell(dir);if(Lake[cell])return Settings.moistureBonus;int x=cell%Width,y=cell/Width;float distance=Settings.moistureDistance;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){if(y+dy<0||y+dy>=Height)continue;int i=(y+dy)*Width+(x+dx+Width)%Width;if(River[i]){float t;distance=Mathf.Min(distance,SegmentDistance(dir,i,out t));}}return Settings.moistureBonus*(1-Mathf.Clamp01(distance/Mathf.Max(.01f,Settings.moistureDistance)));}
        public void Carve(PlanetTerrainData data)
        {
            int sub=data.Subdivisions;for(int f=0;f<6;f++)for(int y=0;y<=sub;y++)for(int x=0;x<=sub;x++)
            {float u=x*2f/sub-1,v=y*2f/sub-1;var dir=(f<2?new Vector3(f==0?1:-1,v,u):f<4?new Vector3(u,f==2?1:-1,v):new Vector3(u,v,f==4?1:-1)).normalized;int i=y*(sub+1)+x;data.FaceHeights[f][i]=Carved(dir,data.FaceHeights[f][i]);}
        }
    }
    // ★ 新增：宽阔高原及独立盆地；噪声对象每次生成仅创建一次，不在顶点循环分配。
    public sealed class PlanetLandformField
    {
        private readonly LandformSettings _settings;private readonly FBMNoise _plateau,_basin;
        public PlanetLandformField(LandformSettings settings,int seed){_settings=settings;var cfg=new FBMConfig{frequency=settings.plateauFrequency,octaves=3,warpStrength=.2f};_plateau=new FBMNoise(cfg,seed+6397);_basin=new FBMNoise(cfg,seed+9413);}
        public float Shape(float height,Vector3 dir,float sea)
        {if(!_settings.enabled)return height;float a=(_plateau.Sample(dir)+1)*.5f,b=(_basin.Sample(dir)+1)*.5f;float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(_settings.plateauThreshold,_settings.plateauThreshold+_settings.plateauTransition,a));float land=Mathf.SmoothStep(0,1,Mathf.InverseLerp(sea,sea+.1f,height));return Mathf.Lerp(height,sea+_settings.plateauHeight,mask*_settings.plateauStrength*land)-_settings.basinStrength*Mathf.Pow(Mathf.Clamp01((.5f-b)*2),2)*land;}
    }
}
