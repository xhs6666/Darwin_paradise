using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：捕食比较实际 size 数值；选目标和最终咬合都检查，猎物长大后旧目标也不能绕过限制。
    public static class PredationSizePolicy
    {
        public static bool Allows(EntityGeneController predator,DarwinEntity prey)
        {
            if(predator==null||prey==null||predator.gameObject==prey.gameObject)return false;
            var settings=EcologyBalanceSettings.Current;
            var a=predator.GetComponent<EntityStats>();var b=prey.GetComponent<EntityStats>();
            if(a==null||b==null||!a.TryGet(settings.sizeAttribute,out float own)||!b.TryGet(settings.sizeAttribute,out float other))return false;
            return AllowsValues(own,other,settings);
        }
        public static bool AllowsValues(float predatorSize,float preySize,EcologyBalanceSettings settings=null)
        {
            settings=settings??EcologyBalanceSettings.Current;
            if(float.IsNaN(predatorSize)||float.IsInfinity(predatorSize)||float.IsNaN(preySize)||float.IsInfinity(preySize)||predatorSize<settings.minimumSize||preySize<settings.minimumSize)return false;
            return preySize<=predatorSize*settings.maximumPreySizeRatio;
        }
    }
}
