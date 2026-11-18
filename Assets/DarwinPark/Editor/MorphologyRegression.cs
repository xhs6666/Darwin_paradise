using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：仅在隔离项目创建新版本测试世界，验证实际部件、分类、物理、事务和保存重载，不修改用户旧数据。
    [InitializeOnLoad]public static partial class MorphologyRegression
    {
        private const string Key="DarwinPark.Morphology";private static bool started;private static readonly List<string> Passed=new();private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static string Folder=>Path.GetFullPath("Artifacts/MorphologyReview");
        static MorphologyRegression(){EditorApplication.update+=()=>{if(!SessionState.GetBool(Key,false))return;EditorApplication.isPaused=false;if(!started&&Application.isPlaying&&LanSession.Instance!=null){started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){if(!Application.dataPath.Replace('\\','/').Contains("/Artifacts/StandaloneServerValidation/Project/Assets"))throw new InvalidOperationException("只允许在隔离验证项目运行");SessionState.SetBool(Key,true);Application.runInBackground=true;EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");if(!Application.isBatchMode)EntityOptimizationRegression.ConfigureView();EditorApplication.EnterPlaymode();}
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Passed.Add(text);Debug.Log("[Morphology] "+text);}
        private static void Reject(Action action,string text){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(InvalidDataException){rejected=true;}Check(rejected,text);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static GeneInstance Add(Genome genome,string id,string state=null){var definition=GeneDatabase.Get(id);var gene=GeneInstance.Create(id,70,definition.Tier);if(state!=null)gene.StateId=state;genome.Add(gene);return gene;}
        private static ExpressedPart Part(EntityBodyView view,string slot)=>view.Parts.First(p=>p.slotId==slot);
        private static void Refresh(DarwinEntity entity){entity.Genes.Recalculate();entity.GetComponent<EntityBodyView>().Rebuild();}
        private static void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);target.Create();float aspect=camera.aspect;camera.aspect=960f/540;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});var active=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(960,540,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),texture.EncodeToPNG());RenderTexture.active=active;camera.aspect=aspect;target.Release();Object.Destroy(target);Object.Destroy(texture);
        }
        // ★ 新增：实际渲染同一落点有无水面及生物，防止深度采样退化成纯色遮挡。
        private static void OceanCapture(Camera camera, PlanetTerrainMeshBuilder planet, DarwinEntity fish, Vector3 up)
        {
            var ocean = planet.transform.Find("OceanSurface").GetComponent<Renderer>();
            fish.GetComponent<EntityBodyView>().Rebuild();
            var entity = fish.GetComponentInChildren<Renderer>();
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var side = Vector3.Cross(up, Vector3.forward).normalized;
            if (side.sqrMagnitude < .1f) side = Vector3.Cross(up, Vector3.right).normalized;
            camera.transform.SetPositionAndRotation(fish.transform.position + up * 5 + side * 2, Quaternion.LookRotation(-up * 5 - side * 2, side));
            try
            {
                Capture(camera,"ocean-swimmer");
                entity.enabled = false;
                Capture(camera,"ocean-without-swimmer");
                entity.enabled = true;
                ocean.enabled = false;
                Capture(camera,"ocean-without-water");
                ocean.enabled = true;
                // ★ 新增：另外留一张日照海岸图，便于核对浅滩和相邻深水的变化。
                var coast = planet.Data.Vertices.Where(v =>
                    planet.WaterRadius - v.magnitude is > 1 and < 5)
                    .OrderByDescending(v => Vector3.Dot(planet.transform.TransformDirection(v.normalized),SunSystem.LightDirection))
                    .First();
                var coastUp = planet.transform.TransformDirection(coast.normalized);
                var coastSide = Vector3.ProjectOnPlane(SunSystem.LightDirection,coastUp).normalized;
                var coastPoint = planet.transform.TransformPoint(coast);
                var coastOffset = coastUp * 27 + coastSide * 19;
                camera.transform.SetPositionAndRotation(coastPoint + coastOffset,Quaternion.LookRotation(-coastOffset,coastSide));
                Capture(camera,"ocean-coast");
            }
            finally
            {
                entity.enabled = ocean.enabled = true;
                camera.transform.SetPositionAndRotation(position,rotation);
            }
            var shown = new Texture2D(2,2);
            var reference = new Texture2D(2,2);
            try
            {
                shown.LoadImage(File.ReadAllBytes(Path.Combine(Folder,"ocean-swimmer.png")));
                foreach (string name in new[]{"ocean-without-swimmer","ocean-without-water"})
                {
                    reference.LoadImage(File.ReadAllBytes(Path.Combine(Folder,name+".png")));
                    int changed = 0;
                    for (int y=240; y<300; y++)
                    for (int x=450; x<510; x++)
                    {
                        var delta = shown.GetPixel(x,y)-reference.GetPixel(x,y);
                        if (Mathf.Abs(delta.r)+Mathf.Abs(delta.g)+Mathf.Abs(delta.b)>.035f) changed++;
                    }
                    Check(changed>30, $"海洋像素验证 {name}：中心变化 {changed} 像素");
                }
            }
            finally { Object.Destroy(shown); Object.Destroy(reference); }
        }

        private static IEnumerator Run()
        {
            var world=Object.FindFirstObjectByType<WorldSessionManager>();var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();typeof(WorldSessionManager).GetMethod("EnsureInitialized",Private).Invoke(world,null);planet.subdivisions=64;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(world,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;NaturalPopulationPace.Current.enabled=false;
            world.Create("部件系统新版本回归",77441,.5f,20,44);double timeout=EditorApplication.timeSinceStartup+180;while(world.Busy){if(EditorApplication.timeSinceStartup>timeout)throw new TimeoutException("world generation");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();PopulationMaintenance.Restore(null);GameplayTuning.Current.mutationRoundsEnabled=false;yield return null;
            Check(LanRelease.Current.contentEpoch==2&&world.Current.version==2,"新世界直接使用内容纪元2和存档格式2");
            Check(GeneDatabase.All.Count()==26&&GeneDatabase.All.Select(g=>g.Category).Distinct().Count()>=8,"26种基因按独立职责分类，无重复探测实现");
            // ★ 新增：组合条件必须在加载阶段报错，避免无效模组静默丢失部件变化。
            var catalog=GeneTreeStorage.ReadCatalog();var invalidPhoto=JsonUtility.FromJson<GeneDefinitionJson>(catalog["gene_photosynthesis"]);
            invalidPhoto.partModifiers[0].requireGene="missing_combination_gene";catalog[invalidPhoto.id]=JsonUtility.ToJson(invalidPhoto);
            Reject(()=>GeneTreeStorage.Validate(GeneTreeStorage.Active,catalog),"未注册组合条件在内容校验时明确拒绝");
            Check(GeneDatabase.All.Where(g=>g.Category==GeneCategory.Reproduction).All(g=>g.PartBindings.Count==0),"全部生殖类基因不注册器官部件");
            Check(GeneDatabase.Get("gene_locomotion").States.Select(s=>s.StateId).SequenceEqual(new[]{"swimming","amphibious"})&&!GeneDatabase.Get("gene_locomotion").AttributeCurves.Any(c=>c.attributeId=="senseRange"||c.attributeId=="attack"),"行动仅游泳/两栖，移出感知、攻击和食性");
            var land=planet.Data.Vertices.Where(v=>v.magnitude>planet.WaterRadius+4).OrderByDescending(v=>Vector3.Dot(v.normalized,SunSystem.LightDirection)).First();Vector3 up=planet.transform.TransformDirection(land.normalized),position=planet.transform.TransformPoint(land)+up;
            if(System.Environment.GetCommandLineArgs().Contains("-editing-only")){yield return FeatureChecks(planet,position);yield break;}
            var genome=GeneMutator.CreateBaseGenome();Add(genome,"gene_observation");var actor=EntitySpawnCapture.Create(genome,position,"形体测试实体");actor.GetComponent<Rigidbody>().isKinematic=true;actor.transform.rotation=Quaternion.LookRotation(Vector3.Cross(up,Vector3.right).normalized,up);yield return null;
            var body=actor.GetComponent<EntityBodyView>();Check(body!=null,"实际客户端工厂安装部件表现，非手工生成测试模型");Refresh(actor);
            Check(body.Parts.Count(p=>p.slotId.StartsWith("eye_"))==2&&body.Parts.Any(p=>p.slotId=="head"),"探测注册双眼并补齐头部结构");int count=body.PartCount;Add(genome,"gene_observation");Refresh(actor);Check(body.PartCount==count,"复制探测基因合并同一槽位，不增加眼睛数量");
            Check(body.Parts.Any(p=>p.slotId=="leaf_left")&&body.Parts.Any(p=>p.slotId=="leaf_right"),"静止阳光自养具有绿色受光部件");var photo=genome.Genes.First(g=>g.DefinitionId=="gene_photosynthesis");photo.Value=20;Refresh(actor);Color low=Part(body,"leaf_left").color;photo.Value=90;Refresh(actor);Color high=Part(body,"leaf_left").color;Check(high.g<low.g&&high.r<low.r,"有效积累效率越高，受光部件颜色越深");
            float leafBefore=Part(body,"leaf_left").scale.x;var storage=Add(genome,"gene_storage");storage.Value=90;Refresh(actor);Check(Part(body,"leaf_left").scale.x>leafBefore,"储存与光合基因共同改变受光部件形状");
            genome.BodyOverrides.Add(new PartOverride{slotId="head",scale=new Vector3(1,.5f,1)});Refresh(actor);var head=Part(body,"head");var eye=Part(body,"eye_left");var offset=Quaternion.Inverse(head.rotation)*(eye.position-head.position);float surface=Mathf.Pow(offset.x/(head.scale.x*.5f),2)+Mathf.Pow(offset.y/(head.scale.y*.5f),2)+Mathf.Pow(offset.z/(head.scale.z*.5f),2);Check(surface>1&&surface<1.6f,"扁椭球头部的眼睛仍按曲面位置贴附，不留在旧球坐标");
            var mesh=actor.GetComponent<MeshFilter>().sharedMesh;var camera=Camera.main;camera.GetComponent<OrbitCamera>().enabled=false;camera.transform.SetPositionAndRotation(position+up*1.3f+actor.transform.forward*3-actor.transform.right*2,Quaternion.LookRotation(-up*.8f-actor.transform.forward*3+actor.transform.right*2,up));Refresh(actor);mesh=actor.GetComponent<MeshFilter>().sharedMesh;
            Check(actor.GetComponentsInChildren<Renderer>().Length==1&&actor.GetComponentsInChildren<Collider>().Length==1&&actor.GetComponentsInChildren<Rigidbody>().Length==1,"多个部件只使用一个Renderer、碰撞体和刚体");Check(mesh.vertexCount>63&&mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"实际合并模型具备有效的部件几何和颜色");yield return null;Capture(camera,"photo-eyes-flat-head");
            var stable=actor.GetComponent<MeshFilter>().sharedMesh;for(int i=0;i<40;i++)Refresh(actor);Check(actor.GetComponent<MeshFilter>().sharedMesh==stable,"相同表达连续重算不重复分配网格");
            var reproduction=new Genome();foreach(var definition in GeneDatabase.All.Where(d=>d.Category==GeneCategory.Reproduction))Add(reproduction,definition.Id);Check(MorphologyExpression.Resolve(reproduction,GeneValueResolver.Resolve(reproduction)).Count==1,"纯生殖组合只有基础身体，不生成额外器官");
            var mineral=GeneMutator.CreateBaseGenome();mineral.Genes.RemoveAll(g=>g.DefinitionId=="gene_photosynthesis");Add(mineral,"gene_mineral_autotrophy");Check(MorphologyExpression.Resolve(mineral,GeneValueResolver.Resolve(mineral)).Any(p=>p.slotId=="root_left"),"静止矿石自养生成吸收根结构");Add(mineral,"gene_locomotion");var mineralParts=MorphologyExpression.Resolve(mineral,GeneValueResolver.Resolve(mineral));Check(!mineralParts.Any(p=>p.slotId.StartsWith("root_"))&&mineralParts.Any(p=>p.slotId=="exchange"),"移动矿石自养改用接触结构，组合规则移除根部");
            // ★ 新增：验证组合参数的进入和退出，移除行动不能留下紧凑形体缓存。
            float spreadLeaf=Part(body,"leaf_left").scale.x;
            var locomotion=Add(genome,"gene_locomotion");Refresh(actor);
            Check(Part(body,"leaf_left").scale.x<spreadLeaf&&body.Parts.Any(p=>p.slotId=="leaf_right"),"行动与光合共同表达收紧叶片并保留受光部件");
            genome.Remove(locomotion.InstanceId);Refresh(actor);
            Check(Mathf.Abs(Part(body,"leaf_left").scale.x-spreadLeaf)<.0001f&&!body.Parts.Any(p=>p.slotId.StartsWith("leg_")),"移除行动基因恢复展开叶片并移除四肢");
            genome.Add(locomotion);Refresh(actor);Check(body.Parts.Count(p=>p.slotId.StartsWith("leg_"))==4&&!body.Parts.Any(p=>p.slotId.StartsWith("fin_")),"两栖模式装配四肢");locomotion.Value=12;Refresh(actor);Check(actor.Genes.GetEvaluation(locomotion).State.StateId=="amphibious","修改强度不意外切换离散运动模式");
            Check(LanEditActions.SetGeneState(actor,locomotion.InstanceId,"swimming"),"模式修改通过真实权威编辑入口");Refresh(actor);Check(body.Parts.Count(p=>p.slotId.StartsWith("fin_"))==2&&!body.Parts.Any(p=>p.slotId.StartsWith("leg_")),"游泳模式替换鳍部件并移除四肢");Check(!LanEditActions.SetGeneState(actor,locomotion.InstanceId,"hunt"),"拒绝把旧食性状态当作运动模式");Check(actor.Genes.GetAttribute("moveSpeed")>0,"游泳演化方向不屏蔽已表达的陆地速度");locomotion.Value=70;Refresh(actor);
            string before=JsonUtility.ToJson(genome);var invalid=new JointMutationGroup{id="missing",genes=new[]{"gene_growth","gene_heterotrophy"},maximumDelta=8};Check(!JointGeneMutation.Apply(genome,invalid).Success&&JsonUtility.ToJson(genome)==before,"联合变异缺少成员时整组不提交");var joint=JointGeneMutation.Apply(genome,new JointMutationGroup{id="photo-storage",genes=new[]{"gene_photosynthesis","gene_storage"},maximumDelta=8,stateChangeChance=0});Check(joint.Success&&joint.Affected.Count==2,"联合变异一次提交多个真实基因实例");Refresh(actor);
            EntityTemplateManager.SaveTemplate(actor);var template=EntityTemplateManager.Templates.First(t=>t.templateName==actor.DisplayName);var copied=template.ToGenome();Check(copied.Genes.First(g=>g.DefinitionId=="gene_locomotion").StateId=="swimming"&&copied.BodyOverrides.Single().scale.y==.5f,"模板复制保留运动模式与个体形体参数");
            var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(actor);typeof(GeneInspectorUI).GetMethod("ShowAddGeneMenu",Private).Invoke(inspector,null);yield return null;var panel=ServiceLocator.Get<UIRoot>().MainCanvas.transform.Find("AddGenePanel");var category=panel.Find("Category").GetComponent<Dropdown>();int reproductive=category.options.FindIndex(o=>o.text=="生殖");category.value=reproductive;yield return null;var buttons=panel.Find("Scroll/View/Content").GetComponentsInChildren<Button>();Check(buttons.Length==8&&buttons.All(b=>GeneDatabase.Get(b.name.Substring(4)).Category==GeneCategory.Reproduction),"实际分类下拉只显示8种生殖基因");category.value=category.options.FindIndex(o=>o.text=="探测");yield return null;buttons=panel.Find("Scroll/View/Content").GetComponentsInChildren<Button>();Check(buttons.Length==1&&buttons[0].name=="Btn_gene_observation","实际探测分类不混入行动或中枢");var canvas=ServiceLocator.Get<UIRoot>().MainCanvas;var renderMode=canvas.renderMode;var canvasCamera=canvas.worldCamera;float plane=canvas.planeDistance;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.5f;category.Show();yield return null;Canvas.ForceUpdateCanvases();Capture(camera,"gene-category-panel");category.Hide();canvas.renderMode=renderMode;canvas.worldCamera=canvasCamera;canvas.planeDistance=plane;panel.gameObject.SetActive(false);
            var modeGenome=GeneMutator.CreateBaseGenome();var motionGene=Add(modeGenome,"gene_locomotion");Add(modeGenome,"gene_nervous_system");var mover=EntitySpawnCapture.Create(modeGenome,position+actor.transform.right*4,"自主行动测试");Vector3 start=mover.transform.position;clock.Play();for(int i=0;i<120;i++)yield return null;clock.Pause();Check(Vector3.ProjectOnPlane(mover.transform.position-start,up).magnitude>.05f&&mover.Genes.GetLogic(modeGenome.Genes.First(g=>g.DefinitionId=="gene_nervous_system")) is CentralGeneLogic,"真实模拟下中枢驱动具有行动基因的实体产生位移");
            // ★ 新增：真实水中物理与呼吸分别验收，不能只验证面板速度或外观。
            Vector3 seaDirection=planet.Data.Vertices.First(v=>v.magnitude<planet.WaterRadius-8).normalized;float seaRadius=PlanetCoordinate.AtLocal(planet.Data,seaDirection*planet.WaterRadius).WaterRadius;Vector3 seaUp=planet.transform.TransformDirection(seaDirection);Vector3 seaPosition=planet.transform.TransformPoint(seaDirection*(seaRadius-1));
            var aquatic=EcologyDefaults.Template("aquatic_swimmer").CreateGenome();var gills=aquatic.Genes.Single(g=>g.DefinitionId=="gene_aquatic_respiration");Check(aquatic.Genes.Any(g=>g.DefinitionId=="gene_locomotion"&&g.StateId=="swimming"),"海洋模板保留游泳演化方向和独立鳃呼吸");var fish=EntitySpawnCapture.Create(aquatic,seaPosition,"水中运动测试");var manual=fish.gameObject.AddComponent<EntityManualControl>();manual.BeginControl(planet.transform);OceanCapture(camera,planet,fish,seaUp);manual.SetInput(Vector3.Cross(seaUp,Vector3.right).normalized,false);Vector3 seaStart=fish.transform.position;
            clock.Play();for(int i=0;i<35;i++)yield return null;clock.Pause();var hazard=fish.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>();hazard.AdvanceShared(.1f);
            Check(hazard.IsSubmerged&&Vector3.ProjectOnPlane(fish.transform.position-seaStart,seaUp).magnitude>.05f,"真实游泳物理保留切向推进，不被旧下沉路径清零");Check(!hazard.IsDrowning,"独立水下呼吸基因防止窒息");
            fish.Genes.Genome.Remove(gills.InstanceId);fish.Genes.Recalculate();var fishStats=fish.GetComponent<EntityStats>();fishStats.TryGet("health",out float hp);hazard.AdvanceShared(.5f);fishStats.TryGet("health",out float afterHp);Check(hazard.IsDrowning&&afterHp<hp,"会游泳但没有呼吸部位时仍有真实溺水状态与伤害");manual.Release();
            string oldTemplate=Path.Combine(Folder,"old-template.json");string rawOld="{\"version\":1,\"templates\":[]}";File.WriteAllText(oldTemplate,rawOld);Reject(()=>EntityTemplateStore.Load(oldTemplate),"旧模板版本拒绝读取，不提供迁移入口");Check(File.ReadAllText(oldTemplate)==rawOld,"拒绝旧模板后保留原文件");
            var install=DarwinEntity.PreparePresentation;DarwinEntity.PreparePresentation=null;DarwinEntity pure=null;
            try{var factory=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("DarwinPark.GameRoot.EntityFactory")).First(t=>t!=null);pure=(DarwinEntity)factory.GetMethod("CreateFromScratch").Invoke(null,new object[]{Genome.Copy(genome),position+up*5,"纯逻辑部件测试"});}finally{DarwinEntity.PreparePresentation=install;}
            Check(pure.GetComponent<EntityBodyView>()==null&&pure.GetComponentsInChildren<Renderer>().Length==0&&MorphologyExpression.Resolve(pure.Genes.Genome,pure.Genes.Evaluation).Count>1,"无客户端适配器时仍有部件数据，但不创建显示模型");
            yield return FeatureChecks(planet,position);
            var saved=EntitySaveState.Capture(actor);world.SaveNow(false);string path=WorldSaveStore.FileFor(world.Current.id);var data=WorldSaveStore.Read(path);Check(data.entities.Any(e=>e.name==actor.DisplayName&&e.genome.BodyOverrides.Count==1&&e.genome.Genes.Any(g=>g.DefinitionId=="gene_locomotion"&&g.StateId=="swimming")),"实际世界存档保存形体参数和运动状态");Check(!string.IsNullOrEmpty(data.morphology),"世界只保存一份共享部件与装配定义快照");
            var old=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(data));old.version=1;old.contentEpoch=1;string current=world.Current.id;Reject(()=>world.Open(old),"旧存档明确拒绝，不运行迁移或先清空现场");Check(world.Current.id==current&&EntityRegistry.All.Contains(actor),"拒绝旧版本后当前世界与实体保持完整");
            world.Open(data);while(world.Busy)yield return null;yield return null;actor=EntityRegistry.All.First(e=>e.DisplayName==saved.name);Refresh(actor);Check(actor.Genes.Genome.BodyOverrides.Count==1&&actor.Genes.Genome.Genes.Any(g=>g.DefinitionId=="gene_locomotion"&&g.StateId=="swimming")&&actor.GetComponent<EntityBodyView>().Parts.Count(p=>p.slotId.StartsWith("fin_"))==2,"真实重载世界恢复个体参数和鳍部件");
            var sun=Object.FindFirstObjectByType<SunSystem>();var visibleSun=GameObject.Find("Sun_Visual_Model").GetComponent<Renderer>().sharedMaterial;Check(visibleSun.shader.name=="DarwinPark/SolarDisc"&&sun.GetComponent<Light>().color==Color.white,"太阳光盘使用独立发光材质，世界照明保持白光");
            File.WriteAllText(Path.Combine(Folder,"world-path.txt"),path);File.WriteAllText(Path.Combine(Folder,"geometry.txt"),"parts="+actor.GetComponent<EntityBodyView>().PartCount+" renderers="+actor.GetComponentsInChildren<Renderer>().Length+" vertices="+actor.GetComponent<MeshFilter>().sharedMesh.vertexCount+"\n");
        }
        public static void VerifyAfterRestart()
        {
            Directory.CreateDirectory(Folder);try{var data=WorldSaveStore.Read(File.ReadAllText(Path.Combine(Folder,"world-path.txt")));MorphologyRegistry.UseSnapshot(data.morphology);GeneRuntimeProfile.Catalog=data.definitions.ToDictionary(d=>d.id,d=>d.json);GeneRuntimeProfile.Tree=data.tree;GeneDatabase.Reload();GeneDatabase.EnsureLoaded();var actor=data.entities.First(e=>e.name=="形体测试实体");var parts=MorphologyExpression.Resolve(actor.genome,GeneValueResolver.Resolve(actor.genome));Check(actor.genome.BodyOverrides.Single().scale.y==.5f&&parts.Count(p=>p.slotId.StartsWith("fin_"))==2,"独立进程恢复形体快照、个体参数与运动部件");File.WriteAllText(Path.Combine(Folder,"restart.txt"),"PASS\n"+string.Join("\n",Passed));EditorApplication.Exit(0);}catch(Exception error){File.WriteAllText(Path.Combine(Folder,"restart.txt"),"FAIL\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
