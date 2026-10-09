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
        _SkinMap("Original skin beneath hosiery",2D)="white" {}
        _SheerAmount("Sheer hosiery",Range(0,1))=0
        _Denier("Visual denier",Range(10,30))=20
        _LaceAmount("Original lace coverage",Range(0,1))=0
        _SkinUseSecondUV("Skin uses fitted UV",Float)=0
        _UseStockingBoundary("Sheer fabric below lace band",Float)=0
        _OpticalDensity("Fabric absorption per visual denier",Float)=.07
        _GrazingFloor("Minimum viewing cosine",Range(.3,1))=.32
        _LaceRelief("Fine lace thread normal",Range(0,.2))=0
        _LaceBodyRepeat("Mirror floral field below original cuff",Float)=0
        _SourceAlphaClip("Original cutout texture",Float)=0
        _SourceCutoff("Original cutout threshold",Range(0,1))=.18
        [HideInInspector] _BootMask("Boot mask",Float)=0
        [HideInInspector] _BootRestMask("Skinned original boot coverage",Float)=0
        [HideInInspector] _OpenFootSoleMask("Covered sole of open footwear",Float)=0
        [HideInInspector] _BootSoleMax("Covered sole height",Float)=0
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
        TEXTURE2D(_SkinMap);SAMPLER(sampler_SkinMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST,_BaseMap_TexelSize,_DetailNormalMap_ST;half4 _BaseColor;half _Smoothness,_BumpScale,_DetailNormalMapScale,_SheerAmount,_Denier,_LaceAmount,_SkinUseSecondUV,_UseStockingBoundary,_OpticalDensity,_GrazingFloor,_LaceRelief,_LaceBodyRepeat,_SourceAlphaClip,_SourceCutoff;float _BootMask,_BootRestMask,_OpenFootSoleMask,_BootSoleMax;float4 _BootBoundsMin,_BootBoundsMax;float4x4 _BootWorldToLocal;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 skinUV:TEXCOORD1;float2 stockingBoundary:TEXCOORD2;float2 bootBoundary:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half4 tangentWS:TEXCOORD3;float2 skinUV:TEXCOORD4;float cuffDistance:TEXCOORD5;half3 vertexLighting:TEXCOORD6;float bootCuffDistance:TEXCOORD7;UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes input)
        {
            Varyings output=(Varyings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
            output.vertexLighting=VertexLighting(output.positionWS,output.normalWS);
            output.skinUV=lerp(input.uv,input.skinUV,_SkinUseSecondUV);
            // Rest-space boundary travels with the skinned surface when seated.
            output.cuffDistance=input.stockingBoundary.y-input.stockingBoundary.x;
            output.bootCuffDistance=input.bootBoundary.y-input.bootBoundary.x;
            output.tangentWS=half4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());return output;
        }
        float2 LaceUV(float2 uv)
        {
            if(_LaceBodyRepeat>0)uv.y=(1-abs(frac(uv.y/(2*_LaceBodyRepeat))*2-1))*_LaceBodyRepeat;
            return uv;
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
            if(_LaceAmount>.5&&_LaceRelief>0)
            {
                float2 du=float2(_BaseMap_TexelSize.x,0),dv=float2(0,_BaseMap_TexelSize.y);
                half x=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,LaceUV(input.uv+du)).a-SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,LaceUV(input.uv-du)).a;
                half y=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,LaceUV(input.uv+dv)).a-SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,LaceUV(input.uv-dv)).a;
                half3 tangent=normalize(input.tangentWS.xyz),bitangent=cross(n,tangent)*input.tangentWS.w;
                n=normalize(n-_LaceRelief*(x*tangent+y*bitangent));
            }
            return n;
        }
        void Coverage(float3 world,float cuffDistance,float3 normal)
        {
            if(_BootRestMask>.5){if(cuffDistance>0)clip(-1);return;}
            float3 p=mul(_BootWorldToLocal,float4(world,1)).xyz;
            if(_BootMask>.5&&all(p>_BootBoundsMin.xyz)&&all(p<_BootBoundsMax.xyz))clip(-1);
            // Sandal openings expose the top of the original foot. Its underside
            // remains covered by the outsole while the foot is inside the shoe.
            if(_BootMask>.5&&_OpenFootSoleMask>.5)
            {
                float3 n=normalize(mul((float3x3)_BootWorldToLocal,normal));
                if(p.y<_BootSoleMax&&n.y<.7&&all(p.xz>_BootBoundsMin.xz)&&all(p.xz<_BootBoundsMax.xz))clip(-1);
            }
        }
        void SourceCutout(float2 uv) { if(_SourceAlphaClip>.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,LaceUV(uv)).a-_SourceCutoff); }
        half4 Frag(Varyings input):SV_Target
        {
            Coverage(input.positionWS,input.bootCuffDistance,input.normalWS);
            SourceCutout(input.uv);
            InputData data=(InputData)0;data.positionWS=input.positionWS;data.positionCS=input.positionCS;data.normalWS=SockNormal(input);
            data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
            data.bakedGI=SampleSH(data.normalWS);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);data.shadowMask=half4(1,1,1,1);
            data.vertexLighting=input.vertexLighting;
            half4 fabric=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,LaceUV(input.uv));
            SurfaceData surface=(SurfaceData)0;surface.albedo=fabric.rgb*_BaseColor.rgb;
            // Composite the original skin on the same source surface. Optical
            // thickness grows at grazing angles without sorting transparent legs
            // or rendering a second coplanar anatomical mesh underneath them.
            half cosine=max(_GrazingFloor,abs(dot(data.normalWS,data.viewDirectionWS)));
            half coverage=1-exp2(-clamp(_Denier,10,30)*_OpticalDensity/cosine);
            // Original alpha separates the black lace threads from open holes.
            // Holes show skin, rather than a second layer of sheer black fabric.
            coverage=lerp(coverage,fabric.a,_LaceAmount);
            half fabricRegion=lerp(1,smoothstep(0,.003,input.cuffDistance),_UseStockingBoundary);
            coverage*=fabricRegion;
            half3 skin=SAMPLE_TEXTURE2D(_SkinMap,sampler_SkinMap,input.skinUV).rgb;
            surface.albedo=lerp(surface.albedo,lerp(skin,surface.albedo,coverage),_SheerAmount);
            half glossRegion=lerp(fabricRegion,fabric.a,_LaceAmount);
            surface.alpha=1;surface.smoothness=lerp(.18h,_Smoothness,glossRegion);surface.occlusion=1;surface.normalTS=half3(0,0,1);
            return UniversalFragmentPBR(data,surface);
        }
        half4 DepthFrag(Varyings input):SV_Target { Coverage(input.positionWS,input.bootCuffDistance,input.normalWS);SourceCutout(input.uv);return 0; }
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
        half4 NormalsFrag(Varyings input):SV_Target { Coverage(input.positionWS,input.bootCuffDistance,input.normalWS);SourceCutout(input.uv);return half4(SockNormal(input),0); }
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
