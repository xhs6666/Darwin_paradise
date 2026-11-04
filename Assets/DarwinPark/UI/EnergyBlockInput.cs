using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：Q仅作用于当前操控实体；不创建按钮，不为无基因或无营养的实体生成能量。
    public sealed class EnergyBlockInput : MonoBehaviour
    {
        // ★ 适配：改键后的能力输入与释放鼠标门控共用设置配置。
        public KeyCode releaseKey=>ControlBindings.Get(ControlAction.ReleaseEnergy);
        private void Update(){if(Application.isFocused&&Input.GetKeyDown(releaseKey))HandleKey(releaseKey);}
        public bool HandleKey(KeyCode key)
        {
            var view=EntityViewController.Instance;var selected=EventSystem.current?.currentSelectedGameObject;
            if(key!=releaseKey||GameInputGate.Blocked||view?.Target==null||ControlBindings.Held(ControlAction.ReleaseCursor)||GetComponent<EntityPossessionInput>()?.CursorReleased==true||
                selected!=null&&selected.GetComponentInParent<InputField>() is InputField field&&field.isFocused)return false;
            var metabolism=view.Target.GetComponent<EnergyBlockMetabolism>();var camera=Camera.main;
            // ★ 适配：成员的基因能力允许使用，但必须让房主扣账和生成实际能量块。
            if(LanSession.Instance?.IsClient==true&&camera!=null){LanSession.Instance.SendCommand(new LanCommand{kind="release",aim=camera.transform.forward});return true;}
            return metabolism!=null&&camera!=null&&metabolism.TryRelease(camera.transform.forward,out _);
        }
    }
}
