using System;
using System.IO;
using System.Text;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Planet
{
    // ★ 新增：大气配色独立配置并热更新，颜色错误时保留上一份有效配置，不重建地形或气壳。
    [Serializable] public sealed class AtmospherePalette
    {
        public int version=1;
        public string dayColor="#3385FF",twilightColor="#FF7138",nightColor="#02040B";
        public bool enableDayNight=true;
        public float nightSurfaceBrightness=.009f,terminatorWidth=.065f;
        public Color Day {get;private set;}
        public Color Twilight {get;private set;}
        public Color Night {get;private set;}
        private static AtmospherePalette _current;private static long _stamp;private static float _poll;
        public static string FilePath=>Path.Combine(GamePaths.Root,"config","atmosphere.json");
        public static AtmospherePalette Current {get{if(_current==null)Reload();return _current;}}
        private static Color ParseColor(string value)
        {
            if(string.IsNullOrEmpty(value)||value.Length!=7||value[0]!='#'||!ColorUtility.TryParseHtmlString(value,out var color))throw new InvalidDataException("大气颜色必须为 #RRGGBB："+value);
            return color;
        }
        public static AtmospherePalette Parse(string text)
        {
            var value=JsonUtility.FromJson<AtmospherePalette>(CommentedJson.Strip(text));
            if(value==null||value.version!=1)throw new InvalidDataException("大气配色版本无效");
            if(!float.IsFinite(value.nightSurfaceBrightness)||value.nightSurfaceBrightness<0||value.nightSurfaceBrightness>1||!float.IsFinite(value.terminatorWidth)||value.terminatorWidth<.01f||value.terminatorWidth>.5f)throw new InvalidDataException("夜间亮度须为0～1，晨昏半宽须为0.01～0.5");
            value.Day=ParseColor(value.dayColor);value.Twilight=ParseColor(value.twilightColor);value.Night=ParseColor(value.nightColor);return value;
        }
        public static void Reload()
        {
            var asset=Resources.Load<TextAsset>("atmosphere");if(asset==null)throw new InvalidDataException("缺少 atmosphere 默认配置");var fallback=Parse(asset.text);
            try
            {
                if(!File.Exists(FilePath)){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));File.WriteAllText(FilePath,asset.text,new UTF8Encoding(false));}
                var next=Parse(File.ReadAllText(FilePath));_stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;_current=next;
            }
            catch(Exception error)when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||error is ArgumentException)
            {if(_current==null)_current=fallback;Debug.LogWarning("[大气配色] 保留有效配置："+error.Message);}
        }
        public static void Poll()
        {
            if(Time.unscaledTime<_poll)return;_poll=Time.unscaledTime+2;
            try{long stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;if(_current==null||stamp!=_stamp){_stamp=stamp;Reload();}}
            catch(Exception error)when(error is IOException||error is UnauthorizedAccessException){Debug.LogWarning("[大气配色] 检查配置失败："+error.Message);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_current=null;_stamp=0;_poll=0;}
    }
}
