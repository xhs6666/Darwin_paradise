using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
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
    // ★ 新增：真实离群建群、磁盘存档、图表口径、第二页入口及门控回归，不以界面文字代替谱系和持久化。
    [InitializeOnLoad] public static class PopulationArchiveRegression
    {
        private const string Key="DarwinPark.PopulationArchiveReview";private static bool _started;
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/PopulationArchiveReview");
        static PopulationArchiveRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);Passed.Add(text);File.WriteAllText(Path.Combine(Folder,"progress.txt"),string.Join("\n",Passed));}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);Debug.Log("[PopulationArchive] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(Vector3 point,string group,AntRole role,AgeState age=AgeState.Adult)
        {
            var genome=new Genome();foreach(var def in new[]{("gene_growth",50f),("gene_population",20f)})genome.Add(GeneInstance.Create(def.Item1,def.Item2,GeneDatabase.Get(def.Item1).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"档案验证实体");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));entity.Genes.mutationChance=0;entity.GetComponent<EntityLifeCycle>().RestoreProgress(age==AgeState.Infant?0:age==AgeState.Adult?LifeCycleSettings.Load().adultProgress:LifeCycleSettings.Load().elderProgress);entity.Genes.RestoreStageState(age,3,2);entity.Genes.Recalculate();
            foreach(var component in entity.GetComponents<MonoBehaviour>())if(component is EntityGeneController||component is DarwinPark.Entities.Vitals.EntityVitals||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component is TerritoryBehavior||component.GetType().Name=="EntityAIController")component.enabled=false;
            entity.GetComponent<Rigidbody>().isKinematic=true;AntColonyMember.Ensure(entity.GetComponent<PopulationBehavior>()).Assign(role,group);entity.DisplayName=role==AntRole.Queen?"山谷蚁群":"成员 "+role;return entity;
        }
        private static IEnumerator Capture(string file){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,file+".png"));yield return null;yield return null;}
        private static IEnumerator Run()
        {
            // ★ 修复（防Bug回归）：日期必须与底栏日历一致，整日/月/年切换不按小数天数四舍五入。
            Check(PopulationWorkbench.Day(6818)=="第 1 年 11 月 17 日","累计681.8天显示为游戏年月日");
            Check(PopulationWorkbench.Day(0)=="第 0 年 1 月 1 日"&&PopulationWorkbench.Day(299.9f)=="第 0 年 1 月 30 日"&&PopulationWorkbench.Day(300)=="第 0 年 2 月 1 日"&&PopulationWorkbench.Day(3650)=="第 1 年 1 月 1 日","日期起点、跨月及跨年与底栏一致");
            yield return null;var session=Object.FindFirstObjectByType<WorldSessionManager>();var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();builder.subdivisions=24;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(builder));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("种群档案与谱系验证",20261104,.5f,20,44);double start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("加载超时");yield return null;}var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);ColonyResource.ClearWorld();PopulationArchive.Clear();
            var root=ServiceLocator.Get<UIRoot>();var control=Object.FindFirstObjectByType<SimulationControlPanel>();var switchPage=typeof(SimulationControlPanel).GetMethod("SwitchPage",Private);var page1=root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page1");var page2=root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page2");
            Check(page1.Find("PopulationList")==null&&page1.GetComponentInChildren<MapPopulationListUI>(true)==null,"第一页不再包含种群列表或入口");
            switchPage.Invoke(control,new object[]{1});yield return null;yield return null;var list=page2.GetComponentInChildren<MapPopulationListUI>(true);Check(list!=null&&list.Card.gameObject.activeInHierarchy,"完整种群入口位于第二页");
            WorldAuthority.CanEdit=false;yield return null;Check(list.gameObject.activeInHierarchy&&!page2.Find("EntitySpawnPanel").gameObject.activeSelf,"普通成员第二页仍可查看种群档案");WorldAuthority.CanEdit=true;
            Vector3 home=EcologyQueries.Surface(builder.transform.position+Vector3.up*(builder.radius+builder.heightScale));string parent=Guid.NewGuid().ToString("N");var queen=Spawn(home,parent,AntRole.Queen);var infant=Spawn(home,parent,AntRole.Worker,AgeState.Infant);var elder=Spawn(home,parent,AntRole.Soldier,AgeState.Elder);
            PopulationArchive.Observe();var record=PopulationArchive.Find(parent);Check(record!=null&&record.current==3&&record.peak==3,"真实成员生成种群统计与峰值");Check(!record.originKnown&&record.events[0].text.Contains("此前历史未记录"),"已有种群首次建档不编造过去");
            var ages=PopulationDistributions.Ages(parent);Check(ages.Select(a=>a.Count).SequenceEqual(new[]{1,1,1}),"年龄阶段分布精确统计幼年成年老年");
            queen.Genes.Genome.Add(GeneInstance.Create("gene_growth",50,GeneDatabase.Get("gene_growth").Tier));queen.Genes.Recalculate();PopulationArchive.Observe();var genes=PopulationDistributions.Genes(parent,false);Check(genes.Single(g=>g.Label==GeneDatabase.Get("gene_growth").DisplayName).Count==3,"复制基因按实体去重统计携带率");Check(PopulationDistributions.Genes(parent,true).All(g=>g.Count<=3),"生效基因人数不超过群体人数");
            list.RefreshNow();Check(list.GroupCount==1&&list.MemberCount==3,"全部种群列表包含当前群体");clock.Play();list.OpenList();var workbench=PopulationWorkbench.Instance;Check(workbench!=null&&workbench.Visible&&GameInputGate.Blocked&&!clock.IsRunning,"档案界面正确暂停并屏蔽游戏输入");workbench.Select(parent,1);yield return Capture("genes-pie");
            var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,list.Card.position)},hits);
            Check(hits.Count>0&&hits[0].gameObject.name=="PopulationWorkbenchInputBlocker","详情窗口实际射线阻止点击穿透到底栏");
            typeof(PopulationWorkbench).GetField("_pie",Private).SetValue(workbench,false);yield return Capture("genes-bars");workbench.Select(parent,2);yield return Capture("ages-bars");typeof(PopulationWorkbench).GetField("_pie",Private).SetValue(workbench,true);yield return Capture("ages-pie");
            Check(PopulationWorkbench.Instance.SelectedTab==2,"同一详情支持基因和年龄扇形/柱状切换");
            workbench.Select(parent,6);Check(!workbench.ApplyDisplay("not-a-color",true),"无效颜色被拒绝");Check(workbench.ApplyDisplay("#D13F82",true),"独立显示配置可应用并实际保存");Check(ColorUtility.ToHtmlStringRGB(ColonyAppearance.ColorFor(parent))=="D13F82","范围和标记使用自定义颜色");
            PopulationRangeLayer.SetVisible(true);yield return new WaitForSecondsRealtime(.7f);var block=new MaterialPropertyBlock();builder.GetComponent<MeshRenderer>().GetPropertyBlock(block);var texture=block.GetTexture("_PopulationData") as Texture2D;Check(texture!=null&&texture.GetPixels().Any(c=>Mathf.Abs(c.r-209/255f)<.001f&&Mathf.Abs(c.g-63/255f)<.001f&&Mathf.Abs(c.b-130/255f)<.001f),"真实范围Shader收到新颜色");
            Check(workbench.ApplyDisplay("#D13F82",false),"显示配置支持隐藏单个种群");yield return new WaitForSecondsRealtime(.7f);Check(PopulationRangeLayer.ActiveCircleCount==0,"隐藏配置实际移除该群地图范围");workbench.ApplyDisplay("#D13F82",true);
            WorldAuthority.IsReplica=true;Check(!workbench.ApplyDisplay("#FFFFFF",true)&&ColorUtility.ToHtmlStringRGB(ColonyAppearance.ColorFor(parent))=="D13F82","成员副本不能越权保存颜色");WorldAuthority.IsReplica=false;workbench.Close();Check(!GameInputGate.Blocked&&clock.IsRunning,"关闭档案恢复原模拟和输入状态");clock.Pause();
            var reproductive=Spawn(home,parent,AntRole.Reproductive);PopulationArchive.Observe();var member=reproductive.GetComponent<AntColonyMember>();member.Advance(.1f);string child=member.GroupId;Check(child!=parent&&member.Role==AntRole.Queen&&PopulationArchive.Find(child)?.parentId==parent,"真实离群建群记录来源种群");Check(PopulationArchive.Find(parent).events.Any(e=>e.text.Contains("离群建立")),"来源种群留下衍生事件");PopulationArchive.Observe();Check(PopulationArchive.Find(child).originKnown,"新建衍生种群具有真实成立记录");
            var second=Spawn(home,child,AntRole.Reproductive).GetComponent<AntColonyMember>();second.Advance(.1f);string grandchild=second.GroupId;Check(PopulationArchive.Find(grandchild)?.parentId==child,"多代发展链记录准确");PopulationArchive.Observe();
            var changing=queen.GetComponent<PopulationBehavior>();changing.Advance(.1f);queen.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_population").Value=80;queen.Genes.Recalculate();changing.Advance(.1f);string wolf=changing.GroupId;
            Check(wolf!=parent&&PopulationArchive.Find(wolf)?.parentId==parent,"种群状态变化独立成群保留原来源");
            var offspring=new Genome();offspring.Add(GeneInstance.Create("gene_growth",50,GeneDatabase.Get("gene_growth").Tier));offspring.Add(GeneInstance.Create("gene_population",80,GeneDatabase.Get("gene_population").Tier));
            var beforeBirth=new HashSet<string>(PopulationArchive.All.Select(r=>r.id));EcologyReproduction.Spawn(queen.Genes,offspring,ColonyActivityRange.Boundary(new ActivityCircle("test",home,150),0));
            Check(PopulationArchive.All.Any(r=>!beforeBirth.Contains(r.id)&&r.parentId==wolf),"非蚁群后代在外独立成群也记录真实来源");PopulationArchive.Observe();
            workbench.Open(parent,4);yield return Capture("descendants");workbench.Select(parent,3);yield return Capture("history");workbench.Select(parent,5);yield return Capture("members");workbench.Select(parent,6);yield return Capture("display-settings");workbench.Close();
            var beforeSamples=PopulationArchive.Find(parent).samples.Count;clock.RestoreState(clock.GameTime+PopulationArchiveSettings.Current.sampleGameSeconds+1,1);PopulationArchive.Observe();Check(PopulationArchive.Find(parent).samples.Count>beforeSamples,"发展历史按模拟时间推进采样");
            foreach(var entity in PopulationArchive.Members(parent))Object.DestroyImmediate(entity.gameObject);PopulationArchive.Observe();list.RefreshNow();Check(PopulationArchive.Find(parent).current==0&&list.GroupCount>=3&&list.Groups.Any(g=>g.Id==parent),"灭绝种群仍在完整列表中保留");Check(PopulationDistributions.Ages(parent).Sum(v=>v.Count)>0&&PopulationDistributions.Genes(parent,false).Count>0,"灭绝后保留最后存活分布快照");
            session.SaveNow(false);var data=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(data.colony.population.groups.Single(g=>g.id==grandchild).parentId==child&&data.colony.population.groups.Single(g=>g.id==parent).color=="#D13F82","磁盘写读成对保存谱系、历史和颜色");
            var frame=LanWorldSnapshot.Frame(1,true);Check(frame.population!=null&&frame.population.groups.Single(r=>r.id==grandchild).parentId==child&&frame.population.groups.Single(r=>r.id==parent).color=="#D13F82","低频联机快照包含历史、谱系和显示配置");
            var invalid=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(data));invalid.colony.population.groups.Single(g=>g.id==parent).parentId=grandchild;bool rejected=false;try{WorldSaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"循环谱系在清场前拒绝");
            invalid=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(data));invalid.colony.population.groups[0].samples[0].time=float.NaN;rejected=false;try{WorldSaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"非有限历史采样在清场前拒绝");
            session.Open(data);start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("读档超时");yield return null;}clock.Pause();Check(PopulationArchive.Find(parent).current==0&&PopulationArchive.Find(grandchild).parentId==child&&ColorUtility.ToHtmlStringRGB(ColonyAppearance.ColorFor(parent))=="D13F82","真实重载恢复已灭绝档案、后代和颜色");
            var legacy=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(data));legacy.colony.population=null;session.Open(legacy);while(session.Busy)yield return null;clock.Pause();PopulationArchive.Observe();Check(PopulationArchive.All.All(g=>!g.originKnown&&string.IsNullOrEmpty(g.parentId)),"旧存档缺档案时兼容且不伪造历史和谱系");
            var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.ShowPause();menu.ReturnToMainMenu();yield return null;Check(!PopulationArchive.All.Any()&&!workbench.Visible,"卸载世界清理档案及独立窗口");
        }
    }
}
