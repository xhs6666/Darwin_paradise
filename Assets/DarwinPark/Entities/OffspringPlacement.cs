using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.Entities
{
    /// <summary>★ 新增：幼体沿亲本所在球面周围随机落在真实地面，不使用三维球内偏移导致重叠或埋地。</summary>
    public static class OffspringPlacement
    {
        public static bool TryGetPosition(Vector3 parent,bool spread,out Vector3 position)
        {
            position=parent;
            var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var ground=builder!=null?builder.GetComponent<MeshCollider>():null;
            if(ground==null || builder.Data==null){Debug.LogWarning("[繁殖] 地形未就绪，暂缓出生");return false;}
            var settings=LifeCycleSettings.Load();var up=(parent-builder.transform.position).normalized;
            var a=Vector3.Cross(up,Mathf.Abs(up.y)>.95f?Vector3.right:Vector3.up).normalized;var b=Vector3.Cross(up,a);
            float min=spread?settings.stationaryBirthMinRadius:settings.movingBirthRadius;
            float max=spread?settings.stationaryBirthMaxRadius:settings.movingBirthRadius;
            float radius=Mathf.Sqrt(Random.Range(min*min,max*max));float angle=Random.Range(0,Mathf.PI*2);
            var direction=(parent-builder.transform.position+(a*Mathf.Cos(angle)+b*Mathf.Sin(angle))*radius).normalized;
            float outside=ground.bounds.extents.magnitude+settings.birthHeight;
            if(!ground.Raycast(new Ray(builder.transform.position+direction*outside,-direction),out var hit,outside))return false;
            position=hit.point+direction*settings.birthHeight;return true;
        }
    }
}
