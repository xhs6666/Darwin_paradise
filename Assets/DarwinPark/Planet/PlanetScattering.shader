Shader "DarwinPark/PlanetScattering"
{
 Properties {
 _SurfaceRadius("Sea radius",Float)=340
 _OuterRadius("Atmosphere radius",Float)=375
 _Density("Optical density",Float)=.08
 _ScatterColor("Rayleigh color",Color)=(.2,.52,1,1)
 }
 SubShader {
 Tags {"RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline"}
 // ★ 修复（防Bug回归）：背面代理覆盖整条视线，近距离进入气壳时仍可渲染；遮挡由真实深度截断。
 Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma target 3.5
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _SurfaceRadius,_OuterRadius,_Density;float4 _ScatterColor;
 CBUFFER_END
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 local:TEXCOORD0;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.local=i.p.xyz;return o;}
 half4 frag(V i):SV_Target{
  float3 origin=TransformWorldToObject(GetCameraPositionWS()),ray=normalize(i.local-origin);
  float b=dot(origin,ray),c=dot(origin,origin),h=b*b-c+_OuterRadius*_OuterRadius;
  if(h<=0)return 0;
  float start=max(0,-b-sqrt(h)),end=-b+sqrt(h);
  float ground=b*b-c+_SurfaceRadius*_SurfaceRadius;
  if(ground>0 && -b-sqrt(ground)>0)end=min(end,-b-sqrt(ground));
  float2 uv=GetNormalizedScreenSpaceUV(i.p);
  float depth=SampleSceneDepth(uv);
  #if !UNITY_REVERSED_Z
   depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
  #endif
  float3 scene=TransformWorldToObject(ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP));
  end=min(end,length(scene-origin));
  float step=max(0,end-start)/8;
  float scale=max(.1,(_OuterRadius-_SurfaceRadius)*.18);
  float optical=0;float3 light=normalize(TransformWorldToObjectDir(GetMainLight().direction));
  // ★ 新增：沿视线积分指数密度；外缘路径长度趋零，避免旧 Fresnel 球壳出现硬蓝圈。
  [unroll]for(int n=0;n<8;n++){
   float3 p=origin+ray*(start+(n+.5)*step);
   float daylight=smoothstep(-.25,.4,dot(normalize(p),light));
   optical+=exp(-max(0,length(p)-_SurfaceRadius)/scale)*step/scale*lerp(.12,1,daylight);
  }
  float alpha=saturate(1-exp(-optical*_Density));
  return half4(_ScatterColor.rgb*alpha,alpha);
 }
 ENDHLSL
 }
 }
}
