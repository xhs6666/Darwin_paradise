Shader "DarwinPark/PlanetCloudVolume"
{
 Properties {
 _BottomRadius("Cloud base",Float)=350
 _TopRadius("Cloud top",Float)=355
 _Coverage("Coverage",Range(0,1))=.48
 _Frequency("Weather scale",Float)=5
 _Detail("Cloud detail",Float)=28
 _Density("Density",Float)=1.7
 _Steps("Ray steps",Float)=12
 }
 SubShader {
 Tags {"RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline"}
 // ★ 修复（防Bug回归）：用背面代理与场景深度积分，避免前壳与山峰相交形成硬三角切口。
 Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
 Pass {
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _BottomRadius,_TopRadius,_Coverage,_Frequency,_Detail,_Density,_Steps;
 CBUFFER_END
 struct A{float4 p:POSITION;};
 struct V{float4 p:SV_POSITION;float3 local:TEXCOORD0;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.local=i.p.xyz;return o;}
 float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
 float noise(float3 p){float3 a=floor(p),f=frac(p); f=f*f*(3-2*f);return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);}
 float fbm(float3 p){return noise(p)*.57+noise(p*2.03+7.1)*.28+noise(p*4.1+13.5)*.15;}
 float density(float3 p){
  float3 d=normalize(p);
  // ★ 新增：球面三维采样无经线接缝；低频天气场经旋流扭曲，细节侵蚀云体边缘。
  float swirl=fbm(d*2.7+19)*1.8;
  float3 q=d*_Frequency+float3(d.z,0,-d.x)*swirl;
  float weather=fbm(q);
  float detail=fbm(d*_Detail+weather*4);
  float cloud=smoothstep(1-_Coverage,1-_Coverage+.17,weather+(detail-.5)*.3);
  float h=saturate((length(p)-_BottomRadius)/max(.01,_TopRadius-_BottomRadius));
  return cloud*smoothstep(0,.2,h)*(1-smoothstep(.65,1,h));
 }
 float2 sphere(float3 o,float3 d,float r){float b=dot(o,d),c=dot(o,o)-r*r;float h=b*b-c;if(h<0)return float2(-1,-1);h=sqrt(h);return float2(-b-h,-b+h);}
 half4 frag(V i):SV_Target{
  float3 origin=TransformWorldToObject(GetCameraPositionWS());
  float3 dir=normalize(i.local-origin);
  float2 outer=sphere(origin,dir,_TopRadius);
  float2 inner=sphere(origin,dir,_BottomRadius);
  float start=max(0,outer.x),end=outer.y;
  if(inner.x>start)end=min(end,inner.x);
  float2 uv=GetNormalizedScreenSpaceUV(i.p);
  float depth=SampleSceneDepth(uv);
  #if !UNITY_REVERSED_Z
   depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
  #endif
  float3 scene=TransformWorldToObject(ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP));
  end=min(end,length(scene-origin));
  float stride=min((end-start)/_Steps,(_TopRadius-_BottomRadius)*.65);
  if(stride<=0)return 0;
  float3 light=normalize(TransformWorldToObjectDir(GetMainLight().direction));
  float alpha=0; float3 color=0;
  [loop]for(int n=0;n<(int)_Steps;n++){
   float3 p=origin+dir*(start+(n+.5)*stride);
   float den=density(p);
   float shadow=density(p+light*(_TopRadius-_BottomRadius)*.65);
   float sunlight=smoothstep(-.15,.45,dot(normalize(p),light));
   float shade=lerp(.68,1.15,exp(-shadow*1.2))*lerp(.3,1,sunlight);
   float a=1-exp(-den*_Density*stride/max(.01,_TopRadius-_BottomRadius));
   color+=(1-alpha)*a*float3(.94,.97,1)*shade;
   alpha+=(1-alpha)*a;
   if(alpha>.96)break;
  }
  return half4(color,alpha);
 }
 ENDHLSL
 }
 }
}
