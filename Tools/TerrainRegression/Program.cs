using DarwinPark.Planet;
using UnityEngine;
using System.Security.Cryptography;
using System.Text.Json;

// ✅ 新增：在拆分前记录完整地形与导航结果，之后逐位比对，防止无意改变种子和计算顺序。
string Digest(PlanetTerrainData data)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write(data.Subdivisions); writer.Write(data.Radius); writer.Write(data.HeightScale); writer.Write(data.SeaLevel);
    foreach (var face in data.FaceHeights) foreach (float height in face) writer.Write(height);
    foreach (var vertex in data.Vertices) { writer.Write(vertex.x); writer.Write(vertex.y); writer.Write(vertex.z); }
    foreach (int triangle in data.Triangles) writer.Write(triangle);
    if (data.Hydrology != null)
    {
        foreach (float value in data.Hydrology.Bed) writer.Write(value);
        foreach (float value in data.Hydrology.Spill) writer.Write(value);
        foreach (int value in data.Hydrology.Downstream) writer.Write(value);
        foreach (bool value in data.Hydrology.Lake) writer.Write(value);
        foreach (bool value in data.Hydrology.River) writer.Write(value);
    }
    writer.Flush();
    return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
}

try
{
var hashes = new Dictionary<string, string>();
DarwinPark.Core.Log.Mask = DarwinPark.Core.Log.Tag.Data;
if (ManagedTerrainGenerator.Generate(null, .5f, .5f) != null) throw new Exception("Null config contract changed");
for (int fixture = 0; fixture < 8; fixture++)
{
    var config = new PlanetTerrainConfig { subdivisions = fixture % 2 == 0 ? 8 : 12, seed = fixture % 3 == 0 ? 78031 : 17 };
    config.navigation.longitudeCells = 32; config.navigation.latitudeCells = 16;
    config.landforms.longitudeCells = 32; config.landforms.latitudeCells = 16;
    config.landforms.enabled = fixture >= 4;
    // ★ 修复（防Bug回归）：原有逐位基准固定旧版；新增版本另做水文性质检查，不能重录基准掩盖旧档漂移。
    config.landforms.version = 2;
    config.erosion.hydraulicIterations = 10;
    if (fixture == 1) config.erosion = null;
    float land = fixture == 2 ? 0 : fixture == 3 ? 1 : .5f;
    var terrain = ManagedTerrainGenerator.Generate(config, .5f, land);
    string hash = Digest(terrain); hashes.Add("fixture-" + fixture, hash);
    Console.WriteLine("PASS fixture-" + fixture + " " + hash);
}
string expected = Path.GetFullPath("Tools/TerrainRegression/expected.json");
if (args.Contains("--record-baseline")) File.WriteAllText(expected, JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true }));
else
{
    var baseline = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(expected));
    foreach (var pair in hashes) if (baseline[pair.Key] != pair.Value) throw new Exception("Terrain output changed: " + pair.Key);
    Console.WriteLine("8 terrain golden fixtures passed (heights, mesh, hydrology; native navigation excluded)");
}
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
