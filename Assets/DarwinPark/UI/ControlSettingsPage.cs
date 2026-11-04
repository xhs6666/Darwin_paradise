using System;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：按键等待状态独立管理，Esc只取消改键，避免同时关闭设置或触发游戏操作。
    public sealed class ControlSettingsPage
    {
        public ControlAction? Waiting {get;private set;}
        private string _message="";
        private Vector2 _scroll;
        public void Cancel()=>Waiting=null;
        public void Begin(ControlAction action){Waiting=action;_message="请按键盘或鼠标按键；Esc 取消";}
        public bool HandleEvent(Event evt)
        {
            if(!Waiting.HasValue)return false;
            KeyCode key;
            if(evt.type==EventType.KeyDown)key=evt.keyCode;
            else if(evt.type==EventType.MouseDown)key=(KeyCode)((int)KeyCode.Mouse0+evt.button);
            else return false;
            if(key==KeyCode.Escape){Cancel();_message="已取消改键";}
            else if(ControlBindings.TryBind(Waiting.Value,key,out _message))Cancel();
            evt.Use();return true;
        }
        public void Draw()
        {
            GUILayout.Label("点击当前按键进行修改",RuntimeMenuTheme.Muted);
            // ★ 适配：长按键列表只滚动右侧内容，左侧分页和底部返回始终可见。
            _scroll=GUILayout.BeginScrollView(_scroll,GUILayout.Height(Mathf.Clamp(Screen.height-300,120,300)));
            foreach(ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                GUILayout.BeginHorizontal();GUILayout.Label(ControlBindings.Labels[(int)action],GUILayout.Height(32));
                if(GUILayout.Button(Waiting==action?"等待按键…":ControlBindings.Display(action),GUILayout.Width(145),GUILayout.Height(30)))Begin(action);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Esc 固定打开菜单。星球暂停和实体跳跃可共用按键。",new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
            if(!string.IsNullOrEmpty(_message))GUILayout.Label(_message,new GUIStyle(GUI.skin.label){wordWrap=true});
            if(RuntimeMenuTheme.Button("恢复默认按键",null,false,36)){Cancel();ControlBindings.RestoreDefaults();_message="已恢复默认并保存";}
            GUILayout.EndScrollView();
        }
    }
}
