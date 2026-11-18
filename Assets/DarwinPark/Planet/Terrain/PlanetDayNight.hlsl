#ifndef DARWIN_PLANET_DAY_NIGHT
#define DARWIN_PLANET_DAY_NIGHT
// ★ 修复（防Bug回归）：沿球面位置分昼夜，不能用地形法线或固定环境光让背阳半球继续发亮。
// x夜间保底亮度、y晨昏半宽、z总开关、w数据图层；单个天气组件只在配置/图层变化时上传。
float4 _PlanetDayNight;
float PlanetDayAmount(float elevation)
{
    float width=max(.001,_PlanetDayNight.y);
    return smoothstep(-width,width,elevation);
}
float PlanetNightFactor(float3 localPosition,float3 lightDirectionWS)
{
    if(_PlanetDayNight.z<.5)return 1;
    float3 lightLocal=TransformWorldToObjectDir(lightDirectionWS);
    return lerp(_PlanetDayNight.x,1,PlanetDayAmount(dot(normalize(localPosition),lightLocal)));
}
#endif
