Shader "DarwinPark/PlanetSurface"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off // ★ 关键：关闭背面剔除，解决星球缺口

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 基础顶点色（代码里根据高度生成的）
                fixed4 col = i.color;
                
                // 如果顶点色是白的，就根据高度给个颜色兜底
                if(col.r > 0.95 && col.g > 0.95 && col.b > 0.95) {
                     col = fixed4(0.4, 0.6, 0.4, 1); // 陆地绿
                }

                // 简单的方向光照
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                // Cull Off 会导致法线翻转，取绝对值确保两面都能受光
                float ndl = max(0.0, abs(dot(normalize(i.normal), lightDir)));
                
                // ★ 增强基础环境光，防止背面死黑
                col.rgb *= (0.3 + 0.7 * ndl); 
                
                return col;
            }
            ENDCG
        }
    }
}