using UnityEngine;
using DarwinPark.Entities;

namespace DarwinPark.UI
{
    // ★ 新增：固定眼位与准星同源，隐藏自身但保留物理碰撞；退出恢复相机和模型。
    public sealed class EntityFirstPersonCamera : MonoBehaviour
    {
        public float eyeHeightRatio = .65f, nearClip = .03f;
        private Renderer[] _renderers;
        private bool[] _hidden;
        private Camera _camera;
        private float _oldNear;
        private DarwinEntity _target;
        public void Begin(DarwinEntity target, Camera camera)
        {
            End(); _target = target; _camera = camera; _oldNear = camera.nearClipPlane;
            camera.nearClipPlane = nearClip;
            _renderers = target.GetComponentsInChildren<Renderer>(); _hidden = new bool[_renderers.Length];
            for (int i=0;i<_renderers.Length;i++) { _hidden[i]=_renderers[i].forceRenderingOff; _renderers[i].forceRenderingOff=true; }
        }
        public void Apply(Vector3 up, Vector3 forward, float pitch, EntityManualControl manual)
        {
            if (_target == null || _camera == null) return;
            var collider = _target.GetComponent<Collider>();
            float radius = collider != null ? Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.y, collider.bounds.extents.z) : .5f;
            Vector3 eye = _target.transform.position + up * radius * eyeHeightRatio;
            Vector3 direction = Quaternion.AngleAxis(pitch, Vector3.Cross(up,forward)) * forward;
            _camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(direction,up));
            manual.SetAim(new Ray(eye,direction));
        }
        public void End()
        {
            if (_renderers != null) for(int i=0;i<_renderers.Length;i++) if(_renderers[i]!=null) _renderers[i].forceRenderingOff=_hidden[i];
            if(_camera!=null) _camera.nearClipPlane=_oldNear;
            _renderers=null; _camera=null; _target=null;
        }
        private void OnDisable()=>End();
    }
}
