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
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：真实物理步验收低速行动、体型与起跳，避免只调用基因逻辑时漏掉地面摩擦造成的停滞。
    [InitializeOnLoad]
    public static class EntityPanelMotionRegression
    {
        private const string Key="DarwinPark.EntityPanelMotionReview";
        private const string SuiteKey="DarwinPark.EntityPanelMotionReview.Suite";
        private static readonly List<string> Passed=new List<string>();
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string Folder=>Path.GetFullPath("Artifacts/EntityPanelMotionReview");
        private static bool _started;
        static EntityPanelMotionRegression()
        {
            // ★ 修复（防Bug回归）：新项目首次索引可能打断编辑器 delayCall，使用独立 update 启动自有验证，避免测试静默未执行。
            EditorApplication.update+=()=>
            {
                if(_started||!Application.isPlaying||!SessionState.GetBool(Key,false)||Object.FindFirstObjectByType<WorldSessionManager>()==null)return;
                _started=true;Validate();
            };
        }
        public static void RunBatch()
        {
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(20,20,1440,960);view.Show();EditorApplication.EnterPlaymode();
        }
        public static void RunAllBatch(){SessionState.SetBool(SuiteKey,true);RunBatch();}
        public static void ValidateSuite(){SessionState.SetBool(SuiteKey,true);Validate();}
        [MenuItem("Tools/Darwin Park/Validate Entity Panel and Motion")]
        public static void Validate()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();if(!Application.isPlaying||session==null||session.Current!=null)throw new InvalidOperationException("请从未加载世界的启动菜单运行");
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");Passed.Clear();session.StartCoroutine(Guard(Run()));
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Passed.Add(message);}
        private static IEnumerator Guard(IEnumerator run)
        {
            string error=null;
            while(true)
            {
                bool next=false;object value=null;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);}
                if(error!=null||!next)break;yield return value;
            }
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));
            Debug.Log("[EntityPanelMotionReview] "+(error==null?"PASS":"FAIL")+": "+Passed.Count+" checks");
            if(SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);}
        }
        private static DarwinEntity Spawn(Vector3 point,params (string,float)[] traits)
        {
            var genome=new Genome();foreach(var trait in traits)genome.Add(GeneInstance.Create(trait.Item1,trait.Item2,GeneDatabase.Get(trait.Item1).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"布局与行动验证");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;entity.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);entity.Genes.RestoreStageState(AgeState.Adult,3,2);entity.Genes.Recalculate();return entity;
        }
        private static void Size(DarwinEntity entity,float size)
        {var stats=entity.GetComponent<EntityStats>();stats.TryGet("size",out var current);stats.Modify("size",size-current,false);entity.Genes.Recalculate();}
        private static IEnumerator Capture(string name)
        {yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;yield return null;}
        private static IEnumerator Run()
        {
            yield return null;
            // 旧状态回归使用尚未生成世界的坐标夹具，必须在真实世界测试之前执行，避免其坐标落入新世界海面以下。
            if(SessionState.GetBool(SuiteKey,false))
            {
                foreach(var test in new (string path,Action run)[]{("Artifacts/EntityStateReview/result.txt",EntityStateRegression.Validate),("Artifacts/SocialEcologyReview/result.txt",SocialEcologyRegression.Validate)})
                {
                    WorldRuntimeGate.Ready=false;GameInputGate.Blocked=true;ServiceLocator.Get<SimulationClock>().Pause();
                    Directory.CreateDirectory(Path.GetDirectoryName(test.path));File.WriteAllText(test.path,"RUNNING");test.run();double began=EditorApplication.timeSinceStartup;
                    while(File.ReadAllText(test.path)=="RUNNING"){if(EditorApplication.timeSinceStartup-began>180)throw new TimeoutException(test.path);yield return null;}
                    string result=File.ReadAllText(test.path);Check(result.StartsWith("PASS"),"兼容回归："+test.path+"\n"+result);yield return null;
                }
            }
            var session=Object.FindFirstObjectByType<WorldSessionManager>();var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var clock=ServiceLocator.Get<SimulationClock>();
            planet.subdivisions=32;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=128;PlanetOreSystem.Instance.mapResolution=64;
            session.Create("实体面板与行动独立验证",78031,.5f,20,44);double start=EditorApplication.timeSinceStartup;
            while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException(session.Status);yield return null;}
            Check(session.Current!=null,"隔离测试世界生成成功");clock.Pause();
            Vector3 home=Vector3.zero;
            for(int i=0;i<192;i++)
            {
                float y=1-2*(i+.5f)/192,angle=i*2.399963f;var dir=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));
                var point=EcologyQueries.Surface(planet.transform.position+dir*(planet.radius+planet.heightScale));if((point-planet.transform.position).magnitude>planet.WaterRadius+4){home=point;break;}
            }
            Check(home!=Vector3.zero,"找到真实陆地上的物理验证位置");
            var actor=Spawn(home,("gene_growth",50),("gene_nutrition",50),("gene_reproduction",20),("gene_locomotion",50));
            // 关闭与行动无关的饥饿、气候及衰老，使位移验证只受真实地形和移动器影响。
            actor.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>().enabled=false;actor.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;
            var body=actor.GetComponent<Rigidbody>();var original=actor.transform.position;clock.SetTimeScale(1);clock.Play();yield return new WaitForSecondsRealtime(1.5f);clock.Pause();
            Check(EcologyQueries.Distance(original,actor.transform.position)>.2f,"1×倍率真实 Update/FixedUpdate 中行动基因可自主移动");
            var stats=actor.GetComponent<EntityStats>();Size(actor,.1f);float smallSpeed=actor.Genes.GetAttribute("moveSpeed"),smallJump=actor.Genes.GetAttribute("jumpAbility");
            Check(smallSpeed<=LocomotionSettings.Load().maximumMoveSpeed&&smallJump<=LocomotionSettings.Load().maximumJumpSpeed,"小体型移动与跳跃能力受全局上限约束");
            Size(actor,4);Check(actor.Genes.GetAttribute("moveSpeed")<smallSpeed&&actor.Genes.GetAttribute("jumpAbility")<smallJump,"大体型同时降低移动和跳跃能力");Size(actor,1);
            float baseSpeed=actor.Genes.Evaluation.Attributes["moveSpeed"],baseSense=actor.Genes.Evaluation.Attributes["senseRange"];
            actor.Genes.Genome.Add(GeneInstance.Create("gene_territory",50,1));actor.Genes.Recalculate();
            Check(Mathf.Abs(actor.Genes.Evaluation.Attributes["moveSpeed"]-baseSpeed)<.001f&&Mathf.Abs(actor.Genes.Evaluation.Attributes["senseRange"]-baseSense)<.001f,"行动与领地共存不重复增加移动和感知属性");
            Check(!GeneDatabase.Get("gene_territory").AttributeCurves.Any(c=>c.attributeId=="moveSpeed")&&GeneDatabase.Get("gene_territory").AttributeCurves.Any(c=>c.attributeId=="territory_radius"),"领地仅展示专属属性并继承共有行动能力");
            var territoryGene=actor.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_territory");territoryGene.Value=0;actor.Genes.Recalculate();
            Check(actor.Genes.Evaluation.Genes.Single(g=>g.Gene==territoryGene).AttributeContributions.Count==0,"失效基因没有本体属性贡献");
            actor.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_locomotion").Enabled=false;territoryGene.Value=50;actor.Genes.Recalculate();
            Check(actor.Genes.GetAttribute("moveSpeed")>0&&actor.Genes.GetAttribute("jumpAbility")>0,"只有领地基因时仍有继承的移动和跳跃能力");
            // 体型改变后的真实起跳速度，而非仅检查显示属性。
            var manual=actor.gameObject.AddComponent<EntityManualControl>();var jump=actor.gameObject.AddComponent<EntityJumpMotor>();manual.BeginControl(planet.transform);jump.Bind(planet.transform);
            var up=EcologyQueries.Up(home);var ground=planet.GetComponent<MeshCollider>();ground.Raycast(new Ray(home+up*10,-up),out var hit,30);
            // 夹具传送须同步插值 Transform 与刚体，并清掉上一段漫游速度，不能用旧朝向投影新跳跃。
            Size(actor,4);actor.transform.position=hit.point+up*(.5f+Physics.defaultContactOffset);body.position=actor.transform.position;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();var launchUp=EcologyQueries.Up(actor.transform.position);clock.Play();
            Check(jump.TryJump(manual.CanMove),"大体型实体可在真实地面起跳");
            float actualJump=Vector3.Dot(body.linearVelocity,launchUp),expectedJump=Mathf.Min(jump.jumpSpeed,actor.Genes.GetAttribute("jumpAbility"));
            Check(Mathf.Abs(actualJump-expectedJump)<.01f,$"实际跳跃冲量使用体型制约后的基因能力：{actualJump:F3}/{expectedJump:F3}");
            manual.Release();clock.Pause();Size(actor,1);
            var queen=Spawn(EcologyQueries.Surface(home+Vector3.Cross(up,Vector3.right).normalized*12),("gene_growth",50),("gene_population",20));queen.GetComponent<PopulationBehavior>().Advance(.1f);
            var worker=Spawn(EcologyQueries.Surface(home+Vector3.Cross(up,Vector3.right).normalized*15),("gene_growth",50),("gene_nutrition",50),("gene_reproduction",20),("gene_population",20));
            var member=worker.GetComponent<AntColonyMember>();member.Assign(AntRole.Worker,queen.GetComponent<PopulationBehavior>().GroupId);member.Advance(.1f);
            Check(!member.Population.ChallengeLeadership(out _)&&member.Population.DuelRemaining==0&&queen.GetComponent<PopulationBehavior>().IsLeader,"直接调用挑战入口也不能挑战王后或消耗决斗冷却");
            Check(EntityStatePresentation.Current(worker).EndsWith("（工匠）"),"种群分工以括号附属于实际实体状态");
            var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(worker);yield return null;Canvas.ForceUpdateCanvases();
            var panel=(GameObject)typeof(GeneInspectorUI).GetField("_panel",Private).GetValue(inspector);
            Check(panel.GetComponentsInChildren<PopulationCommandPanel>(true).Length==0,"实体详情已取消捕猎采集集结守卫及挑战按钮区");
            foreach(var block in panel.GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("Block_")))
            {
                var icon=block.Find("Row1/Color").GetComponent<Image>();Check(icon.sprite!=null&&icon.rectTransform.rect.width>=24&&block.rect.height>=70,"图标资源及基因行独立高度："+block.name);
                Check(block.Find("Row1").GetComponent<RectTransform>().rect.height>=28&&block.Find("Row2").GetComponent<RectTransform>().anchoredPosition.y<=-38,"标题与数值行不重叠："+block.name);
            }
            yield return Capture("entity-panel");
            inspector.ShowEntityInfo(actor);yield return null;Canvas.ForceUpdateCanvases();
            var evaluation=actor.Genes.Evaluation.Genes.Single(g=>g.Gene==territoryGene);
            typeof(GeneInspectorUI).GetMethod("ShowGeneTooltip",Private).Invoke(inspector,new object[]{evaluation});
            var tooltip=(GameObject)typeof(GeneInspectorUI).GetField("_tooltipPanel",Private).GetValue(inspector);
            Check(!tooltip.GetComponentInChildren<Text>().text.Contains("移动速度"),"领地提示框不会冒用整只实体的行动总属性");
            inspector.enabled=false;tooltip.transform.position=new Vector3(panel.transform.position.x+panel.GetComponent<RectTransform>().rect.width+30,panel.transform.position.y-250,0);
            yield return Capture("territory-tooltip");
            inspector.enabled=true;inspector.HideEntityInfo();
            // ★ 适配：真实第二页随机投放与储存出错规则复用已生成的独立世界，避免只验证静态配置。
            var randomLayout=LayoutRandomSpawnRegression.Verify(actor,planet,Check);while(randomLayout.MoveNext())yield return randomLayout.Current;
            // 保存并逆序读取固定角色，确认表现和实际属性在读档后相同。
            session.SaveNow(false);var saved=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));
            Check(saved.entities.Any(e=>e.ecology?.ant?.role==AntRole.Worker.ToString()),"角色和基因状态继续写入世界存档");
            if(SessionState.GetBool(SuiteKey,false))
            {
                // ★ 适配：复用已有回归，使用 update 启动的同一验证会话规避新项目索引打断 delayCall。
                var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.ShowPause();menu.ReturnToMainMenu();yield return null;
                foreach(var test in new (string path,Action run)[]{("Artifacts/AntColonyReview/result.txt",AntColonyRegression.Validate)})
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(test.path));File.WriteAllText(test.path,"RUNNING");test.run();double began=EditorApplication.timeSinceStartup;
                    while(File.ReadAllText(test.path)=="RUNNING"){if(EditorApplication.timeSinceStartup-began>180)throw new TimeoutException(test.path);yield return null;}
                    string result=File.ReadAllText(test.path);Check(result.StartsWith("PASS"),"兼容回归："+test.path+"\n"+result);yield return null;
                }
                SessionState.SetBool(SuiteKey,false);
            }
        }
    }
}
