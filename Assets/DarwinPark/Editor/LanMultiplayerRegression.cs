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
    // ★ 新增：必须由两个独立Unity进程通过实际TCP验收，覆盖恶意越权请求、角色、操控、王后RTS、作弊撤销与断线释放。
    [InitializeOnLoad] public static class LanMultiplayerRegression
    {
        [Serializable] private sealed class Fixture
        {public int port;public string queen,soldier,worker,foreign,editor,gene;public Vector3 movePoint;}
        private const string Key="DarwinPark.LanReview";private static bool _started;private static readonly List<string> Passed=new List<string>();
        private static string Role=>SessionState.GetString(Key,"");
        private static string Root
        {get{var args=System.Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-lanTestRoot");return index>=0&&index+1<args.Length?args[index+1]:Path.GetFullPath("Artifacts/LanMultiplayerReview");}}
        static LanMultiplayerRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&Role!=""&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunHost(){Begin("host");}public static void RunClient(){Begin("client");}
        private static void Begin(string role){SessionState.SetString(Key,role);Application.runInBackground=true;EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(20,20,1440,960);view.Show();EditorApplication.EnterPlaymode();}
        private static void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);Passed.Add(text);File.WriteAllText(Path.Combine(Root,Role+"-progress.txt"),string.Join("\n",Passed));Debug.Log("[LanReview] "+Role+" "+text);}
        private static IEnumerator Wait(Func<bool> predicate,string message,double timeout=60)
        {double start=EditorApplication.timeSinceStartup;while(!predicate()){if(EditorApplication.timeSinceStartup-start>timeout)throw new TimeoutException(message+" · "+LanSession.Instance.Status);yield return null;}}
        private static IEnumerator Guard()
        {
            Application.runInBackground=true;Directory.CreateDirectory(Root);string role=Role,error=null;Passed.Clear();File.WriteAllText(Path.Combine(Root,role+"-result.txt"),"RUNNING");yield return null;
            var stack=new Stack<IEnumerator>();stack.Push(role=="host"?Host():Client());
            while(stack.Count>0)
            {
                bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}
                if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            File.WriteAllText(Path.Combine(Root,role+"-result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetString(Key,"");Debug.Log("[LanReview] "+role+" "+(error==null?"PASS":"FAIL"));EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(Vector3 point,params string[] ids)
        {
            var genome=new Genome();foreach(string id in ids)genome.Add(GeneInstance.Create(id,id=="gene_population"?20:50,GeneDatabase.Get(id).Tier));var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"联机验证");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;entity.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);entity.Genes.RestoreStageState(AgeState.Adult,3,2);entity.Genes.Recalculate();
            entity.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>().enabled=false;entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;entity.GetComponent<EntityStats>().Modify("storedNutrient",100,false);return entity;
        }
        private static IEnumerator Host()
        {
            var world=Object.FindFirstObjectByType<WorldSessionManager>();var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var clock=ServiceLocator.Get<SimulationClock>();var net=LanSession.Instance;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Root,"compact-main-menu.png"));yield return null;
            planet.subdivisions=32;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(world,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=128;PlanetOreSystem.Instance.mapResolution=64;
            world.Create("局域网独立回归",78031,.5f,20,44);yield return Wait(()=>!world.Busy,"生成世界");clock.Pause();
            Vector3 home=Vector3.zero;for(int i=0;i<192;i++){float y=1-2*(i+.5f)/192,angle=i*2.399963f;var dir=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));var point=EcologyQueries.Surface(planet.transform.position+dir*(planet.radius+planet.heightScale));if((point-planet.transform.position).magnitude>planet.WaterRadius+4){home=point;break;}}
            Check(home!=Vector3.zero,"房主建立真实陆地世界");var up=EcologyQueries.Up(home);var axis=Vector3.Cross(up,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.Cross(up,Vector3.right).normalized;
            Func<float,Vector3> at=distance=>EcologyQueries.Surface(home+axis*distance);
            var queen=Spawn(at(0),"gene_growth","gene_population");var soldier=Spawn(at(5),"gene_growth","gene_population");var worker=Spawn(at(8),"gene_growth","gene_population");var foreign=Spawn(at(110),"gene_growth","gene_population");var editor=Spawn(at(150),"gene_growth","gene_locomotion","gene_nutrient_utilization");
            string group=Guid.NewGuid().ToString("N");queen.GetComponent<AntColonyMember>().Assign(AntRole.Queen,group);soldier.GetComponent<AntColonyMember>().Assign(AntRole.Soldier,group);worker.GetComponent<AntColonyMember>().Assign(AntRole.Worker,group);foreign.GetComponent<AntColonyMember>().Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));
            editor.GetComponent<Rigidbody>().isKinematic=true;editor.Genes.enabled=false;
            Check(!RtsOrderController.Supports(worker,RtsOrderKind.Attack)&&RtsOrderController.Supports(worker,RtsOrderKind.Collect)&&!RtsOrderController.Supports(queen,RtsOrderKind.Attack),"RTS扩展能力受工匠和王后基因限制");
            var victim=Spawn(at(12),"gene_growth");var attack=RtsOrderController.Ensure(soldier);Check(attack.Issue(RtsOrderKind.Attack,victim.transform.position,victim),"攻击指令使用真实可攻击目标");victim.GetComponent<EntityStats>().Modify("health",-100,false);Check(!attack.Destination(out _,out _)&&!attack.State.active,"目标死亡取消RTS攻击");victim.gameObject.SetActive(false);Object.Destroy(victim.gameObject);
            var corpse=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=group,kind=ColonyResourceKind.Corpse,energy=10,scale=Vector3.one,local=EcologyQueries.ToLocal(worker.transform.position)});
            Check(RtsOrderController.Ensure(worker).Issue(RtsOrderKind.Collect,corpse.transform.position,null,corpse)&&worker.GetComponent<AntColonyMember>().State.resourceTarget==corpse.State.id,"工匠RTS采集接入原独占搬运任务");worker.GetComponent<AntColonyMember>().Suspend();corpse.Remove();
            var origin=soldier.transform.position;net.IssueRts(new[]{EcologyIdentity.Of(soldier)},RtsOrderKind.Move,at(16));clock.Play();yield return Wait(()=>EcologyQueries.Distance(origin,soldier.transform.position)>.5f,"离线RTS真实移动",10);clock.Pause();Check(true,"单人管理员RTS实际推动实体移动");
            net.IssueRts(new[]{EcologyIdentity.Of(soldier)},RtsOrderKind.Guard,soldier.transform.position);world.SaveNow(false);var saved=WorldSaveStore.Read(WorldSaveStore.FileFor(world.Current.id));Check(saved.entities.Single(e=>e.ecology.id==EcologyIdentity.Of(soldier)).ecology.rts.active,"RTS守卫命令实际写入并读取世界文件");net.IssueRts(new[]{EcologyIdentity.Of(soldier)},RtsOrderKind.Stop,soldier.transform.position);
            Check(WorldEntityActions.SetLimit(50,false),"房主保存联机实体上限");
            Check(net.StartHost(0,false)&&net.IsHost&&net.CanManage,"房主打开真实TCP服务并保有唯一管理员权限");clock.Play();
            var fixture=new Fixture{port=net.Port,queen=EcologyIdentity.Of(queen),soldier=EcologyIdentity.Of(soldier),worker=EcologyIdentity.Of(worker),foreign=EcologyIdentity.Of(foreign),editor=EcologyIdentity.Of(editor),gene=editor.Genes.Genome.Genes[0].InstanceId,movePoint=at(-15)};File.WriteAllText(Path.Combine(Root,"fixture.json"),JsonUtility.ToJson(fixture));
            yield return Wait(()=>File.Exists(Path.Combine(Root,"basic-done")),"等待成员权限和王后RTS验证",180);
            Check(net.MemberCount==1&&net.Players.Count(p=>p.administrator)==1&&net.RejectedCommands>=3,"服务端拒绝越权请求且始终只有一名管理员");
            var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.ShowPause();float time=clock.GameTime;yield return new WaitForSecondsRealtime(.5f);Check(clock.GameTime>time,"房主打开Esc菜单不暂停其他玩家的世界");
            net.SetCheats(true);yield return Wait(()=>File.Exists(Path.Combine(Root,"cheat-done")),"等待作弊编辑验证",60);
            Check(editor.Genes.Genome.Find(fixture.gene).Value==61&&editor.Genes.Genome.HasDefinition("gene_repair"),"作弊成员的修改和添加基因实际在房主生效");Check(EntityPopulationLimit.Maximum==60&&WorldSaveStore.Read(WorldSaveStore.FileFor(world.Current.id)).gameSettings.entityLimit==60,"远端实体上限由房主实际写盘");net.SetCheats(false);
            yield return Wait(()=>File.Exists(Path.Combine(Root,"client-left")),"等待成员退出",60);yield return Wait(()=>net.MemberCount==0,"释放连接",15);
            Check(editor.GetComponent<EntityManualControl>()?.IsControlled!=true,"成员断线后服务端释放所占实体");
            world.SaveNow(false);saved=WorldSaveStore.Read(WorldSaveStore.FileFor(world.Current.id));Check(saved.entities.Single(e=>e.ecology.id==fixture.editor).genome.Find(fixture.gene).Value==61,"远端编辑由房主成对保存和读取");
            // ★ 修复（防Bug回归）：等服务端观察到释放再重连，否则新连接抢在断线断言前到达会造成假失败。
            File.WriteAllText(Path.Combine(Root,"release-verified"),"PASS");
            yield return Wait(()=>File.Exists(Path.Combine(Root,"rejoin-ready")),"等待重连",60);Check(net.MemberCount==1,"成员可重新连接同一房间");net.CloseHost();yield return Wait(()=>File.Exists(Path.Combine(Root,"room-closed")),"验证房主关闭通知",30);Check(!net.Online&&!WorldAuthority.IsReplica&&WorldAuthority.CanEdit,"关闭局域网恢复单人权限");
            Check(net.StartHost(0,false),"房间关闭后可再次开放");fixture.port=net.Port;File.WriteAllText(Path.Combine(Root,"reload-fixture.json"),JsonUtility.ToJson(fixture));
            yield return Wait(()=>File.Exists(Path.Combine(Root,"drop-loading")),"等待客户端加载中断测试",60);net.CloseHost();yield return Wait(()=>File.Exists(Path.Combine(Root,"loading-aborted")),"取消断线加载",30);Check(true,"加载期间关闭房间也不遗留连接");
        }
        private static IEnumerator Client()
        {
            PlanetClimateMap.Instance.mapResolution=128;PlanetOreSystem.Instance.mapResolution=64;yield return Wait(()=>File.Exists(Path.Combine(Root,"fixture.json")),"等待房间信息",180);var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Root,"fixture.json")));var net=LanSession.Instance;var world=Object.FindFirstObjectByType<WorldSessionManager>();
            net.Join("127.0.0.1",fixture.port,"取消的旧连接");net.LeaveToMenu();Check(!net.Online&&!WorldAuthority.IsReplica,"连接发起后可立即取消并释放权限");
            net.Join("127.0.0.1",fixture.port,"真实客户端成员");yield return Wait(()=>net.IsClient&&WorldRuntimeGate.Ready&&!world.Busy&&net.Players.Count==2,"实际TCP连接并加载房主世界",180);
            Check(EntityPopulationLimit.Maximum==50,"成员通过世界快照获得同一存档上限");
            Check(WorldAuthority.IsReplica&&!WorldAuthority.CanEdit&&!net.IsHost&&net.Players.Count(p=>p.administrator)==1,"成员接收到世界且不能声明为管理员");
            Check(!Object.FindFirstObjectByType<SimulationPauseHotkey>().HandlePauseKey(KeyCode.P,true,false),"普通成员不能暂停所有人的世界");
            net.RequestEntity(fixture.queen);yield return Wait(()=>net.OwnedEntityId==fixture.queen&&EntityViewController.Instance.Target!=null,"选择王后",20);
            var editor=EcologyIdentity.Find(fixture.editor);var queen=EcologyIdentity.Find(fixture.queen);var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(editor);yield return null;yield return null;
            var panel=ServiceLocator.Get<UIRoot>().MainCanvas.transform.Find("GenePanel");Check(panel.GetComponentsInChildren<InputField>().All(f=>!f.interactable),"普通成员基因和名称输入均只读");
            Check(!LanEditActions.SetGene(editor,fixture.gene,61)&&!LanEditActions.AddGene(editor,"gene_repair")&&!EntityPlacementUtility.Spawn(null,queen.transform.position)&&!RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject).StartBatch(null,10,true),"普通成员不能通过公开编辑与生成入口绕过权限");
            net.SendCommand(new LanCommand{kind="gene",entity=fixture.editor,gene=fixture.gene,value=99});yield return Wait(()=>net.Status.Contains("权限"),"服务端拒绝伪造编辑",10);Check(editor.Genes.Genome.Find(fixture.gene).Value==50,"伪造基因修改报文未改变权威世界");
            int beforeManagement=EntityRegistry.All.Count;net.SendCommand(new LanCommand{kind="worldSettings",count=1});net.SendCommand(new LanCommand{kind="clearEntities"});yield return new WaitForSecondsRealtime(.5f);Check(EntityPopulationLimit.Maximum==50&&EntityRegistry.All.Count==beforeManagement&&EcologyQueries.Alive(queen),"伪造上限和一键清除报文被服务端拒绝");
            net.SendCommand(new LanCommand{kind="spawn",point=queen.transform.position});yield return new WaitForSecondsRealtime(.4f);net.SendCommand(new LanCommand{kind="cheats",enabled=true});yield return new WaitForSecondsRealtime(.4f);Check(!net.Cheats,"成员不能自行打开作弊模式");
            net.RequestEntity(fixture.editor);yield return Wait(()=>net.Status.Contains("进入游戏"),"拒绝中途切换实体",10);Check(net.OwnedEntityId==fixture.queen,"普通成员不能中途换取其他实体");
            net.SetRtsMode(true);Check(net.CanUseRts&&WorldAuthority.RtsMode,"普通成员操控王后时可进入RTS");
            var camera=Camera.main;var up=EcologyQueries.Up(queen.transform.position);var position=queen.transform.position+up*70;var axis=Vector3.Cross(up,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.right;camera.GetComponent<OrbitCamera>().RestoreView(position,Quaternion.LookRotation(queen.transform.position-position,axis),Vector3.Distance(position,EcologyQueries.Planet.transform.position));yield return null;
            var rts=Object.FindFirstObjectByType<RtsSelectionInput>();rts.SelectRectangle(Vector2.zero,new Vector2(Screen.width,Screen.height));Check(rts.Selected.Count>0&&rts.Selected.All(e=>e.GetComponent<AntColonyMember>()?.GroupId==queen.GetComponent<AntColonyMember>().GroupId),"实际框选只选中王后自己的种群");
            net.IssueRts(new[]{fixture.foreign},RtsOrderKind.Move,fixture.movePoint);yield return Wait(()=>net.Status.Contains("外群"),"服务端拒绝指挥外群",10);Check(EcologyIdentity.Find(fixture.foreign).GetComponent<RtsOrderController>()?.State.active!=true,"伪造外群RTS命令被拒绝");
            var soldier=EcologyIdentity.Find(fixture.soldier);var origin=soldier.transform.position;net.IssueRts(new[]{fixture.soldier},RtsOrderKind.Move,fixture.movePoint);yield return Wait(()=>EcologyQueries.Distance(origin,soldier.transform.position)>.5f,"远端王后RTS实际移动",15);Check(true,"客户端王后指令经服务端驱动本群移动并同步回来");net.IssueRts(new[]{fixture.soldier},RtsOrderKind.Stop,soldier.transform.position);
            net.SetRtsMode(false);EntityViewController.Instance.EnterView(queen);var possession=EntityViewController.Instance.GetComponent<EntityPossessionInput>();possession.enabled=false;var manual=queen.GetComponent<EntityManualControl>();origin=queen.transform.position;var direction=Vector3.ProjectOnPlane(Vector3.forward,EcologyQueries.Up(origin)).normalized;manual.SetInput(direction,false);
            yield return Wait(()=>Vector3.Dot(queen.transform.position-origin,direction)>.3f,"成员输入驱动服务端实体",15);manual.SetInput(Vector3.zero,false);Check(queen.GetComponent<Rigidbody>().isKinematic,"客户端不模拟物理但实际操控结果由房主同步");possession.enabled=true;EntityViewController.Instance.ExitView();EntityViewController.Instance.EnterView(queen);
            File.WriteAllText(Path.Combine(Root,"basic-done"),"PASS");yield return Wait(()=>net.Cheats&&WorldAuthority.CanEdit,"房主打开作弊",30);
            Check(net.Players.Count(p=>p.administrator)==1&&!net.IsHost,"作弊开放能力而不增加第二名管理员");LanEditActions.SetGene(editor,fixture.gene,61);yield return Wait(()=>editor.Genes.Genome.Find(fixture.gene).Value==61,"作弊修改基因",15);LanEditActions.AddGene(editor,"gene_repair");yield return Wait(()=>editor.Genes.Genome.HasDefinition("gene_repair"),"作弊添加基因",15);Check(true,"作弊成员能修改值及添加基因且收到权威同步");
            var sameEditor=editor;Check(LanEditActions.Submit(new LanCommand{kind="worldSettings",count=60,running=true}),"作弊成员可请求修改存档实体上限");yield return Wait(()=>EntityPopulationLimit.Maximum==60,"实体上限轻量同步",15);Check(EcologyIdentity.Find(fixture.editor)==sameEditor,"上限同步不重新加载或重建客户端实体");
            int count=EntityRegistry.All.Count;Check(EntityPlacementUtility.Spawn(null,EcologyQueries.Surface(queen.transform.position+direction*30)),"作弊成员可以请求放置实体");yield return Wait(()=>EntityRegistry.All.Count>count,"服务端放置并同步",15);
            count=EntityRegistry.All.Count;Check(RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject).StartBatch(null,2,true),"作弊成员可以请求全星球随机生成");yield return Wait(()=>EntityRegistry.All.Count>=count+2,"房主Esc期间执行远端随机生成",15);
            Check(true,"房主打开Esc期间远端随机生成仍正常完成");
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();yield return Wait(()=>!clock.IsRunning,"作弊成员请求暂停",15);Check(true,"作弊成员时间操作由服务端同步");clock.Play();yield return Wait(()=>clock.IsRunning,"恢复联机时间",15);
            net.RequestEntity(fixture.editor);yield return Wait(()=>net.OwnedEntityId==fixture.editor,"作弊更换实体",15);File.WriteAllText(Path.Combine(Root,"cheat-done"),"PASS");yield return Wait(()=>!net.Cheats&&!WorldAuthority.CanEdit,"房主撤销作弊",30);Check(!LanEditActions.SetGene(editor,fixture.gene,90),"作弊关闭后编辑权限立即撤回");
            float food=Food(editor),energy=ColonyResource.All.Where(r=>r.State.kind==ColonyResourceKind.Energy).Sum(r=>r.State.energy);net.SendCommand(new LanCommand{kind="release",aim=direction});yield return Wait(()=>Food(editor)<food&&ColonyResource.All.Where(r=>r.State.kind==ColonyResourceKind.Energy).Sum(r=>r.State.energy)>energy,"普通成员保留自身基因Q能力",15);Check(Mathf.Abs(food-Food(editor)-(ColonyResource.All.Where(r=>r.State.kind==ColonyResourceKind.Energy).Sum(r=>r.State.energy)-energy))<.02f,"普通成员的Q能力仍由服务端实际扣账且能量守恒");
            string file=WorldSaveStore.FileFor(world.Current.id);bool existed=File.Exists(file);world.SaveNow();Check(existed==File.Exists(file),"客户端保存入口不写入管理员世界存档");
            net.LeaveToMenu();Check(!WorldRuntimeGate.Ready&&world.Current==null&&!net.Online,"退出完整清理客户端世界与连接");File.WriteAllText(Path.Combine(Root,"client-left"),"PASS");
            yield return Wait(()=>File.Exists(Path.Combine(Root,"release-verified")),"等待房主确认连接释放",30);
            net.Join("127.0.0.1",fixture.port,"重连成员");yield return Wait(()=>net.IsClient&&WorldRuntimeGate.Ready&&!world.Busy,"重连房间",60);File.WriteAllText(Path.Combine(Root,"rejoin-ready"),"PASS");
            yield return Wait(()=>!net.Online&&!WorldRuntimeGate.Ready,"房主关闭房间",30);Check(world.Current==null&&EntityRegistry.All.Count==0,"房主关闭房间后客户端自动清场返回主界面");File.WriteAllText(Path.Combine(Root,"room-closed"),"PASS");
            yield return Wait(()=>File.Exists(Path.Combine(Root,"reload-fixture.json")),"等待再次开放",30);fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Root,"reload-fixture.json")));net.Join("127.0.0.1",fixture.port,"加载中断成员");
            yield return Wait(()=>world.Busy&&WorldRuntimeGate.Loading,"等待开始加载",30);File.WriteAllText(Path.Combine(Root,"drop-loading"),"PASS");
            yield return Wait(()=>!net.Online&&!world.Busy,"断线停止加载",30);yield return new WaitForSecondsRealtime(.5f);Check(world.Current==null&&!WorldRuntimeGate.Ready&&EntityRegistry.All.Count==0,"加载中断不继续变成本地世界或写盘");File.WriteAllText(Path.Combine(Root,"loading-aborted"),"PASS");
        }
        private static float Food(DarwinEntity entity){var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);return food+reserve;}
    }
}
