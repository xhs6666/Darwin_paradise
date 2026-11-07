using System;
using System.IO;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Logic
{
    [Serializable] public sealed class MultipleBirthState {public int planned,remaining;}
    // ★ 新增：多胎只扩展一次孕育的后代数；每个后代仍独立检查出生位置、上限和卵寄生宿主。
    public static class MultipleBirthPolicy
    {
        public static int Count(EntityGeneController genes)
        {
            var gene=Ecology.EcologyQueries.Active(genes,MultipleBirthEvolutionUpgrade.GeneId);
            if(gene==null)return 1;
            int maximum=Mathf.FloorToInt(GameplayTuning.Current.multipleBirthMaximum);
            return Mathf.Clamp(2+Mathf.FloorToInt(Mathf.Clamp01(gene.EffectiveStrength/100f)*Mathf.Max(0,maximum-2)),1,maximum);
        }
        public static MultipleBirthState Begin(EntityGeneController genes)=>new MultipleBirthState{planned=Count(genes),remaining=Count(genes)};
        public static MultipleBirthState Copy(MultipleBirthState value)=>value==null||value.planned==0&&value.remaining==0?null:new MultipleBirthState{planned=value.planned,remaining=value.remaining};
        public static void Validate(MultipleBirthState value)
        {
            // ★ 适配：旧档没有胎数视为单胎，已怀孕的合法胎数不受后续外部上限调整影响。
            // ★ 修复（防Bug回归）：Unity内联JSON会把null子对象还原成全零对象；全零仍表示旧档缺省单胎。
            if(value!=null&&!(value.planned==0&&value.remaining==0)&&(value.planned<1||value.planned>16||value.remaining<0||value.remaining>value.planned))throw new InvalidDataException("多胎孕育数量无效");
        }
        public static bool Deliver(ref MultipleBirthState state,Func<bool> spawn,Action afterBirth=null)
        {
            if(state==null||state.planned==0&&state.remaining==0)state=new MultipleBirthState{planned=1,remaining=1};
            while(state.remaining>0)
            {
                if(!EntityPopulationLimit.CanSpawn||!spawn())return false;
                state.remaining--;afterBirth?.Invoke();
            }
            return true;
        }
    }
    public sealed class MultipleBirthGeneLogic:ReproductionGeneLogic { }
}
