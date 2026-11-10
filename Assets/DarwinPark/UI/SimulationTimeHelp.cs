using DarwinPark.Core;
using UnityEngine;
using UnityEngine.EventSystems;
namespace DarwinPark.UI
{
    // ★ 新增：不占用规则卡布局的单位解释，悬浮游戏天数时给出实际换算；暂停不计时、过载可能更慢。
    public sealed class SimulationTimeHelp:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        // ★ 新增：界面按天校验并换算，报错也使用玩家当前看到的单位。
        public static float ToSeconds(float days)
        {
            float day=DarwinPark.Entities.LifeCycleSettings.Load().secondsPerDay,seconds=days*day;
            if(!float.IsFinite(seconds)||seconds<1||seconds>86400)throw new System.FormatException($"周期应为{1/day:0.####}～{86400/day:0.####}游戏天（1游戏天={day:0.##}模拟秒）");
            return seconds;
        }
        private bool _hover;private GUIStyle _style;
        public void OnPointerEnter(PointerEventData data){_hover=true;}public void OnPointerExit(PointerEventData data){_hover=false;}
        private void OnDisable(){_hover=false;}
        private void OnGUI()
        {
            if(!_hover)return;var skin=GUI.skin;int depth=GUI.depth;try{GUI.depth=-1100;RuntimeMenuTheme.Apply();_style??=new GUIStyle(GUI.skin.box){wordWrap=true,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,12,9,9)};float day=DarwinPark.Entities.LifeCycleSettings.Load().secondsPerDay;
            string text=$"1游戏天 = {day:0.##}模拟秒\n1倍速正常运行约{day:0.##}现实秒；2倍速约{day/2:0.##}秒\n暂停不计时；负载过高时可能更慢。";var position=Input.mousePosition;float width=Mathf.Min(330,Screen.width-20);float height=_style.CalcHeight(new GUIContent(text),width);GUI.Box(new Rect(Mathf.Clamp(position.x,10,Screen.width-width-10),Mathf.Clamp(Screen.height-position.y-height-15,10,Screen.height-height-10),width,height),text,_style);}
            finally{GUI.skin=skin;GUI.depth=depth;}
        }
    }
}
