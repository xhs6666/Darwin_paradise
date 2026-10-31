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
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：独立测试世界外的实体，验证标签授权、饱食边界、捕猎感知、配置改阈值及存档快照。
    public static class EntityStateRegression
    {
        private static readonly List<DarwinEntity> Created=new List<DarwinEntity>();
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/EntityStateReview");
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);Passed.Add(message);}
        private static DarwinEntity Spawn(Vector3 point,string gene,float value)
        {
            var genome=new Genome();genome.Add(GeneInstance.Create(gene,value,GeneDatabase.Get(gene).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"状态回归");
            var entity=EntityRegistry.All.Single(e=>!before.Contains(e));entity.Genes.mutationChance=0;Created.Add(entity);return entity;
        }
        private static void Food(DarwinEntity entity,float value){entity.GetComponent<EntityStats>().Modify("nutrient",-100,false);entity.GetComponent<EntityStats>().Modify("nutrient",value,false);}
        [MenuItem("Tools/Darwin Park/Validate Entity States %&F11")]
        public static void Validate()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();
            if(!Application.isPlaying||session==null||session.Current!=null||session.Busy)throw new InvalidOperationException("请从未加载世界的启动菜单运行");
            Directory.CreateDirectory(Folder);Passed.Clear();session.StartCoroutine(Guard(Run()));
        }
        private static IEnumerator Guard(IEnumerator operation)
        {
            try
            {
                while(true)
                {
                    bool next;object current=null;
                    try{next=operation.MoveNext();if(next)current=operation.Current;}
                    catch(Exception ex){File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL: "+ex+"\n"+string.Join("\n",Passed));Debug.LogException(ex);yield break;}
                    if(!next)break;yield return current;
                }
                File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS\n"+string.Join("\n",Passed));Debug.Log("[EntityStateReview] PASS: "+Passed.Count+" checks");
            }
            finally
            {
                foreach(var entity in Created)if(entity!=null){entity.gameObject.SetActive(false);Object.DestroyImmediate(entity.gameObject);}Created.Clear();
                Object.FindFirstObjectByType<GeneInspectorUI>()?.HideEntityInfo();
                EntityStateRegistry.Clear();GeneRuntimeProfile.Clear();GeneDatabase.Reload();GeneDatabase.EnsureLoaded();
                AntColonySettings.Clear();
            }
        }
        private static IEnumerator Run()
        {
            yield return null;ServiceLocator.Get<SimulationClock>().Pause();
            // ★ 适配：此回归专测可配置的原状态注册表；默认的受击反击策略由蚁群专项覆盖。
            var legacyPolicy=AntColonySettings.Parse(AntColonySettings.CurrentJson);legacyPolicy.patrolRetaliationOnly=false;AntColonySettings.UseSnapshot(JsonUtility.ToJson(legacyPolicy));
            var catalog=GeneTreeStorage.ReadCatalog();GeneDatabase.Reload();GeneDatabase.EnsureLoaded();
            var registry=EntityStateRegistry.Current;string originalJson=EntityStateRegistry.CurrentJson;
            var patrol=registry.states.Single(s=>s.Behavior==EntityStateBehavior.TerritoryPatrol);
            var hunt=registry.states.Single(s=>s.Behavior==EntityStateBehavior.Hunt);
            Check(registry.Get(registry.defaultStateId)!=null&&registry.states.Count>=3,"默认、巡逻、捕猎状态在注册表中存在");
            var actor=Spawn(new Vector3(0,320,0),"gene_territory",20);Food(actor,6);
            var state=actor.GetComponent<EntityStateController>();var territory=actor.GetComponent<TerritoryBehavior>();territory.Advance(.1f);
            // ★ 适配：此项验证状态条件，显式置于巡逻窗口；巡逻/休息时序由新专项回归覆盖。
            PatrolRhythm.Ensure(actor.gameObject).Restore(new PatrolRhythmSave{active=100});
            Check(state!=null&&state.Available.Any(s=>s.id==patrol.id)&&state.Available.Any(s=>s.id==hunt.id),"有效基因标签安装组件并授予可迁移状态");
            state.Advance(.1f);Check(state.Current.id==patrol.id,"饱食度 6 且无捕猎者进入巡逻");
            Check(EcologyMotion.Directive(actor.Genes,out _,out var target,out _)&&target==null,"巡逻状态产生实际巡逻意图");
            Food(actor,5);state.Advance(.1f);Check(state.Current.id==hunt.id,"饱食度等于 5 不满足严格大于条件");
            Food(actor,5.001f);state.Advance(.1f);Check(state.Current.id==patrol.id,"饱食度刚大于 5 恢复巡逻");
            var hunter=Spawn(new Vector3(1,320,0),"gene_locomotion",75);state.Advance(.1f);
            Check(state.Current.id==hunt.id&&state.DetectedTarget==hunter,"巡逻发现行动基因捕猎分支时切到实体捕猎");
            Check(actor.Genes.Evaluation.Genes.Single().State.StateId=="patrol"&&actor.Genes.Genome.Genes.Single().Value==20,"实体切换不改写领地基因巡逻分支或基因值");
            Check(EcologyMotion.Directive(actor.Genes,out _,out target,out _)&&target==hunter,"捕猎状态通过实际行动器锁定发现的捕猎者");
            hunter.GetComponent<EntityStats>().TryGet("health",out float hp);actor.Genes.AdvanceSimulation(.1f);hunter.GetComponent<EntityStats>().TryGet("health",out float after);
            Check(after<hp,"切换为捕猎后真实近身攻击生效");
            var hunterGene=hunter.Genes.Genome.Genes.Single();hunterGene.Value=25;hunter.Genes.Recalculate();state.Advance(.1f);
            Check(state.Current.id==patrol.id,"周围只有行动基因食草分支时仍巡逻");
            hunterGene.Value=75;hunter.Genes.Recalculate();hunter.transform.position=new Vector3(150,320,0);state.Advance(.1f);Check(state.Current.id==patrol.id,"感知范围外的捕猎者不触发状态");
            hunter.transform.position=new Vector3(1,320,0);hunterGene.Enabled=false;hunter.Genes.Recalculate();state.Advance(.1f);Check(state.Current.id==patrol.id,"失效捕猎基因不触发状态");
            hunterGene.Enabled=true;hunter.Genes.Recalculate();hunter.GetComponent<EntityStats>().Modify("health",-1000,false);state.Advance(.1f);Check(state.Current.id==patrol.id,"死亡捕猎者不触发状态");
            hunter.gameObject.SetActive(false);Food(actor,4);state.Advance(0);Check(state.Current.id==patrol.id,"模拟暂停不推进状态切换");
            state.Advance(.1f);Check(state.Current.id==hunt.id,"饥饿时退出巡逻而非持续巡逻饿死");
            var actorGene=actor.Genes.Genome.Genes.Single();actorGene.Enabled=false;actor.Genes.Recalculate();Check(state.Current.id==registry.defaultStateId&&!state.Available.Any(s=>s.id==patrol.id),"基因禁用后撤销巡逻授权");actorGene.Enabled=true;actor.Genes.Recalculate();
            Food(actor,8);var custom=EntityStateRegistry.Parse(originalJson);
            foreach(var condition in custom.states.SelectMany(s=>s.entryAny.Concat(s.transitions.SelectMany(t=>t.any))).SelectMany(r=>r.all).Where(c=>c.kind=="stat"))condition.value=9;
            string customJson=JsonUtility.ToJson(custom);EntityStateRegistry.UseSnapshot(customJson);state.Advance(.1f);
            Check(state.Current.Behavior==EntityStateBehavior.Hunt,"只修改 JSON 阈值为 9 即改变行为，运行逻辑没有写死 5");
            var form=new GeneValueForm();form.Load(catalog["gene_territory"]);Check(JsonUtility.FromJson<GeneDefinitionJson>(form.Export()).entityStateTags.Contains(patrol.id),"工具表单导出保留实体状态标签");
            var legacy=JsonUtility.FromJson<GeneDefinitionJson>(catalog["gene_territory"]);legacy.entityStateTagVersion=0;legacy.entityStateTags.Clear();legacy.attributeCurves[0].maxValue=123;
            var upgraded=JsonUtility.FromJson<GeneDefinitionJson>(GeneEntityStateTags.Upgrade(JsonUtility.ToJson(legacy)));
            Check(upgraded.entityStateTags.Contains(patrol.id)&&upgraded.attributeCurves[0].maxValue==123,"旧 mods/旧世界只补标签且保留自定义数值");
            legacy.entityStateTagVersion=1;Check(JsonUtility.FromJson<GeneDefinitionJson>(GeneEntityStateTags.Upgrade(JsonUtility.ToJson(legacy))).entityStateTags.Count==0,"显式空标签不会被旧档迁移重新补回");
            bool rejected=false;legacy.entityStateTags.Add("unregistered_state");try{GeneEntityStateTags.Validate(legacy,custom);}catch(InvalidDataException){rejected=true;}Check(rejected,"未注册标签明确拒绝");
            var invalid=EntityStateRegistry.Parse(customJson);invalid.states[0].behavior="UnregisteredBehavior";rejected=false;try{EntityStateRegistry.Parse(JsonUtility.ToJson(invalid));}catch(InvalidDataException){rejected=true;}Check(rejected,"未注册行为类型明确拒绝");
            var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(actor);
            Check(EntityStatePresentation.Current(actor)==hunt.displayName&&inspector.GetComponentsInChildren<Text>().Any(t=>t.name=="EntityStateTags"&&t.text.Contains(patrol.displayName)),"实体面板分别展示当前活动与基因实体状态标签");
            var data=new WorldSaveData{id=Guid.NewGuid().ToString("N"),name="实体状态回归",terrainParameters=JsonUtility.ToJson(Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>()),environmentParameters=JsonUtility.ToJson(ServiceLocator.Get<DarwinPark.Environment.EnvironmentSystem>()),ore=new float[1],oreResolution=1,
                definitions=catalog.Select(d=>new SavedGeneDefinition{id=d.Key,json=d.Value}).ToList(),tree=GeneTreeStorage.Load(),entityStateRegistry=customJson,entities=new List<EntitySaveState>{EntitySaveState.Capture(actor)}};
            string file=Path.Combine(Folder,"roundtrip.json");WorldSaveStore.Write(data,file);var read=WorldSaveStore.Read(file);
            Check(read.entityStateRegistry==customJson&&read.entities[0].ecology.entityStateId==hunt.id,"世界规则快照与实体活动状态成对写盘读取");
            actor.gameObject.SetActive(false);var restored=Spawn(read.entities[0].position,"gene_territory",20);read.entities[0].Restore(restored);
            Check(restored.GetComponent<EntityStateController>().Current.id==hunt.id,"实体读档立即保留捕猎状态");
            string exported=Path.Combine(Folder,Guid.NewGuid().ToString("N"));GeneTreeStorage.SaveTo(exported,data.tree,catalog);
            Check(File.Exists(Path.Combine(exported,"entity_states.json"))&&EntityStateRegistry.Parse(File.ReadAllText(Path.Combine(exported,"entity_states.json"))).states.Single(s=>s.id==patrol.id).entryAny[0].all[0].value==9,"工具导出基因和注册表保持相同阈值");
            EntityStateRegistry.UseSnapshot(originalJson);restored.GetComponent<EntityStateController>().Advance(.1f);Check(restored.GetComponent<EntityStateController>().Current.id==patrol.id,"阈值恢复后按当前数据重新决策");
            read.entityStateRegistry=null;read.entities[0].ecology.entityStateId=null;WorldSaveStore.Write(read,Path.Combine(Folder,"legacy.json"));Check(WorldSaveStore.Read(Path.Combine(Folder,"legacy.json")).entities.Count==1,"旧档没有实体状态字段仍能读取");
            var extended=EntityStateRegistry.Parse(originalJson);extended.states.Add(new EntityStateDefinition{id="rest_for_test",displayName="测试休息",behavior="None",priority=100});EntityStateRegistry.UseSnapshot(JsonUtility.ToJson(extended));
            var def=restored.Genes.Evaluation.Genes.Single().Definition;var tags=new List<string>(def.EntityStateTags);def.EntityStateTags.Add("rest_for_test");EntityStateController.Rebind(restored.Genes);restored.GetComponent<EntityStateController>().Restore(extended.defaultStateId);restored.GetComponent<EntityStateController>().Advance(.1f);
            Check(restored.GetComponent<EntityStateController>().Current.id=="rest_for_test","注册新 ID 并赋标签即可扩展现有行为类型，无需新增状态分支代码");def.EntityStateTags=tags;
        }
    }
}
