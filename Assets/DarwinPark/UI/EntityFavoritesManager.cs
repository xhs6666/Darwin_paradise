using System.Collections.Generic;
using UnityEngine;
using DarwinPark.Core;
using DarwinPark.Entities;

namespace DarwinPark.UI
{
    public class EntityFavoritesManager : MonoBehaviour
    {
        public static EntityFavoritesManager Instance { get; private set; }
        private HashSet<DarwinEntity> _favorites = new HashSet<DarwinEntity>();

        private GameObject _listPanel;
        private RectTransform _contentParent;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            // ★ 修复（防Bug回归）：开始时依赖未就绪不应抛异常，打开列表时还会重试。
            BuildUI();
            if (_listPanel != null) _listPanel.SetActive(false);
        }

        // ★ 新增：供按钮调用的显隐切换
        public void TogglePanel()
        {
            // ★ 修复（防Bug回归）：首次打开必须刷新空状态，否则只有背景，看起来像按钮没生效。
            if (_listPanel == null) BuildUI();
            if (_listPanel == null) return;
            bool show = !_listPanel.activeSelf;
            if (show) { RefreshList(); _listPanel.transform.SetAsLastSibling(); }
            _listPanel.SetActive(show);
        }

        public bool IsFavorite(DarwinEntity entity) => _favorites.Contains(entity);

        public void ToggleFavorite(DarwinEntity entity)
        {
            if (entity == null) return;

            if (_favorites.Contains(entity))
            {
                _favorites.Remove(entity);
                RemoveHighlight(entity);
            }
            else
            {
                _favorites.Add(entity);
                AddHighlight(entity);
            }

            RefreshList();
        }

        private void AddHighlight(DarwinEntity entity)
        {
            // ★ 修复：旧版点光源在 URP 下不可靠；改用不依赖光照的环形高亮。
            EntityFavoriteHighlight.Attach(entity);
        }

        private void RemoveHighlight(DarwinEntity entity)
        {
            EntityFavoriteHighlight.Detach(entity);
        }

        private void BuildUI()
        {
            if (_listPanel != null) return;
            var root = ServiceLocator.Get<UIRoot>("收藏列表需要 UIRoot");
            if (root == null || root.MainCanvas == null) return;

            // 左侧悬浮列表
            _listPanel = UIFactory.NewRect("FavoritesPanel", root.MainCanvas.transform, typeof(UnityEngine.UI.Image));
            var rt = _listPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(20, 0);
            rt.sizeDelta = new Vector2(200, 400);
            _listPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.85f);

            var title = UIFactory.NewText("Title", _listPanel.transform, "⭐ 收藏列表", 16, TextAnchor.MiddleCenter);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 40);
            trt.anchoredPosition = new Vector2(0, -5);

            var contentGO = UIFactory.NewRect("Content", _listPanel.transform);
            _contentParent = contentGO.GetComponent<RectTransform>();
            _contentParent.anchorMin = Vector2.zero;
            _contentParent.anchorMax = Vector2.one;
            _contentParent.offsetMin = new Vector2(5, 5);
            _contentParent.offsetMax = new Vector2(-5, -45);

            var vlg = contentGO.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing = 5;
            // ★ 修复（防Bug回归）：空状态声明的首选高度要由布局实际采用。
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
        }

        // ★ 适配：实体改名后，已经打开的收藏列表不能保留旧标签。
        public void NotifyEntityRenamed() { if (_listPanel != null && _listPanel.activeSelf) RefreshList(); }

        private void RefreshList()
        {
            if (_contentParent == null) return;

            // 清空旧列表
            // ★ 修复（防Bug回归）：延迟销毁的旧条目本帧不再参与布局或拦截点击。
            foreach (Transform child in _contentParent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }

            // ★ 修复：实体死亡/销毁后从收藏集合清理，避免列表只剩空气按钮。
            _favorites.RemoveWhere(e => e == null);

            if (_favorites.Count == 0)
            {
                var empty = UIFactory.NewText("Empty", _contentParent, "暂无收藏\n右键实体查看面板，点击 ☆ 收藏", 13, TextAnchor.MiddleCenter);
                empty.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 60;
            }

            // 重新生成按钮
            foreach (var entity in _favorites)
            {
                if (entity == null) continue; // 如果实体被销毁了，跳过

                var btn = UIFactory.NewButton("Fav_" + entity.DisplayName, _contentParent, entity.DisplayName, () => {
                    LocateEntity(entity);
                    // 打开实体详细面板 (找到场景中的 GeneInspectorUI)
                    var inspector = FindFirstObjectByType<GeneInspectorUI>();
                    if (inspector != null) inspector.ShowEntityInfo(entity);
                }, new Vector2(180, 30));

                // ★ 修复（防Bug回归）：布局控制高度后，纯色 Button 没有首选高度，会被压成 0。
                var rowLayout = btn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                rowLayout.minHeight = 30f; rowLayout.preferredHeight = 30f; rowLayout.flexibleHeight = 0f;

                // ★ 新增：右键收藏条目只定位，不打开面板，便于连续观察多个实体。
                var rightClick = btn.gameObject.AddComponent<RightClickHandler>();
                rightClick.OnRightClick += () => LocateEntity(entity);
            }
        }

        /// <summary>
        /// ★ 修复：定位实体时把相机放到实体所在球面法线方向，
        /// 旧版 OrbitCamera.FocusOn 计算到了球心背面，且固定 180 距离会钻进星球内部。
        /// </summary>
        private void LocateEntity(DarwinEntity entity)
        {
            if (entity == null) return;
            // ★ 适配：收藏定位应切回轨道相机，不能被仍在运行的跟随视角覆盖。
            EntityViewController.Instance?.ExitView();
            var cam = Camera.main != null ? Camera.main.GetComponent<OrbitCamera>() : null;
            if (cam == null)
            {
                Log.Error(Log.Tag.UI, "收藏定位失败：主相机缺少 OrbitCamera");
                return;
            }

            // 用碰撞体中心而不是脚底坐标，避免定位到地表以下。
            var col = entity.GetComponent<Collider>();
            Vector3 focusPoint = col != null ? col.bounds.center : entity.transform.position;
            cam.FocusOn(focusPoint);
        }
    }
}
