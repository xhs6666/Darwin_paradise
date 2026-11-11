using System.Collections.Generic;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：重建地形时能量/尸体同样保留离地高度，不能仅移动活体造成资源埋地或悬空。
    public sealed class TerrainResourceRelocation
    {
        private struct Item {public ColonyResource resource;public Vector3 direction;public float altitude;}
        private readonly List<Item> _items=new();
        public TerrainResourceRelocation(PlanetTerrainMeshBuilder planet,float minimum,float maximum)
        {var carried=new HashSet<string>();foreach(var member in AntColonyMember.All)if(!string.IsNullOrEmpty(member.State.carriedResource))carried.Add(member.State.carriedResource);foreach(var resource in ColonyResource.All){if(carried.Contains(resource.State.id))continue;var offset=resource.transform.position-planet.transform.position;var direction=offset.normalized;float ground=planet.radius+PlanetTerrainSampler.SampleHeight(planet.Data,planet.transform.InverseTransformDirection(direction))*planet.heightScale;_items.Add(new Item{resource=resource,direction=direction,altitude=Mathf.Clamp(offset.magnitude-ground,minimum,maximum)});}}
        public void Restore(PlanetTerrainMeshBuilder planet)
        {foreach(var item in _items)if(item.resource!=null&&item.resource.isActiveAndEnabled){float radius=planet.radius+PlanetTerrainSampler.SampleHeight(planet.Data,planet.transform.InverseTransformDirection(item.direction))*planet.heightScale;item.resource.CarryTo(planet.transform.position+item.direction*(radius+item.altitude));}_items.Clear();}
    }
}
