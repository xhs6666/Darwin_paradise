using DarwinPark.Core;
using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 修复（防Bug回归）：气壳不能低于高地；第一人称只增强无遮挡天空的散射，避免把近处实体洗成雾色。
    public static class AtmosphereViewPolicy
    {
        public static float OuterRadius(PlanetTerrainData data,float original,float clearance)
        {float maximum=0;if(data?.Vertices!=null)foreach(var point in data.Vertices)maximum=Mathf.Max(maximum,point.sqrMagnitude);return Mathf.Max(original,Mathf.Sqrt(maximum)+clearance);}
        public static void Apply(Material material,Camera camera)
        {if(material==null)return;bool entity=camera!=null&&camera.GetComponent<OrbitCamera>()?.EntityViewActive==true;material.SetFloat("_EntitySkyDensity",entity?SimulationOptimization.Current.entitySkyDensityMultiplier:1);}
    }
}
