using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    /// <summary>★ 新增：点击生成按钮进入投放，下一次地表左键确认，右键/ESC 取消；保留原拖拽入口。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class EntityPlacementTool : MonoBehaviour
    {
        public static EntityPlacementTool Instance { get; private set; }
        public bool IsPlacing { get; private set; }
        public float spawnHeight = 8f;
        public float previewHeight = 3f;
        public Vector2 hintPosition = new Vector2(0, 225);
        public string placementHint = "选择地表：左键放置，右键或 ESC 取消";
        private Genome _genome;
        private GameObject _preview, _hint;
        private int _beginFrame;
        private void Awake() { Instance = this; }

        public void BeginPlacement(GameObject prefab, Genome genome)
        {
            CancelPlacement();
            if (prefab == null) { Log.Error(Log.Tag.UI, "默认实体预制体未配置，无法投放"); return; }
            _genome = genome; IsPlacing = true; OrbitCamera.IsDraggingUI = true; _beginFrame = Time.frameCount;
            _preview = EntityPlacementUtility.CreatePreview(prefab);
            _preview?.SetActive(false);
            if (ServiceLocator.TryGet<UIRoot>(out var root) && root.MainCanvas != null)
            {
                var text = UIFactory.NewText("PlacementHint", root.MainCanvas.transform, placementHint, 18, TextAnchor.MiddleCenter);
                var rt = text.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0);
                rt.pivot = new Vector2(.5f, 0); rt.anchoredPosition = hintPosition;
                rt.sizeDelta = new Vector2(640, 35); _hint = text.gameObject;
            }
        }

        private void Update()
        {
            if (!IsPlacing || Time.frameCount == _beginFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { CancelPlacement(); return; }
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool valid = !overUI && EntityPlacementUtility.TryGetSurface(Camera.main, Input.mousePosition,
                out _, out _);
            if (_preview != null) _preview.SetActive(valid);
            if (!valid) return;
            EntityPlacementUtility.TryGetSurface(Camera.main, Input.mousePosition, out var point, out var outward);
            if (_preview != null)
            {
                _preview.transform.position = point + outward * previewHeight;
                _preview.transform.up = outward;
            }
            if (Input.GetMouseButtonDown(0) && EntityPlacementUtility.Spawn(_genome, point + outward * spawnHeight))
                CancelPlacement();
        }

        public void CancelPlacement()
        {
            if (IsPlacing) OrbitCamera.IsDraggingUI = false;
            IsPlacing = false; _genome = null;
            EntityPlacementUtility.DestroyPreview(_preview); _preview = null;
            if (_hint != null) Destroy(_hint); _hint = null;
        }
        private void OnDisable() => CancelPlacement();
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
