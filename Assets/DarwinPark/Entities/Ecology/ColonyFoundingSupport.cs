using System;
using System.Linq;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class ColonyFoundingSave {public int version;public bool collecting;public float foodIssued;}
    // ★ 新增：创业保障只有一次有限储粮；第一批工匠成年前王后临时采集，之后交回普通分工，不能无限凭空产能。
    public static class ColonyFoundingSupport
    {
        private static bool Enabled=>WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading&&!WorldAuthority.IsReplica&&EcologyDefaults.Current.enableFoundingSupport;
        public static bool Collecting(AntColonyMember queen)=>Enabled&&queen.Role==AntRole.Queen&&!queen.Young&&!queen.State.founding&&queen.State.startup?.collecting==true;
        public static bool NeedsWorker(AntColonyMember queen)
            =>Enabled&&queen.Role==AntRole.Queen&&queen.Group.Count(m=>m.Role==AntRole.Worker)<EcologyDefaults.Current.minimumWorkers;
        public static void Advance(AntColonyMember queen,float dt)
        {
            if(!Enabled||dt<=0||queen.Young||queen.Role!=AntRole.Queen||queen.State.founding)return;
            var settings=EcologyDefaults.Current;var state=queen.State.startup??=new ColonyFoundingSave();
            if(state.version==0)
            {
                // 标记先于发粮，保存/重算/重复进入不能重复领取；已有成熟大群也不能因升级获得额外储粮。
                state.version=1;
                if(queen.Group.Count()<=settings.maximumFoundingMembers&&queen.Group.All(m=>m.Role!=AntRole.Worker||m.Young)&&settings.foundingFood>0)
                {ColonyResource.Convert(queen.GroupId,queen.transform.position,settings.foundingFood,true);state.foodIssued=settings.foundingFood;}
            }
            bool needsWorkers=queen.Group.Count(m=>m.Role==AntRole.Worker&&!m.Young)<settings.minimumWorkers;
            // 工匠已到位时仍送完手中货物，不能在交接瞬间遗失/复制被搬运的实体。
            bool carrying=!string.IsNullOrEmpty(queen.State.carriedResource)||!string.IsNullOrEmpty(queen.State.carriedLiving);
            state.collecting=needsWorkers||carrying;
            if(state.collecting)AntColonyWorker.Advance(queen,dt);
        }
        public static void Validate(ColonyFoundingSave state)
        {
            if(state==null)return;
            if(state.version<0||state.version>1||!float.IsFinite(state.foodIssued)||state.foodIssued<0||state.version==0&&(state.collecting||state.foodIssued!=0))throw new System.IO.InvalidDataException("建群创业保障状态无效");
        }
    }
}
