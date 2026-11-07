using System;
using System.IO;
using UnityEngine;
namespace DarwinPark.UI
{
    // ★ 新增：帧率上限独立于模拟倍率；桌面VSync会覆盖targetFrameRate，因此应用锁帧时统一关闭VSync。
    public static class FramePacingSettings
    {
        [Serializable]private sealed class Config{public int version=0,defaultLimit=0;public int[] limits=Array.Empty<int>();}
        public const string PreferenceKey="FrameRateLimit";private static Config _config;private static int _limit;private static bool _loaded;
        public static int Limit{get{Ensure();return _limit;}}
        public static int[] Choices{get{Ensure();return (int[])_config.limits.Clone();}}
        private static void Ensure()
        {
            if(_config==null){var asset=Resources.Load<TextAsset>("frame_pacing");if(asset==null)throw new InvalidDataException("缺少frame_pacing配置");var config=JsonUtility.FromJson<Config>(asset.text);if(config==null||config.version!=1||config.limits==null||config.limits.Length==0||Array.IndexOf(config.limits,config.defaultLimit)<0)throw new InvalidDataException("锁帧配置无效");for(int i=0;i<config.limits.Length;i++)if(config.limits[i]<0||config.limits[i]>480||i>0&&config.limits[i]<=config.limits[i-1])throw new InvalidDataException("锁帧选项必须升序且处于0到480");_config=config;}
            if(!_loaded){int saved=PlayerPrefs.GetInt(PreferenceKey,_config.defaultLimit);_limit=Array.IndexOf(_config.limits,saved)>=0?saved:_config.defaultLimit;_loaded=true;}
        }
        public static void Apply(){Ensure();QualitySettings.vSyncCount=0;Application.targetFrameRate=_limit==0?-1:_limit;}
        public static bool SetLimit(int value){Ensure();if(Array.IndexOf(_config.limits,value)<0)return false;_limit=value;Apply();return true;}
        public static void Cycle(int direction){Ensure();int index=Array.IndexOf(_config.limits,_limit);SetLimit(_config.limits[(index+_config.limits.Length+(direction<0?-1:1))%_config.limits.Length]);}
        public static void Save(){Ensure();PlayerPrefs.SetInt(PreferenceKey,_limit);PlayerPrefs.Save();}
        public static void Reload(){_loaded=false;Apply();}
        public static void Draw()
        {
            GUILayout.Label("锁帧（最大帧率）");GUILayout.BeginHorizontal();
            if(GUILayout.Button("‹",GUILayout.Width(36),GUILayout.Height(30)))Cycle(-1);
            if(GUILayout.Button(Limit==0?"不限制":Limit+" FPS",GUILayout.Height(30)))Cycle(1);
            if(GUILayout.Button("›",GUILayout.Width(36),GUILayout.Height(30)))Cycle(1);
            GUILayout.EndHorizontal();GUILayout.Label("性能不足时仍会低于上限；可调低目标来减少波动。",new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_config=null;_loaded=false;_limit=0;}
    }
}
