using UnityEngine;
using UnityEngine.EventSystems;
using DarwinPark.Genetics;
using DarwinPark.Core;

namespace DarwinPark.UI
{
    public class EntitySpawnerUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public GameObject entityPrefab;
        public float spawnHeight = 8f;
        public float ghostHeight = 3f;
        [HideInInspector] public Genome CustomGenome;
        private GameObject _ghostObj;
        private bool _dragging;

        // ★ 适配：单击原来没有处理器；把新点击投放交互交给独立工具，原拖拽继续有效。
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || data.dragging) return;
            var tool = EntityPlacementTool.Instance;
            if (tool == null) { Log.Error(Log.Tag.UI, "场景缺少 EntityPlacementTool"); return; }
            tool.BeginPlacement(entityPrefab, CustomGenome);
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || entityPrefab == null) return;
            EntityPlacementTool.Instance?.CancelPlacement();
            _dragging = true; OrbitCamera.IsDraggingUI = true;
            // ★ 修复（防Bug回归）：只复制网格外观，预览不运行实体脚本，不污染实体注册表。
            _ghostObj = EntityPlacementUtility.CreatePreview(entityPrefab);
            if (_ghostObj != null) _ghostObj.SetActive(false);
        }
        public void OnDrag(PointerEventData data)
        {
            if (!_dragging || _ghostObj == null) return;
            bool valid = EntityPlacementUtility.TryGetSurface(Camera.main, data.position, out var point, out var outward);
            _ghostObj.SetActive(valid);
            if (valid) { _ghostObj.transform.position = point + outward * ghostHeight; _ghostObj.transform.up = outward; }
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (!_dragging) return;
            _dragging = false; OrbitCamera.IsDraggingUI = false;
            EntityPlacementUtility.DestroyPreview(_ghostObj); _ghostObj = null;
            // ★ 修复（防Bug回归）：松手仍在 UI/射线未命中指定星球时不能生成；距离使用相机裁剪面。
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(data.pointerId)) return;
            if (EntityPlacementUtility.TryGetSurface(Camera.main, data.position, out var point, out var outward))
                EntityPlacementUtility.Spawn(CustomGenome, point + outward * spawnHeight);
        }
        private void OnDisable()
        {
            if (_dragging) OrbitCamera.IsDraggingUI = false;
            _dragging = false; EntityPlacementUtility.DestroyPreview(_ghostObj); _ghostObj = null;
        }
    }
}
