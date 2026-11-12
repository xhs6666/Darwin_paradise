using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：河流/湖泊使用一个静态合并网格，不增加逐河流Update、灯光或碰撞体。
    public sealed class PlanetFreshWaterMesh:MonoBehaviour
    {
        private Mesh _mesh;private Material _material;private GameObject _surface;
        public static void Rebuild(PlanetTerrainMeshBuilder planet)
        {var visual=planet.GetComponent<PlanetFreshWaterMesh>()??planet.gameObject.AddComponent<PlanetFreshWaterMesh>();visual.Build(planet.Data);}
        private void Build(PlanetTerrainData data)
        {
            if(_surface!=null){_surface.SetActive(false);Destroy(_surface);_surface=null;}if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);
            var hydro=data?.Hydrology;if(hydro==null)return;var vertices=new List<Vector3>();var triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);if(Vector3.Dot(Vector3.Cross(b-a,c-a),a)<0){triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});}else triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
            for(int i=0;i<hydro.Bed.Length;i++)
            {
                if(hydro.Settings.version<2&&hydro.Lake[i]){float u=(i%hydro.Width)/(float)hydro.Width,v=(i/hydro.Width)/(float)hydro.Height,r=data.Radius+hydro.Spill[i]*data.HeightScale+.03f;Quad(PlanetTerrainSampler.DirectionFromUV(u,v)*r,PlanetTerrainSampler.DirectionFromUV(u+1f/hydro.Width,v)*r,PlanetTerrainSampler.DirectionFromUV(u+1f/hydro.Width,v+1f/hydro.Height)*r,PlanetTerrainSampler.DirectionFromUV(u,v+1f/hydro.Height)*r);}
                if(!hydro.River[i])continue;int next=hydro.Downstream[i];var a=hydro.Directions[i]*(data.Radius+hydro.Spill[i]*data.HeightScale-hydro.Settings.riverSurfaceInset+.03f);var b=hydro.Directions[next]*(data.Radius+hydro.Spill[next]*data.HeightScale-hydro.Settings.riverSurfaceInset+.03f);var side=Vector3.Cross((a+b).normalized,b-a).normalized*hydro.Settings.riverHalfWidth;Quad(a-side,a+side,b+side,b-side);
            }
            // ★ 修复（防Bug回归）：新版湖面细分并按真实三角形岸线裁切，不能直接把水文格子画成悬空正方形。
            if(hydro.Settings.version>=2)AppendLakes(data,vertices,triangles);
            if(vertices.Count==0)return;var go=new GameObject("RiversAndLakes",typeof(MeshFilter),typeof(MeshRenderer));_surface=go;go.transform.SetParent(transform,false);
            _mesh=new Mesh{name="PlanetRiversAndLakes",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};_mesh.SetVertices(vertices);_mesh.SetTriangles(triangles,0);_mesh.RecalculateBounds();_mesh.RecalculateNormals();go.GetComponent<MeshFilter>().sharedMesh=_mesh;
            // 使用项目已打包的Unlit依赖；独立不透明材质，不改任何共享海洋/地形材质。
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null){Debug.LogError("[水系] 缺少已打包的Unlit Shader");return;}_material=new Material(shader){name="FreshWater"};_material.SetColor("_BaseColor",hydro.Settings.waterColor);_material.SetFloat("_Surface",0);_material.SetFloat("_ZWrite",1);_material.SetFloat("_Cull",0);go.GetComponent<MeshRenderer>().sharedMaterial=_material;
        }
        private struct ShorePoint {public Vector3 surface;public float depth;}
        private static void AppendLakes(PlanetTerrainData data,List<Vector3> vertices,List<int> triangles)
        {
            var hydro=data.Hydrology;int cuts=Mathf.Clamp(hydro.Settings.lakeMeshSubdivisions,1,8);var input=new ShorePoint[3];var polygon=new ShorePoint[4];
            ShorePoint Sample(float u,float v){var dir=PlanetTerrainSampler.DirectionFromUV(u,v);float water=hydro.LakeWater(dir),radius=data.Radius+water*data.HeightScale;float ground=RadialTerrainSurface.TrySampleLocal(data,dir,out var hit)?hit.point.magnitude:data.Radius+PlanetTerrainSampler.SampleHeight(data,dir)*data.HeightScale;return new ShorePoint{surface=dir*(radius+.03f),depth=water>data.SeaLevel?(radius-ground):-1};}
            void Triangle(ShorePoint a,ShorePoint b,ShorePoint c)
            {
                input[0]=a;input[1]=b;input[2]=c;int count=0;
                for(int k=0;k<3;k++){var start=input[(k+2)%3];var end=input[k];bool inside=end.depth>0;if((start.depth>0)!=inside){float t=start.depth/(start.depth-end.depth);polygon[count++]=new ShorePoint{surface=Vector3.Lerp(start.surface,end.surface,t)};}if(inside)polygon[count++]=end;}
                if(count<3)return;int offset=vertices.Count;for(int k=0;k<count;k++)vertices.Add(polygon[k].surface);for(int k=1;k<count-1;k++){bool reverse=Vector3.Dot(Vector3.Cross(polygon[k].surface-polygon[0].surface,polygon[k+1].surface-polygon[0].surface),polygon[0].surface)<0;triangles.Add(offset);triangles.Add(offset+(reverse?k+1:k));triangles.Add(offset+(reverse?k:k+1));}
            }
            for(int cell=0;cell<hydro.Bed.Length;cell++)
            {
                int column=cell%hydro.Width,row=cell/hydro.Width;bool nearby=false;for(int dy=-1;dy<=1&&!nearby;dy++)for(int dx=-1;dx<=1;dx++){if(row+dy<0||row+dy>=hydro.Height)continue;if(hydro.Lake[(row+dy)*hydro.Width+(column+dx+hydro.Width)%hydro.Width]){nearby=true;break;}}if(!nearby)continue;
                for(int y=0;y<cuts;y++)for(int x=0;x<cuts;x++){float u=(column+x/(float)cuts)/hydro.Width,v=(row+y/(float)cuts)/hydro.Height,du=1f/(cuts*hydro.Width),dv=1f/(cuts*hydro.Height);var a=Sample(u,v);var b=Sample(u+du,v);var c=Sample(u+du,v+dv);var d=Sample(u,v+dv);Triangle(a,b,c);Triangle(a,c,d);}
            }
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
