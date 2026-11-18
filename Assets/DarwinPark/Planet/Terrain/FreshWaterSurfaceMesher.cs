using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Planet
{
    // ★ 修复（防Bug回归）：河湖共用唯一球面网格，在实际岸线上裁切，不再叠加互相折返/交叉的河带。
    public static class FreshWaterSurfaceMesher
    {
        private struct Sample {public uint key;public Vector3 surface,current;public float depth,waterDepth;}
        // ★ 修复（防Bug回归）：ulong默认将两端异或，连续编号的相邻边会集中到极少桶中，退化为平方查找。
        private sealed class EdgeHash : IEqualityComparer<ulong>
        {
            public bool Equals(ulong a,ulong b)=>a==b;
            public int GetHashCode(ulong key){unchecked{key^=key>>33;key*=0xff51afd7ed558ccdUL;key^=key>>33;key*=0xc4ceb9fe1a85ec53UL;key^=key>>33;return (int)key;}}
        }
        public static Mesh Build(PlanetTerrainData data,float spacing,int maximumCuts,float lift)
        {
            var hydro=data.Hydrology;float radius=data.Radius+data.SeaLevel*data.HeightScale;
            float step=Mathf.Min(Mathf.Max(.05f,spacing),hydro.Settings.riverHalfWidth*.75f);
            int cuts=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(2*Mathf.PI*radius/hydro.Width,Mathf.PI*radius/hydro.Height)/Mathf.Max(.01f,step)),1,Mathf.Clamp(maximumCuts,1,32));
            int columns=hydro.Width*cuts,rows=hydro.Height*cuts;
            var vertices=new List<Vector3>();var colors=new List<Color>();var currents=new List<Vector3>();var triangles=new List<int>();var samples=new Dictionary<uint,Sample>();var indices=new Dictionary<uint,int>();var edgeIndices=new Dictionary<ulong,int>(new EdgeHash());
            Sample At(int x,int y)
            {
                x=(x+columns)%columns;if(y==0||y==rows)x=0;uint key=(uint)(y*columns+x);if(samples.TryGetValue(key,out var cached))return cached;
                var direction=PlanetTerrainSampler.DirectionFromUV(x/(float)columns,y/(float)rows);
                float water=hydro.SurfaceWater(direction,out float coverage,out int river,out float along),waterRadius=data.Radius+water*data.HeightScale;
                float ground=RadialTerrainSurface.TrySampleLocal(data,direction,out var hit)?hit.point.magnitude:data.Radius+PlanetTerrainSampler.SampleHeight(data,direction)*data.HeightScale;
                // 海平面的水由已有海洋表面负责，淡水只绘制高于海面的部分，避免两层水面深度争抢。
                var point=new Sample{key=key,surface=direction*(waterRadius+lift),waterDepth=Mathf.Max(0,waterRadius-ground),depth=Mathf.Min(coverage,Mathf.Min(waterRadius-ground,(water-data.SeaLevel)*data.HeightScale-lift))};
                point.current=hydro.VisualCurrent(direction,river,along);
                samples.Add(key,point);return point;
            }
            int Vertex(Sample point)
            {if(indices.TryGetValue(point.key,out int index))return index;index=vertices.Count;vertices.Add(point.surface);colors.Add(new Color(point.waterDepth,point.depth,0,1));currents.Add(point.current);indices.Add(point.key,index);return index;}
            int Edge(Sample a,Sample b)
            {
                ulong key=((ulong)Math.Min(a.key,b.key)<<32)|Math.Max(a.key,b.key);if(edgeIndices.TryGetValue(key,out int index))return index;
                float t=a.depth/(a.depth-b.depth);index=vertices.Count;vertices.Add(Vector3.Lerp(a.surface,b.surface,t));colors.Add(new Color(Mathf.Lerp(a.waterDepth,b.waterDepth,t),0,0,1));currents.Add(Vector3.Lerp(a.current,b.current,t));edgeIndices.Add(key,index);return index;
            }
            var input=new Sample[3];var polygon=new int[4];
            void Triangle(Sample a,Sample b,Sample c)
            {
                input[0]=a;input[1]=b;input[2]=c;int count=0;
                for(int i=0;i<3;i++){var start=input[(i+2)%3];var end=input[i];bool inside=end.depth>0;if((start.depth>0)!=inside)polygon[count++]=Edge(start,end);if(inside)polygon[count++]=Vertex(end);}
                for(int i=1;i<count-1;i++)
                {
                    int a0=polygon[0],b0=polygon[i],c0=polygon[i+1];var normal=Vector3.Cross(vertices[b0]-vertices[a0],vertices[c0]-vertices[a0]);if(normal.sqrMagnitude<1e-10f)continue;
                    bool reverse=Vector3.Dot(normal,vertices[a0])<0;triangles.Add(a0);triangles.Add(reverse?c0:b0);triangles.Add(reverse?b0:c0);
                }
            }
            float width=hydro.Settings.version>=3?Mathf.Max(radius*1.5f/data.Subdivisions,hydro.Settings.riverMaximumWidth+hydro.Settings.bankWidth):hydro.Settings.riverHalfWidth;
            int ry=Mathf.Min(hydro.Height-1,2+Mathf.CeilToInt(width/(Mathf.PI*radius/hydro.Height)));
            for(int cell=0;cell<hydro.Bed.Length;cell++)
            {
                int column=cell%hydro.Width,row=cell/hydro.Width;float cosine=Mathf.Sqrt(Mathf.Max(0,1-hydro.Directions[cell].y*hydro.Directions[cell].y));
                int rx=Mathf.Min(hydro.Width/2,2+Mathf.CeilToInt(width/Mathf.Max(.001f,2*Mathf.PI*radius/hydro.Width*cosine)));bool nearby=false;
                for(int dy=-ry;dy<=ry&&!nearby;dy++)for(int dx=-rx;dx<=rx;dx++){if(row+dy<0||row+dy>=hydro.Height)continue;int index=(row+dy)*hydro.Width+(column+dx+hydro.Width)%hydro.Width;if(hydro.River[index]||hydro.Lake[index]){nearby=true;break;}}
                if(!nearby)continue;
                for(int y=0;y<cuts;y++)for(int x=0;x<cuts;x++){int gx=column*cuts+x,gy=row*cuts+y;var a=At(gx,gy);var b=At(gx+1,gy);var c=At(gx+1,gy+1);var d=At(gx,gy+1);Triangle(a,b,c);Triangle(a,c,d);}
            }
            var mesh=new Mesh{name="PlanetRiversAndLakes",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(1,currents);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateNormals();return mesh;
        }
    }
}
