using DarwinPark.Planet;
using UnityEngine;
using System.Security.Cryptography;
using System.Text.Json;

string Digest(PlanetClimateResult value)
{
    using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
    foreach(var field in typeof(PlanetClimateResult).GetFields().OrderBy(f=>f.Name,StringComparer.Ordinal))
    {
        writer.Write(field.Name);var item=field.GetValue(value);
        if(item is float n)writer.Write(n);
        else if(item is int count)writer.Write(count);
        else if(item is Array array)
        {
            writer.Write(array.Length);
            foreach(var element in array)
            {
                if(element is float f)writer.Write(f);
                else if(element is bool b)writer.Write(b);
                else if(element is Color c){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
                else if(element is Enum e)writer.Write(Convert.ToInt32(e));
                else throw new Exception("Unverified result array element");
            }
        }
        else throw new Exception("Unverified result field "+field.Name);
    }
    writer.Flush();return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
}
var hashes=new Dictionary<string,string>();
for(int fixture=0;fixture<6;fixture++)
{
    int grid=16,seed=fixture%2==0?78031:17;
    var terrain=new PlanetTerrainData{Subdivisions=grid,Radius=300,HeightScale=90,SeaLevel=fixture==4?0:fixture==5?1:.48f,FaceHeights=new float[6][]};
    for(int face=0;face<6;face++)
    {
        terrain.FaceHeights[face]=new float[(grid+1)*(grid+1)];
        for(int y=0;y<=grid;y++)for(int x=0;x<=grid;x++)terrain.FaceHeights[face][y*(grid+1)+x]=.5f+.27f*Mathf.Sin((x+face*grid)*.37f)*Mathf.Cos(y*.31f);
    }
    var config=new PlanetClimateConfig();config.earth.enabled=fixture%2==0;
    float temperature=fixture==2?-15:0,humidity=fixture==3?20:0;
    var current=new PlanetClimateModel(terrain,config,seed).Generate(64,temperature,humidity);
    string key="fixture-"+fixture,hash=Digest(current);hashes.Add(key,hash);
#if BASELINE_COMPARE
    string baseline=Digest(new BaselineClimateModel(terrain,config,seed).Generate(64,temperature,humidity));
    if(hash!=baseline)throw new Exception("Climate output differs from original: "+key);
#endif
    Console.WriteLine("PASS "+key+" "+hash);
}
string expectedPath=Path.GetFullPath("Tools/ClimateRegression/expected.json");
#if BASELINE_COMPARE
File.WriteAllText(expectedPath,JsonSerializer.Serialize(hashes,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("6 baseline-equivalence fixtures passed; all result arrays and statistics compared bit-for-bit");
#else
var expected=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(expectedPath));
foreach(var pair in hashes)if(expected[pair.Key]!=pair.Value)throw new Exception("Climate regression: "+pair.Key);
Console.WriteLine("6 golden-output fixtures passed");
#endif
