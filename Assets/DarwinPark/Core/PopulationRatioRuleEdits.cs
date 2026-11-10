namespace DarwinPark.Core
{
    // ★ 新增：只保存比例调节开关，保留外部注释、其他规则和未知字段。
    public static class PopulationRatioRuleEdits
    {
        public static string Edit(string text,bool enabled)
        {
            text=MutationRuleEdits.Set(text,"populationRatioControlEnabled",enabled?"true":"false");
            GameplayTuning.Parse(text);return text;
        }
    }
}
