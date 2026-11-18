Shader "DarwinPark/GlobeMinimap"
{
 Properties
 {
  [PerRendererData] _MainTex("Surface atlas",2D)="white" {}
  _Color("Tint",Color)=(1,1,1,1)
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False"}
  Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct A {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct V {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;};
   sampler2D _MainTex;float4 _Color,_ClipRect;float4x4 _GlobeRotation;
   V vert(A i){V o;o.local=i.vertex;o.position=UnityObjectToClipPos(i.vertex);o.color=i.color*_Color;o.uv=i.uv;return o;}
   // ★ 新增：仅在132像素导航圆内投影共享地表图，替代主线程每次重算2万个像素并上传纹理。
   fixed4 frag(V i):SV_Target
   {
    float2 p=i.uv*2-1;float radius=dot(p,p);clip(1-radius);
    float3 normal=float3(p,-sqrt(max(0,1-radius)));float3 direction=mul((float3x3)_GlobeRotation,normal);
    float2 uv=float2(frac(atan2(direction.x,direction.z)/6.28318530718+.5),asin(clamp(direction.y,-1,1))/3.14159265359+.5);
    fixed4 color=tex2D(_MainTex,uv)*i.color;color.rgb*=.35+.65*max(0,dot(normal,normalize(float3(-.4,.6,-1))));
    #ifdef UNITY_UI_CLIP_RECT
    color.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
    #endif
    return color;
   }
   ENDCG
  }
 }
}
