using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>地形配置加载。★ 适配：继承 JsonResourceDatabase 消除重复加载逻辑。</summary>
    public class PlanetTerrainConfigDatabase : JsonResourceDatabase<PlanetTerrainConfig>
    {
        public PlanetTerrainConfigDatabase() : base(Log.Tag.Planet) { }

        protected override string ResourceFolder => "Terrain";
        protected override string GetId(PlanetTerrainConfig item) => item.id;

        protected override PlanetTerrainConfig ConvertFromJson(string jsonText, string assetName)
        {
            var cfg = JsonUtility.FromJson<PlanetTerrainConfig>(jsonText);
            if (cfg != null && cfg.subdivisions < 1)
            {
                Log.Error(Log.Tag.Planet, $"{assetName} subdivisions 必须≥1");
                return null;
            }
            return cfg;
        }

        public static readonly PlanetTerrainConfigDatabase Instance = new();
    }
}