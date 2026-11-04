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
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：用真实世界和实际存档覆盖营养守恒、寄生生命周期、外来卵、工匠运输以及展开后的下拉菜单。
    [InitializeOnLoad] public static class NutritionEcologyRegression
    {
        private const string Key="DarwinPark.NutritionEcologyReview";
        private static bool _started;
        private static readonly List<string> Passed=new List<string>();
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static string Folder=>Path.GetFullPath("Artifacts/NutritionEcologyReview");
        static NutritionEcologyRegression()
        {
            EditorApplication.update+=()=>
            {
                if(_started||!Application.isPlaying||!SessionState.GetBool(Key,false)||LanSession.Instance==null)return;
                _started=true;LanSession.Instance.StartCoroutine(Guard());
            };
        }
        public static void RunBatch()
        {
            Application.runInBackground=true;SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Passed.Add(message);}
        private static void Near(float a,float b,string message)=>Check(Mathf.Abs(a-b)<.002f,message+" ["+a+" / "+b+"]");
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");
            string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool next=false;object current=null;
                try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}
                catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}
                if(!next){stack.Pop();continue;}
                if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));
            SessionState.SetBool(Key,false);Debug.Log("[NutritionEcologyReview] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);
            EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(Vector3 point,params (string id,float value)[] definitions)
        {
            var genome=new Genome();foreach(var def in definitions)genome.Add(GeneInstance.Create(def.id,def.value,GeneDatabase.Get(def.id).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"营养回归实体");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;entity.GetComponent<EntityLifeCycle>().RestoreProgress(0);entity.Genes.RestoreStageState(AgeState.Infant,1,1);entity.Genes.Recalculate();
            foreach(var component in entity.GetComponents<MonoBehaviour>())
                if(component is EntityGeneController||component is DarwinPark.Entities.Vitals.EntityVitals||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component is TerritoryBehavior||component.GetType().Name=="EntityAIController")component.enabled=false;
            entity.GetComponent<Rigidbody>().isKinematic=true;return entity;
        }
        private static EntityStats Stats(DarwinEntity entity)=>entity.GetComponent<EntityStats>();
        private static float Food(DarwinEntity entity){Stats(entity).TryGet("nutrient",out float food);Stats(entity).TryGet("storedNutrient",out float stored);return food+stored;}
        private static void Fill(DarwinEntity entity,float food,float stored=0)
        {var stats=Stats(entity);stats.TryGet("nutrient",out float a);stats.TryGet("storedNutrient",out float b);stats.Modify("nutrient",food-a,false);stats.Modify("storedNutrient",stored-b,false);}
        private static void Clear(){foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);ColonyResource.ClearWorld();}
        private static IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;yield return null;}
        private static IEnumerator Run()
        {
            yield return null;yield return null;
            var session=Object.FindFirstObjectByType<WorldSessionManager>();var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var clock=ServiceLocator.Get<SimulationClock>();
            builder.subdivisions=24;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(builder));
            PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("营养生态专项回归",20261101,.5f,20,44);double start=EditorApplication.timeSinceStartup;
            while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("生成世界超时");yield return null;}
            Check(session.Current!=null,"真实世界初始化");clock.Pause();Clear();
            var home=EcologyQueries.Surface(builder.transform.position+Vector3.up*(builder.radius+builder.heightScale));
            Vector3 At(float x)=>EcologyQueries.Surface(home+Vector3.right*x);

            var catalog=GeneTreeStorage.ReadCatalog();var oldTree=JsonUtility.FromJson<GeneTreeDocument>(Resources.Load<TextAsset>("Genetics/default_tree").text);
            oldTree.nutritionVersion=0;oldTree.nodes.RemoveAll(n=>n.id=="gene_heterotrophy"||n.id=="gene_scavenging");oldTree.edges.RemoveAll(e=>e.to=="gene_heterotrophy"||e.to=="gene_scavenging");
            NutritionEvolutionUpgrade.Apply(oldTree,catalog);
            Check(oldTree.edges.Any(e=>e.from=="gene_nutrition"&&e.to=="gene_heterotrophy"&&e.kind=="evolution")&&oldTree.edges.Any(e=>e.from=="gene_heterotrophy"&&e.to=="gene_scavenging"&&e.kind=="error"),"旧树在基因库已包含新基因时仍补齐分支");
            int count=oldTree.edges.Count;NutritionEvolutionUpgrade.Apply(oldTree,catalog);Check(oldTree.edges.Count==count,"重复迁移不复制连线");
            oldTree.edges.RemoveAll(e=>e.to=="gene_scavenging");NutritionEvolutionUpgrade.Apply(oldTree,catalog);Check(oldTree.edges.All(e=>e.to!="gene_scavenging"),"完成迁移后保留用户主动删除的关系");
            var oldEgg=JsonUtility.FromJson<GeneDefinitionJson>(catalog["gene_parasitic"]);oldEgg.logicScriptId="ParasiticGeneLogic";oldEgg.displayName="寄生基因";catalog[oldEgg.id]=JsonUtility.ToJson(oldEgg);NutritionEvolutionUpgrade.Apply(oldTree,catalog);
            Check(JsonUtility.FromJson<GeneDefinitionJson>(catalog[oldEgg.id]).logicScriptId=="EggParasiticGeneLogic","旧寄生定义升级为卵寄生，保留ID");
            Check(GeneEvolutionRules.Allows("gene_heterotrophy","gene_scavenging","error",50)&&!GeneEvolutionRules.Allows("gene_nutrition","gene_scavenging","evolution",80),"食腐只能来自异养出错路径");
            var mutationGenome=new Genome();mutationGenome.Add(GeneInstance.Create("gene_heterotrophy",75,1));
            GeneMutator.Mutate(mutationGenome,new GeneMutationSettings{ErrorWeight=1,DuplicateWeight=0,DeletionWeight=0});Check(mutationGenome.Genes[0].DefinitionId=="gene_scavenging","真实出错变异产生食腐基因");

            var natural=Spawn(At(0),("gene_growth",50));float biomass=ColonyResource.Biomass(natural);var corpse=EntityDeathSettlement.Die(natural);
            Near(corpse.State.energy,biomass,"自然死亡保留全部营养");Check(EntityDeathSettlement.Die(natural)==null&&ColonyResource.All.Count()==1,"同帧重复死亡只留一具尸体");
            var eater=Spawn(At(1),("gene_growth",50),("gene_heterotrophy",75),("gene_locomotion",75));Fill(eater,0);
            var prey=Spawn(At(2),("gene_growth",50));float available=ColonyResource.Biomass(prey);float beforeFood=Food(eater);
            EntityCombat.Ensure(prey.gameObject).Receive(eater,1);Near(Food(eater),beforeFood,"非致死攻击不凭空产粮");
            EntityCombat.Ensure(prey.gameObject).Receive(eater,1000);float gain=Food(eater)-beforeFood;var remains=ColonyResource.All.Single(r=>r!=corpse);
            Near(gain+remains.State.energy,available,"消化收入与剩余尸体严格守恒");Check(gain>0,"消化状态击败后实际吸收营养");
            Fill(eater,10,100000);var fullPrey=Spawn(At(3),("gene_growth",50));available=ColonyResource.Biomass(fullPrey);int beforeCorpses=ColonyResource.All.Count();EntityCombat.Ensure(fullPrey.gameObject).Receive(eater,1000);
            var fullCorpse=ColonyResource.All.Last();Near(fullCorpse.State.energy,available,"满仓击败保留全部营养为尸体");Check(ColonyResource.All.Count()==beforeCorpses+1,"满仓击败也只生成一具尸体");
            Fill(eater,0);Stats(eater).SetMax("nutrient",10000);Stats(eater).SetMax("storedNutrient",10000);eater.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_heterotrophy").Value=100;eater.Genes.Recalculate();
            var allPrey=Spawn(At(4),("gene_growth",50));beforeCorpses=ColonyResource.All.Count();EntityCombat.Ensure(allPrey.gameObject).Receive(eater,1000);Check(ColonyResource.All.Count()==beforeCorpses,"完全吸收后没有零营养尸体");
            var mover=Spawn(At(5),("gene_locomotion",75));Check(!NutritionTransfer.CanHuntForFood(mover.Genes),"只有行动能力不自动获得消化食性");
            eater.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_locomotion").Value=25;eater.Genes.Recalculate();
            Check(!NutritionTransfer.FoodTargetAllowed(eater.Genes,mover)&&NutritionTransfer.FoodTargetAllowed(eater.Genes,Spawn(At(6),("gene_growth",50))),"食草倾向只选择静止猎物");
            eater.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_locomotion").Value=75;eater.Genes.Recalculate();Check(NutritionTransfer.FoodTargetAllowed(eater.Genes,mover),"捕猎倾向可选择移动猎物");

            Clear();var host=Spawn(At(0),("gene_growth",50));Stats(host).Modify("size",3,false);
            var parasite=Spawn(host.transform.position,("gene_growth",50),("gene_heterotrophy",25),("gene_locomotion",75));var feeding=parasite.GetComponent<NutritionParasite>();Fill(parasite,0);Fill(host,10);
            Check(feeding.TryAttach(host),"幼年寄生体实际附着宿主");
            Check(parasite.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled)&&parasite.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),"寄生取消渲染和碰撞");
            Check(EcologyMotion.Immobilized(parasite.Genes)&&parasite.Genes.GetAttribute("moveSpeed")==0&&parasite.Genes.GetAttribute("attack")==0,"寄生期间不能移动攻击或跳跃");
            Check(EntityStatePresentation.Current(host).EndsWith("（被寄生）"),"宿主状态以括号标记被寄生");
            host.transform.position=At(4);feeding.Advance(1);Near(Vector3.Distance(host.transform.position,parasite.transform.position),0,"寄生体只随宿主移动");Near(Food(host)+Food(parasite),10,"寄生营养转移守恒");
            feeding.Advance(1000);Stats(host).TryGet("nutrient",out float protectedFood);Stats(host).TryGet("health",out float hp);Check(protectedFood>=NutritionEcologySettings.Current.hostProtectedNutrient&&hp==100,"持续寄生保留宿主安全口粮且不直接致死");
            parasite.Genes.CurrentAge=AgeState.Adult;parasite.Genes.Recalculate();feeding.Advance(0);
            Check(feeding.Host==null&&NutritionParasite.CountOn(host)==0&&parasite.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled)&&parasite.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled),"成年自动脱离并恢复渲染碰撞");Check(NutritionTransfer.CanDigest(parasite.Genes),"成年寄生食性转为独立消化");
            parasite.Genes.CurrentAge=AgeState.Infant;parasite.Genes.Recalculate();parasite.transform.position=host.transform.position;Check(feeding.TryAttach(host),"重建寄生用于失效回归");
            parasite.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_heterotrophy").Enabled=false;parasite.Genes.Recalculate();feeding.Advance(0);Check(feeding.Host==null,"禁用基因立即退出寄生");
            parasite.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_heterotrophy").Enabled=true;parasite.Genes.Recalculate();parasite.transform.position=host.transform.position;feeding.TryAttach(host);EntityDeathSettlement.Die(host);feeding.Advance(0);Check(feeding.Host==null&&parasite.gameObject.activeSelf,"宿主死亡后寄生体独立存活并脱离");

            Clear();var workerEntity=Spawn(At(0),("gene_growth",50),("gene_population",20));workerEntity.Genes.CurrentAge=AgeState.Adult;workerEntity.Genes.Recalculate();var worker=workerEntity.GetComponent<AntColonyMember>();worker.Assign(AntRole.Worker,Guid.NewGuid().ToString("N"));Fill(workerEntity,10,100000);
            var neutral=Spawn(worker.transform.position,("gene_growth",50));var neutralCorpse=EntityDeathSettlement.Die(neutral);available=neutralCorpse.State.energy;
            var scavenger=Spawn(worker.transform.position,("gene_scavenging",75));Fill(scavenger,0);var scavenging=scavenger.GetComponent<CorpseFeeding>();beforeFood=Food(scavenger);scavenging.Advance(.1f);Near(Food(scavenger)-beforeFood+neutralCorpse.State.energy,available,"无行动食腐体在接触范围吸收且守恒");
            available=neutralCorpse.State.energy;worker.Advance(.1f);Check(worker.State.carriedResource==neutralCorpse.State.id&&neutralCorpse.HasLiveClaim,"工匠实际领取中立自然死亡尸体");
            beforeFood=Food(scavenger);scavenging.Advance(1);Near(Food(scavenger),beforeFood,"食腐不抢食已被工匠领取的尸体");
            worker.transform.position=EcologyQueries.ToWorld(worker.State.deliveryPoint);worker.Advance(.1f);var block=ColonyResource.All.Single(r=>r.State.kind==ColonyResourceKind.Nursery||r.State.kind==ColonyResourceKind.Supply);Near(block.State.energy,available,"工匠运达后按剩余营养转换能量块");
            Check(ColonyResource.All.All(r=>r.State.kind!=ColonyResourceKind.Corpse),"转化完成移除原尸体");
            var stale=EntityDeathSettlement.Die(Spawn(worker.transform.position,("gene_growth",50)));stale.State.claim=worker.Id;Check(stale.Claim(worker),"失效旧任务不会永久锁死尸体");stale.Release(worker.Id);

            // ★ 新增：运输还要经过完整Update/FixedUpdate真实行走，不能仅靠瞬移到交货点证明AI可用。
            Clear();Vector3 liveHome=home;
            for(int i=0;i<192;i++)
            {
                float y=1-2*(i+.5f)/192,angle=i*2.399963f;var dir=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));
                var point=EcologyQueries.Surface(builder.transform.position+dir*(builder.radius+builder.heightScale));
                if((point-builder.transform.position).magnitude>builder.WaterRadius+5){liveHome=point;break;}
            }
            var axis=Vector3.Cross(EcologyQueries.Up(liveHome),Vector3.forward).normalized;
            Vector3 LiveAt(float distance)=>EcologyQueries.Surface(liveHome+axis*distance);
            string liveGroup=Guid.NewGuid().ToString("N");var queenEntity=Spawn(LiveAt(0),("gene_growth",50),("gene_population",20));queenEntity.Genes.CurrentAge=AgeState.Adult;queenEntity.Genes.Recalculate();queenEntity.GetComponent<AntColonyMember>().Assign(AntRole.Queen,liveGroup);
            workerEntity=Spawn(LiveAt(5),("gene_growth",50),("gene_population",20));workerEntity.Genes.CurrentAge=AgeState.Adult;workerEntity.Genes.Recalculate();worker=workerEntity.GetComponent<AntColonyMember>();worker.Assign(AntRole.Worker,liveGroup);Fill(workerEntity,10,100000);
            var liveCorpse=EntityDeathSettlement.Die(Spawn(LiveAt(11),("gene_growth",50)));string liveCorpseId=liveCorpse.State.id;float liveEnergy=liveCorpse.State.energy;Vector3 workerStart=worker.transform.position;
            worker.Genes.enabled=true;worker.Population.enabled=true;worker.GetComponent<PlanetGravity>().enabled=true;worker.GetComponent<EntitySurfaceAnchor>().enabled=true;worker.GetComponent<Rigidbody>().isKinematic=false;
            clock.SetTimeScale(2);clock.Play();start=EditorApplication.timeSinceStartup;bool pickedUp=false;
            while(ColonyResource.Find(liveCorpseId)!=null)
            {
                pickedUp|=worker.State.carriedResource==liveCorpseId;
                if(EditorApplication.timeSinceStartup-start>35)throw new TimeoutException("工匠真实运输超时："+worker.Activity+" 距尸体="+EcologyQueries.Distance(worker.transform.position,liveCorpse.transform.position));
                yield return null;
            }
            clock.Pause();Check(pickedUp&&EcologyQueries.Distance(workerStart,worker.transform.position)>.5f,"工匠真实行走接近、拾取并搬运尸体");Near(ColonyResource.All.Sum(r=>r.State.energy),liveEnergy,"真实运输交付能量守恒");

            Clear();host=Spawn(At(0),("gene_growth",50));Stats(host).Modify("size",3,false);parasite=Spawn(host.transform.position,("gene_heterotrophy",25),("gene_growth",50));feeding=parasite.GetComponent<NutritionParasite>();feeding.TryAttach(host);
            var parent=Spawn(At(1),("gene_growth",50),("gene_parasitic",75));var offspring=new Genome();offspring.Add(GeneInstance.Create("gene_growth",72,0));
            Check(EggHostPregnancy.TryDeposit(parent.Genes,offspring),"卵寄生将外来卵放入宿主");var pregnancy=host.GetComponent<EggHostPregnancy>();Check(pregnancy!=null&&pregnancy.Count==1&&!host.Genes.IsGestating,"宿主获得独立外来孕育状态");
            Check(pregnancy.Capture()[0].genome.Genes[0].Value==72&&host.Genes.Genome.Genes[0].Value==50,"卵使用亲本后代基因，与宿主基因无关");Check(EntityStatePresentation.Current(host).Contains("卵寄生孕育"),"宿主显示特殊卵寄生孕育状态");
            offspring.Genes[0].Value=1;Check(pregnancy.Capture()[0].genome.Genes[0].Value==72,"存放外来卵时深拷贝基因，避免别名改写");
            string hostId=EcologyIdentity.Of(host),parasiteId=EcologyIdentity.Of(parasite);float remaining=pregnancy.Capture()[0].remaining;
            session.SaveNow(false);string savePath=WorldSaveStore.FileFor(session.Current.id);var data=WorldSaveStore.Read(savePath);
            Check(data.entities.Single(e=>e.ecology.id==hostId).ecology.foreignEggs.Count==1&&data.entities.Single(e=>e.ecology.id==parasiteId).ecology.nutritionParasite.host==hostId,"真实写盘读回外来卵与寄生宿主关系");
            var invalid=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(data));invalid.entities.Single(e=>e.ecology.id==hostId).ecology.foreignEggs[0].remaining=float.NaN;bool rejected=false;try{WorldSaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"坏卵计时在清场前拒绝");
            data.entities.Reverse();session.Open(data);start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("读档超时");yield return null;}clock.Pause();
            host=EcologyIdentity.Find(hostId);parasite=EcologyIdentity.Find(parasiteId);Check(parasite.GetComponent<NutritionParasite>().Host==host&&NutritionParasite.CountOn(host)==1,"逆序读档恢复隐藏寄生与宿主计数");pregnancy=host.GetComponent<EggHostPregnancy>();Near(pregnancy.Capture()[0].remaining,remaining,"读档保留外来卵孕育计时");
            int entities=EntityRegistry.All.Count;pregnancy.Advance(remaining+1);Check(pregnancy.Count==0&&EntityRegistry.All.Count==entities+1,"外来卵实际孵化并移除载荷");var child=EntityRegistry.All.Last();Check(child.Genes.Genome.Genes.Single().Value==72,"孵化后代保留外来卵基因");
            var replicaState=EcologySaveState.Capture(parasite);NutritionReplicaPresentation.Apply(child,replicaState);Check(child.GetComponentsInChildren<Renderer>().All(r=>!r.enabled)&&child.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"成员副本隐藏寄生渲染与碰撞");NutritionReplicaPresentation.Apply(child,new EcologySaveState());Check(child.GetComponentsInChildren<Renderer>().Any(r=>r.enabled)&&child.GetComponentsInChildren<Collider>().Any(c=>c.enabled),"副本解除寄生恢复渲染与碰撞");

            var display=FrameRateDisplay.Instance;Check(display!=null&&display.backgroundOpacity<=.25f,"FPS默认背景半透明");display.backgroundOpacity=0;yield return Capture("fps-transparent");display.backgroundOpacity=.18f;
            var filter=Object.FindFirstObjectByType<MapEntityGeneFilterUI>(FindObjectsInactive.Include);filter.minimumWidth=0;yield return null;var dropdown=filter.Card.Find("IncludeGenes").GetComponent<Dropdown>();dropdown.Show();yield return null;yield return null;
            var list=dropdown.transform.Find("Dropdown List");Check(list!=null,"实际展开组合筛选下拉菜单");Check(list.GetComponentsInChildren<Toggle>(true).All(t=>t.graphic==null||!t.graphic.enabled),"展开项没有默认绿色勾选色块");Check(list.GetComponentsInChildren<Scrollbar>(true).All(s=>s.GetComponent<Image>().sprite!=null&&s.handleRect.GetComponent<Image>().sprite!=null),"展开滚动条使用主题精灵，不出现白块");yield return Capture("dropdown-themed");dropdown.Hide();
        }
    }
}
