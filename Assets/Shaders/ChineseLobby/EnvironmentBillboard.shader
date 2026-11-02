Shader "==MAIN==/Environment_Variant_Billboard"
{
    Properties
    {
        _NormalMap ("Normal RGB / roughness A", 2D) = "bump" {}
        _LightmapTex ("Custom lightmap RGB / visibility A", 2D) = "gray" {}
        _MainTex ("_MainTex", 2D) = "white" {}
        _A1 ("_A1", Color) = (1,0,0,0)
        _A2 ("_A2", Color) = (1,0,0,0)
        _A3 ("_A3", Color) = (1,0,0,0)
        _Color0 ("_Color0", Color) = (1,1,1,0)
        _Color1 ("_Color1", Color) = (1,1,1,0)
        _E1 ("_E1", Color) = (1,0,0,0)
        _E2 ("_E2", Color) = (1,0,0,0)
        _EmissColor ("_EmissColor", Color) = (1,1,1,0)
        _EmissionColor ("_EmissionColor", Color) = (0,0,0,1)
        _EnvOffset ("_EnvOffset", Vector) = (-113.27,103.56,151.4,0)
        _FresnelColor ("_FresnelColor", Color) = (1,1,1,0)
        _FresnelColor2 ("_FresnelColor2", Color) = (1,1,1,0)
        _FresnelColor2a ("_FresnelColor2a", Color) = (1,1,1,0)
        _FresnelColora ("_FresnelColora", Color) = (1,1,1,0)
        _GlassColor ("_GlassColor", Color) = (1,1,1,1)
        _LightColor ("_LightColor", Color) = (1.7401739,2.1173515,2.9279118,0)
        _LocalLightOffset ("_LocalLightOffset", Vector) = (0,0,0,0)
        _MainColor ("_MainColor", Color) = (1,1,1,0)
        _MaskColor ("_MaskColor", Color) = (1,1,1,0)
        _MaskColor2 ("_MaskColor2", Color) = (0.5,0.5,0.5,0)
        _MatcapColor ("_MatcapColor", Color) = (2,2,2,0)
        _ReflacMul ("_ReflacMul", Color) = (0.6356297,0.6886792,0.47427908,1)
        _ReflectionColor ("_ReflectionColor", Color) = (1,1,1,1)
        _ScaleOffset0 ("_ScaleOffset0", Vector) = (3,3,3,0)
        _ScaleOffset1 ("_ScaleOffset1", Vector) = (10,10,10,0)
        _SpecularColor ("_SpecularColor", Color) = (1,1,1,0)
        _UVScallePaner ("_UVScallePaner", Vector) = (1,1,0,0)
        _AlphaClip ("_AlphaClip", Float) = 0
        _AlphaOffset ("_AlphaOffset", Float) = 0
        _AlphaToMask ("_AlphaToMask", Float) = 0
        _BakedDiffuseNormalBias ("_BakedDiffuseNormalBias", Float) = 0
        _Blend ("_Blend", Float) = 0
        _BlendMode ("_BlendMode", Float) = 0
        _BlendOp ("_BlendOp", Float) = 0
        _BlendOpAlpha ("_BlendOpAlpha", Float) = 0
        _CAMERFLIP ("_CAMERFLIP", Float) = 0
        _CLM ("_CLM", Float) = 0
        _Cull ("_Cull", Float) = 2
        _CustomAlpha ("_CustomAlpha", Float) = 1
        _CustomBakedShadowScale ("_CustomBakedShadowScale", Float) = 1
        _CustomLightMapScale ("_CustomLightMapScale", Float) = 1
        _Cutoff ("_Cutoff", Float) = 0.5
        _Depth ("_Depth", Float) = 2
        _Depth1 ("_Depth1", Float) = 1
        _DepthOffset ("_DepthOffset", Float) = 0
        _DisableEnvironmentReflection ("_DisableEnvironmentReflection", Float) = 1
        _DisableMainCityFogMode ("_DisableMainCityFogMode", Float) = 0
        _DisablePremultipliedAlpha ("_DisablePremultipliedAlpha", Float) = 0
        _DisableReceiveShadow ("_DisableReceiveShadow", Float) = 0
        _DisableShadowCaster ("_DisableShadowCaster", Float) = 0
        _DisableSpecularHighlight ("_DisableSpecularHighlight", Float) = 1
        _DstBlend ("_DstBlend", Float) = 0
        _DstBlendAlpha ("_DstBlendAlpha", Float) = 0
        _DstBlendFactor ("_DstBlendFactor", Float) = 0
        _DstBlendFactorAlpha ("_DstBlendFactorAlpha", Float) = 10
        _Emiss ("_Emiss", Float) = 0
        _EnvironmentAmbientFloor ("_EnvironmentAmbientFloor", Float) = 0.08
        _EnvironmentLegacyToneMap ("_EnvironmentLegacyToneMap", Float) = 1
        _FresnelEffectBias ("_FresnelEffectBias", Float) = 1
        _GlassColorAngle ("_GlassColorAngle", Float) = 0.5
        _GlassColorIntensity ("_GlassColorIntensity", Float) = 1
        _HeightBlendWeight ("_HeightBlendWeight", Float) = 0.065
        _IFEmiss ("_IFEmiss", Float) = 0
        _IFFLIP ("_IFFLIP", Float) = 0
        _IFMask ("_IFMask", Float) = 0
        _IFUseUV1 ("_IFUseUV1", Float) = 0
        _Layer0Metaloffset ("_Layer0Metaloffset", Float) = 0.5
        _Layer0Roughoffset ("_Layer0Roughoffset", Float) = 0.14
        _Layer1Metaloffset ("_Layer1Metaloffset", Float) = 0.5
        _Layer1Normal1 ("_Layer1Normal1", Float) = 1
        _Layer1Roughoffset ("_Layer1Roughoffset", Float) = 0.03
        _Layer2Normal1 ("_Layer2Normal1", Float) = 1
        _MASK ("_MASK", Float) = 0
        _MaskFlip ("_MaskFlip", Float) = 0
        _Matcap ("_Matcap", Float) = 0
        _Metallic ("_Metallic", Float) = 0
        _MetallicOffset ("_MetallicOffset", Float) = 0
        _NIGHT_LIGHT ("_NIGHT_LIGHT", Float) = 0
        _NormalScale ("_NormalScale", Float) = 1
        _NormalStrong ("_NormalStrong", Float) = 1
        _Num ("_Num", Float) = 1
        _OcclusionScale ("_OcclusionScale", Float) = 1
        _QualityLevel ("_QualityLevel", Float) = 2
        _QueueOffset ("_QueueOffset", Float) = 0
        _Random_Pattern ("_Random_Pattern", Float) = 1
        _ReceiveShadows ("_ReceiveShadows", Float) = 1
        _ReflacInLM ("_ReflacInLM", Float) = 0.25
        _ReflecPow ("_ReflecPow", Float) = 0.8
        _ReflectionOcclusion ("_ReflectionOcclusion", Float) = 1
        _ReflectionRoughness ("_ReflectionRoughness", Float) = 0
        _RenderQueueOffset ("_RenderQueueOffset", Float) = 0
        _RenderType ("_RenderType", Float) = 2450
        _RoughNessOffset ("_RoughNessOffset", Float) = 0
        _RoughnessOffset ("_RoughnessOffset", Float) = 0
        _SM ("_SM", Float) = 1
        _SpecularInLM ("_SpecularInLM", Float) = 1
        _SrcBlend ("_SrcBlend", Float) = 1
        _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
        _SrcBlendFactor ("_SrcBlendFactor", Float) = 1
        _SrcBlendFactorAlpha ("_SrcBlendFactorAlpha", Float) = 5
        _StencilComp ("_StencilComp", Float) = 8
        _StencilData ("_StencilData", Float) = 0
        _StencilFailOp ("_StencilFailOp", Float) = 0
        _StencilPassOp ("_StencilPassOp", Float) = 0
        _StencilReadMask ("_StencilReadMask", Float) = 255
        _StencilWriteMask ("_StencilWriteMask", Float) = 255
        _StencilZFailOp ("_StencilZFailOp", Float) = 0
        _Surface ("_Surface", Float) = 0
        _UseAlphaClip ("_UseAlphaClip", Float) = 0
        _UseAlphaInput ("_UseAlphaInput", Float) = 0
        _UseCustomLightMap ("_UseCustomLightMap", Float) = 0
        _UseCustomQuality ("_UseCustomQuality", Float) = 1
        _UseCustomRendering ("_UseCustomRendering", Float) = 0
        _UseGlassRendering ("_UseGlassRendering", Float) = 0
        _UseMixMap ("_UseMixMap", Float) = 0
        _UseNormalMap ("_UseNormalMap", Float) = 0
        _UseUnlitMode ("_UseUnlitMode", Float) = 0
        _Use_Reflection_Texture ("_Use_Reflection_Texture", Float) = 0
        _ZTest ("_ZTest", Float) = 4
        _ZWrite ("_ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Offset [_DepthOffset], [_DepthOffset]
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EnvironmentVertex
            #pragma fragment EnvironmentSurfaceFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #define ENV_BILLBOARD 1
            #include "EnvironmentCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Offset [_DepthOffset], [_DepthOffset]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EnvironmentVertex
            #pragma fragment EnvironmentDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define ENV_BILLBOARD 1
            #define ENV_SHADOW_PASS 1
            #include "EnvironmentCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Offset [_DepthOffset], [_DepthOffset]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EnvironmentVertex
            #pragma fragment EnvironmentDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #define ENV_BILLBOARD 1
            #include "EnvironmentCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Offset [_DepthOffset], [_DepthOffset]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EnvironmentVertex
            #pragma fragment EnvironmentDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define ENV_BILLBOARD 1
            #include "EnvironmentCommon.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
