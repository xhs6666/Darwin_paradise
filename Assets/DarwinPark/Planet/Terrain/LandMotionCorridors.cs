using System;
using UnityEngine;

namespace DarwinPark.Planet
{
    // ★ 新增：在生成好的立方球网格上验证整片陆地。查验失败继续使用精确岸线，不从格心推断可通行性。
    internal sealed class LandMotionCorridors
    {
        private const int TileSize = 8;
        private const float AngularMargin = .0001f;
        private readonly float[] _edges, _edgeLengths;
        private readonly bool[] _verified;
        private readonly int _width;

        internal LandMotionCorridors(PlanetTerrainData data, float surfaceRadius, float slope, float clearance)
        {
            int subdivisions = data.Subdivisions;
            _width = (subdivisions + TileSize - 1) / TileSize;
            _edges = new float[_width + 1];
            _edgeLengths = new float[_width + 1];
            _verified = new bool[6 * _width * _width];
            if (subdivisions < 2 || data.Vertices == null || data.Triangles?.Length != 36 * subdivisions * subdivisions ||
                data.HeightScale < 0 || surfaceRadius <= 0 || slope <= 0) return;
            for (int i = 0; i <= _width; i++)
            {
                float edge = 2f * Math.Min(subdivisions, i * TileSize) / subdivisions - 1f;
                _edges[i] = edge;
                _edgeLengths[i] = Mathf.Sqrt(1 + edge * edge);
            }
            for (int face = 0; face < 6; face++)
            for (int row = 0; row < _width; row++)
            for (int column = 0; column < _width; column++)
            {
                float water = WaterUpperBound(data, face, column, row);
                float minimum = water + clearance + Mathf.Max(.01f, Mathf.Abs(water) * AngularMargin);
                _verified[Index(face, column, row)] =
                    VerifyTile(data, face, column, row, minimum, slope * surfaceRadius * (1 - AngularMargin));
            }
        }

        private int Index(int face, int column, int row) => (face * _width + row) * _width + column;

        private float WaterUpperBound(PlanetTerrainData data, int face, int column, int row)
        {
            if (data.Hydrology == null) return data.Radius + data.SeaLevel * data.HeightScale;
            var normal = PlanetTerrainGenerator.FaceNormal(face);
            float left = _edges[column], right = _edges[column + 1], bottom = _edges[row], top = _edges[row + 1];
            var axis = PlanetTerrainGenerator.FacePoint(normal, (left + right) * .5f, (bottom + top) * .5f).normalized;
            float cosine = Mathf.Min(
                Mathf.Min(Cosine(left, bottom), Cosine(left, top)),
                Mathf.Min(Cosine(right, bottom), Cosine(right, top)));
            float cap = Mathf.Acos(Mathf.Clamp(cosine, -1, 1)) + AngularMargin;
            return data.Radius + data.Hydrology.MaximumWaterInCap(axis, cap) * data.HeightScale;

            float Cosine(float u, float v) => Vector3.Dot(axis, PlanetTerrainGenerator.FacePoint(normal, u, v).normalized);
        }

        private static bool VerifyTile(PlanetTerrainData data, int face, int column, int row, float minimum, float gradient)
        {
            int size = data.Subdivisions;
            int endX = Math.Min(size, (column + 1) * TileSize);
            int endY = Math.Min(size, (row + 1) * TileSize);
            for (int y = row * TileSize; y < endY; y++)
            for (int x = column * TileSize; x < endX; x++)
            {
                int index = (face * size * size + y * size + x) * 6;
                if (!VerifyTriangle(data, index, minimum, gradient) ||
                    !VerifyTriangle(data, index + 3, minimum, gradient)) return false;
            }
            return true;
        }

        private static bool VerifyTriangle(PlanetTerrainData data, int index, float minimum, float gradient)
        {
            var a = data.Vertices[data.Triangles[index]];
            var b = data.Vertices[data.Triangles[index + 1]];
            var c = data.Vertices[data.Triangles[index + 2]];
            var direction = a.normalized;
            // 三角形内部是顶点的凸组合，向同一单位矢量的投影给出整个表面的半径下界。
            if (!(Mathf.Min(a.magnitude, Mathf.Min(Vector3.Dot(direction, b), Vector3.Dot(direction, c))) > minimum))
                return false;
            var normal = Vector3.Cross(b - a, c - a).normalized;
            // 这里沿用真实三角形的外法线。径向函数的最大角梯度不超过 r_max*tan(最斜的法线夹角)。
            float cosine = Mathf.Min(Vector3.Dot(normal, direction),
                Mathf.Min(Vector3.Dot(normal, b.normalized), Vector3.Dot(normal, c.normalized))) - AngularMargin;
            if (cosine <= 0) return false;
            float radiusSquared = Mathf.Max(a.sqrMagnitude, Mathf.Max(b.sqrMagnitude, c.sqrMagnitude));
            return radiusSquared * (1 - cosine * cosine) < gradient * gradient * cosine * cosine;
        }

        internal bool ContainsSweep(Vector3 start, Vector3 end, float radius)
        {
            if (!(radius >= 0) || !RadialTerrainSurface.TryFace(start, out int face, out float u, out float v, out float depth))
                return false;
            int x = Tile(u / depth), y = Tile(v / depth);
            if (!_verified[Index(face, x, y)]) return false;
            // 同一立方球块由四个过球心的半空间组成。两端球体完全在半空间内，则中间的胶囊扫掠也在其中。
            float padding = radius + Mathf.Max(.01f, depth * AngularMargin);
            if (!Contains(u, v, depth, x, y, padding)) return false;
            RadialTerrainSurface.ProjectFace(face, end, out u, out v, out depth);
            return Contains(u, v, depth, x, y, padding);
        }

        private int Tile(float coordinate)
        {
            // 最后一个块可能小于 TileSize，按真实边界修正等宽索引。
            int index = Mathf.Clamp(Mathf.FloorToInt((coordinate + 1) * .5f * _width), 0, _width - 1);
            return index > 0 && coordinate < _edges[index] ? index - 1 : index;
        }

        private bool Contains(float u, float v, float depth, int x, int y, float padding) =>
            u - _edges[x] * depth > padding * _edgeLengths[x] &&
            _edges[x + 1] * depth - u > padding * _edgeLengths[x + 1] &&
            v - _edges[y] * depth > padding * _edgeLengths[y] &&
            _edges[y + 1] * depth - v > padding * _edgeLengths[y + 1];
    }
}
