using System.Collections;
using System;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Genetics;
using DarwinPark.Entities;
using DarwinPark.Planet;
using UnityEngine;
using Random=UnityEngine.Random;

namespace DarwinPark.UI
{
    // ★ 新增：按球面方向分散实体，仅射线命中真实地表才生成；分帧限制工作量避免一键生成时卡住。
    public sealed class RandomPlanetSpawner : MonoBehaviour
    {
        public int maxCount=200,attemptsPerEntity=96,attemptsPerFrame=32;
        public float spawnHeight=2,landClearance=.5f;
        public bool IsBusy {get;private set;}
        public int Spawned {get;private set;}
        public int Requested {get;private set;}
        public string Status {get;private set;}="选择数量，一键分散到星球各处";
        private Coroutine _job;
        private float _nextMaintenance;private int _nextRule;private string _automaticRule;
        public static RandomPlanetSpawner Ensure(GameObject host)=>host.GetComponent<RandomPlanetSpawner>()??host.AddComponent<RandomPlanetSpawner>();
        public bool StartBatch(Genome genome,int count,bool landOnly)
            =>StartRecipe(new RandomSpawnRecipe{template=RandomSpawnRecipe.Clone(genome)},count,landOnly);
        // ★ 新增：每次实际出生重新生成受约束基因组，不能把一次随机结果复制整批。
        public bool StartRecipe(RandomSpawnRecipe source,int count,bool landOnly)=>StartInternal(source,count,landOnly,null);
        private bool StartInternal(RandomSpawnRecipe source,int count,bool landOnly,PopulationMaintenanceRule automatic)
        {
            if(IsBusy||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||automatic==null&&(!WorldAuthority.CanEdit||GameInputGate.Blocked&&!WorldAuthority.RemoteCommand))return false;
            try{if(source==null)return false;source.Validate(GeneDatabase.All.Select(d=>d.Id).ToArray());}catch(Exception ex){Status=ex.Message;return false;}
            if(!WorldAuthority.IsReplica&&!EntityPopulationLimit.CanSpawn){Status="已达到存档实体上限";return false;}
            // ★ 适配：作弊成员也由房主生成，客户端不创建无权威来源的实体。
            if(LanSession.Instance?.IsClient==true){if(automatic!=null)return false;Requested=Mathf.Clamp(count,1,maxCount);Spawned=0;IsBusy=true;Status="已请求管理员生成…";LanSession.Instance.SendCommand(new LanCommand{kind="randomRecipe",recipe=source,count=Requested,landOnly=landOnly});return true;}
            var planet=FindFirstObjectByType<PlanetTerrainMeshBuilder>();var surface=planet!=null?planet.GetComponent<MeshCollider>():null;
            if(planet==null||planet.Data==null||surface==null){Status="地表尚未就绪";return false;}
            EntityViewController.Instance?.ExitView();EntityPlacementTool.Instance?.CancelPlacement();
            Requested=Mathf.Clamp(count,1,maxCount);Spawned=0;IsBusy=true;Status="随机生成中…";
            _automaticRule=automatic?.id;
            // ★ 新增：成批任务只输出开始与结束，避免数量越多日志越慢。
            Debug.Log("[生成] 全星球随机生成，数量="+Requested+"，仅陆地="+landOnly);
            var snapshot=JsonUtility.FromJson<RandomSpawnRecipe>(JsonUtility.ToJson(source));_job=StartCoroutine(Generate(planet,surface,snapshot,landOnly));return true;
        }
        private IEnumerator Generate(PlanetTerrainMeshBuilder planet,MeshCollider surface,RandomSpawnRecipe source,bool landOnly)
        {
            int attempted=0,budget=Requested*Mathf.Max(1,attemptsPerEntity),slice=0;
            while(Spawned<Requested&&attempted<budget)
            {
                if(!EntityPopulationLimit.CanSpawn){Finish("已达到存档实体上限，已生成 "+Spawned+" 个实体");yield break;}
                if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||planet==null||surface==null){Finish("已停止：世界已切换");yield break;}
                if(_automaticRule!=null)
                {
                    var rule=PopulationMaintenance.Rules.FirstOrDefault(r=>r.id==_automaticRule);
                    if(rule==null||!rule.enabled||PopulationMaintenance.Needed(rule)<=0){Finish("维持目标已满足或停止");yield break;}
                    if(ServiceLocator.Get<SimulationClock>().IsRunning==false){yield return null;continue;}
                }
                // ★ 适配：联机房主的Esc只暂停自己的输入，不应阻止其他管理员权限成员的生成任务。
                if(GameInputGate.Blocked&&!WorldAuthority.Online){yield return null;continue;}
                attempted++;slice++;var direction=Random.onUnitSphere;float outside=surface.bounds.extents.magnitude+spawnHeight;
                if(surface.Raycast(new Ray(planet.transform.position+direction*outside,-direction),out var hit,outside*2)&&
                    (!landOnly||(hit.point-planet.transform.position).magnitude>planet.WaterRadius+landClearance)&&
                    Spawn(source.CreateGenome(),hit.point+direction*spawnHeight))Spawned++;
                Status="已生成 "+Spawned+" / "+Requested;
                if(slice>=Mathf.Max(1,attemptsPerFrame)){slice=0;yield return null;}
            }
            Finish(Spawned==Requested?"已随机生成 "+Spawned+" 个实体":"合适地表不足，已生成 "+Spawned+" / "+Requested);
        }
        private bool Spawn(Genome genome,Vector3 point)
        {
            // ★ 适配：已由管理员授权的世界规则由房主推进，不借客户端生成接口执行第二份模拟。
            if(WorldAuthority.IsReplica||_automaticRule==null&&!WorldAuthority.CanEdit||DarwinEntity.Spawner==null||!EntityPopulationLimit.CanSpawn)return false;
            DarwinEntity created=null;void Registered(DarwinEntity entity)=>created=entity;EntityRegistry.OnRegistered+=Registered;
            try{DarwinEntity.Spawner(genome,point,"");}finally{EntityRegistry.OnRegistered-=Registered;}
            if(created==null)return false;
            // ★ 修复（防Bug回归）：这些是按明确来源投放的初始实体，出生当帧不再重抽一次变异破坏“完全模板/必有必无”；成年变异仍正常。
            created.GetComponent<EntityLifeCycle>().RestoreProgress(0);created.Genes.RestoreStageState(AgeState.Infant,1,1);created.Genes.Recalculate();return true;
        }
        private void Update()
        {
            if(WorldAuthority.IsReplica||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||IsBusy)return;
            var clock=ServiceLocator.Get<SimulationClock>();if(!clock.IsRunning||clock.GameTime<_nextMaintenance)return;_nextMaintenance=clock.GameTime+PopulationMaintenanceSettings.Current.pollGameSeconds;
            int count=PopulationMaintenance.Rules.Count;
            for(int i=0;i<count;i++)
            {
                var rule=PopulationMaintenance.Rules[(_nextRule+i)%count];if(!PopulationMaintenance.RequiresRefill(rule))continue;int needed=PopulationMaintenance.Needed(rule);
                _nextRule=(_nextRule+i+1)%count;StartInternal(rule.source,needed,rule.landOnly,rule);break;
            }
        }
        private void Finish(string status){Status=status;IsBusy=false;_job=null;_automaticRule=null;Debug.Log("[生成] "+status);}
        public void Cancel(){if(LanSession.Instance?.IsClient==true){if(WorldAuthority.CanEdit)LanSession.Instance.SendCommand(new LanCommand{kind="randomCancel"});return;}if(_job!=null)StopCoroutine(_job);_job=null;if(IsBusy)Finish("已取消，保留已生成的 "+Spawned+" 个实体");}
        public void ApplyRemoteProgress(bool busy,int spawned,int requested,string status){IsBusy=busy;Spawned=spawned;Requested=requested;Status=status??"";}
        public void ResetWorld(){if(_job!=null)StopCoroutine(_job);_job=null;IsBusy=false;Spawned=Requested=0;_automaticRule=null;_nextMaintenance=0;_nextRule=0;}
        private void OnDisable()=>Cancel();
    }
}
