Shader "DarwinPark/SolarDisc"
{
    Properties
    {
        _DiscRadius("Solar radius (world units)", Float) = 7
        _RimColor("Solar rim", Color) = (1,.68,.28,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-30" "DisableBatching"="True" }
        Blend One One
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _RimColor;
            float _DiscRadius;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 disc : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 centerVS = TransformWorldToView(TransformObjectToWorld(float3(0,0,0)));
                output.disc = input.positionOS.xy * float2(16, 12);
                centerVS.xy += output.disc * _DiscRadius;
                output.positionCS = mul(UNITY_MATRIX_P, float4(centerVS, 1));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.disc);
                float aa = max(fwidth(radius), .02);
                float disc = 1 - smoothstep(1 - aa, 1 + aa, radius);
                float photosphere = sqrt(saturate(1 - radius * radius));
                float3 core = lerp(_RimColor.rgb * 1.45, float3(3.9, 3.7, 3.32), photosphere);
                float corona = .52 * exp2(-1.55 * radius) + .11 * exp2(-.48 * radius);
                corona *= 1 - smoothstep(7, 12, radius);
                float streak = .11 * exp2(-11 * abs(input.disc.y)) *
                    (1 - smoothstep(1, 16, abs(input.disc.x)));
                float3 glow = lerp(_RimColor.rgb, float3(1, .96, .83), .38) * corona;
                return half4(core * disc + glow + float3(1, .85, .61) * streak, 0);
            }
            ENDHLSL
        }
    }
}
