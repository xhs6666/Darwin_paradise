using System;
using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class EcologySaveState
    {
        public string id,groupId,mode,targetId,hostId;public long joinOrder;public bool leader,hasCenter,hostKinematic;
        // ★ 适配：独立实体状态按注册表 ID 持久化，不能用显示名称或基因值代替。
        public string entityStateId;
        // ★ 适配：固定分工、运输、孕育、巡逻休息与反击记忆成对保存。
        public AntColonyMemberSave ant;
        public PatrolRhythmSave patrol;
        public CombatIntentSave combat;
        public RtsOrderSave rts;
        public Vector3 center,threat,commandPoint,hostOffset;
        public float evictionRemaining,commandRemaining,manualRemaining,duelRemaining,incubationRemaining;
        public PopulationCommand command;
        public bool hostDetected;public float hostDetectionRemaining;
        // ★ 修复（防Bug回归）：Unity 内联类缺字段时可能生成空对象；仅完全空的生态记录视作旧档无此字段。
        public bool IsEmpty=>string.IsNullOrEmpty(id)&&string.IsNullOrEmpty(groupId)&&string.IsNullOrEmpty(mode)&&string.IsNullOrEmpty(targetId)&&string.IsNullOrEmpty(hostId)&&
            string.IsNullOrEmpty(entityStateId)&&joinOrder==0&&!leader&&!hasCenter&&!hostKinematic&&!hostDetected&&center==Vector3.zero&&threat==Vector3.zero&&commandPoint==Vector3.zero&&hostOffset==Vector3.zero&&
            evictionRemaining==0&&commandRemaining==0&&manualRemaining==0&&duelRemaining==0&&incubationRemaining==0&&hostDetectionRemaining==0&&command==PopulationCommand.None&&ant?.HasData!=true;
        // ★ 新增：先保存本体与稳定标识，再在所有实体创建后恢复群体目标和宿主引用，不依赖加载先后顺序。
        public static EcologySaveState Capture(DarwinEntity entity)
        {
            var state=new EcologySaveState{id=EcologyIdentity.Of(entity)};var territory=entity.GetComponent<TerritoryBehavior>();
            if(territory!=null){state.hasCenter=territory.HasCenter;state.center=territory.CenterLocal;state.threat=territory.ThreatLocal;state.evictionRemaining=territory.EvictionRemaining;}
            var pop=entity.GetComponent<PopulationBehavior>();
            if(pop!=null){state.groupId=pop.GroupId;state.joinOrder=pop.JoinOrder;state.leader=pop.IsLeader;state.mode=pop.Mode;state.command=pop.Command;state.commandRemaining=pop.CommandRemaining;state.commandPoint=pop.CommandPointLocal;state.manualRemaining=pop.ManualRemaining;state.duelRemaining=pop.DuelRemaining;state.targetId=pop.Target!=null?EcologyIdentity.Of(pop.Target):null;}
            var parasite=entity.GetComponent<ParasiteBehavior>();
            if(parasite!=null&&parasite.Host!=null){state.hostId=EcologyIdentity.Of(parasite.Host);state.hostOffset=parasite.Offset;state.hostKinematic=parasite.OriginalKinematic;state.hostDetected=parasite.DetectedByHost;state.hostDetectionRemaining=parasite.DetectionRemaining;}
            state.entityStateId=entity.GetComponent<EntityStateController>()?.Current.id;
            state.ant=entity.GetComponent<AntColonyMember>()?.Capture();state.patrol=entity.GetComponent<PatrolRhythm>()?.Capture();state.combat=entity.GetComponent<CombatIntent>()?.Capture();
            state.rts=entity.GetComponent<RtsOrderController>()?.Capture();state.incubationRemaining=entity.GetComponent<EggIncubation>()?.Remaining??0;return state;
        }
        public void Restore(DarwinEntity entity)
        {
            EcologyIdentity.Ensure(entity.gameObject).Restore(id);
            // ★ 适配：RTS目标使用持久ID和本地坐标，与实体按任意顺序恢复兼容。
            if(rts?.active==true)RtsOrderController.Ensure(entity).Restore(rts);
            entity.GetComponent<TerritoryBehavior>()?.Restore(center,hasCenter,evictionRemaining,threat);
            entity.GetComponent<PopulationBehavior>()?.Restore(this);
            if(ant?.HasData==true)
            {var population=entity.GetComponent<PopulationBehavior>()??entity.gameObject.AddComponent<PopulationBehavior>();population.Restore(this);AntColonyMember.Ensure(population).Restore(ant);}
            if(patrol!=null)PatrolRhythm.Ensure(entity.gameObject).Restore(patrol);
            if(incubationRemaining>0){(entity.GetComponent<EggIncubation>()??entity.gameObject.AddComponent<EggIncubation>()).Begin(incubationRemaining);entity.GetComponent<EntityLifeCycle>()?.WaitForHatch();}
            entity.GetComponent<EntityStateController>()?.Restore(entityStateId);
        }
        public void Resolve(DarwinEntity entity,Dictionary<string,DarwinEntity> entities)
        {
            entities.TryGetValue(targetId??"",out var target);entity.GetComponent<PopulationBehavior>()?.RestoreTarget(target);
            if(combat!=null){entities.TryGetValue(combat.target??"",out var attacker);(entity.GetComponent<CombatIntent>()??entity.gameObject.AddComponent<CombatIntent>()).Restore(attacker,combat.remaining);}
            if(!string.IsNullOrEmpty(hostId)&&entities.TryGetValue(hostId,out var host))
            {var parasite=entity.GetComponent<ParasiteBehavior>();if(parasite!=null&&parasite.TryAttach(host,true,hostOffset,hostKinematic))parasite.RestoreDetection(hostDetected,hostDetectionRemaining);}
        }
    }
}
