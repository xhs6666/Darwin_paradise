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
                if(hydro.Lake[i]){float u=(i%hydro.Width)/(float)hydro.Width,v=(i/hydro.Width)/(float)hydro.Height,r=data.Radius+hydro.Spill[i]*data.HeightScale+.03f;Quad(PlanetTerrainSampler.DirectionFromUV(u,v)*r,PlanetTerrainSampler.DirectionFromUV(u+1f/hydro.Width,v)*r,PlanetTerrainSampler.DirectionFromUV(u+1f/hydro.Width,v+1f/hydro.Height)*r,PlanetTerrainSampler.DirectionFromUV(u,v+1f/hydro.Height)*r);}
                if(!hydro.River[i])continue;int next=hydro.Downstream[i];var a=hydro.Directions[i]*(data.Radius+hydro.Spill[i]*data.HeightScale-hydro.Settings.riverSurfaceInset+.03f);var b=hydro.Directions[next]*(data.Radius+hydro.Spill[next]*data.HeightScale-hydro.Settings.riverSurfaceInset+.03f);var side=Vector3.Cross((a+b).normalized,b-a).normalized*hydro.Settings.riverHalfWidth;Quad(a-side,a+side,b+side,b-side);
            }
            if(vertices.Count==0)return;var go=new GameObject("RiversAndLakes",typeof(MeshFilter),typeof(MeshRenderer));_surface=go;go.transform.SetParent(transform,false);
            _mesh=new Mesh{name="PlanetRiversAndLakes",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};_mesh.SetVertices(vertices);_mesh.SetTriangles(triangles,0);_mesh.RecalculateBounds();_mesh.RecalculateNormals();go.GetComponent<MeshFilter>().sharedMesh=_mesh;
            // 使用项目已打包的Unlit依赖；独立不透明材质，不改任何共享海洋/地形材质。
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null){Debug.LogError("[水系] 缺少已打包的Unlit Shader");return;}_material=new Material(shader){name="FreshWater"};_material.SetColor("_BaseColor",hydro.Settings.waterColor);_material.SetFloat("_Surface",0);_material.SetFloat("_ZWrite",1);_material.SetFloat("_Cull",0);go.GetComponent<MeshRenderer>().sharedMaterial=_material;
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
