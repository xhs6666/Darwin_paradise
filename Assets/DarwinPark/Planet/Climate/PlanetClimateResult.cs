using UnityEngine;
namespace DarwinPark.Planet
{
    public sealed class PlanetClimateResult
    {
        public int Resolution;
        public float SeaLevel;
        public ClimateCirculation Circulation;

        public float[] Height;
        public float[] Temperature;
        public float[] Humidity;
        public float[] Precipitation;
        public float[] CoastProximity;
        public float[] WindFetch;
        public float[] Continentality;
        public float[] Relief;
        public bool[] Ocean;
        public Color[] Pixels;
        public ClimateType[] Types;

        public int LandCells;
        public int DesertCells;
        public int SnowCells;
        public float MinTemperature = float.MaxValue;
        public float MaxTemperature = float.MinValue;
        public float MinHumidity = float.MaxValue;
        public float MaxHumidity = float.MinValue;
    }
}
