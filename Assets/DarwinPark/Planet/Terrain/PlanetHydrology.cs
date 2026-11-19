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
        private bool[] _possibleRiver;
        private int[] _nextRiverColumn;
        private bool[] _possibleLake;
        private float[] _waterUpperBound;
        public int RiverCells{get;private set;}public int LakeCells{get;private set;}
        internal float MaximumWaterLevel { get; private set; }
        public Vector3[] ChannelDirections {get;private set;}
        public float[] ChannelWidths {get;private set;}
        public Vector3[][] ChannelPaths {get;private set;}
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
            // ★ 修复（防Bug回归）：洪泛父节点只保证可排水，不代表最陡下坡；平坦区保留洪泛拓扑打破环路。
            if(settings.version>=3)PlanetDrainageNetwork.Route(result,order,data);
            for(int i=0;i<seen.Length;i++)result.Accumulation[i]=Mathf.Max(.01f,Mathf.Sqrt(1-result.Directions[i].y*result.Directions[i].y));
            for(int i=order.Count-1;i>=0;i--){int cell=order[i],down=result.Downstream[cell];if(down>=0)result.Accumulation[down]+=result.Accumulation[cell];}
            for(int i=0;i<seen.Length;i++){result.Lake[i]=result.Bed[i]>data.SeaLevel&&result.Spill[i]-result.Bed[i]>=settings.lakeMinimumDepth/result._scale;
                result.River[i]=result.Bed[i]>data.SeaLevel&&result.Downstream[i]>=0&&result.Accumulation[i]>=settings.riverCatchment;
                if(result.Lake[i])result.LakeCells++;if(result.River[i])result.RiverCells++;}
            // ★ 新增：新版将有深水核心的同水位洼地扩展到浅岸，不把湖泊最浅一圈裁成水文网格方块。
            if(settings.version>=2){var fringe=new Queue<int>();for(int i=0;i<seen.Length;i++)if(result.Lake[i])fringe.Enqueue(i);while(fringe.Count>0){int cell=fringe.Dequeue(),x=cell%result.Width,y=cell/result.Width;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){if(y+dy<0||y+dy>=result.Height)continue;int next=(y+dy)*result.Width+(x+dx+result.Width)%result.Width;if(result.Lake[next]||result.Bed[next]<=data.SeaLevel||result.Spill[next]<=result.Bed[next]||Mathf.Abs(result.Spill[cell]-result.Spill[next])>1e-5f)continue;result.Lake[next]=true;result.LakeCells++;fringe.Enqueue(next);}}}
            if(settings.version>=3)
            {
                result.RiverCells=0;
                for(int i=0;i<seen.Length;i++){result.River[i]&=!result.Lake[i];if(result.River[i])result.RiverCells++;}
                result.ChannelDirections=PlanetDrainageNetwork.Smooth(result,data);
                result.ChannelPaths=PlanetDrainageNetwork.Curves(result,data);
                result.ChannelWidths=new float[seen.Length];
                for(int i=0;i<seen.Length;i++)result.ChannelWidths[i]=Mathf.Min(settings.riverMaximumWidth,settings.riverHalfWidth*Mathf.Pow(Mathf.Max(1,result.Accumulation[i]/settings.riverCatchment),settings.riverWidthExponent));
            }
            result.IndexPotentialRivers();
            return result;
        }

        // ★ 适配：按纬度行建立保守上界。只有整个查询范围不可能含河段时，水位才走无河快速路径。
        private void IndexPotentialRivers()
        {
            MaximumWaterLevel=_sea;
            _possibleRiver=new bool[River.Length];
            _nextRiverColumn=new int[Height*(Width+1)];
            _possibleLake=new bool[Lake.Length];
            _waterUpperBound=new float[Lake.Length];
            Array.Fill(_waterUpperBound,_sea);
            // ★ 适配：按原纬度/经度顺序直达范围内的下一条河，避免每次水位查询扫描大量无河格。
            for (int row = 0; row < Height; row++)
            {
                int rowStart = row * (Width + 1);
                int next = Width;
                _nextRiverColumn[rowStart + Width] = next;
                for (int column = Width - 1; column >= 0; column--)
                {
                    int cell = row * Width + column;
                    if (River[cell])
                    {
                        next = column;
                        float highestSpill=Mathf.Max(Spill[cell],Spill[Downstream[cell]]);
                        MaximumWaterLevel=Mathf.Max(MaximumWaterLevel,highestSpill-Settings.riverSurfaceInset/_scale);
                    }
                    _nextRiverColumn[rowStart + column] = next;
                    if (!Lake[cell]) continue;
                    MaximumWaterLevel=Mathf.Max(MaximumWaterLevel,Spill[cell]);
                    for (int y = Mathf.Max(0, row - 1); y <= Mathf.Min(Height - 1, row + 1); y++)
                        for (int x = -1; x <= 1; x++)
                        {
                            int sample=y*Width+(column+x+Width)%Width;
                            _possibleLake[sample]=true;
                            _waterUpperBound[sample]=Mathf.Max(_waterUpperBound[sample],Spill[cell]);
                        }
                }
            }
            SearchRange(Vector3.right,out _,out int latitudeReach);
            var longitudeReach=new int[Height];
            for(int row=0;row<Height;row++)
            {
                // 同一格越靠极点，覆盖经线越宽。取靠极边界，加一格抵消经纬浮点舍入。
                float edge=(row<Height/2?row:row+1)/(float)Height;
                var direction=PlanetTerrainSampler.DirectionFromUV(.5f,edge);
                SearchRange(direction,out int maxReach,out _);
                longitudeReach[row]=Mathf.Min(Width/2,maxReach+1);
            }
            for(int cell=0;cell<River.Length;cell++)
            {
                if(!River[cell])continue;
                int column=cell%Width,row=cell/Width;
                float highest=Mathf.Max(Spill[cell],Spill[Downstream[cell]])-Settings.riverSurfaceInset/_scale;
                for(int y=Mathf.Max(0,row-latitudeReach);y<=Mathf.Min(Height-1,row+latitudeReach);y++)
                for(int x=-longitudeReach[y];x<=longitudeReach[y];x++)
                {
                    int sample=y*Width+(column+x+Width)%Width;
                    _possibleRiver[sample]=true;
                    _waterUpperBound[sample]=Mathf.Max(_waterUpperBound[sample],highest);
                }
            }
        }

        public Vector3 Channel(int cell)=>ChannelDirections!=null?ChannelDirections[cell]:Directions[cell];
        public float ChannelWidth(int cell)=>ChannelWidths!=null?ChannelWidths[cell]:Settings.riverHalfWidth;
        // ★ 修复（防Bug回归）：一次水位查询复用球面坐标，不重复计算 atan2/asin。
        public int Cell(Vector3 dir)=>Cell(PlanetTerrainSampler.DirectionToUV(dir));
        private int Cell(Vector2 uv){return Mathf.Min(Height-1,(int)(uv.y*Height))*Width+Mathf.Min(Width-1,(int)(uv.x*Width));}
        public float Water(Vector3 dir)
        {float level=SurfaceWater(dir,out float coverage);return coverage>=0?level:_sea;}

        // ★ 新增：取覆盖球冠的整片水文格上界，跨经线和含极点的范围都按完整区域处理。
        internal float MaximumWaterInCap(Vector3 axis, float angle)
        {
            var uv=PlanetTerrainSampler.DirectionToUV(axis);
            float latitude=Mathf.Asin(Mathf.Clamp(axis.y,-1,1));
            float halfPi=Mathf.PI*.5f;
            int firstRow=Mathf.Clamp(Mathf.FloorToInt((uv.y-angle/Mathf.PI)*Height)-1,0,Height-1);
            int lastRow=Mathf.Clamp(Mathf.FloorToInt((uv.y+angle/Mathf.PI)*Height)+1,0,Height-1);
            int column=Mathf.Min(Width-1,(int)(uv.x*Width));
            int reach=Mathf.Abs(latitude)+angle>=halfPi
                ? Width/2
                : Mathf.Min(Width/2,Mathf.CeilToInt(Mathf.Asin(Mathf.Clamp01(Mathf.Sin(angle)/Mathf.Cos(latitude)))*
                    Width/(2*Mathf.PI))+1);
            float upper=_sea;
            for(int row=firstRow;row<=lastRow;row++)
                for(int offset=-reach;offset<=reach;offset++)
                    upper=Mathf.Max(upper,_waterUpperBound[row*Width+(column+offset+Width)%Width]);
            return upper;
        }

        // ★ 新增：地面高过本格所涉海面、湖泊和所有候选河段时，岸线守卫只需水位上界。
        // 需要真实水位时复用同一经纬转换，不能用上界代替浸没深度。
        internal bool WaterForShore(Vector3 direction,float radius,float height,float ground,float clearance,out float water)
        {
            var uv=PlanetTerrainSampler.DirectionToUV(direction);
            int cell=Cell(uv);
            float upper=radius+_waterUpperBound[cell]*height;
            if(height>0&&Mathf.Abs(direction.sqrMagnitude-1)<.00001f&&
                ground-upper>clearance+Mathf.Max(.001f,Mathf.Abs(upper)*.000002f))
            {
                water=upper;
                return false;
            }
            float level=SurfaceWater(direction,uv,cell,out float coverage,out _,out _);
            water=radius+(coverage>=0?level:_sea)*height;
            return true;
        }
        // ★ 修复（防Bug回归）：岸外采样仍保留相邻河面的高度，用单独覆盖距离裁切；否则边缘插值会把高处河面拉向海平面，形成斜三角。
        public float SurfaceWater(Vector3 dir,out float coverage) =>
            SurfaceWater(dir,out coverage,out _,out _);

        internal float SurfaceWater(Vector3 dir,out float coverage,out int river,out float along)
        {
            var uv=PlanetTerrainSampler.DirectionToUV(dir);
            return SurfaceWater(dir,uv,Cell(uv),out coverage,out river,out along);
        }

        private float SurfaceWater(Vector3 dir,Vector2 uv,int cell,out float coverage,out int river,out float along)
        {
            river=-1;along=0;
            float water=LakeWater(uv,cell);
            if(_possibleRiver!=null&&!_possibleRiver[cell]&&Mathf.Abs(dir.sqrMagnitude-1)<.00001f)
            {
                coverage=water>_sea?float.PositiveInfinity:-_radius;
                return water;
            }
            coverage=water>_sea?float.PositiveInfinity:float.NegativeInfinity;float extended=_sea,extendedCoverage=float.NegativeInfinity;
            int extendedRiver=-1;float extendedAlong=0;
            SearchRange(dir,out int rx,out int ry);
            int x=cell%Width,y=cell/Width;
            int first=(x-rx+Width)%Width;
            int last=first+2*rx;
            for(int row=Mathf.Max(0,y-ry);row<=Mathf.Min(Height-1,y+ry);row++)
            {
                int lookup=row*(Width+1);
                int column=_nextRiverColumn[lookup+first];
                // 经线接缝按原扫描顺序绕回。极区若原范围重复覆盖首列，仍保持原顺序与选择。
                for(int offset=0;offset<=last;offset+=Width)
                {
                    int end=Mathf.Min(Width-1,last-offset);
                    for(;column<=end;column=_nextRiverColumn[lookup+column+1])
                    {
                        int i=row*Width+column;
                        float distance=SegmentDistance(dir,i,out float t),width=Mathf.Lerp(ChannelWidth(i),ChannelWidth(Downstream[i]),t);
                        // 与真实下切的岸坡共用范围；水面只在低于水位的实际地形处出现，不以名义半宽截断岸边。
                        if(Settings.version>=3)width=Mathf.Max(_bankWidth,width+Settings.bankWidth-Settings.riverHalfWidth);
                        float mask=width-distance,level=Mathf.Lerp(Spill[i],Spill[Downstream[i]],t)-Settings.riverSurfaceInset/_scale;
                        if(mask>=0&&level>water){water=level;coverage=mask;river=i;along=t;}
                        else if(mask<0&&mask>extendedCoverage&&level>_sea){extended=level;extendedCoverage=mask;extendedRiver=i;extendedAlong=t;}
                    }
                    if(last>=Width&&offset==0)column=_nextRiverColumn[lookup];
                }
            }
            if(water<=_sea){water=extended;coverage=float.IsNegativeInfinity(extendedCoverage)?-_radius:extendedCoverage;river=extendedRiver;along=extendedAlong;}
            return water;
        }

        internal Vector3 VisualCurrent(Vector3 direction,int river,float along)
        {
            if(river<0)return Vector3.zero;
            int downstream=Downstream[river];
            var path=ChannelPaths?[river];
            Vector3 tangent;
            if(path==null)tangent=Channel(downstream)-Channel(river);
            else
            {
                int last=path.Length-1;
                float step=along*last;
                int index=Mathf.Min((int)step,last-1);
                Vector3 a=path[index+1]-path[Mathf.Max(0,index-1)];
                Vector3 b=path[Mathf.Min(last,index+2)]-path[index];
                tangent=Vector3.Lerp(a,b,step-index);
            }
            float drop=Mathf.Max(0,Spill[river]-Spill[downstream])*_scale;
            float speed=Mathf.Lerp(.45f,2f,Mathf.Clamp01(drop*.35f));
            return Vector3.ProjectOnPlane(tangent,direction).normalized*speed;
        }
        // ★ 新增：显示和玩法共用湖水位；新版从相邻格中心延展到真实山坡，边缘由地形裁切而非格子边裁切。
        public float LakeWater(Vector3 dir)
        {
            var uv=PlanetTerrainSampler.DirectionToUV(dir);return LakeWater(uv,Cell(uv));
        }
        private float LakeWater(Vector2 uv,int cell)
        {
            if(Settings.version<2)return Lake[cell]?Spill[cell]:_sea;
            if (_possibleLake != null && !_possibleLake[cell]) return _sea;
            float x=uv.x*Width-.5f,y=uv.y*Height-.5f;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);float best=float.PositiveInfinity,water=_sea;
            for(int dy=0;dy<=1;dy++)for(int dx=0;dx<=1;dx++){int row=Mathf.Clamp(iy+dy,0,Height-1),column=(ix+dx+Width)%Width,i=row*Width+column;if(!Lake[i])continue;float distance=(ix+dx-x)*(ix+dx-x)+(iy+dy-y)*(iy+dy-y);if(distance>=best)continue;best=distance;water=Spill[i];}return water;
        }
        public float SegmentDistance(Vector3 direction,int river,out float t)
        {
            var path=ChannelPaths?[river];int count=path!=null?path.Length-1:1;float closest=float.PositiveInfinity;t=0;
            // ★ 新增：玩法查询、河床雕刻和网格共用同一条细分曲线，不能只把视觉河水画弯。
            for(int step=0;step<count;step++){var a=path!=null?path[step]:Channel(river);var b=path!=null?path[step+1]:Channel(Downstream[river]);var delta=b-a;float along=Mathf.Clamp01(Vector3.Dot(direction-a,delta)/Mathf.Max(1e-10f,delta.sqrMagnitude));float d=(direction-Vector3.Lerp(a,b,along).normalized).sqrMagnitude;if(d<closest){closest=d;t=(step+along)/count;}}
            return Mathf.Sqrt(closest)*_radius;
        }
        public float Carved(Vector3 dir,float original)
        {
            Vector2 uv = PlanetTerrainSampler.DirectionToUV(dir);
            int cell = Cell(uv), x = cell % Width, y = cell / Width;
            float height = original;
            // ★ 修复（防Bug回归）：按已经积水的自然湖盆加深，而不是只加深人工生成盆地。
            // 只切水位以下，越接近岸边下切越平滑；最低保留在海平面，湖泊不变成海床。
            if (Settings.version >= 5 && original > _sea)
            {
                float lake = LakeWater(uv,cell);
                float depth = lake - original;
                if (depth > 0)
                {
                    float shore = Mathf.SmoothStep(0, 1, Mathf.Clamp01(depth * _scale / Settings.lakeMinimumDepth));
                    height = Mathf.Max(_sea, original - depth * (Settings.lakeDepthMultiplier - 1) * shore);
                }
            }
            SearchRange(dir,out int rx,out int ry);for(int dy=-ry;dy<=ry;dy++)for(int dx=-rx;dx<=rx;dx++)
            {if(y+dy<0||y+dy>=Height)continue;int i=(y+dy)*Width+(x+dx+Width)%Width;if(!River[i])continue;float t;float d=SegmentDistance(dir,i,out t);float bank=Mathf.Max(Settings.riverHalfWidth,_bankWidth);if(Settings.version<3&&d>bank)continue;
                float bottom=Mathf.Lerp(Spill[i],Spill[Downstream[i]],t)-(Settings.riverSurfaceInset+Settings.riverBedDepth*(Settings.version>=4?Settings.riverDepthMultiplier:1))/_scale;
                float width=Mathf.Lerp(ChannelWidth(i),ChannelWidth(Downstream[i]),t);bank=Mathf.Max(bank,width+Settings.bankWidth-Settings.riverHalfWidth);if(d>bank)continue;
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(width,bank,d));height=Mathf.Min(height,Mathf.Lerp(original,bottom,weight));}
            return Mathf.Max(0,height);
        }
        public float Moisture(Vector3 dir)
        {int cell=Cell(dir);if(Lake[cell])return Settings.moistureBonus;SearchRange(dir,out int rx,out int ry,true);int x=cell%Width,y=cell/Width;float distance=Settings.moistureDistance;for(int dy=-ry;dy<=ry;dy++)for(int dx=-rx;dx<=rx;dx++){if(y+dy<0||y+dy>=Height)continue;int i=(y+dy)*Width+(x+dx+Width)%Width;if(River[i]){float t;distance=Mathf.Min(distance,SegmentDistance(dir,i,out t));}}return Settings.moistureBonus*(1-Mathf.Clamp01(distance/Mathf.Max(.01f,Settings.moistureDistance)));}
        private void SearchRange(Vector3 dir,out int rx,out int ry,bool moisture=false)
        {
            rx=ry=1;if(Settings.version<3)return;
            // ★ 修复（防Bug回归）：河宽、平滑位移和极区经线收敛都进入查询范围，视觉水面与扣血/导航共用同一河床。
            float reach=moisture?Settings.moistureDistance:Mathf.Max(_bankWidth,Settings.riverMaximumWidth+Settings.bankWidth);
            ry=Mathf.Min(Height-1,1+Mathf.CeilToInt(reach/(_radius*Mathf.PI/Height)));
            float cosine=Mathf.Sqrt(Mathf.Max(0,1-dir.y*dir.y));
            rx=Mathf.Min(Width/2,1+Mathf.CeilToInt(reach/Mathf.Max(.001f,_radius*2*Mathf.PI/Width*cosine)));
        }
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
        {if(!_settings.enabled)return height;float a=(_plateau.Sample(dir)+1)*.5f,b=(_basin.Sample(dir)+1)*.5f;float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(_settings.plateauThreshold,_settings.plateauThreshold+_settings.plateauTransition,a));float land=Mathf.SmoothStep(0,1,Mathf.InverseLerp(sea,sea+.1f,height));// ★ 修复（防Bug回归）：旧盆地把小噪声偏差再次平方，实际下切近乎为零；新版改为可配置宽碗遮罩。
            float bowl=_settings.version<2?Mathf.Pow(Mathf.Clamp01((.5f-b)*2),2):1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(_settings.basinThreshold-_settings.basinTransition,_settings.basinThreshold,b));
            return Mathf.Lerp(height,sea+_settings.plateauHeight,mask*_settings.plateauStrength*land)-_settings.basinStrength*bowl*land;}
    }
}
