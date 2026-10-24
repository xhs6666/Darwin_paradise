Shader "DarwinPark/URP_PlanetSurface"
{
    Properties
    {
        // ★ 新增：图三风格地形配色与高度阈值独立配置，气候图层仍使用真实气候数据。
        _DetailScale ("Surface detail scale", Float) = 85
        _DetailStrength ("Surface detail strength", Range(0,1)) = 0.35
        _LowlandColor ("Lowland", Color) = (0.43, 0.68, 0.30, 1)
        _HillColor ("Foothills", Color) = (0.72, 0.72, 0.39, 1)
        _RockColor ("Mountain rock", Color) = (0.60, 0.32, 0.16, 1)
        _PeakColor ("Peak", Color) = (0.90, 0.93, 0.94, 1)
        _ElevationBands ("Elevation bands", Vector) = (0.08, 0.23, 0.36, 0.62)
        _SeaLevel ("Sea Level", Float) = 0.45
        _PlanetRadius ("Planet Radius", Float) = 300.0
        _HeightScale ("Height Scale", Float) = 90.0
        _MaxHeight ("Max Normalized Height", Float) = 1.0
        _LayerMode ("Layer Mode", Float) = 0 
        _OreMap ("Ore Map", 2D) = "black" {} 
        _ClimateMap ("Climate Map", 2D) = "gray" {} 
        _ClimateReady ("Climate Ready", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off 
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "Terrain/PlanetSurfaceDetail.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewDirWS : TEXCOORD1; float3 positionWS : TEXCOORD2; float3 positionOS : TEXCOORD3; };

            CBUFFER_START(UnityPerMaterial)
                float4 _LowlandColor, _HillColor, _RockColor, _PeakColor, _ElevationBands;
                float _DetailScale, _DetailStrength;
                float _SeaLevel; 
                float _PlanetRadius;
                float _HeightScale;
                float _MaxHeight;
                float _LayerMode;
                float _ClimateReady;
            CBUFFER_END

            TEXTURE2D(_OreMap); SAMPLER(sampler_OreMap);
            TEXTURE2D(_ClimateMap); SAMPLER(sampler_ClimateMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
                output.positionWS = vertexInput.positionWS;
                // ★ 修复：UV 不能在顶点里算完再插值。等距柱状投影在 x=0,z<0
                // 的经线处 u 从 ~1 跳回 ~0，跨接缝的三角形会把整张气候图
                // 横向压缩进一条窄带，形成一条笔直的“断层”。改为把物体空间
                // 坐标传到片元，在片元里逐像素求 UV。
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            float2 SphereUVFromDirection(float3 dir)
            {
                dir = normalize(dir);
                float u = atan2(dir.x, dir.z) / (2.0 * 3.14159265) + 0.5;
                float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.14159265 + 0.5;
                return float2(u, v);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                if (dot(normalWS, viewDirWS) < 0.0) normalWS = -normalWS;

                float2 sphereUV = SphereUVFromDirection(input.positionOS);

                // ★ 核心修复：使用动态传入的半径和高度倍数
                // ★ 修复（防Bug回归）：高度与地形生成共用本地空间，星球移动不应把整片陆地变成高山雪地。
                float distanceToCenter = length(input.positionOS);
                float heightRatio = (distanceToCenter - _PlanetRadius) / _HeightScale; 
                
                float seaLevel = _SeaLevel;
                float3 surfaceColor = float3(0,0,0);
                bool isMapLayer = false; 

                if (_LayerMode < 0.5) // === 0: 默认地形 ===
                {
                    if (heightRatio < seaLevel) {
                        // 海洋
                        surfaceColor = lerp(float3(0.1, 0.3, 0.6), float3(0.02, 0.05, 0.2), smoothstep(seaLevel, 0.0, heightRatio));
                    } else {
                        // ★ 修复（防Bug回归）：默认地形应呈现山系/低地层次，不能用气候纹理把所有山地刷成同一绿色。
                        float landH = saturate((heightRatio - seaLevel) / max(0.01, _MaxHeight - seaLevel));
                        // ★ 适配：连续三维微地貌为平原、山脊增加地表层次；只修饰颜色，不改变真实气候与碰撞。
                        float detail = SurfaceDetail(normalize(input.positionOS), _DetailScale);
                        float reliefH = saturate(landH + (detail - .5) * _DetailStrength * .22);
                        surfaceColor = lerp(_LowlandColor.rgb, _HillColor.rgb,
                            smoothstep(_ElevationBands.x, _ElevationBands.y, reliefH));
                        surfaceColor = lerp(surfaceColor, _RockColor.rgb,
                            smoothstep(_ElevationBands.y, _ElevationBands.z, reliefH));
                        surfaceColor = lerp(surfaceColor, _PeakColor.rgb,
                            smoothstep(_ElevationBands.z, _ElevationBands.w, reliefH));
                        surfaceColor *= lerp(1.0 - _DetailStrength, 1.0 + _DetailStrength * .25, detail);
                        float beach = 1.0 - smoothstep(0.0, 0.012, heightRatio - seaLevel);
                        surfaceColor = lerp(surfaceColor, float3(.80,.77,.52), beach*.55);
                    }
                }
                else if (_LayerMode < 1.5) // === 1: 高程图 ===
                {
                    isMapLayer = true;
                    if (heightRatio < seaLevel) {
                        surfaceColor = lerp(float3(0.0, 0.0, 0.4), float3(0.1, 0.4, 0.8), heightRatio / seaLevel);
                    } else {
                        float t = saturate((heightRatio - seaLevel) / max(0.01, _MaxHeight - seaLevel));
                        // ★ 修复：旧版用 if/else 分四段硬切颜色，高程图会出现明显的等高线色带。
                        // 改成连续 lerp，山体起伏才能平滑显示。
                        float3 low = float3(0.2, 0.8, 0.2);      // 平原绿
                        float3 mid = float3(0.8, 0.8, 0.2);      // 丘陵黄
                        float3 high = float3(0.8, 0.4, 0.1);     // 高山橙
                        float3 peak = float3(1.0, 1.0, 1.0);     // 峰顶白
                        surfaceColor = lerp(low, mid, smoothstep(0.08, 0.30, t));
                        surfaceColor = lerp(surfaceColor, high, smoothstep(0.30, 0.62, t));
                        surfaceColor = lerp(surfaceColor, peak, smoothstep(0.68, 0.92, t));
                    }
                }
                else if (_LayerMode < 2.5) // === 2: 真实气候图 ===
                {
                    isMapLayer = true;
                    if (_ClimateReady > 0.5) {
                        // ★ 修复：气候图已经在 CPU 端平滑混合为连续颜色，Shader 不再用
                        // if/else 硬分类，从根源上消除色块断层。
                        surfaceColor = SAMPLE_TEXTURE2D(_ClimateMap, sampler_ClimateMap, sphereUV).rgb;
                    } else {
                        // 气候图尚未生成时的程序化兜底：连续色带，绝不能输出全白。
                        float latAbs = abs(normalize(input.positionOS).y);
                        float temp = lerp(32.0, -35.0, pow(1.0 - latAbs, 1.2));
                        temp -= saturate((heightRatio - seaLevel) / max(0.01, _MaxHeight - seaLevel)) * 25.0;
                        float hum = lerp(80.0, 40.0, latAbs);
                        float cold = 1.0 - smoothstep(-8.0, 6.0, temp);
                        float wet = smoothstep(30.0, 75.0, hum);
                        surfaceColor = lerp(float3(0.42, 0.58, 0.32), float3(0.18, 0.42, 0.22), wet);
                        surfaceColor = lerp(surfaceColor, float3(0.80, 0.71, 0.48), (1.0 - wet) * smoothstep(10.0, 28.0, temp));
                        surfaceColor = lerp(surfaceColor, float3(0.92, 0.94, 0.98), cold);
                    }
                }
                else // === 3: 矿石图 ===
                {
                    isMapLayer = true;
                    float oreAmount = SAMPLE_TEXTURE2D(_OreMap, sampler_OreMap, sphereUV).r;
                    // 确保底色纯黑，矿石纯黄
                    surfaceColor = lerp(float3(0.02, 0.02, 0.02), float3(1.0, 0.85, 0.0), oreAmount);
                }

                Light mainLight = GetMainLight();
                float ndl = saturate(dot(normalWS, mainLight.direction));
                
                if (isMapLayer) {
                    // ★ 修复：数据图层也叠加轻微山体阴影，山脉/盆地不会因“无光照”而看起来是平的。
                    float mapShade = 0.78 + 0.22 * ndl;
                    return half4(surfaceColor * mapShade, 1.0);
                }

                // 将光照的乘算系数从 1.5 降到 1.0，防止过曝
                // ★ 修复（防Bug回归）：URP 光方向指向光源，旧符号反转把受光面变成黑坑。
                // 地图风格保留可读的环境照明，同时让山脊坡度清楚可辨。
                float3 litColor = surfaceColor * (0.60 + mainLight.color * ndl * 0.55);
                return half4(litColor, 1.0);
            }
            ENDHLSL
        }
     // ★ 修复（防Bug回归）：天气积分需要地形/海面的真实深度，深度预通道也必须绘制这些不透明表面。
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/DepthNormals"
}
}