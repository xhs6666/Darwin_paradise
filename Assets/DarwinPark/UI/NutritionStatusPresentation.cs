using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;

namespace DarwinPark.UI
{
    public static class NutritionStatusPresentation
    {
        public static string Append(DarwinEntity entity,string current)
        {
            var replica=entity.GetComponent<NutritionReplicaPresentation>();
            bool attached=replica!=null?replica.attached:entity.GetComponent<NutritionParasite>()?.Host!=null;
            int count=replica!=null?replica.parasites:NutritionParasite.CountOn(entity);
            int eggs=replica!=null?replica.foreignEggs:entity.GetComponent<EggHostPregnancy>()?.Count??0;
            if(attached)current="寄生中";
            if(count>0)current+="（被寄生）";
            if(eggs>0)current+="（卵寄生孕育 "+eggs+"）";
            return current;
        }
    }
}
