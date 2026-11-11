using System;
using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：年平均近似环流；保留海岸、地形和风程，不把纬度当作唯一气候分类。
    [Serializable]public sealed class EarthClimateSettings
    {
        public bool enabled=true;public float solarWeight=.95f,equatorialRain=28,subtropicalDryness=31,frontRain=10,polarDryness=16;
        public float equatorialWidth=14,subtropicalLatitude=28,subtropicalWidth=11,frontLatitude=55,frontWidth=14;
        public float tundraTemperature=2,taigaTemperature=9,steppeHumidity=40;
        public Color rainforest=new(.025f,.24f,.20f),forest=new(.13f,.43f,.19f),savanna=new(.77f,.64f,.23f),grassland=new(.42f,.65f,.29f),taiga=new(.16f,.31f,.35f),steppe=new(.66f,.62f,.40f),coldDesert=new(.69f,.65f,.56f);
    }
    public static class EarthClimateRules
    {
        public static float SolarWeight(PlanetClimateConfig cfg)=>cfg.earth?.enabled==true?cfg.earth.solarWeight:cfg.solarTemperatureWeight;
        private static float Bell(float value,float center,float width){float d=(value-center)/Mathf.Max(.1f,width);return Mathf.Exp(-d*d);}
        public static float Humidity(Vector3 direction,PlanetClimateConfig cfg)
        {var e=cfg.earth;if(e?.enabled!=true)return 0;float latitude=Mathf.Abs(Mathf.Asin(Mathf.Clamp(direction.y,-1,1))*Mathf.Rad2Deg);return e.equatorialRain*Bell(latitude,0,e.equatorialWidth)-e.subtropicalDryness*Bell(latitude,e.subtropicalLatitude,e.subtropicalWidth)+e.frontRain*Bell(latitude,e.frontLatitude,e.frontWidth)-e.polarDryness*Mathf.SmoothStep(0,1,Mathf.InverseLerp(65,90,latitude));}
        public static Vector3 Wind(Vector3 direction,PlanetClimateConfig cfg)
        {if(cfg.earth?.enabled!=true)return cfg.GetWindDirection();float latitude=Mathf.Asin(Mathf.Clamp(direction.y,-1,1))*Mathf.Rad2Deg;var east=Vector3.Cross(Vector3.up,direction);if(east.sqrMagnitude<1e-8f)east=Vector3.right;east.Normalize();var north=Vector3.ProjectOnPlane(Vector3.up,direction).normalized;float mid=Mathf.SmoothStep(0,1,Mathf.InverseLerp(25,35,Mathf.Abs(latitude)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(55,65,Mathf.Abs(latitude))));return (east*Mathf.Lerp(-1,1,mid)+north*(-Mathf.Sign(latitude)*.35f*(1-mid))).normalized;}
        public static ClimateType Classify(float t,float h,PlanetClimateConfig cfg)
        {var e=cfg.earth;if(t<=cfg.polarIceTemp)return ClimateType.PolarIce;if(t<e.tundraTemperature)return h<=cfg.desertHumidity?ClimateType.ColdDesert:ClimateType.Tundra;if(h<=cfg.desertHumidity)return t<cfg.desertTemp?ClimateType.ColdDesert:ClimateType.Desert;if(h<e.steppeHumidity)return ClimateType.Steppe;if(t>=cfg.tropicalTemp)return h>=cfg.tropicalHumidity?ClimateType.TropicalRain:ClimateType.Savanna;if(h>=cfg.forestHumidity)return t<e.taigaTemperature?ClimateType.Taiga:ClimateType.Forest;return ClimateType.Grassland;}
        // 独立色板以较窄连续过渡区混合，雨林深青绿、热带草原金黄、森林鲜绿易区分。
        public static Color Color(float t,float h,PlanetClimateConfig cfg)
        {var e=cfg.earth;float tropical=Mathf.SmoothStep(0,1,Mathf.InverseLerp(cfg.tropicalTemp-2,cfg.tropicalTemp+2,t));var color=UnityEngine.Color.Lerp(e.grassland,e.forest,Mathf.SmoothStep(0,1,Mathf.InverseLerp(cfg.forestHumidity-4,cfg.forestHumidity+4,h)));var hot=UnityEngine.Color.Lerp(e.savanna,e.rainforest,Mathf.SmoothStep(0,1,Mathf.InverseLerp(cfg.tropicalHumidity-5,cfg.tropicalHumidity+5,h)));color=UnityEngine.Color.Lerp(color,hot,tropical);color=UnityEngine.Color.Lerp(color,e.taiga,(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(e.taigaTemperature-2,e.taigaTemperature+2,t)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(cfg.forestHumidity-5,cfg.forestHumidity+5,h)));color=UnityEngine.Color.Lerp(color,e.steppe,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(e.steppeHumidity-3,e.steppeHumidity+3,h)));color=UnityEngine.Color.Lerp(color,t<cfg.desertTemp?e.coldDesert:cfg.desert,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cfg.desertHumidity-3,cfg.desertHumidity+3,h)));color=UnityEngine.Color.Lerp(color,cfg.tundra,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(e.tundraTemperature-2,e.tundraTemperature+2,t)));return UnityEngine.Color.Lerp(color,cfg.polarIce,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cfg.polarIceTemp-3,cfg.polarIceTemp+3,t)));}
    }
}
