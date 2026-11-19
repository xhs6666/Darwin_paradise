using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 新增：短时间内复用真实三角形平面，重新投影当前径向位置；跨三角形立即重新取样，不延用斜坡旧点。
    public sealed class GroundSurfacePatch
    {
        private Vector3 _a, _ab, _ac, _normal;
        private float _uu, _uv, _vv, _inverse;
        private bool _ready;
        public void Capture(PlanetTerrainMeshBuilder planet, int triangle)
            => Capture(planet.Data,planet.transform.localToWorldMatrix,planet.transform.position,triangle);

        internal void Capture(PlanetTerrainData data,in Matrix4x4 matrix,Vector3 center,int triangle)
        {
            int index = triangle * 3;
            _ready = false;
            if (index < 0 || index + 2 >= data.Triangles.Length) return;
            _a = matrix.MultiplyPoint3x4(data.Vertices[data.Triangles[index]]);
            _ab = matrix.MultiplyPoint3x4(data.Vertices[data.Triangles[index + 1]]) - _a;
            _ac = matrix.MultiplyPoint3x4(data.Vertices[data.Triangles[index + 2]]) - _a;
            _normal = Vector3.Cross(_ab, _ac).normalized;
            if (Vector3.Dot(_normal, _a - center) < 0) _normal = -_normal;
            _uu = Vector3.Dot(_ab, _ab); _uv = Vector3.Dot(_ab, _ac); _vv = Vector3.Dot(_ac, _ac);
            float determinant = _uu * _vv - _uv * _uv;
            if (Mathf.Abs(determinant) < 1e-12f) return;
            _inverse = 1 / determinant; _ready = true;
        }
        internal void Clear() => _ready=false;
        public bool TryRead(Vector3 center, Vector3 up, out Vector3 point, out Vector3 normal)
        {
            point = default; normal = _normal;
            float alignment = Vector3.Dot(_normal, up);
            if (!_ready || alignment <= 1e-6f) return false;
            point = center + up * (Vector3.Dot(_normal, _a - center) / alignment);
            var offset = point - _a; float u = Vector3.Dot(offset, _ab), v = Vector3.Dot(offset, _ac);
            float x = (_vv * u - _uv * v) * _inverse, y = (_uu * v - _uv * u) * _inverse;
            return x >= -.00001f && y >= -.00001f && x + y <= 1.00001f;
        }
    }
}
