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
            for (int i=0;i<_renderers.Length;i++) { BodyVisualScheduler.Release(_renderers[i]); _hidden[i]=_renderers[i].forceRenderingOff; _renderers[i].forceRenderingOff=true; }
        }
        public void Apply(Vector3 up, Vector3 forward, float pitch, EntityManualControl manual)
        {
            if (_target == null || _camera == null) return;
            var collider = _target.GetComponent<Collider>();
            float radius = collider != null ? Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.y, collider.bounds.extents.z) : .5f;
            Vector3 eye = _target.transform.position + up * radius * eyeHeightRatio;
            Vector3 direction = LookDirection(up,forward,pitch);
            _camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(direction,up));
            manual.SetAim(new Ray(eye,direction));
        }
        // ★ 修复（防Bug回归）：经纬极点或初始方向恰好朝天时，投影不能留下零向量，导致俯仰轴消失。
        public static Vector3 LookDirection(Vector3 up,Vector3 forward,float pitch)
        {
            up=up.normalized;forward=Vector3.ProjectOnPlane(forward,up).normalized;
            if(forward.sqrMagnitude<1e-8f)forward=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.right).normalized;
            return (Quaternion.AngleAxis(pitch,Vector3.Cross(up,forward).normalized)*forward).normalized;
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
