Shader "DarwinPark/PlanetAtmosphere"
{
    Properties
    {
        _AtmosphereColor ("Atmosphere Color", Color) = (0.3, 0.6, 1.0, 1.0)
        _RimPower ("Rim Power", Float) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha 
        ZWrite Off 
        Cull Off // ★ 改为双面，保证任何角度都能看到大气光晕

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _AtmosphereColor;
                float _RimPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                float3 cameraPos = GetCameraPositionWS();
                output.viewDirWS = normalize(cameraPos - vertexInput.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                // 菲涅尔边缘发光
                float rim = 1.0 - saturate(abs(dot(normalWS, viewDirWS)));
                rim = pow(rim, _RimPower);
                
                // 乘一个基础亮度，确保边缘够亮
                float alpha = rim * _AtmosphereColor.a * 1.5;
                alpha = saturate(alpha);
                
                return half4(_AtmosphereColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}