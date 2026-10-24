Shader "DarwinPark/PlanetOcean"
{
 Properties {
  _ShallowColor("Shallow water", Color)=(.12,.62,.82,1)
  _DeepColor("Deep water", Color)=(.015,.13,.32,1)
  _LayerMode("Layer",Float)=0
  _ClimateReady("Climate ready",Float)=0
  _ClimateMap("Climate",2D)="gray"{}
  _OreMap("Ore",2D)="black"{}
 }
 SubShader {
 Tags {"RenderType"="Opaque" "Queue"="Geometry+1" "RenderPipeline"="UniversalPipeline"}
 Pass {
 ZWrite On Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A {float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR;};
 struct V {float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0; float3 normalWS:TEXCOORD1; float depth:TEXCOORD2;};
 CBUFFER_START(UnityPerMaterial)
 float4 _ShallowColor,_DeepColor; float _LayerMode,_ClimateReady;
 CBUFFER_END
 TEXTURE2D(_ClimateMap); SAMPLER(sampler_ClimateMap);
 TEXTURE2D(_OreMap); SAMPLER(sampler_OreMap);
 V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.positionOS=i.positionOS.xyz; o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.depth=i.color.r;return o; }
 half4 frag(V i):SV_Target {
  float3 d=normalize(i.positionOS);
  float2 uv=float2(atan2(d.x,d.z)/6.2831853+.5,asin(clamp(d.y,-1,1))/3.14159265+.5);
  float3 color=lerp(_ShallowColor.rgb,_DeepColor.rgb,smoothstep(0,1,i.depth));
  if(_LayerMode>2.5) return half4(lerp(float3(.02,.02,.02),float3(1,.85,0),SAMPLE_TEXTURE2D(_OreMap,sampler_OreMap,uv).r),1);
  if(_LayerMode>1.5 && _ClimateReady>.5) color=SAMPLE_TEXTURE2D(_ClimateMap,sampler_ClimateMap,uv).rgb;
  float light=saturate(dot(normalize(i.normalWS),GetMainLight().direction));
  return half4(color*lerp(.55,1.12,light),1);
 }
 ENDHLSL
 }
  // ★ 修复（防Bug回归）：天气积分需要地形/海面的真实深度，深度预通道也必须绘制这些不透明表面。
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/DepthNormals"
}
}
