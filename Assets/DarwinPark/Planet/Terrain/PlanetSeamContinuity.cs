using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>★ 新增：在球面邻域平滑六面交界坡度；只焊接边缘高度仍会留下侵蚀缓冲带的直线折痕。</summary>
    public static class PlanetSeamContinuity
    {
        public static void Smooth(PlanetTerrainData data, ErosionConfig config)
        {
            if(config==null || config.seamSmoothIterations<=0 || config.seamSmoothCells<=0)return;
            float before=MeanCurvature(data);
            int sub=data.Subdivisions, stride=sub+1;
            float step=2f/sub;
            for(int pass=0;pass<config.seamSmoothIterations;pass++)
            {
                var next=new float[6][];
                for(int face=0;face<6;face++)
                {
                    next[face]=(float[])data.FaceHeights[face].Clone();
                    for(int y=0;y<=sub;y++)for(int x=0;x<=sub;x++)
                    {
                        float edge=Mathf.Min(Mathf.Min(x,y),Mathf.Min(sub-x,sub-y));
                        if(edge>=config.seamSmoothCells)continue;
                        var dir=Direction(face,x,y,sub);
                        var a=Vector3.Cross(dir,Mathf.Abs(dir.y)>.95f?Vector3.right:Vector3.up).normalized*step;
                        var b=Vector3.Cross(dir,a).normalized*step;
                        float average=(PlanetTerrainSampler.SampleHeight(data,(dir+a).normalized)+PlanetTerrainSampler.SampleHeight(data,(dir-a).normalized)
                            +PlanetTerrainSampler.SampleHeight(data,(dir+b).normalized)+PlanetTerrainSampler.SampleHeight(data,(dir-b).normalized))*.25f;
                        float weight=1-Mathf.SmoothStep(0,1,edge/config.seamSmoothCells);
                        int i=y*stride+x;
                        next[face][i]=Mathf.Lerp(data.FaceHeights[face][i],average,Mathf.Clamp01(config.seamSmoothStrength)*weight);
                    }
                }
                // 所有面都读上一轮快照，不能先改一面再让相邻面读取修改后的边缘。
                data.FaceHeights=next;
            }
            Log.Info(Log.Tag.Planet,$"跨面坡度平滑：接缝曲率 {before:F6} → {MeanCurvature(data):F6}");
        }
        public static Vector3 Direction(int face,int x,int y,int sub)
        {
            float u=2f*x/sub-1,v=2f*y/sub-1;
            return (face==0?new Vector3(1,v,u):face==1?new Vector3(-1,v,u):face==2?new Vector3(u,1,v)
                :face==3?new Vector3(u,-1,v):face==4?new Vector3(u,v,1):new Vector3(u,v,-1)).normalized;
        }
        public static float MeanCurvature(PlanetTerrainData data)
        {
            float sum=0,step=2f/data.Subdivisions;int count=0;
            for(int face=0;face<6;face++)for(int edge=0;edge<4;edge++)for(int i=1;i<data.Subdivisions;i++)
            {
                int x=edge==0?0:edge==1?data.Subdivisions:i;
                int y=edge==2?0:edge==3?data.Subdivisions:i;
                var d=Direction(face,x,y,data.Subdivisions);
                var inside=Direction(face,Mathf.Clamp(x+(edge==0?1:edge==1?-1:0),0,data.Subdivisions),
                    Mathf.Clamp(y+(edge==2?1:edge==3?-1:0),0,data.Subdivisions),data.Subdivisions);
                var t=Vector3.ProjectOnPlane(inside-d,d).normalized*step;
                sum+=Mathf.Abs(PlanetTerrainSampler.SampleHeight(data,(d+t).normalized)+PlanetTerrainSampler.SampleHeight(data,(d-t).normalized)-2*PlanetTerrainSampler.SampleHeight(data,d));count++;
            }
            return sum/Mathf.Max(1,count);
        }
    }
}
