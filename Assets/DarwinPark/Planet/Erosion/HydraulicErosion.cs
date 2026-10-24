using UnityEngine;
using DarwinPark.Planet;

namespace DarwinPark.Planet.Erosion
{
    /// <summary>
    /// 水力侵蚀（droplet 模型）。
    /// ★ 新增：沿梯度方向冲刷并沉积，形成谷地。
    /// </summary>
    public static class HydraulicErosion
    {
        public static void Apply(float[,] height, int resolution, int iterations,
                                 float strength, DeterministicRandom rng)
        {
            int size = resolution;
            for (int iter = 0; iter < iterations; iter++)
            {
                int px = rng.Next(size + 1);
                int py = rng.Next(size + 1);
                float water = 1f, speed = 0f, sediment = 0f;
                const float depositRate = 0.3f;
                const float erosionRate = 0.3f;
                const float evaporation = 0.05f;

                for (int step = 0; step < 30; step++)
                {
                    int cx = Mathf.Clamp(px, 0, size - 1);
                    int cy = Mathf.Clamp(py, 0, size - 1);
                    float h00 = height[cx, cy], h10 = height[cx + 1, cy];
                    float h01 = height[cx, cy + 1], h11 = height[cx + 1, cy + 1];
                    float gx = (h10 + h11 - h00 - h01) * 0.5f;
                    float gy = (h01 + h11 - h00 - h10) * 0.5f;
                    float mag = Mathf.Sqrt(gx * gx + gy * gy);
                    if (mag < 1e-6f) break;
                    float dx = -gx / mag, dy = -gy / mag;

                    int nx = Mathf.Clamp(px + Mathf.RoundToInt(dx), 0, size);
                    int ny = Mathf.Clamp(py + Mathf.RoundToInt(dy), 0, size);
                    int sxp = Mathf.Clamp(px, 0, size);
                    int syp = Mathf.Clamp(py, 0, size);
                    float deltaH = height[nx, ny] - height[sxp, syp];

                    float carry = Mathf.Max(0f, -deltaH) * speed * water;
                    if (carry > 0f)
                    {
                        float erode = Mathf.Min(carry * erosionRate, Mathf.Max(0f, -deltaH));
                        height[sxp, syp] -= erode * strength;
                        sediment += erode * strength;
                    }

                    // ★ 修复（防Bug回归）：old code did sqrt(speed² + deltaH).
                    // Downhill deltaH is negative, so sqrt(negative) produced NaN and
                    // corrupted the whole height field, making mountains disappear.
                    float downhill = Mathf.Max(0f, -deltaH);
                    speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed + downhill));
                    if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0f;
                    water *= (1f - evaporation);

                    if (carry < sediment * depositRate)
                    {
                        float deposit = sediment * depositRate;
                        height[nx, ny] += deposit;
                        sediment -= deposit;
                    }

                    px = nx; py = ny;
                    if (water < 0.01f) break;
                }
            }
        }
    }
}