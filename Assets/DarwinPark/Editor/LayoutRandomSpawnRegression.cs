using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：验收真实第二页按钮、球面分散/落地、模板独立、取消/门控及旧演化关系迁移，不能仅检查生成数量。
    public static class LayoutRandomSpawnRegression
    {
        private static string Folder=>System.IO.Path.GetFullPath("Artifacts/LayoutRandomSpawnReview");
        public static IEnumerator Verify(DarwinEntity actor,PlanetTerrainMeshBuilder planet,Action<bool,string> check)
        {
            System.IO.Directory.CreateDirectory(Folder);var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(actor);yield return null;Canvas.ForceUpdateCanvases();
            var canvas=ServiceLocator.Get<UIRoot>().MainCanvas.transform;var panel=(RectTransform)canvas.Find("GenePanel");
            check(panel.rect.width>420&&panel.rect.height<700,"实体信息面板更宽且更矮");
            VerifyEvolution(check);
            var control=Object.FindFirstObjectByType<SimulationControlPanel>();var page=typeof(SimulationControlPanel).GetMethod("SwitchPage",BindingFlags.NonPublic|BindingFlags.Instance);
            page.Invoke(control,new object[]{1});yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var ui=Object.FindFirstObjectByType<RandomSpawnPanel>();check(ui!=null&&ui.gameObject.activeInHierarchy,"第二页实际显示随机生成栏");
            // ★ 新增：实际布局宽度及IMGUI命令栏命中回归，避免缩窄被LayoutElement覆盖和点击穿透。
            check(ui.preferredWidth==240&&ui.GetComponent<RectTransform>().rect.width<=241,"随机生成栏实际宽度缩至原来一半");
            var bar=RtsSelectionInput.CommandBarRect;check(RtsSelectionInput.IsOverCommandBar(new Vector2(bar.center.x,Screen.height-bar.center.y))&&!RtsSelectionInput.IsOverCommandBar(Vector2.zero),"RTS命令栏命中坐标正确且不吞场景输入");
            var service=RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject);var count=ui.transform.Find("Quantity").GetComponent<InputField>();var source=ui.transform.Find("Source").GetComponent<Button>();var generate=ui.transform.Find("Generate").GetComponent<Button>();
            var before=EntityRegistry.All.ToArray();count.text="16";ui.SetRecipe(new RandomSpawnRecipe{template=RandomSpawnRecipe.DefaultGenome()});generate.onClick.Invoke();double start=EditorApplication.timeSinceStartup;
            while(service.IsBusy){if(EditorApplication.timeSinceStartup-start>30)throw new TimeoutException(service.Status);yield return null;}
            var spawned=EntityRegistry.All.Where(e=>!before.Contains(e)).ToArray();check(spawned.Length==16&&service.Spawned==16,"第二页按钮精确随机生成指定数量的默认实体");
            check(spawned.Any(a=>spawned.Any(b=>Vector3.Angle(EcologyQueries.Up(a.transform.position),EcologyQueries.Up(b.transform.position))>60)),"随机实体分散到星球不同区域而非同一落点");
            check(spawned.All(e=>(e.transform.position-planet.transform.position).magnitude>planet.WaterRadius+service.landClearance),"仅陆地模式避开海面以下位置");
            var surface=planet.GetComponent<MeshCollider>();float outside=surface.bounds.extents.magnitude+20;
            check(spawned.All(e=>{var up=EcologyQueries.Up(e.transform.position);return surface.Raycast(new Ray(planet.transform.position+up*outside,-up),out var hit,outside*2)&&Vector3.Distance(hit.point,e.transform.position)>=service.spawnHeight-.2f&&Vector3.Distance(hit.point,e.transform.position)<service.spawnHeight+.5f;}),"每个实体均由真实地表射线定位并保持出生高度");
            string name="随机模板验证-"+Guid.NewGuid().ToString("N"),oldName=actor.DisplayName;actor.DisplayName=name;EntityTemplateManager.SaveTemplate(actor);actor.DisplayName=oldName;
            yield return null;var selectedTemplate=EntityTemplateManager.Templates.SingleOrDefault(t=>t.templateName==name);check(selectedTemplate!=null,"选择界面实时列出新保存的模板");
            source.onClick.Invoke();yield return null;check(RandomSpawnWorkbench.Instance.Visible,"选择实体按钮实际打开两列来源界面");RandomSpawnWorkbench.Instance.SelectTemplate(selectedTemplate);RandomSpawnWorkbench.Instance.ApplySelection();
            count.text="3";before=EntityRegistry.All.ToArray();generate.onClick.Invoke();start=EditorApplication.timeSinceStartup;
            while(service.IsBusy){if(EditorApplication.timeSinceStartup-start>30)throw new TimeoutException(service.Status);yield return null;}
            var copies=EntityRegistry.All.Where(e=>!before.Contains(e)).ToArray();var template=EntityTemplateManager.Templates.Single(t=>t.templateName==name);
            check(copies.Length==3&&copies.All(e=>template.genes.All(g=>e.Genes.Genome.Genes.Any(copy=>copy.DefinitionId==g.definitionId&&copy.Value==g.value))),"模板随机投放保留全部遗传数值");
            float initial=copies[1].Genes.Genome.Genes[0].Value;copies[0].Genes.Genome.Genes[0].Value=initial+1;copies[0].Genes.Recalculate();
            check(copies[1].Genes.Genome.Genes[0].Value==initial&&template.genes[0].value==initial&&!ReferenceEquals(copies[0].Genes.Genome,copies[1].Genes.Genome),"随机模板副本互不共享基因或改写模板");
            before=EntityRegistry.All.ToArray();GameInputGate.Blocked=true;check(!service.StartBatch(null,10,true)&&EntityRegistry.All.Count==before.Length,"覆盖菜单阻止启动随机生成");GameInputGate.Blocked=false;
            check(service.StartBatch(null,service.maxCount,true)&&!service.StartBatch(null,10,true),"生成任务运行中不叠加重复任务");
            int paused=EntityRegistry.All.Count;GameInputGate.Blocked=true;yield return null;yield return null;check(EntityRegistry.All.Count==paused,"打开覆盖菜单暂停尚未完成的生成任务");GameInputGate.Blocked=false;
            ui.transform.Find("Cancel").GetComponent<Button>().onClick.Invoke();int cancelled=EntityRegistry.All.Count;yield return null;check(!service.IsBusy&&EntityRegistry.All.Count==cancelled,"取消按钮停止剩余生成并保留已生成实体");
            float clearance=service.landClearance;int attempts=service.attemptsPerEntity;service.landClearance=planet.radius*10;service.attemptsPerEntity=1;
            before=EntityRegistry.All.ToArray();service.StartBatch(null,2,true);while(service.IsBusy)yield return null;
            check(service.Spawned==0&&EntityRegistry.All.Count==before.Length,"没有合适陆地时有界结束且不把实体放入海底");service.landClearance=clearance;service.attemptsPerEntity=attempts;
            service.StartBatch(null,service.maxCount,true);int progress=service.Spawned;page.Invoke(control,new object[]{0});yield return null;yield return null;
            check(service.Spawned>progress,"切换底栏页面后分帧任务仍正常推进");service.Cancel();page.Invoke(control,new object[]{1});yield return null;
            ui.SetRecipe(new RandomSpawnRecipe{template=RandomSpawnRecipe.DefaultGenome()});count.text="10";inspector.ShowEntityInfo(actor);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Folder,"panel-and-random-spawn.png"));yield return null;
        }
        private static void VerifyEvolution(Action<bool,string> check)
        {
            const string target="gene_nutrient_utilization",parent="gene_storage";
            check(GeneDatabase.Get(target).ParentGeneId==parent&&GeneTreeStorage.Active.nodes.Single(n=>n.id==target).error&&GeneTreeStorage.Active.edges.Any(e=>e.from==parent&&e.to==target&&e.kind=="error"),"营养利用为储存基因的菱形出错分支");
            check(GeneEvolutionRules.Allows(parent,target,"error",50)&&!GeneEvolutionRules.Allows(parent,target,"evolution",50)&&!GeneEvolutionRules.Allows("gene_nutrition",target,"error",50)&&!GeneEvolutionRules.Allows("gene_nutrition",target,"evolution",50),"工具与自然变异都拒绝错误的营养利用来源");
            var genome=new Genome();genome.Add(GeneInstance.Create(parent,50,GeneDatabase.Get(parent).Tier));GeneMutator.Mutate(genome,new GeneMutationSettings{ErrorWeight=1,DuplicateWeight=0,DeletionWeight=0,MaxErrorDelta=0});
            check(genome.Genes.Single().DefinitionId==target,"储存基因通过真实出错变异产生营养利用基因");
            var catalog=GeneTreeStorage.ReadCatalog();var definition=JsonUtility.FromJson<GeneDefinitionJson>(catalog[target]);definition.parentGeneId="gene_nutrition";definition.displayName="保留世界自定义名称";definition.attributeCurves[0].minValue=4.25f;catalog[target]=JsonUtility.ToJson(definition);
            var tree=JsonUtility.FromJson<GeneTreeDocument>(Resources.Load<TextAsset>("Genetics/default_tree").text);tree.edges.RemoveAll(e=>e.to==target);tree.edges.Add(new GeneTreeEdge{from="gene_nutrition",to=target,kind="evolution",value=42});tree.nodes.Single(n=>n.id==target).error=false;
            string legacyTree=JsonUtility.ToJson(tree),legacyDefinition=catalog[target];
            GeneTreeStorage.Validate(tree,catalog);var upgraded=JsonUtility.FromJson<GeneDefinitionJson>(catalog[target]);
            check(upgraded.parentGeneId==parent&&upgraded.displayName==definition.displayName&&upgraded.attributeCurves[0].minValue==4.25f&&tree.edges.Any(e=>e.from==parent&&e.to==target&&e.kind=="error"&&e.value==42),"旧保存关系迁移且保留自定义名称、曲线和边权重");
            GeneTreeStorage.Validate(tree,catalog);check(tree.edges.Count(e=>e.to==target)==1,"重复迁移不产生重复关系");
            var saved=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(Object.FindFirstObjectByType<WorldSessionManager>().Current));
            saved.tree=JsonUtility.FromJson<GeneTreeDocument>(legacyTree);saved.definitions.Single(d=>d.id==target).json=legacyDefinition;
            string file=System.IO.Path.Combine(Folder,"legacy-world.json");WorldSaveStore.Write(saved,file);var loaded=WorldSaveStore.Read(file);
            check(JsonUtility.FromJson<GeneDefinitionJson>(loaded.definitions.Single(d=>d.id==target).json).parentGeneId==parent&&loaded.tree.edges.Any(e=>e.from==parent&&e.to==target&&e.kind=="error"),"旧世界实际写盘重读保持父定义与出错连线一致");
        }
    }
}
