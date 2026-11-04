using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：在清场前校验外来基因与计时，坏档不能先破坏当前世界，也不能把宿主的基因当作默认后代。
    public static class NutritionSaveValidation
    {
        public static void Validate(EcologySaveState state,ICollection<string> catalog)
        {
            if(state==null)return;
            if(state.nutritionParasiteCount<0||!string.IsNullOrEmpty(state.nutritionParasite?.host)&&(!Guid.TryParseExact(state.nutritionParasite.host,"N",out _)||state.nutritionParasite.host==state.id))throw new InvalidDataException("营养寄生宿主标识或数量无效");
            if(state.foreignEggs==null)return;
            foreach(var egg in state.foreignEggs)
                if(egg==null||!float.IsFinite(egg.remaining)||egg.remaining<0||!Guid.TryParseExact(egg.parent,"N",out _)||egg.genome?.Genes==null||egg.genome.Genes.Count==0||egg.genome.Genes.Any(g=>g==null||!catalog.Contains(g.DefinitionId)||!float.IsFinite(g.Value)||g.MissingStateIds==null))throw new InvalidDataException("卵寄生载荷的亲本、计时或基因组无效");
        }
    }
}
