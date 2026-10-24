using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>★ 新增：按球面方向焊接六面重复顶点，让几何和法线使用同一拓扑。</summary>
    public static class PlanetMeshTopology
    {
        public static void Weld(PlanetTerrainData data)
        {
            var lookup = new Dictionary<Vector3Int, int>();
            var vertices = new List<Vector3>();
            var remap = new int[data.Vertices.Length];
            for (int i = 0; i < remap.Length; i++)
            {
                Vector3 p = data.Vertices[i];
                Vector3 d = p.normalized;
                var key = new Vector3Int(Mathf.RoundToInt(d.x * 100000f),
                    Mathf.RoundToInt(d.y * 100000f), Mathf.RoundToInt(d.z * 100000f));
                if (!lookup.TryGetValue(key, out int index))
                {
                    index = vertices.Count;
                    lookup.Add(key, index);
                    vertices.Add(p);
                }
                remap[i] = index;
            }
            for (int i = 0; i < data.Triangles.Length; i++)
                data.Triangles[i] = remap[data.Triangles[i]];
            data.Vertices = vertices.ToArray();
        }
    }
}
