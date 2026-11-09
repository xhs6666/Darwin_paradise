using UnityEngine;
namespace DarwinPark.Planet
{
    public readonly struct RadialSurfaceHit
    {
        public readonly Vector3 point,normal;public readonly int triangle;
        public RadialSurfaceHit(Vector3 point,Vector3 normal,int triangle){this.point=point;this.normal=normal;this.triangle=triangle;}
    }
    // ★ 新增：生成地形是径向Cube-Sphere，面/格坐标可直接定位两个真实三角形，无需查询整个PhysX网格。
    public static class RadialTerrainSurface
    {
        public static bool TrySample(PlanetTerrainMeshBuilder builder,MeshCollider ground,Vector3 point,out RadialSurfaceHit hit)
        {
            hit=default;var data=builder!=null?builder.Data:null;
            if(data==null||ground==null||!ground.enabled||ground.sharedMesh!=builder.GeneratedMesh||data.Subdivisions<2||data.Triangles==null||data.Vertices==null||data.Triangles.Length!=36*data.Subdivisions*data.Subdivisions)return false;
            var transform=builder.transform;var scale=transform.lossyScale;
            // 非均匀/负缩放交回真实碰撞体，不能用错误的法线变换影响斜坡支撑。
            if(scale.x<=0||Mathf.Abs(scale.x-scale.y)>.00001f||Mathf.Abs(scale.x-scale.z)>.00001f)return false;
            var local=transform.InverseTransformPoint(point);float ax=Mathf.Abs(local.x),ay=Mathf.Abs(local.y),az=Mathf.Abs(local.z),largest;int face;float u,v;
            if(ax>=ay&&ax>=az){largest=ax;face=local.x>=0?0:1;u=local.z;v=local.y;}
            else if(ay>=az){largest=ay;face=local.y>=0?2:3;u=local.x;v=local.z;}
            else{largest=az;face=local.z>=0?4:5;u=local.x;v=local.y;}
            if(largest<.00001f)return false;
            int n=data.Subdivisions,x=Mathf.Clamp(Mathf.FloorToInt((u/largest+1)*.5f*n),0,n-1),y=Mathf.Clamp(Mathf.FloorToInt((v/largest+1)*.5f*n),0,n-1),index=(face*n*n+y*n+x)*6;
            var direction=local.normalized;
            // 使用焊接后的实际顶点/索引，而非双线性高度近似；接缝和陡坡仍命中与碰撞体相同的平面。
            for(int i=0;i<2;i++)if(Triangle(data,index+i*3,direction,out var surface,out var normal))
            {hit=new RadialSurfaceHit(transform.TransformPoint(surface),transform.TransformDirection(normal).normalized,index/3+i);return true;}
            return false;
        }
        private static bool Triangle(PlanetTerrainData data,int index,Vector3 direction,out Vector3 point,out Vector3 normal)
        {
            point=normal=default;int ia=data.Triangles[index],ib=data.Triangles[index+1],ic=data.Triangles[index+2];
            if((uint)ia>=(uint)data.Vertices.Length||(uint)ib>=(uint)data.Vertices.Length||(uint)ic>=(uint)data.Vertices.Length)return false;
            var a=data.Vertices[ia];var e1=data.Vertices[ib]-a;var e2=data.Vertices[ic]-a;var p=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,p);
            if(Mathf.Abs(det)<1e-12f)return false;float inverse=1/det;var origin=-a;float u=Vector3.Dot(origin,p)*inverse;if(u<-.00001f||u>1.00001f)return false;
            var q=Vector3.Cross(origin,e1);float v=Vector3.Dot(direction,q)*inverse;if(v<-.00001f||u+v>1.00001f)return false;
            float distance=Vector3.Dot(e2,q)*inverse;if(distance<=0)return false;point=direction*distance;normal=Vector3.Cross(e1,e2).normalized;return normal.sqrMagnitude>.5f;
        }
    }
}
