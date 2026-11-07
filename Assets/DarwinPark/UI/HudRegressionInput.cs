#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;

namespace DarwinPark.EditorTools
{
    // ★ 新增：只替换测试鼠标来源，按下/松开仍由真实StandaloneInputModule处理，不直接调用按钮回调。
    public sealed class HudRegressionInput:BaseInput
    {
        public Vector2 Position;public bool Down,Up,Held;
        public override bool mousePresent=>true;
        public override Vector2 mousePosition=>Position;
        public override Vector2 mouseScrollDelta=>Vector2.zero;
        public override bool touchSupported=>false;
        public override int touchCount=>0;
        public override bool GetMouseButtonDown(int button)=>button==0&&Down;
        public override bool GetMouseButtonUp(int button)=>button==0&&Up;
        public override bool GetMouseButton(int button)=>button==0&&Held;
        public override float GetAxisRaw(string axisName)=>0;
        public override bool GetButtonDown(string buttonName)=>false;
    }
}

#endif
