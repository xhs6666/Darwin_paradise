using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DarwinPark.Planet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：验证真实生成数据的水文性质和旧档确定性，并输出实际水位/地表地图供人工检查。
    public static class HydrologyRegression
    {
        private static readonly List<string> Passed=new();
        private static readonly StringBuilder Metrics=new();
        private static readonly string Folder=Path.GetFullPath("Artifacts/HydrologyReview");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Passed.Add(label);}
        public static void RunBatch()
        {
            Directory.CreateDirectory(Folder);
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
                BasinFixture();
                var legacy=new PlanetTerrainConfig{landforms=new LandformSettings{version=2,riverHalfWidth=4,bankWidth=5,lakeMinimumDepth=5}};
                DarwinPark.UI.WorldLandformEdits.Apply(legacy,"landforms",1);LandformSaveCompatibility.Validate(JsonUtility.ToJson(legacy));
                Check(legacy.landforms.version==LandformSettings.CurrentVersion&&legacy.landforms.riverHalfWidth==4&&legacy.landforms.lakeMinimumDepth==5,"显式升级保留旧档自定义宽河及深湖阈值并补齐新版参数");
                Check(JsonUtility.FromJson<PlanetTerrainConfig>("{\"landforms\":{\"version\":2}}").landforms.version==2,"旧档版本读取后不自动提升");
                foreach(int seed in new[]{78031,742091})
                {
                    var config=builder.CaptureConfiguration();config.seed=seed;config.subdivisions=128;config.navigation.longitudeCells=64;config.navigation.latitudeCells=32;
                    config.landforms=new LandformSettings{version=2};var old=PlanetTerrainGenerator.Generate(config,.5f,.5f);
                    config.landforms.version=3;var watch=System.Diagnostics.Stopwatch.StartNew();var current=PlanetTerrainGenerator.Generate(config,.5f,.5f);watch.Stop();
                    Validate(current,seed);Metrics.AppendLine("seed="+seed+" generationMs="+watch.ElapsedMilliseconds+" oldRiver="+old.Hydrology.RiverCells+" newRiver="+current.Hydrology.RiverCells+" oldLake="+old.Hydrology.LakeCells+" newLake="+current.Hydrology.LakeCells);
                    Export(old,"before-"+seed);Export(current,"after-"+seed);
                    // 复用同一配置重复生成，只比较确定性数据，不修改历史基准文件。
                    var again=PlanetTerrainGenerator.Generate(config,.5f,.5f);
                    Check(current.Hydrology.Downstream.SequenceEqual(again.Hydrology.Downstream)&&current.FaceHeights.SelectMany(v=>v).SequenceEqual(again.FaceHeights.SelectMany(v=>v)),seed+" 同配置重复生成完全一致");
                    config.landforms.version=2;var restored=PlanetTerrainGenerator.Generate(config,.5f,.5f);
                    Check(old.FaceHeights.SelectMany(v=>v).SequenceEqual(restored.FaceHeights.SelectMany(v=>v))&&old.Hydrology.Downstream.SequenceEqual(restored.Hydrology.Downstream),seed+" 第三版生成不污染旧版世界结果");
                }
                var production=builder.CaptureConfiguration();production.seed=78031;production.landforms=new LandformSettings();var productionWatch=System.Diagnostics.Stopwatch.StartNew();var full=PlanetTerrainGenerator.Generate(production,builder.seaLevelRatio,builder.continentRatio);productionWatch.Stop();Validate(full,78031320);Export(full,"after-default-78031");
                Metrics.AppendLine("defaultSubdivisions="+production.subdivisions+" vertices="+full.Vertices.Length+" generationMs="+productionWatch.ElapsedMilliseconds+" riverCells="+full.Hydrology.RiverCells+" lakeCells="+full.Hydrology.LakeCells);
                File.WriteAllText(Path.Combine(Folder,"metrics.txt"),Metrics.ToString());File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));EditorApplication.Exit(0);
            }
            catch(Exception error){File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL\n"+error+"\n"+string.Join("\n",Passed));Debug.LogException(error);EditorApplication.Exit(1);}
        }
        private static void Validate(PlanetTerrainData data,int seed)
        {
            var hydro=data.Hydrology;int n=hydro.Bed.Length;double rain=0,discharge=0;int merged=0,wide=0;var incoming=new int[n];
            for(int i=0;i<n;i++)
            {
                int down=hydro.Downstream[i];rain+=Mathf.Max(.01f,Mathf.Sqrt(1-hydro.Directions[i].y*hydro.Directions[i].y));
                if(down<0){discharge+=hydro.Accumulation[i];continue;}incoming[down]++;
                if(hydro.Spill[down]>hydro.Spill[i]+1e-6f)throw new InvalidOperationException("水位逆坡");
                int cursor=i,steps=0;while(hydro.Downstream[cursor]>=0){cursor=hydro.Downstream[cursor];if(++steps>n)throw new InvalidOperationException("出流循环");}
                if(hydro.River[i])
                {
                    if(hydro.Lake[i])throw new InvalidOperationException("湖内残留河道条纹");
                    if(hydro.ChannelWidth(i)>hydro.Settings.riverHalfWidth*1.2f)wide++;
                    if(hydro.Accumulation[down]+1e-3f<hydro.Accumulation[i])throw new InvalidOperationException("汇水丢失");
                    if(hydro.ChannelWidth(down)+1e-5f<hydro.ChannelWidth(i))throw new InvalidOperationException("下游异常收窄");
                }
            }
            merged=incoming.Count(v=>v>1);
            Check(Math.Abs(rain-discharge)<rain*.0001,seed+" 球面面积加权汇水守恒");
            Check(true,seed+" 全部下游链无环且水位不逆坡");
            Check(hydro.RiverCells>0&&merged>0&&wide>0,seed+" 存在支流汇合和主干加宽");
            Check(hydro.LakeCells>0&&!Enumerable.Range(0,n).Any(i=>hydro.Lake[i]&&hydro.River[i]),seed+" 盆地湖泊有水且湖内不重复绘制河流");
            Check(Mathf.Abs(hydro.Water(PlanetTerrainSampler.DirectionFromUV(0,.5f))-hydro.Water(PlanetTerrainSampler.DirectionFromUV(1,.5f)))<.0001f,seed+" 全球经线接缝水位连续");
            string json=JsonUtility.ToJson(new PlanetTerrainConfig{landforms=hydro.Settings});LandformSaveCompatibility.Validate(json);
            Check(JsonUtility.FromJson<PlanetTerrainConfig>(json).landforms.version==hydro.Settings.version,seed+" 新水系配置读写成对且通过校验");
            var go=new GameObject("HydrologyMeshFixture");var visual=go.AddComponent<PlanetFreshWaterMesh>();
            typeof(PlanetFreshWaterMesh).GetMethod("Build",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(visual,new object[]{data});
            var mesh=go.GetComponentInChildren<MeshFilter>().sharedMesh;
            Check(mesh.vertexCount>0&&mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z))&&mesh.triangles.All(i=>i>=0&&i<mesh.vertexCount),seed+" 实际河湖网格顶点与三角索引有效");
            Object.DestroyImmediate(go);
            Check(Enumerable.Range(0,n).Where(i=>hydro.River[i]).All(i=>hydro.ChannelPaths[i].Length==hydro.Settings.riverCurveSegments+1&&hydro.ChannelPaths[i][0]==hydro.Channel(i)&&hydro.ChannelPaths[i].Last()==hydro.Channel(hydro.Downstream[i])),seed+" 曲线端点与汇流节点完全连续");
        }
        private static void BasinFixture()
        {
            var data=new PlanetTerrainData{Radius=300,HeightScale=90,SeaLevel=.5f,Subdivisions=64,FaceHeights=new float[6][]};int sub=data.Subdivisions;
            for(int f=0;f<6;f++){data.FaceHeights[f]=new float[(sub+1)*(sub+1)];for(int y=0;y<=sub;y++)for(int x=0;x<=sub;x++){float u=x*2f/sub-1,v=y*2f/sub-1;var dir=(f<2?new Vector3(f==0?1:-1,v,u):f<4?new Vector3(u,f==2?1:-1,v):new Vector3(u,v,f==4?1:-1)).normalized;data.FaceHeights[f][y*(sub+1)+x]=dir.y<-.45f?.3f:.7f;}}
            var settings=new LandformSettings{basinCount=4,longitudeCells=128,latitudeCells=64};
            var before=PlanetHydrology.Build(data,settings);int generated=PlanetDrainageNetwork.CarveBasins(data,settings,78031);var after=PlanetHydrology.Build(data,settings);
            Check(generated==settings.basinCount&&before.LakeCells==0&&after.LakeCells>0,"原本无湖的平缓内陆雕刻盆地后真实蓄水成湖");
            Metrics.AppendLine("syntheticBasins="+generated+" newLakeCells="+after.LakeCells);
        }
        private static void Export(PlanetTerrainData data,string name)
        {
            int width=1024,height=512;var pixels=new Color[width*height];var hydro=data.Hydrology;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var dir=PlanetTerrainSampler.DirectionFromUV((x+.5f)/width,(y+.5f)/height);float ground=PlanetTerrainSampler.SampleHeight(data,dir),water=hydro.Water(dir);
                float altitude=(ground-data.SeaLevel)*data.HeightScale;
                Color color=Color.Lerp(new Color(.35f,.53f,.25f),new Color(.85f,.82f,.72f),Mathf.Clamp01(altitude/20));
                if(altitude<0)color=new Color(.06f,.22f,.36f);else if(water>ground)color=new Color(.10f,.58f,.77f);
                pixels[y*width+x]=color;
            }
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),texture.EncodeToPNG());Object.DestroyImmediate(texture);
        }
    }
}
