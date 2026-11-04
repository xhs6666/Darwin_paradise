using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Core
{
    public enum ControlAction { Forward, Back, Left, Right, Jump, Attack, ReleaseCursor, ExitView, ReleaseEnergy, Pause, WorldPause, Rts, CycleLayer, ClearLayer }
    // ★ 新增：所有可改键输入共用同一份绑定；只保存本机偏好，联机指令仍由房主验证权限。
    public static class ControlBindings
    {
        [Serializable] public sealed class Entry { public string action, key; }
        [Serializable] public sealed class Data { public Entry[] bindings; }
        private const string Preference="DarwinPark.Controls.v1";
        private static Dictionary<ControlAction,KeyCode> _keys;
        public static readonly string[] Labels={"前进","后退","向左","向右","跳跃","攻击","按住释放鼠标","退出实体操控","吐出能量块","暂停 / 继续","星球视角暂停","切换 RTS","切换图层","取消图层显示"};
        private static Dictionary<ControlAction,KeyCode> Defaults()
        {
            var asset=Resources.Load<TextAsset>("control_bindings");
            if(asset==null)throw new InvalidOperationException("缺少 control_bindings 配置");
            var result=new Dictionary<ControlAction,KeyCode>();
            foreach(var entry in JsonUtility.FromJson<Data>(asset.text).bindings)
                result.Add((ControlAction)Enum.Parse(typeof(ControlAction),entry.action),(KeyCode)Enum.Parse(typeof(KeyCode),entry.key));
            if(result.Count!=Enum.GetValues(typeof(ControlAction)).Length)throw new InvalidOperationException("默认按键配置不完整");
            return result;
        }
        private static void Ensure()
        {
            if(_keys!=null)return;_keys=Defaults();
            if(!PlayerPrefs.HasKey(Preference))return;
            try
            {
                var saved=JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Preference));
                var proposed=new Dictionary<ControlAction,KeyCode>(_keys);
                foreach(var entry in saved.bindings)
                {
                    if(!Enum.TryParse(entry.action,out ControlAction action)||!Enum.IsDefined(typeof(ControlAction),action)||!Enum.TryParse(entry.key,out KeyCode key)||!Valid(key))throw new FormatException("无效按键");
                    proposed[action]=key;
                }
                foreach(var a in proposed)foreach(var b in proposed)
                    if(a.Key!=b.Key&&SameKey(a.Value,b.Value)&&Overlaps(a.Key,b.Key))throw new FormatException("按键冲突");
                _keys=proposed;
            }
            catch(Exception ex){Debug.LogWarning("[按键] 保存的绑定无效，使用默认值："+ex.Message);}
        }
        public static KeyCode Get(ControlAction action){Ensure();return _keys[action];}
        public static bool Held(ControlAction action)
        {
            var key=Get(action);return Input.GetKey(key)||key==KeyCode.LeftAlt&&Input.GetKey(KeyCode.RightAlt);
        }
        public static bool Down(ControlAction action)=>Input.GetKeyDown(Get(action));
        // 空格可分别承担星球暂停和实体跳跃；其他同时生效的操作不能绑定同一个键。
        private static bool Overlaps(ControlAction a,ControlAction b)=>a!=ControlAction.WorldPause&&b!=ControlAction.WorldPause||a>=ControlAction.Rts||b>=ControlAction.Rts||a==ControlAction.Pause||b==ControlAction.Pause;
        private static bool SameKey(KeyCode a,KeyCode b)=>(a==KeyCode.RightAlt?KeyCode.LeftAlt:a)==(b==KeyCode.RightAlt?KeyCode.LeftAlt:b);
        private static bool Valid(KeyCode key)=>Enum.IsDefined(typeof(KeyCode),key)&&key!=KeyCode.None&&key!=KeyCode.Escape;
        public static bool TryBind(ControlAction action,KeyCode key,out string message)
        {
            Ensure();message="";
            if(!Valid(key)){message="该键保留给菜单或不可用";return false;}
            foreach(var pair in _keys)if(pair.Key!=action&&SameKey(pair.Value,key)&&Overlaps(action,pair.Key)){message="已用于："+Labels[(int)pair.Key];return false;}
            _keys[action]=key;Save();message="已保存";return true;
        }
        public static void Save()
        {
            Ensure();var entries=new List<Entry>();foreach(var pair in _keys)entries.Add(new Entry{action=pair.Key.ToString(),key=pair.Value.ToString()});
            PlayerPrefs.SetString(Preference,JsonUtility.ToJson(new Data{bindings=entries.ToArray()}));PlayerPrefs.Save();
        }
        public static void RestoreDefaults(){_keys=Defaults();Save();}
        public static string Display(ControlAction action)
        {
            var key=Get(action);return key==KeyCode.Mouse0?"鼠标左键":key==KeyCode.Mouse1?"鼠标右键":key==KeyCode.Mouse2?"鼠标中键":key==KeyCode.Space?"空格":key.ToString();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void ClearCache()=>_keys=null;
    }
}
