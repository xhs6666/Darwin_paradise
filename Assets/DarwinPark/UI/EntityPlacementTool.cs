using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    /// <summary>★ 新增：点击槽位进入连续投放，每次地表左键生成一批，右键/ESC 取消；保留原拖拽入口。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class EntityPlacementTool : MonoBehaviour
    {
        public static EntityPlacementTool Instance { get; private set; }
        public bool IsPlacing { get; private set; }
        public float spawnHeight = 8f;
        public float previewHeight = 3f;
        public Vector2 hintPosition = new Vector2(0, 225);
        public string placementHint = "选择地表：左键放置，右键或 ESC 取消";
        public int PlacementCount { get; private set; }=1;
        public float batchSpacing=2f;
        private Genome _genome;
        private GameObject _preview, _hint;
        private int _beginFrame;
        private void Awake() { Instance = this; }

        public void BeginPlacement(GameObject prefab, Genome genome, int count=1)
        {
            // ★ 适配：开始投放时退出实体控制，避免两个模式争用键鼠与相机。
            EntityViewController.Instance?.ExitView();
            CancelPlacement();
            if (prefab == null) { Log.Error(Log.Tag.UI, "默认实体预制体未配置，无法投放"); return; }
            _genome = genome; IsPlacing = true; OrbitCamera.IsDraggingUI = true; _beginFrame = Time.frameCount;
            PlacementCount=Mathf.Max(1,count);
            _preview = EntityPlacementUtility.CreatePreview(prefab);
            _preview?.SetActive(false);
            if (ServiceLocator.TryGet<UIRoot>(out var root) && root.MainCanvas != null)
            {
                var text = UIFactory.NewText("PlacementHint", root.MainCanvas.transform, $"{placementHint}（每次 {PlacementCount} 个）", 18, TextAnchor.MiddleCenter);
                text.raycastTarget=false;
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
            // ★ 适配：与拖拽共用实时坐标检测，避免上一帧 UI 命中阻断地表确认。
            bool overUI = EntityPlacementUtility.IsPointerOverUI(Input.mousePosition);
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
            if (Input.GetMouseButtonDown(0)) TryPlaceAt(Input.mousePosition);
        }

        // ★ 修复（防Bug回归）：确认投放共用实时 UI/正面射线判定，失败可重新选位置，成功保持连续模式。
        public bool TryPlaceAt(Vector2 screen)
        {
            if(!IsPlacing || EntityPlacementUtility.IsPointerOverUI(screen) ||
                !EntityPlacementUtility.TryGetSurface(Camera.main,screen,out var point,out var outward))return false;
            // ★ 新增：成功后保留当前模板与预览，支持连续点放；只有右键/Esc 取消。
            return EntityPlacementUtility.SpawnBatch(_genome,point,outward,spawnHeight,PlacementCount,batchSpacing)>0;
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
