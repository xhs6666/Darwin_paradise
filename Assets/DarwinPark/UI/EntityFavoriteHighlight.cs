using UnityEngine;
using DarwinPark.Entities;

namespace DarwinPark.UI
{
    /// <summary>
    /// 收藏实体高亮。
    /// ★ 新增：旧版用点光源做高亮，URP 附加灯数量受限时几乎不可见。
    /// 现在改为不依赖光照的环形描边 + 呼吸脉动，任何图层/昼夜都清晰可见。
    /// </summary>
    [DisallowMultipleComponent]
    public class EntityFavoriteHighlight : MonoBehaviour
    {
        [Header("高亮样式（数据驱动）")]
        public Color ringColor = new Color(1f, 0.82f, 0.15f, 1f);
        public float ringRadius = 3.2f;
        public float ringWidth = 0.35f;
        public float pulseAmount = 0.12f;
        public float pulseSpeed = 3.5f;
        public int segments = 64;

        private GameObject _ringGO;
        private Transform _ringRoot;
        private LineRenderer _ring;
        private Transform _planet;
        private float _baseRadius;
        private bool _built;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
        }

        private void LateUpdate()
        {
            if (!_built || _ringRoot == null) return;

            if (_planet == null)
            {
                var planetGO = GameObject.Find("Planet");
                if (planetGO != null) _planet = planetGO.transform;
            }

            Vector3 outward = transform.up;
            if (_planet != null)
                outward = (transform.position - _planet.position).normalized;
            if (outward.sqrMagnitude < 1e-6f) outward = Vector3.up;

            _ringRoot.position = transform.position + outward * 0.45f;
            _ringRoot.rotation = Quaternion.FromToRotation(Vector3.up, outward);

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            _ringRoot.localScale = Vector3.one * _baseRadius * pulse;

            if (_ring != null)
            {
                var color = ringColor;
                color.a = 0.75f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.2f;
                _ring.startColor = color;
                _ring.endColor = color;
            }
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            _ringGO = new GameObject("FavHighlightRing");
            _ringGO.transform.SetParent(transform, false);
            _ringRoot = _ringGO.transform;

            _ring = _ringGO.AddComponent<LineRenderer>();
            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.positionCount = Mathf.Max(12, segments);
            _ring.widthMultiplier = Mathf.Max(0.05f, ringWidth);
            _ring.numCapVertices = 2;
            _ring.numCornerVertices = 2;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.textureMode = LineTextureMode.Stretch;
            _ring.alignment = LineAlignment.View;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "FavHighlightMat" };
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", ringColor);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ringColor);
                _ring.material = mat;
            }

            _baseRadius = Mathf.Max(1f, ringRadius);
            var col = GetComponent<Collider>();
            if (col != null)
            {
                float extent = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
                _baseRadius = Mathf.Max(_baseRadius, extent * 2.2f);
            }

            var positions = new Vector3[_ring.positionCount];
            for (int i = 0; i < positions.Length; i++)
            {
                float angle = (float)i / positions.Length * Mathf.PI * 2f;
                positions[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            _ring.SetPositions(positions);
        }

        private void OnDestroy()
        {
            // ★ 修复：销毁组件时必须同时清理子物体，否则 LineRenderer 会留在实体身上。
            if (_ringGO != null) Destroy(_ringGO);
        }

        public static void Attach(DarwinEntity entity)
        {
            if (entity == null) return;
            var highlight = entity.GetComponent<EntityFavoriteHighlight>();
            if (highlight == null) highlight = entity.gameObject.AddComponent<EntityFavoriteHighlight>();
            highlight.enabled = true;
            highlight.BuildIfNeeded();
        }

        public static void Detach(DarwinEntity entity)
        {
            if (entity == null) return;
            var highlight = entity.GetComponent<EntityFavoriteHighlight>();
            if (highlight != null) Destroy(highlight);
        }
    }
}
