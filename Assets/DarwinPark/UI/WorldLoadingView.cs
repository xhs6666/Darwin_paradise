using UnityEngine;
using DarwinPark.Core;

namespace DarwinPark.UI
{
    // ★ 新增：进场和存档内参数更新共用进度动画，纯展示，不触发重建世界或恢复实体。
    public static class WorldLoadingView
    {
        public static void Draw(string status,float progress,string caption="正在准备世界")
        {
            GUILayout.Space(50);GUILayout.Label(status,new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter});
            var rect=GUILayoutUtility.GetRect(100,30,GUILayout.ExpandWidth(true));
            GUI.DrawTexture(new Rect(rect.x,rect.y+9,rect.width-56,10),UIVisualTheme.SurfaceSprite("track").texture,ScaleMode.StretchToFill,true,0,Color.white,0,5);
            GUI.DrawTexture(new Rect(rect.x,rect.y+9,(rect.width-56)*Mathf.Clamp01(progress),10),UIVisualTheme.SurfaceSprite("fill").texture,ScaleMode.StretchToFill,true,0,Color.white,0,5);
            GUI.Label(new Rect(rect.xMax-50,rect.y,50,30),Mathf.RoundToInt(progress*100)+"%");
            GUILayout.Label(caption+new string('·',(int)(Time.unscaledTime*2)%4));
        }
    }
}
