using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.Core
{
    /// <summary>
    /// 全局 UI 根节点。
    /// ★ 新增：全场景唯一 Canvas + GraphicRaycaster，所有业务 UI 都挂到 MainCanvas 下，
    /// 消除多个 Canvas 的射线/层级冲突（规则3.5）。
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        public Canvas MainCanvas { get; private set; }
        public Canvas WorldCanvas { get; private set; }

        private void Awake()
        {
            if (ServiceLocator.TryGet<UIRoot>(out _))
            {
                Log.Warn(Log.Tag.UI, "重复 UIRoot，销毁多余实例");
                Destroy(this);
                return;
            }
            ServiceLocator.Register(this);

            MainCanvas = EnsureCanvas("MainCanvas", RenderMode.ScreenSpaceOverlay);
            WorldCanvas = EnsureCanvas("WorldCanvas", RenderMode.ScreenSpaceCamera);
            WorldCanvas.sortingOrder = 10;

            EnsureEventSystem();
        }

        private Canvas EnsureCanvas(string name, RenderMode mode)
        {
            var child = transform.Find(name);
            if (child == null)
            {
                var go = new GameObject(name, typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasScaler));
                go.transform.SetParent(transform, false);
                child = go.transform;
            }
            // ★ 修复（防Bug回归）：同名场景节点未必已有完整 UI 组件，逐项补齐以免初始化中途抛异常。
            var canvas = child.GetComponent<Canvas>() ?? child.gameObject.AddComponent<Canvas>();
            if (child.GetComponent<CanvasScaler>() == null) child.gameObject.AddComponent<CanvasScaler>();
            canvas.renderMode = mode;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            if (mode == RenderMode.ScreenSpaceOverlay)
            {
                // ★ 修复：明确主 HUD 排序，避免 WorldCanvas/其他全屏 UI 遮住底部控制栏。
                // 底部栏在 SimulationControlPanel 里还会再抬到 200，这里给一个足够高的基线。
                canvas.sortingOrder = 200;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            return canvas;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return; // ★ 适配 Unity 6 新 API
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(transform, false);
            Log.Info(Log.Tag.UI, "UIRoot 自动补建 EventSystem");
        }

        // ★ 修复（防Bug回归）：禁用再启用必须重新注册；重复根节点销毁时不能注销真正的根。
        private void OnEnable()
        {
            if (MainCanvas != null && (!ServiceLocator.TryGet<UIRoot>(out var root) || root != this))
                ServiceLocator.Register(this);
        }

        private void OnDisable()
        {
            if (ServiceLocator.TryGet<UIRoot>(out var root) && root == this)
                ServiceLocator.Unregister<UIRoot>();
        }
    }
}