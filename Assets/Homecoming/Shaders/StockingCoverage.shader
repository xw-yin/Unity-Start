Shader "Homecoming/Stocking Coverage"
{
    Properties
    {
        [MainTexture] _BaseMap("Lace",2D)="white" {}
        [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
        [Normal] _BumpMap("Normal",2D)="bump" {}
        _BumpScale("Normal strength",Float)=1
        [Normal] _DetailNormalMap("Sock weave",2D)="bump" {}
        _DetailNormalMapScale("Weave strength",Float)=.35
        _Smoothness("Smoothness",Range(0,1))=.18
        [HideInInspector] _BootMask("Boot mask",Float)=0
        [HideInInspector] _BootBoundsMin("Boot minimum",Vector)=(0,0,0,0)
        [HideInInspector] _BootBoundsMax("Boot maximum",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
        TEXTURE2D(_DetailNormalMap);SAMPLER(sampler_DetailNormalMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST,_DetailNormalMap_ST;half4 _BaseColor;half _Smoothness,_BumpScale,_DetailNormalMapScale;float _BootMask;float4 _BootBoundsMin,_BootBoundsMax;float4x4 _BootWorldToLocal;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half4 tangentWS:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes input)
        {
            Varyings output=(Varyings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
            output.tangentWS=half4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());return output;
        }
        half3 SockNormal(Varyings input)
        {
            half3 n=normalize(input.normalWS);
            #if defined(_NORMALMAP)
            half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_BumpScale);
            #if defined(_DETAIL_NORMALMAP)
            half3 detail=UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap,sampler_DetailNormalMap,input.uv*_DetailNormalMap_ST.xy+_DetailNormalMap_ST.zw),_DetailNormalMapScale);
            normalTS=normalize(half3(normalTS.xy+detail.xy,normalTS.z*detail.z));
            #endif
            half3 tangent=normalize(input.tangentWS.xyz);half3 bitangent=cross(n,tangent)*input.tangentWS.w;
            n=normalize(tangent*normalTS.x+bitangent*normalTS.y+n*normalTS.z);
            #endif
            return n;
        }
        void Coverage(float3 world)
        {
            float3 p=mul(_BootWorldToLocal,float4(world,1)).xyz;
            if(_BootMask>.5&&all(p>_BootBoundsMin.xyz)&&all(p<_BootBoundsMax.xyz))clip(-1);
        }
        half4 Frag(Varyings input):SV_Target
        {
            Coverage(input.positionWS);
            InputData data=(InputData)0;data.positionWS=input.positionWS;data.positionCS=input.positionCS;data.normalWS=SockNormal(input);
            data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
            data.bakedGI=SampleSH(data.normalWS);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);data.shadowMask=half4(1,1,1,1);
            SurfaceData surface=(SurfaceData)0;surface.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
            surface.alpha=1;surface.smoothness=_Smoothness;surface.occlusion=1;surface.normalTS=half3(0,0,1);
            return UniversalFragmentPBR(data,surface);
        }
        half4 DepthFrag(Varyings input):SV_Target { Coverage(input.positionWS);return 0; }
        float3 _LightDirection,_LightPosition;
        Varyings ShadowVert(Attributes input)
        {
            Varyings output=Vert(input);
            float3 direction=_LightDirection;
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
            direction=normalize(_LightPosition-output.positionWS);
            #endif
            output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,normalize(output.normalWS),direction));
            #if UNITY_REVERSED_Z
            output.positionCS.z=min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE);
            #else
            output.positionCS.z=max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE);
            #endif
            return output;
        }
        half4 NormalsFrag(Varyings input):SV_Target { Coverage(input.positionWS);return half4(SockNormal(input),0); }
        ENDHLSL
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma shader_feature_local_fragment _NORMALMAP
            #pragma shader_feature_local_fragment _DETAIL_NORMALMAP
            ENDHLSL
        }
        Pass
        {
            Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _NORMALMAP
            #pragma shader_feature_local_fragment _DETAIL_NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
}
