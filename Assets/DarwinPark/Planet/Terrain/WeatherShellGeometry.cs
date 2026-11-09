using DarwinPark.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace DarwinPark.Planet
{
    // ★ 修复（防Bug回归）：云和大气的真实边界由Shader解析球面确定，代理不应复制百万三角形地形。
    public static class WeatherShellGeometry
    {
        public static Mesh Create(PlanetTerrainData data,float radius,string name)
        {
            var settings=SimulationOptimization.Current;bool proxy=name!="OceanSurface"&&settings.weatherProxyMeshes;
            Vector3[] vertices,normals;int[] triangles;
            if(proxy)
            {
                int n=settings.weatherProxySubdivisions,stride=n+1;vertices=new Vector3[6*stride*stride];triangles=new int[36*n*n];int offset=0;
                var axes=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
                for(int face=0;face<6;face++)
                {
                    Vector3 normal=axes[face],u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up,v=Vector3.Cross(normal,u);int baseIndex=face*stride*stride;
                    for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)vertices[baseIndex+y*stride+x]=(normal+u*(2f*x/n-1)+v*(2f*y/n-1)).normalized;
                    for(int y=0;y<n;y++)for(int x=0;x<n;x++){int a=baseIndex+y*stride+x;triangles[offset++]=a;triangles[offset++]=a+1;triangles[offset++]=a+stride;triangles[offset++]=a+1;triangles[offset++]=a+stride+1;triangles[offset++]=a+stride;}
                }
            }
            else{vertices=new Vector3[data.Vertices.Length];for(int i=0;i<vertices.Length;i++)vertices[i]=data.Vertices[i].normalized;triangles=(int[])data.Triangles.Clone();}
            float minimumPlane=1;
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]],normal=Vector3.Cross(b-a,c-a);
                if(Vector3.Dot(normal,a+b+c)<0){(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);normal=-normal;}
                if(proxy)minimumPlane=Mathf.Min(minimumPlane,Vector3.Dot(normal.normalized,a));
            }
            // ★ 修复（防Bug回归）：外接代理的每个面均在解析球面外，防止低模弦面裁掉天空边缘或穿壳镜头。
            float scale=proxy?radius/Mathf.Max(.1f,minimumPlane):radius;normals=new Vector3[vertices.Length];for(int i=0;i<vertices.Length;i++){normals[i]=vertices[i];vertices[i]*=scale;}
            var mesh=new Mesh{name=name,indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
    }
}
