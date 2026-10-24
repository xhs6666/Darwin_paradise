// ★ 新增：球面三维细节，直接采样方向而不使用面 UV，避免重新引入经线/六面接缝。
#ifndef DARWIN_SURFACE_DETAIL_INCLUDED
#define DARWIN_SURFACE_DETAIL_INCLUDED
float SurfaceHash(float3 p)
{
    p = frac(p * .1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}
float SurfaceNoise(float3 p)
{
    float3 a = floor(p), f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(lerp(SurfaceHash(a), SurfaceHash(a + float3(1,0,0)), f.x),
        lerp(SurfaceHash(a + float3(0,1,0)), SurfaceHash(a + float3(1,1,0)), f.x), f.y),
        lerp(lerp(SurfaceHash(a + float3(0,0,1)), SurfaceHash(a + float3(1,0,1)), f.x),
        lerp(SurfaceHash(a + float3(0,1,1)), SurfaceHash(a + 1), f.x), f.y), f.z);
}
float SurfaceDetail(float3 direction, float scale)
{
    float3 p = direction * scale;
    return SurfaceNoise(p) * .57 + SurfaceNoise(p * 2.03 + 7.1) * .28
        + SurfaceNoise(p * 4.1 + 13.5) * .15;
}
#endif
