using DarwinPark.Core;

namespace DarwinPark.Environment
{
    /// <summary>环境配置加载器。★ 新增：环境伤害和气候基准都从 Resources/Environment 读取。</summary>
    public class EnvironmentConfigDatabase : JsonResourceDatabase<EnvironmentConfig>
    {
        public EnvironmentConfigDatabase() : base(Log.Tag.Environment) { }

        protected override string ResourceFolder => "Environment";
        protected override string GetId(EnvironmentConfig item) => item.id;

        protected override EnvironmentConfig ConvertFromJson(string jsonText, string assetName)
        {
            var cfg = UnityEngine.JsonUtility.FromJson<EnvironmentConfig>(jsonText);
            if (cfg == null)
            {
                Log.Error(Log.Tag.Environment, $"{assetName} 解析为空");
                return null;
            }
            return cfg;
        }

        public static readonly EnvironmentConfigDatabase Instance = new();
    }
}
