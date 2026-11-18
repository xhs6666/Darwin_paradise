Shader "DarwinPark/PlanetOcean"
{
 Properties {
  _ShallowColor("Shallow water", Color)=(.12,.62,.82,1)
  _DeepColor("Deep water", Color)=(.015,.13,.32,1)
  _LayerMode("Layer",Float)=0
  _ClimateReady("Climate ready",Float)=0
  _ClimateMap("Climate",2D)="gray"{}
  _OreMap("Ore",2D)="black"{}
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
  _FreshWater("Fresh water surface",Float)=0
  [HideInInspector] _OceanOptics("Ocean optics",Float)=0
  [HideInInspector] _WaterDepthWrite("Depth write",Float)=1
  [HideInInspector] _WaterDstBlend("Destination blend",Float)=0
  _Visibility("Visibility distance",Float)=14
  _ShelfDepth("Shelf depth",Float)=3
  [HideInInspector] _SeaRadius("Sea radius",Float)=300
  _WaveStrength("Wave strength",Range(0,.25))=.065
  _RippleScale("Ripple texture scale",Float)=3
  _RippleDetail("Ripple texture detail",Range(0,1))=.65
 }
 SubShader {
 Tags {"RenderType"="Opaque" "Queue"="Geometry+1" "RenderPipeline"="UniversalPipeline"}
 Pass {
 Blend One [_WaterDstBlend]
 ZWrite [_WaterDepthWrite] Cull [_Cull]
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 #include "Terrain/PopulationRangeOverlay.hlsl"
 #include "Terrain/PlanetDayNight.hlsl"
 struct A {float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float3 current:TEXCOORD1;};
 struct V {float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 waterBed:TEXCOORD2; float3 positionWS:TEXCOORD3; float3 current:TEXCOORD4;};
 CBUFFER_START(UnityPerMaterial)
 float4 _ShallowColor,_DeepColor; float _LayerMode,_ClimateReady,_FreshWater;
 float _OceanOptics,_Visibility,_ShelfDepth,_WaveStrength,_SeaRadius,_RippleScale,_RippleDetail;
 CBUFFER_END
 TEXTURE2D(_ClimateMap); SAMPLER(sampler_ClimateMap);
 TEXTURE2D(_OreMap); SAMPLER(sampler_OreMap);
 V vert(A i) { V o; o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.positionOS=i.positionOS.xyz; o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.waterBed=i.color.rg;o.current=i.current;return o; }

 float3 CurrentRipples(float3 p,float3 flow)
 {
  float3 crossFlow = cross(normalize(p),flow);
  float bend=sin(dot(p,float3(.82,-.32,.91)));
  float swell=sin(dot(p,float3(-.41,.89,.47))+bend);
  float3 q=p+crossFlow*(.29*bend)+flow*(.14*swell);
  float curl=sin(dot(p,float3(.91,2.91,-2.20)));
  float wave=sin(dot(q,float3(5.91,1.87,-4.45))+curl*1.4);
  float crossWave=sin(dot(q,float3(-2.27,5.09,3.37))-curl);
  float highlight=1-smoothstep(.05,.47,abs(wave+.36*crossWave));
  highlight*=smoothstep(-.6,.75,curl*crossWave+swell*.42);
  return float3(wave*crossWave,curl,highlight);
 }

 float3 FreshRipples(V i)
 {
  // Cross-fade two advecting phases so the current stays continuous when the animation wraps.
  float footprint=max(length(ddx(i.positionOS)),length(ddy(i.positionOS)));
  float visibility=1-smoothstep(.14,.54,footprint);
  if(visibility<=0)return 0;
  float phase=frac(_Time.y*.28);
  float other=frac(phase+.5);
  float3 a=CurrentRipples(i.positionOS-i.current*(phase*3.57),i.current);
  float3 b=CurrentRipples(i.positionOS-i.current*(other*3.57),i.current);
  return lerp(a,b,abs(2*phase-1))*visibility;
 }

 // ★ 修复（防Bug回归）：河湖和海面共享程序化三维纹理及动画，不为每段河流新增更新脚本或贴图接缝。
 float3 WaterNormal(V i,float3 radial)
 {
  // ★ 新增：局部三维波纹随星球旋转，无贴图接缝，且不会移动物理水位。
  float time = _Time.y;
  float3 p = i.positionOS;
  float3 wave = float3(sin(dot(p,float3(.63,.28,.17))+time*1.6),
                      sin(dot(p,float3(.18,.47,.59))-time*1.2),
                      sin(dot(p,float3(.38,-.29,.53))+time*.8));
  // ★ 新增：细波纹叠加慢波，导数过滤分别限制细节频率，避免只有大片平滑高光而看不见纹理。
  float footprint = max(length(ddx(p)),length(ddy(p)));
  float3 q=p*_RippleScale;
  float3 detail=float3(sin(dot(q,float3(.81,.31,.22))+time*2.8),sin(dot(q,float3(.24,.73,.46))-time*2.1),sin(dot(q,float3(.39,-.42,.67))+time*2.4));
  wave+=detail*_RippleDetail*(1-smoothstep(.2,.8,footprint*_RippleScale));
  wave = TransformObjectToWorldDir(wave,false);
  // ★ 新增：远处波纹逐渐收敛，避免全球视角出现高频闪烁。
  float waveStrength = _WaveStrength * (1-smoothstep(.4,2,footprint));
  float3 normal = normalize(radial + waveStrength*(wave-radial*dot(radial,wave)));
  return normal;
 }
 half4 FreshWaterSurface(V i)
 {
  float3 radial=normalize(i.normalWS),sun=GetMainLight().direction;
  float3 normal=WaterNormal(i,radial),view=GetWorldSpaceNormalizeViewDir(i.positionWS);
  float night=PlanetNightFactor(i.positionOS,sun),light=saturate(dot(radial,sun));
  float shelf=smoothstep(0,max(.01,_ShelfDepth),i.waterBed.x);
  float3 water=lerp(_ShallowColor.rgb,_DeepColor.rgb,shelf);
  // ★ 新增：波纹明暗形成连续流动纹理，远处由导数滤波收敛，物理水位和河岸保持静止。
  float ripple=dot(normal-radial,sun);
  float fresnel=pow(1-saturate(abs(dot(normal,view))),5);
  float glint=pow(saturate(dot(normal,SafeNormalize(sun+view))),80)*light;
  float3 color=(water*lerp(.55,1.12,light)*(1+ripple*2)+float3(.16,.32,.4)*fresnel*.3+glint*.12)*night;
  return half4(ApplyPopulationRange(color,i.positionOS),1);
 }

 half4 OceanSurface(V i, bool frontFace, bool freshWater)
 {
  // ★ 新增：从海底高度取得海岸带，从场景深度取得海面到海床或生物的实际视线距离。
  float3 radial = normalize(i.normalWS);
  if(freshWater)radial=normalize(lerp(TransformObjectToWorldDir(normalize(i.positionOS)),radial,.4));
  float3 sun = GetMainLight().direction;
  float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
  float3 cameraOS = TransformWorldToObject(GetCameraPositionWS());
  bool underwater = freshWater ? !frontFace : dot(cameraOS,cameraOS) < _SeaRadius*_SeaRadius;
  if(!freshWater) clip(frontFace == underwater ? -1 : 1);
  float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
  float rawDepth = SampleSceneDepth(screenUV);
  #if !UNITY_REVERSED_Z
  rawDepth = lerp(UNITY_NEAR_CLIP_VALUE,1,rawDepth);
  #endif
  float3 behind = ComputeWorldSpacePosition(screenUV,rawDepth,UNITY_MATRIX_I_VP);
  float path = distance(behind,i.positionWS);
  if (underwater) path = distance(GetCameraPositionWS(),i.positionWS);
  // ★ 适配：淡水岸边的光学深度受真实床深限制，天空与深远背景不会把浅河画成实心。
  else if(freshWater) path = min(path,i.waterBed.x/max(.12,abs(dot(radial,view))));
  float opacity = 1-exp2(-2.3 * path / max(.01,_Visibility));

  float3 normal = WaterNormal(i,radial);
  float3 currentRipples=0;
  if(freshWater)
  {
   currentRipples=FreshRipples(i)*saturate(length(i.current)*2);
   float3 stream=TransformObjectToWorldDir(i.current,false);
   normal=normalize(normal+.10*(stream*currentRipples.y+cross(radial,stream)*currentRipples.x));
  }
  float rippleLight = dot(normal-radial,sun);
  normal *= underwater ? -1 : 1;
  float fresnel = .025 + .8 * pow(1-saturate(dot(normal,view)),5);
  float light = saturate(dot(radial,sun));
  float night = PlanetNightFactor(i.positionOS,sun);
  float shelf = smoothstep(0,max(.01,_ShelfDepth),i.waterBed.x);
  float3 water = lerp(_ShallowColor.rgb,_DeepColor.rgb,shelf)*lerp(.55,1.12,light)*night;
  if(freshWater) water*=1+.19*currentRipples.x+.11*currentRipples.y;
  float3 reflection = lerp(water,float3(.22,.43,.58)*night,.55) * (1+rippleLight*.8);
  float3 halfDir = SafeNormalize(sun+view);
  float glint = underwater ? 0 : .55*pow(saturate(dot(normal,halfDir)),100)*light*night;
  float alpha = saturate(opacity+(1-opacity)*fresnel);
  float3 surface=lerp(water,reflection,fresnel);
  float shoreline=freshWater?smoothstep(0,.42,i.waterBed.y):1;
  if(freshWater&&!underwater)
  {
   // Fragmented foam is restricted to the shallow banks and moves with the channel.
   float shore=smoothstep(.015,.09,i.waterBed.x)*(1-smoothstep(.15,.6,i.waterBed.x));
   float foam=shore*smoothstep(.17,.70,currentRipples.z);
   surface=lerp(surface,float3(.54,.77,.76)*night*lerp(.40,1,light),foam*.64);
   surface+=float3(.12,.25,.31)*currentRipples.z*night*lerp(.45,1,light);
   alpha=saturate(alpha+foam*.26);
  }
  alpha*=shoreline;
  return half4(ApplyPopulationRange(surface,i.positionOS)*alpha+glint*shoreline,alpha);
 }

 half4 frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC):SV_Target {
  if(_OceanOptics>.5 && _LayerMode<.5) return OceanSurface(i,IS_FRONT_VFACE(face,true,false),_FreshWater>.5);
  // ★ 适配：淡水在数据图层保留旧的纯色标记，不随夜面变暗，也不采样海洋数据贴图。
  if(_FreshWater>.5&&_PlanetDayNight.w>.5)return half4(_ShallowColor.rgb,1);
  if(_FreshWater>.5) return FreshWaterSurface(i);
  float3 d=normalize(i.positionOS);
  float2 uv=float2(atan2(d.x,d.z)/6.2831853+.5,asin(clamp(d.y,-1,1))/3.14159265+.5);
  float3 color=lerp(_ShallowColor.rgb,_DeepColor.rgb,smoothstep(0,_OceanOptics>.5?_ShelfDepth:1,i.waterBed.x));
  if(_LayerMode>2.5) return half4(lerp(float3(.02,.02,.02),float3(1,.85,0),SAMPLE_TEXTURE2D(_OreMap,sampler_OreMap,uv).r),1);
  if(_LayerMode>1.5 && _ClimateReady>.5) color=SAMPLE_TEXTURE2D(_ClimateMap,sampler_ClimateMap,uv).rgb;
  float3 sun=GetMainLight().direction;float light=saturate(dot(normalize(i.normalWS),sun));
  // ★ 修复（防Bug回归）：普通观察显示真实夜半球，数据图层维持原亮度，避免读图困难。
  float night=_LayerMode<.5?PlanetNightFactor(i.positionOS,sun):1;
  // ★ 适配：范围跨海岸时水面也显示同一合并区域，避免被海面盖掉形成缺口。
  return half4(ApplyPopulationRange(color*lerp(.55,1.12,light)*night,i.positionOS),1);
 }
 ENDHLSL
 }
  // ★ 修复（防Bug回归）：天气积分需要地形/海面的真实深度，深度预通道也必须绘制这些不透明表面。
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/DepthNormals"
}
}
