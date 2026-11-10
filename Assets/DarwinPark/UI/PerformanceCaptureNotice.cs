using UnityEngine;
using DarwinPark.Core;
namespace DarwinPark.UI
{
    // ★ 新增：采样在返回游戏后结束，结果必须直接出现在游戏画面；纯绘制通知不占鼠标、不改变底栏或弹窗布局。
    public sealed class PerformanceCaptureNotice:MonoBehaviour
    {
        public float duration=12,width=720,top=48;
        public string Message{get;private set;}="";
        private float _until;private GUIStyle _label;
        public bool Visible=>!string.IsNullOrEmpty(Message)&&Time.realtimeSinceStartup<_until;
        public void Show(string message){Message=message;_until=Time.realtimeSinceStartup+Mathf.Max(1,duration);}
        private void OnGUI()
        {
            if(!Visible||WorldRuntimeGate.Recording||Event.current.type!=EventType.Repaint)return;
            var skin=GUI.skin;int depth=GUI.depth;var color=GUI.color;
            try
            {
                GUI.depth=-1200;GUI.color=Color.white;RuntimeMenuTheme.Apply();
                _label??=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=14};
                float w=Mathf.Min(width,Screen.width-24);var content=new GUIContent(Message);float h=_label.CalcHeight(content,w-28)+24;
                var rect=new Rect((Screen.width-w)/2,top,w,h);GUI.Box(rect,GUIContent.none);GUI.Label(new Rect(rect.x+14,rect.y+12,w-28,h-24),content,_label);
            }
            finally{GUI.skin=skin;GUI.depth=depth;GUI.color=color;}
        }
    }
}
