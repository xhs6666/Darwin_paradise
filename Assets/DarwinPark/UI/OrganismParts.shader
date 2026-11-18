Shader "DarwinPark/OrganismParts"
{
 Properties{_BaseColor("Status tint",Color)=(1,1,1,1) _BodyColor("Far body color",Color)=(.6,.7,.5,1) _FarBody("Simplified body",Float)=0}
 SubShader{
 Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
 Pass{
 ZWrite On Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 // ★ 新增：共享部件网格的个体颜色作为实例参数提交，避免属性块令远处相同模型逐个绘制。
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float3 normal:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 UNITY_INSTANCING_BUFFER_START(BodyProps)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyColor)
 UNITY_DEFINE_INSTANCED_PROP(float,_FarBody)
 UNITY_INSTANCING_BUFFER_END(BodyProps)
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);o.p=TransformObjectToHClip(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.color=i.color;return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);float4 tint=UNITY_ACCESS_INSTANCED_PROP(BodyProps,_BaseColor);float3 color=lerp(i.color.rgb,UNITY_ACCESS_INSTANCED_PROP(BodyProps,_BodyColor).rgb,UNITY_ACCESS_INSTANCED_PROP(BodyProps,_FarBody));color=lerp(color,tint.rgb,saturate(length(tint.rgb-1)));float light=saturate(dot(normalize(i.normal),GetMainLight().direction));return half4(color*(.4+light*.6),1);}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
