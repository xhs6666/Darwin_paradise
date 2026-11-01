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
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：复现“深色球无法右键”，验证真正的尸体、无碰撞拾取、遮挡、旧新快照及活体切换。
    [InitializeOnLoad] public static class CorpseInspectionRegression
    {
        private const string Key="DarwinPark.CorpseInspection";private static bool _started;private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/CorpseInspectionReview");
        static CorpseInspectionRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);Passed.Add(text);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;Passed.Clear();var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);Debug.Log("[CorpseInspection] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(Vector3 point,string name)
        {
            var genome=new Genome();genome.Add(GeneInstance.Create("gene_growth",50,0));var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,name);var e=EntityRegistry.All.Single(x=>!before.Contains(x));e.Genes.mutationChance=0;e.Genes.enabled=false;e.GetComponent<EntityLifeCycle>().RestoreProgress(0);e.Genes.RestoreStageState(AgeState.Infant,1,1);e.GetComponent<Rigidbody>().isKinematic=true;e.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>().enabled=false;e.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;return e;
        }
        private static IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;}
        private static IEnumerator Run()
        {
            yield return null;yield return null;WorldRuntimeGate.Ready=true;typeof(GameMainMenu).GetMethod("Resume",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(Object.FindFirstObjectByType<GameMainMenu>(),null);ServiceLocator.Get<SimulationClock>().Pause();
            foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);ColonyResource.ClearWorld();
            var root=ServiceLocator.Get<UIRoot>();var control=Object.FindFirstObjectByType<SimulationControlPanel>();var stats=root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page1/MapEntityStatistics").GetComponent<MapEntityStatisticsUI>();
            Check(stats.gameObject.activeInHierarchy,"实体总数及设置/清除面板在第一页");var page=typeof(SimulationControlPanel).GetMethod("SwitchPage",BindingFlags.NonPublic|BindingFlags.Instance);page.Invoke(control,new object[]{1});Check(!stats.gameObject.activeInHierarchy,"第二页不重复显示实体总数");page.Invoke(control,new object[]{0});
            var origin=new Vector3(0,350,0);var victim=Spawn(origin,"被士兵击败的实体");victim.GetComponent<EntityStats>().Modify("size",.75f,false);victim.GetComponent<EntityStats>().Modify("health",-100,false);string group=Guid.NewGuid().ToString("N");var corpse=ColonyResource.LeaveCorpse(victim,group);Object.DestroyImmediate(victim.gameObject);
            var live=Spawn(origin+Vector3.right*3,"正常活体");yield return null;Physics.SyncTransforms();
            Check(corpse.GetComponent<DarwinEntity>()==null&&EntityRegistry.All.Count==1,"深色物体确认为尸体资源，不冒充活体或增加实体总数");Check(corpse.GetComponents<Collider>().Length==0&&corpse.GetComponents<Rigidbody>().Length==0,"拾取修复不新增物理碰撞体或刚体");
            Check(corpse.transform.Find("CorpseStatusMark")?.GetComponent<MeshRenderer>()!=null&&corpse.GetComponents<Light>().Length==0,"尸体增加可见叉号，不增加点光源");
            var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();var ray=new Ray(origin+Vector3.back*10,Vector3.forward);
            Check(inspector.InspectAt(ray)&&WorldResourceInspector.Instance?.Current==corpse,"原右键入口可选中没有碰撞体的尸体");
            var panel=root.MainCanvas.transform.Find("WorldResourcePanel");Check(panel.gameObject.activeSelf&&panel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("已死亡"))&&panel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("被士兵击败的实体")&&t.text.Contains("1.75")),"属性面板明确显示死亡状态、生前名称和体型");
            stats.RefreshNow();Check(stats.Total==1&&panel.GetComponentsInChildren<InputField>().Length==0,"尸体属性只读，存活统计排除尸体");
            var camera=Camera.main;camera.GetComponent<OrbitCamera>().enabled=false;camera.transform.position=origin+new Vector3(2,3,-8);camera.transform.LookAt(origin+Vector3.right);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="验证地面";ground.transform.position=origin+Vector3.down*1.2f;ground.transform.localScale=new Vector3(30,1,30);yield return Capture("corpse-properties");
            Check(inspector.InspectAt(new Ray(live.transform.position+Vector3.back*10,Vector3.forward))&&inspector.CurrentEntity==live&&WorldResourceInspector.Instance.Current==null,"右键活体仍打开原基因属性面板并关闭尸体面板");
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=origin+Vector3.back*3;blocker.transform.localScale=new Vector3(2,2,.2f);Physics.SyncTransforms();Check(!inspector.InspectAt(ray)&&WorldResourceInspector.Instance.Current==null,"尸体拾取遵守地形及实物遮挡，不透墙");Object.DestroyImmediate(blocker);
            corpse.transform.localScale=new Vector3(2,1,3);Check(WorldInspectablePicker.Pick(ray,100,out _,out var scaled)&&scaled==corpse,"非均匀缩放尸体仍可准确拾取");
            var energy=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=group,kind=ColonyResourceKind.Energy,energy=12,scale=Vector3.one,local=EcologyQueries.ToLocal(origin+Vector3.right*6)});energy.transform.rotation=Quaternion.Euler(20,30,10);
            Check(inspector.InspectAt(new Ray(energy.transform.position+Vector3.back*10,Vector3.forward))&&WorldResourceInspector.Instance.Current==energy,"旋转的能量块也可查看只读能量信息");
            var outside=new Ray(energy.transform.TransformPoint(new Vector3(.7f,0,-10)),energy.transform.TransformDirection(Vector3.forward));WorldInspectablePicker.Pick(outside,100,out _,out var miss);Check(miss!=energy,"旋转方块外侧射线不因包围盒误命中");
            var moved=origin+new Vector3(0,4,5);corpse.CarryTo(moved);Check(inspector.InspectAt(new Ray(moved+Vector3.back*10,Vector3.forward))&&WorldResourceInspector.Instance.Current==corpse,"搬运后立即按新位置拾取");Check(Vector3.Dot(corpse.transform.Find("CorpseStatusMark").up,EcologyQueries.Up(moved))>.999f,"搬运后叉号沿星球法线对齐");
            var saved=corpse.Capture();string path=Path.Combine(Folder,"corpse.json");File.WriteAllText(path,JsonUtility.ToJson(saved));corpse.Remove();yield return null;Check(WorldResourceInspector.Instance.Current==null&&!panel.gameObject.activeSelf,"尸体被运走/移除后属性面板自动关闭");
            var restored=ColonyResource.Create(JsonUtility.FromJson<ColonyResourceSave>(File.ReadAllText(path)));Check(restored.State.sourceName=="被士兵击败的实体"&&Mathf.Abs(restored.State.sourceSize-1.75f)<.0001f,"尸体来源属性实际写盘读取并恢复");
            var legacy=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=group,kind=ColonyResourceKind.Corpse,energy=3,scale=Vector3.one,local=EcologyQueries.ToLocal(origin+Vector3.left*3)});WorldResourceInspector.Instance.Show(legacy);Check(panel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("旧存档未记录")),"旧存档尸体可查看，不捏造缺失的生前属性");
            Check(legacy.transform.Find("CorpseStatusMark").GetComponent<MeshFilter>().sharedMesh==restored.transform.Find("CorpseStatusMark").GetComponent<MeshFilter>().sharedMesh,"所有尸体叉号复用同一网格");
            WorldAuthority.CanEdit=false;WorldResourceInspector.Instance.Show(restored);Check(panel.gameObject.activeSelf&&panel.GetComponentsInChildren<InputField>().Length==0,"普通联机成员同样可读尸体属性");WorldAuthority.CanEdit=true;
            var state=ColonyWorldState.Capture();state.resources[0].sourceSize=float.NaN;bool rejected=false;try{ColonyWorldState.Validate(state);}catch(InvalidDataException){rejected=true;}Check(rejected,"损坏来源体型在恢复世界前被拒绝");
            WorldRuntimeGate.Ready=false;yield return null;Check(WorldResourceInspector.Instance.Current==null&&!panel.gameObject.activeSelf,"离开世界关闭资源面板并释放选择引用");
        }
    }
}
