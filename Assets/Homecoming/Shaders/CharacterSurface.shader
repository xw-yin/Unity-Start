Shader "Homecoming/Character Surface"
{
 Properties {
  [MainTexture] _BaseMap("Source diffuse",2D)="white" {}
  [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  [Normal] _BumpMap("Source normal",2D)="bump" {}
  _BumpScale("Normal strength",Range(0,1))=.7
  _AmbientMap("Source skin AO / roughness",2D)="white" {}
  _UseSurfaceMap("Use skin surface channels",Float)=1
  [Normal] _DetailNormalMap("Source pore normal",2D)="bump" {}
  _DetailStrength("Pore strength",Range(0,1))=.15
  _DetailTiling("Pore tiling",Float)=8
  _Smoothness("Fallback smoothness",Range(0,1))=.5
  _SpecColor("Dielectric reflection",Color)=(.028,.028,.03,1)
  _OcclusionStrength("Source occlusion strength",Range(0,1))=.3
  _Wrap("Diffuse wrap",Range(0,.5))=.2
  _ScatterStrength("Skin diffuse softness",Range(0,1))=.35
  _ScatterColor("Terminator bleed",Color)=(.16,.05,.035,1)
  [Toggle(_CHARACTER_HAIR)] _Hair("Hair shading",Float)=0
  _ShiftMap("Source strand shift",2D)="gray" {}
  _ShiftTiling("Strand shift tiling",Vector)=(5,5,0,0)
  _ShiftScale("Strand shift",Float)=.3
  _ShiftOffset("Strand highlight offset",Float)=.22
  _HairSpecular("Strand reflection",Color)=(.075,.08,.085,1)
  _HairExponent("Strand lobe exponent",Float)=48
  _Cutoff("Source alpha threshold",Range(0,1))=.35
  [HideInInspector] _SrcBlend("Source blend",Float)=1
  [HideInInspector] _DstBlend("Destination blend",Float)=0
  [HideInInspector] _ZWrite("Depth write",Float)=1
  [HideInInspector] _AlphaToMask("MSAA coverage",Float)=1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Cull Off
  HLSLINCLUDE
  #define _SPECULAR_SETUP 1
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
  TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
  TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
  TEXTURE2D(_AmbientMap);SAMPLER(sampler_AmbientMap);
  TEXTURE2D(_DetailNormalMap);SAMPLER(sampler_DetailNormalMap);
  TEXTURE2D(_ShiftMap);SAMPLER(sampler_ShiftMap);
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseMap_ST,_ShiftTiling;half4 _BaseColor,_SpecColor,_ScatterColor,_HairSpecular;
  half _BumpScale,_UseSurfaceMap,_DetailStrength,_DetailTiling,_Smoothness,_OcclusionStrength,_Wrap,_ScatterStrength,_Hair,_ShiftScale,_ShiftOffset,_HairExponent,_Cutoff,_SrcBlend,_DstBlend,_ZWrite,_AlphaToMask;
  CBUFFER_END
  struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
  struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half3 vertexLighting:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
  Varyings Vert(Attributes v) {
   Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
   o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
   o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.tangentWS=half4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.vertexLighting=VertexLighting(o.positionWS,o.normalWS);return o;
  }
  void Cutout(float2 uv) {
   #if defined(_CHARACTER_HAIR)
   half alpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).a;
    #if defined(_HAIR_FRINGE)
    clip(alpha-.01h);clip(_Cutoff-alpha);
    #else
    clip(alpha-_Cutoff);
    #endif
   #endif
  }
  half3 SurfaceNormal(Varyings i,half facing) {
   half3 g=normalize(i.normalWS),t=normalize(i.tangentWS.xyz),b=cross(g,t)*i.tangentWS.w;
   half3 n=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
   #if !defined(_CHARACTER_HAIR)
   half3 d=UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap,sampler_DetailNormalMap,i.uv*_DetailTiling),_DetailStrength);n=normalize(half3(n.xy+d.xy,n.z*d.z));
   #endif
   return normalize(t*n.x+b*n.y+g*n.z)*facing;
  }
  // The wrap term is a bounded real-time approximation, not screen-space diffusion.
  // It changes direct diffuse response only and respects every light's shadows.
  half3 ExtraLight(Light light,InputData inputData,half3 geometric,half3 normal,half3 strand,half3 albedo,half shift) {
   half attenuation=light.distanceAttenuation*light.shadowAttenuation;
   #if defined(_CHARACTER_HAIR)
   half3 h=SafeNormalize(light.direction+inputData.viewDirectionWS);
   half3 a=normalize(strand+normal*shift),b=normalize(strand+normal*(shift-.14h));
   half x=saturate(1-dot(a,h)*dot(a,h)),y=saturate(1-dot(b,h)*dot(b,h));
   half front=saturate(dot(normal,light.direction));
   half lobes=pow(x,_HairExponent*.5h)+.35h*pow(y,_HairExponent*.25h);
   half3 reflection=_HairSpecular.rgb*lobes*sqrt(front);
   half3 transmission=albedo*.10h*saturate(-dot(normal,light.direction));
   return (reflection+transmission)*light.color*attenuation;
   #else
   half baseline=saturate(dot(normal,light.direction));
   half nl=dot(normalize(lerp(normal,geometric,.35h)),light.direction);
   half wrapped=saturate((nl+_Wrap)/(1+_Wrap));
   half diffuse=lerp(baseline,pow(wrapped,1.3h),_ScatterStrength);
   half bleed=wrapped*(1-wrapped)*_ScatterStrength;
   return albedo*((diffuse-baseline)+_ScatterColor.rgb*bleed)*light.color*attenuation;
   #endif
  }
  half3 ExtraLighting(InputData inputData,half3 geometric,half3 normal,half3 strand,half3 albedo,half shift) {
   half3 c=ExtraLight(GetMainLight(inputData.shadowCoord,inputData.positionWS,half4(1,1,1,1)),inputData,geometric,normal,strand,albedo,shift);
   #if defined(_ADDITIONAL_LIGHTS)
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++) {
     CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
     c+=ExtraLight(GetAdditionalLight(lightIndex,inputData.positionWS,half4(1,1,1,1)),inputData,geometric,normal,strand,albedo,shift);
    }
    #endif
    uint count=GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
     c+=ExtraLight(GetAdditionalLight(lightIndex,inputData.positionWS,half4(1,1,1,1)),inputData,geometric,normal,strand,albedo,shift);
    LIGHT_LOOP_END
   #endif
   return c;
  }
  half4 Frag(Varyings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {
   Cutout(i.uv);half facing=IS_FRONT_VFACE(face,1,-1);
   half3 geometric=normalize(i.normalWS)*facing,normal=SurfaceNormal(i,facing);
   InputData inputData=(InputData)0;inputData.positionWS=i.positionWS;inputData.positionCS=i.positionCS;inputData.normalWS=normal;inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);inputData.shadowCoord=TransformWorldToShadowCoord(i.positionWS);inputData.bakedGI=SampleSH(geometric);inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);inputData.shadowMask=half4(1,1,1,1);inputData.vertexLighting=i.vertexLighting;
   SurfaceData surface=(SurfaceData)0;surface.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;surface.alpha=1;surface.normalTS=half3(0,0,1);
   #if defined(_CHARACTER_HAIR)
    half hairAlpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a;
    #if defined(_HAIR_FRINGE)
    surface.alpha=saturate(hairAlpha/max(_Cutoff,.01h));
    #else
    surface.alpha=saturate((hairAlpha-_Cutoff)/max(fwidth(hairAlpha),.001h)+.5h);
    #endif
    surface.specular=_SpecColor.rgb;surface.smoothness=_Smoothness;surface.occlusion=1;
   #else
    half3 map=SAMPLE_TEXTURE2D(_AmbientMap,sampler_AmbientMap,i.uv).rgb;
    surface.specular=_SpecColor.rgb;surface.smoothness=lerp(_Smoothness,1-clamp(map.g*.5h+.12h,.22h,.8h),_UseSurfaceMap);surface.occlusion=lerp(1,map.r,_OcclusionStrength*_UseSurfaceMap);
   #endif
   half3 strand=normalize(cross(normal,normalize(i.tangentWS.xyz))*i.tangentWS.w);
   half shift=(SAMPLE_TEXTURE2D(_ShiftMap,sampler_ShiftMap,i.uv*_ShiftTiling.xy).r-.5h)*_ShiftScale+_ShiftOffset;
   surface.emission=ExtraLighting(inputData,geometric,normal,strand,surface.albedo,shift);
   return UniversalFragmentPBR(inputData,surface);
  }
  float3 _LightDirection,_LightPosition;
  Varyings ShadowVert(Attributes v) {
   Varyings o=Vert(v);float3 direction=_LightDirection;
   #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    direction=normalize(_LightPosition-o.positionWS);
   #endif
   o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,normalize(o.normalWS),direction));
   #if UNITY_REVERSED_Z
    o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
   #else
    o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
   #endif
   return o;
  }
  half4 DepthFrag(Varyings i):SV_Target {Cutout(i.uv);return 0;}
  half4 NormalFrag(Varyings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {Cutout(i.uv);return half4(SurfaceNormal(i,IS_FRONT_VFACE(face,1,-1)),0);}
  ENDHLSL
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] AlphaToMask [_AlphaToMask]
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma shader_feature_local _CHARACTER_HAIR
   #pragma shader_feature_local _HAIR_FRINGE
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ColorMask 0
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex ShadowVert
   #pragma fragment DepthFrag
   #pragma shader_feature_local _CHARACTER_HAIR
   #pragma shader_feature_local _HAIR_FRINGE
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   ENDHLSL
  }
  Pass {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask R
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment DepthFrag
   #pragma shader_feature_local _CHARACTER_HAIR
   #pragma shader_feature_local _HAIR_FRINGE
   ENDHLSL
  }
  Pass {
   Name "DepthNormals" Tags {"LightMode"="DepthNormals"}
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment NormalFrag
   #pragma shader_feature_local _CHARACTER_HAIR
   #pragma shader_feature_local _HAIR_FRINGE
   ENDHLSL
  }
 }
}
