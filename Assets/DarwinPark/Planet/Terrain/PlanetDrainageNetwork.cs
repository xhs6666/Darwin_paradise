using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Planet
{
    // ★ 新增：Priority-Flood 解洼后按球面距离选择 D8 最陡出流；等高面以洪泛顺序破环，保持唯一出口。
    public static class PlanetDrainageNetwork
    {
        public static void Route(PlanetHydrology hydro,List<int> order,PlanetTerrainData data)
        {
            var rank=new int[order.Count];for(int i=0;i<order.Count;i++)rank[order[i]]=i;
            for(int i=0;i<hydro.Bed.Length;i++)
            {
                if(hydro.Bed[i]<=data.SeaLevel)continue;
                int x=i%hydro.Width,y=i/hydro.Width,best=hydro.Downstream[i];float steepest=0;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dy==0||y+dy<0||y+dy>=hydro.Height)continue;
                    int next=(y+dy)*hydro.Width+(x+dx+hydro.Width)%hydro.Width;
                    float drop=hydro.Spill[i]-hydro.Spill[next];
                    if(drop<=0||rank[next]>=rank[i])continue;
                    float slope=drop/Mathf.Max(1e-7f,Vector3.Distance(hydro.Directions[i],hydro.Directions[next]));
                    if(slope>steepest){steepest=slope;best=next;}
                }
                hydro.Downstream[i]=best;
            }
        }
        public static Vector3[] Smooth(PlanetHydrology hydro,PlanetTerrainData data)
        {
            var result=(Vector3[])hydro.Directions.Clone();var incoming=new Vector3[result.Length];var weights=new float[result.Length];
            for(int i=0;i<result.Length;i++)if(hydro.River[i]){int next=hydro.Downstream[i];incoming[next]+=hydro.Directions[i]*hydro.Accumulation[i];weights[next]+=hydro.Accumulation[i];}
            for(int i=0;i<result.Length;i++)
            {
                if(!hydro.River[i]||weights[i]<=0)continue;int next=hydro.Downstream[i];
                var target=(incoming[i]/weights[i]+hydro.Directions[next]).normalized;
                var candidate=Vector3.Lerp(result[i],target,hydro.Settings.channelSmoothing).normalized;
                // ★ 修复（防Bug回归）：平滑只允许在当前河谷内，不把河道横移到高山或穿过不同水位湖岸。
                if(PlanetTerrainSampler.SampleHeight(data,candidate)<=hydro.Spill[i]+hydro.Settings.riverBedDepth/data.HeightScale)result[i]=candidate;
            }
            return result;
        }
        public static int CarveBasins(PlanetTerrainData data,LandformSettings settings,int seed)
        {
            var centers=new List<Vector3>();var bottoms=new List<float>();var axes=new List<Vector3>();float radius=data.Radius+data.SeaLevel*data.HeightScale;
            float angular=settings.basinRadius/radius;var random=new DeterministicRandom(seed+51437);
            for(int attempt=0;attempt<settings.basinCandidates&&centers.Count<settings.basinCount;attempt++)
            {
                // 等面积球面候选，避免经纬网在两极聚集；同种子固定顺序，不依赖 Unity 全局随机状态。
                float y=random.NextFloat()*2-1,phi=random.NextFloat()*Mathf.PI*2;
                var direction=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(phi),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(phi));
                bool close=false;foreach(var center in centers)if(Vector3.Distance(center,direction)<angular*settings.basinSeparation){close=true;break;}if(close)continue;
                float ground=PlanetTerrainSampler.SampleHeight(data,direction),minimum=ground,maximum=ground;
                var side=Vector3.Cross(direction,Mathf.Abs(direction.y)<.9f?Vector3.up:Vector3.right).normalized;var up=Vector3.Cross(direction,side);
                for(int sample=0;sample<8;sample++){float a=sample*Mathf.PI*.25f;float h=PlanetTerrainSampler.SampleHeight(data,(direction+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*angular).normalized);minimum=Mathf.Min(minimum,h);maximum=Mathf.Max(maximum,h);}
                if(minimum<=data.SeaLevel+(settings.lakeMinimumDepth+settings.basinLakeDepth)/data.HeightScale||maximum-minimum>settings.basinMaximumRelief/data.HeightScale)continue;
                centers.Add(direction);bottoms.Add(minimum-settings.basinLakeDepth/data.HeightScale);float orientation=random.NextFloat()*Mathf.PI*2;axes.Add(side*Mathf.Cos(orientation)+up*Mathf.Sin(orientation));
            }
            int sub=data.Subdivisions;
            for(int face=0;face<6;face++)for(int y=0;y<=sub;y++)for(int x=0;x<=sub;x++)
            {
                float u=x*2f/sub-1,v=y*2f/sub-1;var dir=(face<2?new Vector3(face==0?1:-1,v,u):face<4?new Vector3(u,face==2?1:-1,v):new Vector3(u,v,face==4?1:-1)).normalized;
                int index=y*(sub+1)+x;float height=data.FaceHeights[face][index];
                for(int basin=0;basin<centers.Count;basin++)
                {
                    var delta=dir-centers[basin];if(delta.sqrMagnitude>angular*angular*(1+settings.basinIrregularity)*(1+settings.basinIrregularity))continue;
                    // ★ 新增：各盆地有独立长轴与岸线扰动，避免新增湖泊全部成为规则圆坑。
                    float a=Vector3.Dot(delta,axes[basin])/angular,b=Vector3.Dot(delta,Vector3.Cross(centers[basin],axes[basin]))/(angular*settings.basinAspectRatio),angle=Mathf.Atan2(b,a);
                    float edge=1+settings.basinIrregularity*(Mathf.Sin(angle*3)+Mathf.Cos(angle*5))*.5f;
                    float t=Mathf.Sqrt(a*a+b*b)/edge;if(t>=1)continue;float bowl=Mathf.SmoothStep(0,1,t);height=Mathf.Min(height,Mathf.Lerp(bottoms[basin],height,bowl));
                }
                data.FaceHeights[face][index]=height;
            }
            return centers.Count;
        }
        public static Vector3[][] Curves(PlanetHydrology hydro,PlanetTerrainData data)
        {
            var incoming=new Vector3[hydro.Bed.Length];var weight=new float[incoming.Length];
            for(int i=0;i<incoming.Length;i++)if(hydro.River[i]){int next=hydro.Downstream[i];incoming[next]+=hydro.Channel(i)*hydro.Accumulation[i];weight[next]+=hydro.Accumulation[i];}
            Vector3 Tangent(int cell){int next=hydro.Downstream[cell];return ((next>=0?hydro.Channel(next):hydro.Channel(cell))-(weight[cell]>0?incoming[cell]/weight[cell]:hydro.Channel(cell))).normalized;}
            var paths=new Vector3[incoming.Length][];int segments=hydro.Settings.riverCurveSegments;
            for(int i=0;i<paths.Length;i++)
            {
                if(!hydro.River[i])continue;int next=hydro.Downstream[i];var a=hydro.Channel(i);var b=hydro.Channel(next);var delta=b-a;float length=delta.magnitude*hydro.Settings.riverCurveStrength;
                var start=Tangent(i);var end=Tangent(next);if(Vector3.Dot(start,delta)<=0)start=delta.normalized;if(Vector3.Dot(end,delta)<=0)end=delta.normalized;
                var c=a+start*length;var d=b-end*length;var path=new Vector3[segments+1];bool valley=true;
                for(int step=0;step<=segments;step++){float t=step/(float)segments,s=1-t;var point=(a*(s*s*s)+c*(3*s*s*t)+d*(3*s*t*t)+b*(t*t*t)).normalized;path[step]=point;if(PlanetTerrainSampler.SampleHeight(data,point)>Mathf.Lerp(hydro.Spill[i],hydro.Spill[next],t)+hydro.Settings.riverBedDepth/data.HeightScale)valley=false;}
                // ★ 修复（防Bug回归）：弯道不允许为了平滑跨山脊；受地形阻挡的河段退回原谷线。
                if(!valley)for(int step=0;step<=segments;step++)path[step]=Vector3.Lerp(a,b,step/(float)segments).normalized;
                paths[i]=path;
            }
            return paths;
        }
    }
}
