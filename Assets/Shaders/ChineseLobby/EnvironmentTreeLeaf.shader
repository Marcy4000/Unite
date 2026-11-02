Shader "==MAIN==/Environment_Variant_TreeLeaf"
{
    Properties
    {
        _LightmapTex ("Custom lightmap RGB / visibility A", 2D) = "gray" {}
        _MainTex ("_MainTex", 2D) = "white" {}
        _NormalMap ("_NormalMap", 2D) = "bump" {}
        _Amplitude ("_Amplitude", Vector) = (0.1,0.1,0.1,0)
        _BaseColor ("_BaseColor", Color) = (1,1,1,1)
        _BrightColor ("_BrightColor", Color) = (1,1,1,0)
        _Color ("_Color", Color) = (1,1,1,1)
        _DarkColor ("_DarkColor", Color) = (1,1,1,0)
        _EmissionColor ("_EmissionColor", Color) = (0,0,0,1)
        _Frequency ("_Frequency", Vector) = (1,1,1,1)
        _GlassColor ("_GlassColor", Color) = (1,1,1,1)
        _LocalLightOffset ("_LocalLightOffset", Vector) = (0,0,0,0)
        _MainColor ("_MainColor", Color) = (1,1,1,0)
        _ReflectionColor ("_ReflectionColor", Color) = (1,1,1,1)
        _RimColor ("_RimColor", Color) = (0.7490196,0.7490196,0.7490196,0)
        _SSSColor ("_SSSColor", Color) = (0.39489144,0.4811321,0.4115408,0)
        _SpecColor ("_SpecColor", Color) = (0.19999996,0.19999996,0.19999996,1)
        _SpecularColor ("_SpecularColor", Color) = (1,1,1,0)
        _TreeAmbientTint ("_TreeAmbientTint", Color) = (0,0,0,0.43137255)
        _TreeFresnelTint ("_TreeFresnelTint", Color) = (0.3333333,0.7372549,0.37106326,1)
        _TreeHighlightDirection ("_TreeHighlightDirection", Vector) = (-1.15,1,0,0)
        _TreeHighlightTint ("_TreeHighlightTint", Color) = (0.16389416,0.5377358,0.07102172,0.3647059)
        _TreeIndirectTint ("_TreeIndirectTint", Vector) = (0.013625,0.15214583,0.218,0.39215687)
        _TreeShadowTint ("_TreeShadowTint", Color) = (2.9960785,2.7137256,1.8980392,1)
        _TreeTranslucencyDirection ("_TreeTranslucencyDirection", Vector) = (0,1,0,0)
        _TreeTranslucencyTint ("_TreeTranslucencyTint", Color) = (0,0,0,0)
        _WindDir ("_WindDir", Vector) = (0,0,-1,0)
        BAKERY_META_ALPHA_ENABLE ("BAKERY_META_ALPHA_ENABLE", Float) = 1
        _AlphaClip ("_AlphaClip", Float) = 0
        _AlphaOffset ("_AlphaOffset", Float) = 0
        _AlphaToMask ("_AlphaToMask", Float) = 0
        _BakedDiffuseNormalBendOffset ("_BakedDiffuseNormalBendOffset", Float) = 0.1
        _BakedDiffuseNormalBias ("_BakedDiffuseNormalBias", Float) = 1
        _Blend ("_Blend", Float) = 0
        _BlendMode ("_BlendMode", Float) = 0
        _BlendOp ("_BlendOp", Float) = 0
        _BlendOpAlpha ("_BlendOpAlpha", Float) = 0
        _BumpOn ("_BumpOn", Float) = 0
        _BumpScale ("_BumpScale", Float) = 1
        _CLM ("_CLM", Float) = 0
        _Cull ("_Cull", Float) = 2
        _CustomAlpha ("_CustomAlpha", Float) = 1
        _CustomBakedShadowScale ("_CustomBakedShadowScale", Float) = 1
        _CustomLightMapBase ("_CustomLightMapBase", Float) = 0
        _CustomLightMapMax ("_CustomLightMapMax", Float) = 1.6
        _CustomLightMapMin ("_CustomLightMapMin", Float) = -1
        _CustomLightMapNormalBias ("_CustomLightMapNormalBias", Float) = 0
        _CustomLightMapScale ("_CustomLightMapScale", Float) = 1
        _CustomSpecularMax ("_CustomSpecularMax", Float) = 0.6
        _CustomSpecularMin ("_CustomSpecularMin", Float) = -3.08
        _CustomSpecularRoughnessOffset ("_CustomSpecularRoughnessOffset", Float) = 0.55
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
        _DisableURPSpecularEnv ("_DisableURPSpecularEnv", Float) = 0
        _DstBlend ("_DstBlend", Float) = 0
        _DstBlendAlpha ("_DstBlendAlpha", Float) = 0
        _DstBlendFactor ("_DstBlendFactor", Float) = 0
        _DstBlendFactorAlpha ("_DstBlendFactorAlpha", Float) = 10
        _EnvironmentAmbientFloor ("_EnvironmentAmbientFloor", Float) = 0.08
        _EnvironmentLegacyToneMap ("_EnvironmentLegacyToneMap", Float) = 1
        _EnvironmentReflections ("_EnvironmentReflections", Float) = 1
        _FresnelEffectBias ("_FresnelEffectBias", Float) = 0.227
        _GlassColorAngle ("_GlassColorAngle", Float) = 0.5
        _GlassColorIntensity ("_GlassColorIntensity", Float) = 1
        _GlossMapScale ("_GlossMapScale", Float) = 0
        _Glossiness ("_Glossiness", Float) = 0
        _GlossyReflections ("_GlossyReflections", Float) = 0
        _HTEX ("_HTEX", Float) = 0
        _HighBlend ("_HighBlend", Float) = 1
        _HighColor ("_HighColor", Float) = 1
        _HighOffset ("_HighOffset", Float) = 0
        _IFMask ("_IFMask", Float) = 1
        _IFTrans ("_IFTrans", Float) = 0
        _IFWIND ("_IFWIND", Float) = 0
        _LightOffset ("_LightOffset", Float) = 0.226
        _LightPow ("_LightPow", Float) = 0.548
        _MaxShadow ("_MaxShadow", Float) = 0
        _Metallic ("_Metallic", Float) = 0
        _MetallicOffset ("_MetallicOffset", Float) = 0
        _NormalScale ("_NormalScale", Float) = 1
        _NormalStrong ("_NormalStrong", Float) = 1
        _OcclusionScale ("_OcclusionScale", Float) = 1
        _OcclusionStrength ("_OcclusionStrength", Float) = 1
        _Power ("_Power", Float) = 1
        _QualityLevel ("_QualityLevel", Float) = 1
        _QueueOffset ("_QueueOffset", Float) = 0
        _ReceiveShadows ("_ReceiveShadows", Float) = 1
        _ReflectionOcclusion ("_ReflectionOcclusion", Float) = 1
        _ReflectionRoughness ("_ReflectionRoughness", Float) = 0
        _RenderQueueOffset ("_RenderQueueOffset", Float) = 0
        _RenderType ("_RenderType", Float) = 2450
        _RoughNessOffset ("_RoughNessOffset", Float) = 0
        _RoughnessOffset ("_RoughnessOffset", Float) = 0
        _SM ("_SM", Float) = 1
        _Smoothness ("_Smoothness", Float) = 0.5
        _SmoothnessTextureChannel ("_SmoothnessTextureChannel", Float) = 0
        _SpecularHighlights ("_SpecularHighlights", Float) = 1
        _SrcBlend ("_SrcBlend", Float) = 1
        _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
        _SrcBlendFactor ("_SrcBlendFactor", Float) = 1
        _SrcBlendFactorAlpha ("_SrcBlendFactorAlpha", Float) = 5
        _Surface ("_Surface", Float) = 0
        _TREEL ("_TREEL", Float) = 1
        _TreeBakedColorDebugMode ("_TreeBakedColorDebugMode", Float) = 0
        _TreeMeshNormalDebugMode ("_TreeMeshNormalDebugMode", Float) = 0
        _TreeMeshVertexColorDebugMode ("_TreeMeshVertexColorDebugMode", Float) = 0
        _TreeRealTimeColorDebugMode ("_TreeRealTimeColorDebugMode", Float) = 0
        _TreeSmoothNormalFactor ("_TreeSmoothNormalFactor", Float) = 0
        _TreeTranslucencyMax ("_TreeTranslucencyMax", Float) = 1
        _TreeTranslucencyMin ("_TreeTranslucencyMin", Float) = 0
        _TreeTranslucentColorDebugMode ("_TreeTranslucentColorDebugMode", Float) = 0
        _UseAlphaClip ("_UseAlphaClip", Float) = 1
        _UseAlphaInput ("_UseAlphaInput", Float) = 1
        _UseCustomLightMap ("_UseCustomLightMap", Float) = 0
        _UseCustomQuality ("_UseCustomQuality", Float) = 1
        _UseCustomRendering ("_UseCustomRendering", Float) = 0
        _UseGlassRendering ("_UseGlassRendering", Float) = 0
        _UseMainMap ("_UseMainMap", Float) = 1
        _UseMixMap ("_UseMixMap", Float) = 0
        _UseNormalMap ("_UseNormalMap", Float) = 1
        _UsePackedNormal ("_UsePackedNormal", Float) = 1
        _UseUnlitMode ("_UseUnlitMode", Float) = 0
        _Use_Reflection_Texture ("_Use_Reflection_Texture", Float) = 0
        _VLM ("_VLM", Float) = 0
        _WaveLength ("_WaveLength", Float) = 1
        _WindNoise ("_WindNoise", Float) = 0.1
        _WindRang ("_WindRang", Float) = 1
        _WindSpeed ("_WindSpeed", Float) = 1
        _WorkflowMode ("_WorkflowMode", Float) = 1
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
            #define ENV_FOLIAGE 1
            #define ENV_TREELEAF 1
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
            #define ENV_FOLIAGE 1
            #define ENV_TREELEAF 1
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
            #define ENV_FOLIAGE 1
            #define ENV_TREELEAF 1
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
            #define ENV_FOLIAGE 1
            #define ENV_TREELEAF 1
            #include "EnvironmentCommon.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
