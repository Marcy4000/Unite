Shader "==MAIN==/Environment_Variant_Metal"
{
    Properties
    {
        _LightmapTex ("Custom lightmap RGB / visibility A", 2D) = "gray" {}
        _MainTex ("_MainTex", 2D) = "white" {}
        _MixMap ("_MixMap", 2D) = "white" {}
        _NormalMap ("_NormalMap", 2D) = "bump" {}
        _BaseColor ("_BaseColor", Color) = (1,1,1,1)
        _Color ("_Color", Color) = (1,1,1,1)
        _Color0 ("_Color0", Color) = (0.8363742,1,1,1)
        _Color10 ("_Color10", Color) = (1,1,1,1)
        _Color11 ("_Color11", Color) = (1,1,1,1)
        _Color12 ("_Color12", Color) = (1,1,1,1)
        _Color4 ("_Color4", Color) = (1,1,0.65,1)
        _Color5 ("_Color5", Color) = (1,1,1,1)
        _Color6 ("_Color6", Color) = (1,1,1,1)
        _EmissionColor ("_EmissionColor", Color) = (0,0,0,1)
        _GlassColor ("_GlassColor", Color) = (1,1,1,1)
        _LocalLightOffset ("_LocalLightOffset", Vector) = (0,0,0,0)
        _MainColor ("_MainColor", Color) = (1,1,1,0)
        _ReflectionColor ("_ReflectionColor", Color) = (1,1,1,1)
        _SpecColor ("_SpecColor", Color) = (0.19999996,0.19999996,0.19999996,1)
        _SpecularColor ("_SpecularColor", Color) = (1,1,1,0)
        _WindDirection ("_WindDirection", Vector) = (0.15,0.05,0.15,4)
        _fogRange ("_fogRange", Vector) = (10,1,10,1)
        _horizonAdjust ("_horizonAdjust", Vector) = (100,1,0,0)
        _rimAdjust ("_rimAdjust", Vector) = (45,90,0,0)
        _AlphaClip ("_AlphaClip", Float) = 0
        _AlphaOffset ("_AlphaOffset", Float) = 0
        _BakedDiffuseNormalBendOffset ("_BakedDiffuseNormalBendOffset", Float) = 0.1
        _BakedDiffuseNormalBias ("_BakedDiffuseNormalBias", Float) = 1
        _Blend ("_Blend", Float) = 0
        _BumpScale ("_BumpScale", Float) = 1
        _Cull ("_Cull", Float) = 2
        _CustomBakedShadowScale ("_CustomBakedShadowScale", Float) = 1
        _CustomLightMapScale ("_CustomLightMapScale", Float) = 1
        _Cutoff ("_Cutoff", Float) = 0.5
        _DepthOffset ("_DepthOffset", Float) = 0
        _DisableCustomShadowCascade ("_DisableCustomShadowCascade", Float) = 0
        _DisableEnvironmentReflection ("_DisableEnvironmentReflection", Float) = 0
        _DisableLegacyGlassLightmap ("_DisableLegacyGlassLightmap", Float) = 0
        _DisableMainCityFogMode ("_DisableMainCityFogMode", Float) = 0
        _DisablePremultipliedAlpha ("_DisablePremultipliedAlpha", Float) = 0
        _DisableReceiveShadow ("_DisableReceiveShadow", Float) = 0
        _DisableShadowCaster ("_DisableShadowCaster", Float) = 0
        _DisableSpecularHighlight ("_DisableSpecularHighlight", Float) = 0
        _DisableURPSpecularEnv ("_DisableURPSpecularEnv", Float) = 1
        _DstBlend ("_DstBlend", Float) = 0
        _DstBlendAlpha ("_DstBlendAlpha", Float) = 0
        _EnvironmentAmbientFloor ("_EnvironmentAmbientFloor", Float) = 0.08
        _EnvironmentLegacyToneMap ("_EnvironmentLegacyToneMap", Float) = 1
        _EnvironmentReflections ("_EnvironmentReflections", Float) = 1
        _FresnelEffectBias ("_FresnelEffectBias", Float) = 0
        _GlassColorAngle ("_GlassColorAngle", Float) = 0.5
        _GlassColorIntensity ("_GlassColorIntensity", Float) = 1
        _GlossMapScale ("_GlossMapScale", Float) = 0
        _Glossiness ("_Glossiness", Float) = 0
        _GlossyReflections ("_GlossyReflections", Float) = 0
        _Metallic ("_Metallic", Float) = 0
        _MetallicOffset ("_MetallicOffset", Float) = 0
        _NoiseIntensity ("_NoiseIntensity", Float) = 1
        _NoiseMoveX ("_NoiseMoveX", Float) = 0.1
        _NoiseMoveY ("_NoiseMoveY", Float) = 0.1
        _NormalScale ("_NormalScale", Float) = 1
        _OcclusionScale ("_OcclusionScale", Float) = 1
        _OcclusionStrength ("_OcclusionStrength", Float) = 1
        _QualityLevel ("_QualityLevel", Float) = 0
        _QueueOffset ("_QueueOffset", Float) = 0
        _ReceiveShadows ("_ReceiveShadows", Float) = 1
        _ReflectionOcclusion ("_ReflectionOcclusion", Float) = 1
        _ReflectionRoughness ("_ReflectionRoughness", Float) = 0
        _RenderQueueOffset ("_RenderQueueOffset", Float) = 0
        _RenderType ("_RenderType", Float) = 2000
        _RoughNessOffset ("_RoughNessOffset", Float) = 0
        _RoughnessOffset ("_RoughnessOffset", Float) = 0
        _Smoothness ("_Smoothness", Float) = 0.5
        _SmoothnessTextureChannel ("_SmoothnessTextureChannel", Float) = 0
        _SpecularHighlights ("_SpecularHighlights", Float) = 1
        _SpeedDifference ("_SpeedDifference", Float) = 12.2
        _SrcBlend ("_SrcBlend", Float) = 1
        _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
        _Surface ("_Surface", Float) = 0
        _UseAlphaClip ("_UseAlphaClip", Float) = 0
        _UseAlphaInput ("_UseAlphaInput", Float) = 0
        _UseCustomLightMap ("_UseCustomLightMap", Float) = 0
        _UseCustomQuality ("_UseCustomQuality", Float) = 0
        _UseCustomRendering ("_UseCustomRendering", Float) = 0
        _UseGlassRendering ("_UseGlassRendering", Float) = 0
        _UseMainMap ("_UseMainMap", Float) = 1
        _UseMixMap ("_UseMixMap", Float) = 1
        _UseNormalMap ("_UseNormalMap", Float) = 1
        _UsePackedNormal ("_UsePackedNormal", Float) = 0
        _UseUnlitMode ("_UseUnlitMode", Float) = 0
        _Use_Reflection_Texture ("_Use_Reflection_Texture", Float) = 0
        _WindSpeed ("_WindSpeed", Float) = 0.539
        _WindStrength ("_WindStrength", Float) = 3.58
        _WorkflowMode ("_WorkflowMode", Float) = 1
        _ZTest ("_ZTest", Float) = 4
        _ZWrite ("_ZWrite", Float) = 1
        _rimRange ("_rimRange", Float) = 0.3
        _rimSoft ("_rimSoft", Float) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
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
            #define ENV_METAL 1
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
            #define ENV_METAL 1
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
            #define ENV_METAL 1
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
            #define ENV_METAL 1
            #include "EnvironmentCommon.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
