// ★ 新增：以米为尺度的近景细节；三维采样跨面连续，导数抗锯齿防止拉远后闪烁。
#ifndef DARWIN_NEAR_SURFACE_DETAIL_INCLUDED
#define DARWIN_NEAR_SURFACE_DETAIL_INCLUDED
float NearSurfaceGrain(float3 p)
{
    float footprint = max(length(ddx(p)), length(ddy(p)));
    float fine = SurfaceNoise(p) - .5;
    float micro = SurfaceNoise(p * 3.07 + 11.3) - .5;
    return fine * saturate(1 - footprint) + micro * .35 * saturate(1 - footprint * 3.07);
}
float3 ApplyNearSurfaceDetail(float3 color, float3 positionOS, float distanceToCamera,
    float density, float strength, float2 fadeDistance)
{
    float fade = 1 - smoothstep(fadeDistance.x, fadeDistance.y, distanceToCamera);
    float grain = NearSurfaceGrain(positionOS * density);
    return color * (1 + grain * strength * fade);
}
#endif
