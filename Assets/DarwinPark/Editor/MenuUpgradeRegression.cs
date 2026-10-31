using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：使用独立测试世界和隔离文件夹，验证旧档迁移、增量基因、缩放坐标及返回菜单后再次读档。
    public static class MenuUpgradeRegression
    {
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/MenuUpgradeReview");
        private static void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);if(!Passed.Contains(message))Passed.Add(message);}
        private static object Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
        [MenuItem("Tools/Darwin Park/Validate Menu Upgrades %&F9")]
        public static void Validate()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();
            if(!Application.isPlaying||session==null||session.Current!=null||session.Busy)throw new InvalidOperationException("请在未加载世界的启动菜单执行");
            Directory.CreateDirectory(Folder);Passed.Clear();session.StartCoroutine(Guard(Run()));
        }
        private static IEnumerator Guard(IEnumerator operation)
        {
            while(true)
            {
                bool next;object value=null;
                try{next=operation.MoveNext();if(next)value=operation.Current;}
                catch(Exception ex){File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL: "+ex+"\n"+string.Join("\n",Passed));Debug.LogException(ex);yield break;}
                if(!next)break;yield return value;
            }
            File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));Debug.Log("[MenuUpgradeReview] PASS: "+Passed.Count+" checks");
        }
        private static IEnumerator Run()
        {
            var view=new GeneGraphViewport();var anchor=new Vector2(180,120);view.Scroll=new Vector2(250,200);
            Vector2 point=view.ToGraph(anchor);view.SetZoom(.5f,anchor);
            Check(Vector2.Distance(point,view.ToGraph(anchor))<.001f,"滚轮缩放保留鼠标下的图坐标");
            view.SetZoom(100,Vector2.zero);Check(view.Zoom==GeneGraphViewport.MaxZoom,"放大上限");
            view.SetZoom(.0001f,Vector2.zero);Check(view.Zoom==GeneGraphViewport.MinZoom,"缩小下限");
            var all=GeneTreeStorage.ReadCatalog();var originalTree=GeneTreeStorage.Load();
            view.Fit(originalTree.nodes,new Vector2(170,65),new Vector2(800,600));
            Check(originalTree.nodes.All(n=>{var p=n.position*view.Zoom-view.Scroll;return p.x>=0&&p.y>=0&&p.x+170*view.Zoom<800&&p.y+65*view.Zoom<600;}),"适应全图包含全部节点");
            var old=all.Where(p=>JsonUtility.FromJson<GeneDefinitionJson>(p.Value).tier==0).ToDictionary(p=>p.Key,p=>p.Value);
            Check(old.Count==7,"构造旧版七基因库");
            string first=old.Keys.First(),original=old[first];var definition=JsonUtility.FromJson<GeneDefinitionJson>(original);definition.displayName="旧世界专用数值";old[first]=JsonUtility.ToJson(definition);
            string customized=old[first];var tree=new GeneTreeDocument();tree.nodes.Add(new GeneTreeNode{id=first,position=new Vector2(777,333)});
            Check(GeneCatalogExpansion.Expand(old,tree)==all.Count-7&&old.Count==all.Count,"旧库增量补齐全部新基因");
            Check(old[first]==customized&&tree.nodes.First(n=>n.id==first).position==new Vector2(777,333),"保留旧世界同名配置和节点坐标");
            Check(tree.edges.Any(e=>e.from=="gene_oviparous"&&e.to=="gene_parasitic"&&e.kind=="error")&&tree.edges.Any(e=>e.from=="gene_territory"&&e.to=="gene_population"),"补库同步新增演化关系");
            Check(GeneCatalogExpansion.Expand(old,tree)==0,"重复补库不新增重复节点或边");

            var data=new WorldSaveData{id=Guid.NewGuid().ToString("N"),name="目录迁移回归",terrainParameters="{}",environmentParameters="{}",ore=new float[1],oreResolution=1,definitions=old.Select(d=>new SavedGeneDefinition{id=d.Key,json=d.Value}).ToList(),tree=tree};
            string root=Path.Combine(Folder,Guid.NewGuid().ToString("N")),legacy=Path.Combine(root,data.id+".json"),current=Path.Combine(root,data.id,"world.json");
            WorldSaveStore.Write(data,legacy);data.gameTime=13;WorldSaveStore.Write(data,legacy);
            Check(WorldSaveStore.ListFrom(root).Single().data.gameTime==13,"旧单文件存档可发现并读取");
            WorldSaveStore.Write(data,current);WorldSaveStore.ArchiveLegacy(root,data.id);
            Check(!File.Exists(legacy)&&File.Exists(Path.Combine(root,data.id,"legacy.json"))&&File.Exists(Path.Combine(root,data.id,"legacy.json.bak")),"旧档与旧备份在新目录内保留");
            Check(WorldSaveStore.ListFrom(root).Count==1&&WorldSaveStore.ListFrom(root)[0].path==current,"每个世界只显示一条新目录存档");
            WorldSaveStore.Write(data,legacy);Check(WorldSaveStore.ListFrom(root).Count==1,"新旧同标识文件去重");
            data.gameTime=27;WorldSaveStore.Write(data,current);
            Check(WorldSaveStore.Read(current+".bak").gameTime==13&&WorldSaveStore.Read(current).gameTime==27,"新目录存档原子替换及备份往返");
            File.WriteAllText(Path.Combine(root,data.id,"future-extension.json"),"扩展文件");Check(WorldSaveStore.ListFrom(root).Count==1,"扩展文件不会误判为世界存档");
            Check(Path.GetDirectoryName(Path.GetDirectoryName(WorldSaveStore.FileFor(data.id)))==GamePaths.Saves,"默认存档按世界分目录");

            var session=Object.FindFirstObjectByType<WorldSessionManager>();var menu=Object.FindFirstObjectByType<GameMainMenu>();
            var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var clock=ServiceLocator.Get<SimulationClock>();
            // ★ 适配：测试只降低本次播放的网格和贴图分辨率，场景配置与玩家存档不被改写。
            builder.subdivisions=32;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,JsonUtility.ToJson(builder));
            PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=64;
            session.Create("菜单升级回归",78031,.5f,20,44);
            while(session.Busy){Check(!clock.IsRunning&&clock.GameTime==0,"新建加载期间不计时");yield return null;}
            Check(session.Current!=null&&WorldRuntimeGate.Ready&&!menu.Visible,"新建后进入游戏");
            var genome=new Genome();genome.Add(GeneInstance.Create("gene_growth",50,0));
            DarwinEntity.Spawner(genome,builder.transform.position+Vector3.up*(builder.WaterRadius+3),"菜单回归实体");yield return null;
            var entity=EntityRegistry.All.Single();EntityFavoritesManager.Instance.ToggleFavorite(entity);
            var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(entity);
            Check(EntityViewController.Instance.EnterView(entity),"实体操控进入成功");
            menu.ShowPause();string id=session.Current.id;float time=clock.GameTime;
            File.WriteAllText(Path.Combine(Folder,"test-world-id.txt"),id);
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"pause.png"));yield return new WaitForEndOfFrame();
            session.Current.id="invalid";bool rejected=false;try{session.ReturnToMenu(true);}catch(InvalidDataException){rejected=true;}finally{session.Current.id=id;}
            Check(rejected&&session.Current!=null&&WorldRuntimeGate.Ready&&entity.isActiveAndEnabled,"保存失败保留当前世界和实体");
            menu.ReturnToMainMenu();
            Check(session.Current==null&&!WorldRuntimeGate.Ready&&GameInputGate.Blocked&&menu.Visible&&!clock.IsRunning,"保存并返回主界面清除会话并保持菜单输入");
            Check(EntityRegistry.All.Count==0&&GeneRuntimeProfile.Catalog==null&&!EntityFavoritesManager.Instance.IsFavorite(entity),"离开世界清理实体、收藏和基因快照");
            Check(EntityViewController.Instance.Target==null&&!Camera.main.GetComponent<OrbitCamera>().EntityViewActive,"返回主界面退出实体操控");
            Check(!((GameObject)Field(inspector,"_panel")).activeSelf,"离开世界关闭实体面板");
            string path=WorldSaveStore.FileFor(id);data=WorldSaveStore.Read(path);
            Check(data.entities.Count==1&&data.gameTime==time&&data.running,"返回主界面前保留实体、时钟与运行状态");
            data.definitions=data.definitions.Where(d=>JsonUtility.FromJson<GeneDefinitionJson>(d.json).tier==0).ToList();
            data.tree=new GeneTreeDocument();session.Open(data);
            while(session.Busy){Check(!clock.IsRunning&&clock.GameTime==time,"旧档加载不推进时钟");yield return null;}
            Check(session.Current!=null&&!menu.Visible&&GeneDatabase.All.Count()==all.Count,"主界面重新进入旧七基因世界时补齐基因库");
            entity=EntityRegistry.All.Single();inspector.ShowEntityInfo(entity);
            typeof(GeneInspectorUI).GetMethod("ShowAddGeneMenu",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(inspector,null);yield return null;
            var panel=(GameObject)Field(inspector,"_addGenePanel");var list=(Transform)Field(inspector,"_addGeneListParent");
            Check(list.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==all.Count,"添加页为全部基因创建可点击条目");
            Check(panel.GetComponentInChildren<ScrollRect>().verticalScrollbar!=null&&panel.GetComponentInChildren<ScrollRect>().content.rect.height>panel.GetComponentInChildren<ScrollRect>().viewport.rect.height,"添加列表可滚动且有滚动条");
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"add-genes.png"));yield return new WaitForEndOfFrame();
            list.Find("Btn_gene_population").GetComponent<Button>().onClick.Invoke();
            Check(entity.Genes.Genome.Genes.Any(g=>g.DefinitionId=="gene_population"),"实际添加按钮能加入种群基因");
            session.SaveNow();Check(WorldSaveStore.Read(path).definitions.Count==all.Count,"补齐后的基因快照再次保存完整");
            clock.Pause();menu.ShowPause();menu.ReturnToMainMenu();yield return null;
            Check(session.Current==null&&EntityRegistry.All.Count==0,"再次返回主界面无残留");
            Check(!WorldSaveStore.Read(path).running,"暂停中的世界返回主界面保留暂停状态");
        }
    }
}
