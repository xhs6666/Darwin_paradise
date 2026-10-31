using System.Collections.Generic;
using System.Linq;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：旧长孕育和长休养必须随新表达规则一起迁移，避免恢复生长后先老死再到生产时间。
    public static class EcologyLegacySaveUpgrade
    {
        public static int Apply(WorldSaveData world,IEnumerable<DarwinEntity> entities)
        {
            if(!string.IsNullOrWhiteSpace(world.ecologyBalanceSettings))return 0;
            var settings=EcologyBalanceSettings.Current;
            var saved=world.entities.SelectMany(e=>e.reproduction).ToLookup(r=>r.gene);int changed=0;
            foreach(var entity in entities)
            {
                if(entity==null)continue;
                var genes=entity.Genes;var stats=entity.GetComponent<EntityStats>();
                if(stats==null||!stats.TryGet(settings.sizeAttribute,out float size))continue;
                size=Mathf.Max(settings.minimumReproductionSize,size);
                foreach(var gene in genes.Genome.Genes)
                {
                    if(!(genes.GetLogic(gene) is ReproductionGeneLogic logic))continue;
                    var state=logic.CaptureState();float priorGestation=state.gestation,priorCooldown=state.cooldown;
                    state.gestation=Mathf.Min(state.gestation,logic.CalculateGestationTime(size));
                    state.cooldown=Mathf.Min(state.cooldown,EcologyReproductionTiming.CooldownSeconds(genes));
                    if(state.gestation==priorGestation&&state.cooldown==priorCooldown)continue;
                    logic.RestoreState(state);changed++;
                    // ★ 适配：内存中的世界快照同步收纳，加载重试时不能重新使用旧长计时；磁盘仍只由正常保存链路写入。
                    foreach(var record in saved[state.gene]){record.gestation=state.gestation;record.cooldown=state.cooldown;}
                }
            }
            world.ecologyBalanceSettings=EcologyBalanceSettings.CurrentJson;
            if(changed>0)Debug.Log("[生态存档] 已收纳 "+changed+" 项旧孕育/休养计时，并登记世界规则快照。");
            return changed;
        }
    }
}
