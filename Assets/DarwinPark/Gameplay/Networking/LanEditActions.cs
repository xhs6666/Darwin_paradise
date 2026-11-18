using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Environment;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：所有创造操作共用一个入口；客户端只发送意图，服务端再次核验权限后执行，不信任客户端实体状态。
    public static class LanEditActions
    {
        public static bool Submit(LanCommand command)
        {if(!WorldAuthority.CanEdit)return false;var session=LanSession.Instance;return session!=null?session.SubmitEdit(command):LanRuntimeServices.Apply(command,null);}
        public static bool SetGene(DarwinEntity entity,string instance,float value)=>Submit(new LanCommand{kind="gene",entity=EcologyIdentity.Of(entity),gene=instance,value=value});
        public static bool AddGene(DarwinEntity entity,string definition)=>Submit(new LanCommand{kind="addGene",entity=EcologyIdentity.Of(entity),gene=definition});
        // ★ 新增：离散状态由权威端校验，不能借改强度绕过状态注册表。
        public static bool SetGeneState(DarwinEntity entity,string instance,string state)=>Submit(new LanCommand{kind="geneState",entity=EcologyIdentity.Of(entity),gene=instance,name=state});
        public static bool Rename(DarwinEntity entity,string name)=>Submit(new LanCommand{kind="rename",entity=EcologyIdentity.Of(entity),name=name});
        public static bool Spawn(Genome genome,Vector3 point)=>Submit(new LanCommand{kind="spawn",genome=genome,point=point});
        public static bool Parameter(string id,float value)=>Submit(new LanCommand{kind="parameter",parameter=id,value=value});
        public static bool Profile(Dictionary<string,string> catalog,GeneTreeDocument tree)=>Submit(new LanCommand{kind="profile",definitions=catalog.Select(p=>new SavedGeneDefinition{id=p.Key,json=p.Value}).ToList(),tree=tree});
    }
}
