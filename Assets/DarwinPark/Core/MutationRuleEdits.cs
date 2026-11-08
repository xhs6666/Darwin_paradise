using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DarwinPark.Core
{
    // ★ 新增：规则卡片只改三个字段，保留外部配置的注释和其他数值；校验成功后原子替换并保留备份。
    public static class MutationRuleEdits
    {
        public static string Edit(string original,bool enabled,float fraction,float seconds)
        {
            if(!float.IsFinite(fraction)||!float.IsFinite(seconds)||fraction<0||fraction>1||seconds<1||seconds>86400)throw new InvalidDataException("比例须为0到100%，周期须为1到86400模拟秒");
            string text=Set(original,"mutationRoundsEnabled",enabled?"true":"false");text=Set(text,"mutationPopulationFraction",fraction.ToString("R",CultureInfo.InvariantCulture));text=Set(text,"mutationRoundSeconds",seconds.ToString("R",CultureInfo.InvariantCulture));GameplayTuning.Parse(text);return text;
        }
        private static string Set(string text,string key,string literal)
        {
            string clean=CommentedJson.Strip(text);var match=Regex.Match(clean,"\\\""+Regex.Escape(key)+"\\\"\\s*:\\s*(?<value>true|false|-?[0-9]+(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)");
            if(match.Success){var value=match.Groups["value"];return text.Substring(0,value.Index)+literal+text.Substring(value.Index+value.Length);}
            int close=clean.LastIndexOf('}');if(close<0)throw new InvalidDataException("数值配置缺少结束符");int last=close-1;while(last>=0&&char.IsWhiteSpace(clean[last]))last--;
            if(last<0)throw new InvalidDataException("数值配置为空");if(clean[last]!='{'){text=text.Insert(last+1,",");close++;}
            return text.Insert(close,"\n  \""+key+"\": "+literal+"\n");
        }
        public static void Save(bool enabled,float fraction,float seconds)
        {
            // 使用前读取最新文件，不能用界面开启时的旧副本覆盖用户在外部编辑的其他字段。
            _=GameplayTuning.Current;string path=GameplayTuning.FilePath;string text=Edit(File.ReadAllText(path),enabled,fraction,seconds),temporary=path+".rules.tmp";
            try{File.WriteAllText(temporary,text,new UTF8Encoding(false));File.Replace(temporary,path,path+".bak",true);GameplayTuning.Reload();}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
