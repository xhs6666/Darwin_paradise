using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Genetics;
using DarwinPark.Entities;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：隔离验证数值状态、真实UGUI卡片、河床几何和GPU水纹，不访问玩家存档。
    public static class GeneRiverRegression
    {
        private static readonly List<string> Passed=new();
        private static string Folder=>Path.GetFullPath("Artifacts/GeneRiverResult");
        private static void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);Passed.Add(label);Debug.Log("[GeneRiver] "+label);}
        public static void RunBatch()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Artifacts/GeneRiverReview/Project/Assets"))throw new InvalidOperationException("需要隔离项目");
            Directory.CreateDirectory(Folder);
            try{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);Genes();ExpressionIndex();Water();DeepWaterAndCirculation();ShaderPreview();File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));EditorApplication.Exit(0);}
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL\n"+e+"\n"+string.Join("\n",Passed));Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void Genes()
        {
            GeneDatabase.EnsureLoaded();var definition=GeneDatabase.Get("gene_locomotion");
            Check(!definition.ExplicitState,"内置行动基因恢复数值状态");
            definition.ExplicitState=true;Check(!definition.ExplicitState,"世界内嵌旧定义也无法重新启用行动模式选择");
            var gene=GeneInstance.Create(definition.Id,25,0);gene.StateId="amphibious";var genome=new Genome();genome.Add(gene);
            var low=GeneValueResolver.Resolve(genome).Genes.Single();Check(low.State.StateId=="swimming","值25忽略保存的两栖模式，求值为游泳");
            gene.Value=61;gene.StateId="swimming";var high=GeneValueResolver.Resolve(genome).Genes.Single();Check(high.State.StateId=="amphibious","截图值61由数值求值为两栖");
            Check(!GeneValueResolver.Resolve(genome).Attributes.ContainsKey("swimSpeed"),"普通行动基因不会绕过陆地寻路");
            var swim = genome.Add(GeneInstance.Create("gene_swimming",75,1));
            Check(GeneValueResolver.Resolve(genome).Attributes["swimSpeed"]>0,"独立游泳基因提供水中推进");
            swim.Enabled=false;
            Check(!GeneValueResolver.Resolve(genome).Attributes.ContainsKey("swimSpeed"),"禁用游泳基因后水中推进立即失效");
            genome.Remove(swim.InstanceId);
            gene.MissingStateIds.Add("amphibious");Check(GeneValueResolver.Resolve(genome).Genes.Single().State.StateId=="swimming","缺失状态按原规则跳过");gene.MissingStateIds.Clear();
            gene.Enabled=false;Check(GeneFailureReasons.InspectGene(gene,null).State.StateId=="amphibious","禁用基因检查与运行状态判定一致");gene.Enabled=true;
            var host=new GameObject("UIFixture");host.SetActive(false);var inspector=host.AddComponent<GeneInspectorUI>();var controller=host.AddComponent<EntityGeneController>();
            var canvas=new GameObject("Canvas",typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));var scroll=new GameObject("Scroll",typeof(RectTransform),typeof(ScrollRect));scroll.transform.SetParent(canvas.transform,false);
            typeof(GeneInspectorUI).GetMethod("CreateGeneRow",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(inspector,new object[]{high,controller,scroll.transform});
            Check(scroll.GetComponentsInChildren<Dropdown>(true).Length==0&&scroll.GetComponentsInChildren<Text>(true).Any(t=>t.name=="StateTxt"&&t.text.Contains("两栖")),"真实UGUI行动卡显示两栖文字且没有下拉列表");
            Check(scroll.GetComponentsInChildren<EventTrigger>(true).Length==0&&scroll.GetComponentsInChildren<GeneHoverSurface>(true).Length==1,"每张基因卡只保留一个合并事件组件");
            int clicks=0,enters=0,exits=0;var card=new GameObject("Events",typeof(RectTransform),typeof(Image));card.transform.SetParent(scroll.transform,false);GeneHoverSurface.Bind(card,()=>enters++,()=>exits++,()=>clicks++);var surface=card.GetComponent<GeneHoverSurface>();
            var events=new GameObject("Events",typeof(EventSystem)).GetComponent<EventSystem>();var pointer=new PointerEventData(events){button=PointerEventData.InputButton.Left,clickCount=2};surface.OnPointerEnter(pointer);surface.OnPointerClick(pointer);surface.OnPointerExit(pointer);Check(clicks==1&&enters==1&&exits==1,"合并事件组件保留双击及提示进入离开事件");
            Object.DestroyImmediate(canvas);Object.DestroyImmediate(host);Object.DestroyImmediate(events.gameObject);
        }
        // ★ 修复（防Bug回归）：缓存命中后必须绑定当前个体，抑制和前置依赖仍支持多个副本。
        private static void ExpressionIndex()
        {
            var action = GeneDatabase.Get("gene_locomotion");
            var environment = GeneDatabase.Get("gene_environment");
            var growth = GeneDatabase.Get("gene_growth");
            var oldActionRequirements = action.RequiresGenes;
            var oldEnvironmentRequirements = environment.RequiresGenes;
            var oldRules = growth.SuppressionRules;
            try
            {
                action.RequiresGenes = new List<string> { environment.Id };
                environment.RequiresGenes = new List<string> { growth.Id };
                growth.SuppressionRules = new List<GeneSuppressionRule>
                {
                    new GeneSuppressionRule { TargetGeneId = action.Id, Coefficient = 100 }
                };
                GeneExpressionCache.Clear();
                var genome = new Genome();
                genome.Add(GeneInstance.Create(action.Id, 60, 0));
                genome.Add(GeneInstance.Create(action.Id, 60, 0)).Enabled = false;
                genome.Add(GeneInstance.Create(action.Id, 70, 0));
                genome.Add(GeneInstance.Create(environment.Id, 60, 0));
                var source = genome.Add(GeneInstance.Create(growth.Id, 50, 0));
                genome.Add(GeneInstance.Create(growth.Id, 5, 0));

                var values = GeneValueResolver.Resolve(genome);
                var copy = Genome.Copy(genome);
                var copied = GeneValueResolver.Resolve(copy);
                Check(copied.Genes[1].Gene == copy.Genes[2] && copied.Genes[1].Gene != values.Genes[1].Gene,
                    "跳过禁用副本后缓存使用正确的个体实例位置");
                Check(values.Genes.Take(2).All(g => g.IsActive && Mathf.Abs(g.SuppressionAmount - 5.5f) < .0001f),
                    "同定义的有效副本收到等量抑制，来源抑制仍按原值叠加");
                source.Enabled = false;
                Check(GeneValueResolver.Resolve(genome).Genes.Take(3).All(g => !g.IsActive && g.MissingRequirements),
                    "最后一个有效前置副本禁用后，依赖按链条级联失效");
            }
            finally
            {
                action.RequiresGenes = oldActionRequirements;
                environment.RequiresGenes = oldEnvironmentRequirements;
                growth.SuppressionRules = oldRules;
                GeneExpressionCache.Clear();
            }
        }

        private static void Water()
        {
            var config=new PlanetTerrainConfig{subdivisions=128,seed=78031};config.navigation.longitudeCells=32;config.navigation.latitudeCells=16;config.landforms.longitudeCells=64;config.landforms.latitudeCells=32;config.landforms.riverCatchment=5;config.landforms.basinCount=2;config.erosion.hydraulicIterations=5;
            config.landforms.version=3;var old=PlanetTerrainGenerator.Generate(config,.5f,.5f);config.landforms.version=4;var current=PlanetTerrainGenerator.Generate(config,.5f,.5f);
            Check(old.Hydrology.Downstream.SequenceEqual(current.Hydrology.Downstream),"加深河床不改变既有汇流拓扑");
            var before=new List<float>();var after=new List<float>();
            for(int i=0;i<current.Hydrology.River.Length;i++)if(current.Hydrology.River[i]){var dir=current.Hydrology.Channel(i);float a=(old.Hydrology.Water(dir)-PlanetTerrainSampler.SampleHeight(old,dir))*old.HeightScale;float b=(current.Hydrology.Water(dir)-PlanetTerrainSampler.SampleHeight(current,dir))*current.HeightScale;if(a>0&&b>0){before.Add(a);after.Add(b);}}
            Check(after.Count>0&&after.Average()>before.Average()*1.4f,"最终地形插值采样的平均河深明显增加");
            File.WriteAllText(Path.Combine(Folder,"depth.txt"),"samples="+after.Count+" oldMean="+before.Average()+" newMean="+after.Average());
            VerifyWaterQueryReuse(current);
            var mesh=FreshWaterSurfaceMesher.Build(current,1,8,.025f);Check(mesh.vertexCount>0&&mesh.colors.Length==mesh.vertexCount&&mesh.colors.All(c=>float.IsFinite(c.r)&&c.r>=0)&&mesh.colors.Max(c=>c.r)>1,"河湖网格携带逐顶点真实水深而非岸线裁切掩码");Object.DestroyImmediate(mesh);
            config.landforms.version=3;var restored=PlanetTerrainGenerator.Generate(config,.5f,.5f);Check(old.FaceHeights.SelectMany(a=>a).SequenceEqual(restored.FaceHeights.SelectMany(a=>a)),"第四版生成不污染旧版地形结果");
            WorldLandformEdits.Apply(config,"landforms",1);Check(config.landforms.version==LandformSettings.CurrentVersion&&config.landforms.riverDepthMultiplier==3,"现有显式升级入口启用加深倍率");LandformSaveCompatibility.Validate(JsonUtility.ToJson(config));var round=JsonUtility.FromJson<PlanetTerrainConfig>(JsonUtility.ToJson(config));Check(round.landforms.riverDepthMultiplier==3,"新河床倍率随世界配置保存与恢复");
        }
        // ★ 修复（防Bug回归）：排除候选河段的快速路径必须逐点等价；覆盖河边、极区及经线两侧。
        private static void VerifyWaterQueryReuse(PlanetTerrainData terrain)
        {
            var hydro=terrain.Hydrology;
            var field=typeof(PlanetHydrology).GetField("_possibleRiver",BindingFlags.Instance|BindingFlags.NonPublic);
            object index=field.GetValue(hydro);
            var directions=new List<Vector3>();
            for(int y=0;y<=128;y++)
                for(int x=0;x<96;x++)
                    directions.Add(PlanetTerrainSampler.DirectionFromUV((x+.43f)/96,y/128f));
            foreach(var path in hydro.ChannelPaths)
                if(path!=null)directions.AddRange(path);
            var expected=new (float level,float coverage)[directions.Count];
            var tuning=DarwinPark.Core.SimulationOptimization.Current;
            bool previous=tuning.shoreSampleReuse;
            try
            {
                field.SetValue(hydro,null);
                for(int i=0;i<directions.Count;i++)
                    expected[i]=(hydro.SurfaceWater(directions[i],out float coverage),coverage);
                field.SetValue(hydro,index);
                for(int i=0;i<directions.Count;i++)
                {
                    var direction=directions[i];
                    float water=hydro.SurfaceWater(direction,out float coverage);
                    if(water!=expected[i].level||coverage!=expected[i].coverage)
                        throw new InvalidOperationException("快速水位与完整水位不一致 "+direction);
                    tuning.shoreSampleReuse=false;
                    bool dry=terrain.Navigation.Dry(direction);
                    tuning.shoreSampleReuse=true;
                    if(terrain.Navigation.Dry(direction)!=dry||terrain.Navigation.Dry(direction)!=dry)
                        throw new InvalidOperationException("岸线缓存改变通行判定 "+direction);
                }
                Check(true,$"{directions.Count} 个方向的快速水位和缓存通行判定与完整查询一致");
            }
            finally
            {
                field.SetValue(hydro,index);
                tuning.shoreSampleReuse=previous;
            }
        }

        private static void DeepWaterAndCirculation()
        {
            var config = new PlanetTerrainConfig{subdivisions=96,seed=78031};
            config.landforms.longitudeCells=64;config.landforms.latitudeCells=32;
            config.landforms.basinCount=8;config.navigation.longitudeCells=32;config.navigation.latitudeCells=16;
            config.landforms.version=4;
            var before=PlanetTerrainGenerator.Generate(config,.5f,.5f);
            config.landforms.version=LandformSettings.CurrentVersion;
            var after=PlanetTerrainGenerator.Generate(config,.5f,.5f);
            float OceanMean(PlanetTerrainData data)=>data.Vertices.Where(v=>v.magnitude<data.Radius+data.SeaLevel*data.HeightScale)
                .Average(v=>data.Radius+data.SeaLevel*data.HeightScale-v.magnitude);
            float oceanBefore=OceanMean(before),oceanAfter=OceanMean(after);
            Check(oceanAfter>oceanBefore*1.05f,$"同种子第五版海域平均深度增加：{oceanBefore:F2} → {oceanAfter:F2}");

            float LakeDepth(PlanetTerrainData data,int index)
            {
                var direction=data.Hydrology.Directions[index];
                return Mathf.Max(0,(data.Hydrology.LakeWater(direction)-PlanetTerrainSampler.SampleHeight(data,direction))*data.HeightScale);
            }
            var lakes=Enumerable.Range(0,after.Hydrology.Bed.Length).Where(i=>before.Hydrology.Lake[i]&&after.Hydrology.Lake[i]).ToArray();
            Check(lakes.Length>0,"同种子对照存在共同内陆湖泊采样点");
            float lakeBefore=lakes.Average(i=>LakeDepth(before,i)),lakeAfter=lakes.Average(i=>LakeDepth(after,i));
            Check(lakeAfter>lakeBefore,$"保有湖泊中的共同采样点平均深度增加：{lakeBefore:F2} → {lakeAfter:F2}");
            File.WriteAllText(Path.Combine(Folder,"basins.txt"),$"oceanBefore={oceanBefore} oceanAfter={oceanAfter} lakeSamples={lakes.Length} lakeBefore={lakeBefore} lakeAfter={lakeAfter}");

            var climateConfig=new PlanetClimateConfig();
            var climate=new PlanetClimateModel(after,climateConfig,config.seed).Generate(64,0,0);
            var circulation=climate.Circulation;
            var oceanIndex=Enumerable.Range(0,climate.Ocean.Length).First(i=>climate.Ocean[i]&&
                circulation.SurfaceCurrent(new Vector2((i%64+.5f)/64,(i/64+.5f)/64)).sqrMagnitude>.2f);
            Check(oceanIndex>=0,"存在有方向和速度的洋流");
            Check((circulation.SurfaceCurrent(new Vector2(0,.7f))-circulation.SurfaceCurrent(new Vector2(1,.7f))).sqrMagnitude<.0001f,
                "洋流跨经线接缝连续");
            float largestShift=0;
            for(int i=0;i<climate.Ocean.Length;i++)
            {
                if(climate.Ocean[i])continue;
                var uv=new Vector2((i%64+.5f)/64,(i/64+.5f)/64);
                var direction=PlanetTerrainSampler.DirectionFromUV(uv.x,uv.y);
                largestShift=Mathf.Max(largestShift,(circulation.Wind(direction,uv,climateConfig,1)-
                    circulation.Wind(direction,uv,climateConfig,-1)).magnitude);
            }
            Check(largestShift>1,"沿海夏冬风向发生明显反转");
        }

        private static Texture2D Draw(Material material,Mesh mesh,float time,string name)
        {
            var target=new RenderTexture(768,432,24);target.Create();var commands=new CommandBuffer();commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,new Color(.02f,.03f,.04f));
            var view=Matrix4x4.TRS(new Vector3(0,0,315),Quaternion.Euler(0,180,0),Vector3.one).inverse;view=Matrix4x4.Scale(new Vector3(1,1,-1))*view;
            commands.SetViewProjectionMatrices(view,GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(40,768f/432,.1f,100),true));commands.SetGlobalVector("_WorldSpaceCameraPos",new Vector4(0,0,315,1));commands.SetGlobalVector("_MainLightPosition",new Vector4(.3f,.4f,1,0));commands.SetGlobalVector("_MainLightColor",Vector4.one);commands.SetGlobalVector("_PlanetDayNight",Vector4.zero);commands.SetGlobalVector("_Time",new Vector4(time/20,time,time*2,time*3));commands.DrawMesh(mesh,Matrix4x4.identity,material,0,0);Graphics.ExecuteCommandBuffer(commands);commands.Release();
            var previous=RenderTexture.active;RenderTexture.active=target;var result=new Texture2D(768,432,TextureFormat.RGB24,false);result.ReadPixels(new Rect(0,0,768,432),0,0);result.Apply();RenderTexture.active=previous;File.WriteAllBytes(Path.Combine(Folder,name+".png"),result.EncodeToPNG());target.Release();Object.DestroyImmediate(target);return result;
        }
        private static void ShaderPreview()
        {
            var shader=Shader.Find("DarwinPark/PlanetOcean");Check(shader!=null,"共用水面着色器存在");var material=new Material(shader);material.SetFloat("_FreshWater",1);material.SetFloat("_Cull",0);material.SetFloat("_WaveStrength",.1f);material.SetFloat("_ShelfDepth",4);material.SetColor("_ShallowColor",new Color(.08f,.4f,.55f));material.SetColor("_DeepColor",new Color(.025f,.16f,.24f));
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-9,-5,300),new Vector3(9,-5,300),new Vector3(9,5,300),new Vector3(-9,5,300)};mesh.normals=Enumerable.Repeat(Vector3.forward,4).ToArray();mesh.colors=new[]{new Color(.1f,0,0),new Color(5,0,0),new Color(5,0,0),new Color(.1f,0,0)};mesh.triangles=new[]{0,1,2,0,2,3};
            var a=Draw(material,mesh,0,"water-time0");var b=Draw(material,mesh,1.5f,"water-time1");var pixels=a.GetPixels();var other=b.GetPixels();double difference=pixels.Zip(other,(x,y)=>(double)(Mathf.Abs(x.r-y.r)+Mathf.Abs(x.g-y.g)+Mathf.Abs(x.b-y.b))).Average();File.WriteAllText(Path.Combine(Folder,"shader.txt"),"pixelDifference="+difference);
            Check(!ShaderUtil.ShaderHasError(shader),"真实GPU着色器编译无错误");Check(difference>.0001,"固定镜头两时刻GPU画面变化，水纹动画生效");Check(pixels.Any(c=>c.b>.1f&&c.g>.08f&&c.r<.25f),"GPU画面包含正常水色");Object.DestroyImmediate(a);Object.DestroyImmediate(b);Object.DestroyImmediate(material);Object.DestroyImmediate(mesh);
        }
    }
}
