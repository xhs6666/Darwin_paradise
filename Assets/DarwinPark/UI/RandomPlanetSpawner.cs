using System.Collections;
using DarwinPark.Core;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;

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
        public static RandomPlanetSpawner Ensure(GameObject host)=>host.GetComponent<RandomPlanetSpawner>()??host.AddComponent<RandomPlanetSpawner>();
        public bool StartBatch(Genome genome,int count,bool landOnly)
        {
            if(!WorldAuthority.CanEdit||IsBusy||GameInputGate.Blocked&&!WorldAuthority.RemoteCommand||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading)return false;
            // ★ 适配：作弊成员也由房主生成，客户端不创建无权威来源的实体。
            if(LanSession.Instance?.IsClient==true){Requested=Mathf.Clamp(count,1,maxCount);Spawned=0;IsBusy=true;Status="已请求管理员生成…";LanSession.Instance.SendCommand(new LanCommand{kind="random",genome=genome,count=Requested,landOnly=landOnly});return true;}
            var planet=FindFirstObjectByType<PlanetTerrainMeshBuilder>();var surface=planet!=null?planet.GetComponent<MeshCollider>():null;
            if(planet==null||planet.Data==null||surface==null){Status="地表尚未就绪";return false;}
            EntityViewController.Instance?.ExitView();EntityPlacementTool.Instance?.CancelPlacement();
            Requested=Mathf.Clamp(count,1,maxCount);Spawned=0;IsBusy=true;Status="随机生成中…";
            // ★ 新增：成批任务只输出开始与结束，避免数量越多日志越慢。
            Debug.Log("[生成] 全星球随机生成，数量="+Requested+"，仅陆地="+landOnly);
            _job=StartCoroutine(Generate(planet,surface,EntityPlacementUtility.CopyGenome(genome),landOnly));return true;
        }
        private IEnumerator Generate(PlanetTerrainMeshBuilder planet,MeshCollider surface,Genome genome,bool landOnly)
        {
            int attempted=0,budget=Requested*Mathf.Max(1,attemptsPerEntity),slice=0;
            while(Spawned<Requested&&attempted<budget)
            {
                if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||planet==null||surface==null){Finish("已停止：世界已切换");yield break;}
                // ★ 适配：联机房主的Esc只暂停自己的输入，不应阻止其他管理员权限成员的生成任务。
                if(GameInputGate.Blocked&&!WorldAuthority.Online){yield return null;continue;}
                attempted++;slice++;var direction=Random.onUnitSphere;float outside=surface.bounds.extents.magnitude+spawnHeight;
                if(surface.Raycast(new Ray(planet.transform.position+direction*outside,-direction),out var hit,outside*2)&&
                    (!landOnly||(hit.point-planet.transform.position).magnitude>planet.WaterRadius+landClearance)&&
                    EntityPlacementUtility.Spawn(genome,hit.point+direction*spawnHeight))Spawned++;
                Status="已生成 "+Spawned+" / "+Requested;
                if(slice>=Mathf.Max(1,attemptsPerFrame)){slice=0;yield return null;}
            }
            Finish(Spawned==Requested?"已随机生成 "+Spawned+" 个实体":"合适地表不足，已生成 "+Spawned+" / "+Requested);
        }
        private void Finish(string status){Status=status;IsBusy=false;_job=null;Debug.Log("[生成] "+status);}
        public void Cancel(){if(LanSession.Instance?.IsClient==true){if(WorldAuthority.CanEdit)LanSession.Instance.SendCommand(new LanCommand{kind="randomCancel"});return;}if(_job!=null)StopCoroutine(_job);_job=null;if(IsBusy)Finish("已取消，保留已生成的 "+Spawned+" 个实体");}
        public void ApplyRemoteProgress(bool busy,int spawned,int requested,string status){IsBusy=busy;Spawned=spawned;Requested=requested;Status=status??"";}
        private void OnDisable()=>Cancel();
    }
}
