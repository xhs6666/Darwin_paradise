using UnityEngine;

namespace DarwinPark.Planet.Erosion
{
    /// <summary>
    /// 热力侵蚀（talus 角模型）：局部高度差超过阈值则坍塌。
    /// ★ 新增：让陡坡自然平缓。
    /// </summary>
    public static class ThermalErosion
    {
        public static void Apply(float[,] height, int resolution, int iterations,
                                 float talusAngle, float strength)
        {
            int size = resolution;
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int y = 0; y <= size; y++)
                {
                    for (int x = 0; x <= size; x++)
                    {
                        float h = height[x, y];
                        float hmax = h; int tx = x, ty = y;
                        Check(height, size, x + 1, y, ref hmax, ref tx, ref ty);
                        Check(height, size, x - 1, y, ref hmax, ref tx, ref ty);
                        Check(height, size, x, y + 1, ref hmax, ref tx, ref ty);
                        Check(height, size, x, y - 1, ref hmax, ref tx, ref ty);
                        float diff = hmax - h;
                        if (diff > talusAngle)
                        {
                            float move = (diff - talusAngle) * 0.5f * strength;
                            height[x, y] += move;
                            height[tx, ty] -= move;
                        }
                    }
                }
            }
        }

        private static void Check(float[,] h, int size, int nx, int ny,
                                  ref float hmax, ref int tx, ref int ty)
        {
            if (nx < 0 || ny < 0 || nx > size || ny > size) return;
            if (h[nx, ny] > hmax) { hmax = h[nx, ny]; tx = nx; ty = ny; }
        }
    }
}