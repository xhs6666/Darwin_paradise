using System;
using System.Globalization;
using System.IO;
using System.Text;
namespace DarwinPark.Core
{
    // ★ 新增：规则扩展只改对应字段，保留外部注释、未知参数与另一页未编辑的设置。
    public static class RuleSelectionEdits
    {
        public static string Mutation(string text,bool enabled,int mode,float probability,float seconds)
        {
            text=MutationRuleEdits.Set(text,"mutationRoundsEnabled",enabled?"true":"false");text=MutationRuleEdits.Set(text,"mutationMode",mode.ToString(CultureInfo.InvariantCulture));
            text=MutationRuleEdits.Set(text,mode==0?"mutationPopulationFraction":"stageMutationProbability",probability.ToString("R",CultureInfo.InvariantCulture));
            if(mode==0)text=MutationRuleEdits.Set(text,"mutationRoundSeconds",seconds.ToString("R",CultureInfo.InvariantCulture));GameplayTuning.Parse(text);return text;
        }
        public static string Cleanup(string text,bool enabled,float seconds)
        {text=MutationRuleEdits.Set(text,"corpseCleanupEnabled",enabled?"true":"false");text=MutationRuleEdits.Set(text,"corpseCleanupSeconds",seconds.ToString("R",CultureInfo.InvariantCulture));GameplayTuning.Parse(text);return text;}
        public static string MutationModes(string text,bool mutationEnabled,int mode,float fraction,float stageProbability,float seconds)
        {
            text=Mutation(text,mutationEnabled,mode,mode==0?fraction:stageProbability,seconds);
            text=MutationRuleEdits.Set(text,mode==0?"stageMutationProbability":"mutationPopulationFraction",(mode==0?stageProbability:fraction).ToString("R",CultureInfo.InvariantCulture));
            text=MutationRuleEdits.Set(text,"mutationRoundSeconds",seconds.ToString("R",CultureInfo.InvariantCulture));GameplayTuning.Parse(text);return text;
        }
        public static void Save(Func<string,string> edit)
        {
            _=GameplayTuning.Current;string path=GameplayTuning.FilePath,temporary=path+".rules.tmp";string text=edit(File.ReadAllText(path));
            try{File.WriteAllText(temporary,text,new UTF8Encoding(false));File.Replace(temporary,path,path+".bak",true);GameplayTuning.Reload();}finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
