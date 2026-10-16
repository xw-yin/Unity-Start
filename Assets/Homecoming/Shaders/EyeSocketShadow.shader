Shader "Homecoming/Eye Socket Shadow" {
 Properties { _BaseMap("Original shadow mask",2D)="white" {} _Strength("Intensity",Range(0,1))=.55 _Exponent("Exponent",Float)=2.2 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
  Pass {
   Name "EyeShadow" Tags {"LightMode"="UniversalForward"}
   Blend DstColor Zero ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;half _Strength,_Exponent;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   V Vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
   half4 Frag(V i):SV_Target {half mask=pow(saturate(1-SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).r),_Exponent)*_Strength;return half4(1-mask,1-mask,1-mask,1);}
   ENDHLSL
  }
 }
}
