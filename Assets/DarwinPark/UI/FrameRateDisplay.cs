using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：使用真实帧耗时采样，模拟倍率和暂停不改变FPS；只画标签，不拦截鼠标或生成逐帧日志。
    public sealed class FrameRateDisplay : MonoBehaviour
    {
        public Vector2 offset=new Vector2(12,10),size=new Vector2(190,28);
        public float sampleInterval=.5f,panelGap=8,simulationLineHeight=18;
        public int fontSize=16;
        [Range(0,1)] public float backgroundOpacity=.18f;
        public static FrameRateDisplay Instance {get;private set;}
        public float CurrentFps {get;private set;}
        public int SampleCount {get;private set;}
        public string DisplayText {get;private set;}="FPS —";
        private float _elapsed,_simulated;private int _frames;private GUIStyle _style,_fpsStyle,_simulationStyle;private string _simulationText;
        public Rect DisplayRect=>new Rect(offset,new Vector2(size.x,size.y+(string.IsNullOrEmpty(_simulationText)?0:simulationLineHeight)));
        private void Awake()=>Instance=this;
        private void Update()
        {
            ServiceLocator.TryGet<SimulationClock>(out var clock);_simulated+=clock!=null?clock.GameDeltaTime:0;
            _elapsed+=Time.unscaledDeltaTime;_frames++;if(_elapsed<Mathf.Max(.1f,sampleInterval))return;
            // ★ 修复（防Bug回归）：低于1帧时保留小数，避免长帧被四舍五入成FPS 0掩盖真实采样。
            CurrentFps=_frames/_elapsed;string fps=CurrentFps<1?CurrentFps.ToString("F2"):CurrentFps.ToString("F0");DisplayText=$"FPS {fps}  |  {_elapsed*1000/_frames:F1} ms";
            // ★ 适配：高倍率超出机器承载时显示实际完成倍率和待算时间，不能把限速后的画面误报为满速16倍。
            _simulationText=clock!=null&&clock.IsRunning&&clock.PendingGameTime>clock.maximumGameDelta?$"模拟 {_simulated/_elapsed:F1}× · 待算 {clock.PendingGameTime:F1}s":null;
            SampleCount++;_elapsed=0;_frames=0;_simulated=0;
        }
        private void OnApplicationFocus(bool focused){_elapsed=0;_frames=0;_simulated=0;}
        private void OnDestroy(){if(Instance==this)Instance=null;}
        public static Vector2 ReserveTop(Vector2 desired,Canvas canvas)
        {
            var display=Instance;if(display==null||!display.isActiveAndEnabled)return desired;
            float scale=canvas!=null?canvas.scaleFactor:1;
            desired.y=Mathf.Min(desired.y,-(display.DisplayRect.yMax+display.panelGap)/Mathf.Max(.01f,scale));return desired;
        }
        private void OnGUI()
        {
            if(_style==null)
            {
                _style=new GUIStyle{font=UIFactory.DefaultFont,fontSize=fontSize,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(8,8,0,0),border=new RectOffset(12,12,12,12)};
                _style.normal.textColor=UIVisualTheme.Accent;_style.normal.background=UIVisualTheme.SurfaceSprite("panel")?.texture;
                _fpsStyle=new GUIStyle(_style);_fpsStyle.normal.background=null;
                _simulationStyle=new GUIStyle(_style){fontSize=12};_simulationStyle.normal.background=null;
            }
            int depth=GUI.depth;var color=GUI.color;GUI.depth=-2000;GUI.color=Color.white;
            // ★ 修复（防Bug回归）：仅背景半透明，文字保持清晰；0可完全取消背景，不再遮挡世界。
            var rect=DisplayRect;var background=GUI.backgroundColor;GUI.backgroundColor=new Color(1,1,1,backgroundOpacity);
            if(backgroundOpacity>0)GUI.Box(rect,GUIContent.none,_style);GUI.backgroundColor=background;GUI.Label(new Rect(offset,size),DisplayText,_fpsStyle);
            if(!string.IsNullOrEmpty(_simulationText))GUI.Label(new Rect(offset.x,offset.y+size.y-2,size.x,simulationLineHeight),_simulationText,_simulationStyle);
            GUI.depth=depth;GUI.color=color;
        }
    }
}
