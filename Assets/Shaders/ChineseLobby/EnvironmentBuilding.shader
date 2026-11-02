Shader "==MAIN==/Environment_Variant_Building"
{
    Properties
    {
        _NormalMap ("Normal RGB / roughness A", 2D) = "bump" {}
        _LightmapTex ("Custom lightmap RGB / visibility A", 2D) = "gray" {}
        _MainTex ("_MainTex", 2D) = "white" {}
        _BaseColor ("_BaseColor", Color) = (1,1,1,1)
        _Color ("_Color", Color) = (1,1,1,1)
        _EmissColor ("_EmissColor", Color) = (0.07119972,0.07544332,0.23584908,0)
        _EmissionColor ("_EmissionColor", Color) = (0,0,0,1)
        _EnvOffset ("_EnvOffset", Vector) = (-20.64,-16.91,0,0)
        _FresnelColor ("_FresnelColor", Color) = (0.57244587,0.57244587,0.9056606,0)
        _FresnelColor2 ("_FresnelColor2", Color) = (0.23351786,0.24743348,0.3992402,0)
        _FresnelColor2a ("_FresnelColor2a", Color) = (1,1,1,0)
        _FresnelColora ("_FresnelColora", Color) = (1,1,1,0)
        _GlassColor ("_GlassColor", Color) = (1,1,1,1)
        _LightColor ("_LightColor", Color) = (1,1,1,0)
        _LocalLightOffset ("_LocalLightOffset", Vector) = (0,0,0,0)
        _MainColor ("_MainColor", Color) = (1,1,1,0)
        _ReflacMul ("_ReflacMul", Color) = (0.31305972,0.32599604,0.478645,1)
        _ReflectionColor ("_ReflectionColor", Color) = (1,1,1,1)
        _SpecColor ("_SpecColor", Color) = (0.19999996,0.19999996,0.19999996,1)
        _SpecularColor ("_SpecularColor", Color) = (1,1,1,0)
        _UVScallePaner ("_UVScallePaner", Vector) = (1,1,1,0)
        _WaterCausticChannel ("_WaterCausticChannel", Vector) = (1,0,0,0)
        _WaterCausticColor ("_WaterCausticColor", Color) = (0,0.15343282,2.755699,1)
        _WaterCoastChannel ("_WaterCoastChannel", Vector) = (0,0,0,1)
        _WaterDepthColor ("_WaterDepthColor", Color) = (0,0.08444444,0.19,1)
        _WaterFoamChannel ("_WaterFoamChannel", Vector) = (0,0,0,0)
        _WaterFoamColor ("_WaterFoamColor", Color) = (16,16,16,1)
        _WaterRippleData ("_WaterRippleData", Vector) = (0.76,2.4,-0.2,1.1)
        _WaterVPMatrixm0 ("_WaterVPMatrixm0", Vector) = (-1.732051,0,0,-20.7846)
        _WaterVPMatrixm1 ("_WaterVPMatrixm1", Vector) = (0,0,-1.732051,-66.5108)
        _WaterVPMatrixm2 ("_WaterVPMatrixm2", Vector) = (0,0,0,0.04163513)
        _WaterVPMatrixm3 ("_WaterVPMatrixm3", Vector) = (0,-0.9999999,0,583.9999)
        _AlphaClip ("_AlphaClip", Float) = 0
        _AlphaOffset ("_AlphaOffset", Float) = 0
        _AlphaToMask ("_AlphaToMask", Float) = 0
        _BakedDiffuseNormalBendOffset ("_BakedDiffuseNormalBendOffset", Float) = 0.1
        _BakedDiffuseNormalBias ("_BakedDiffuseNormalBias", Float) = 1
        _Blend ("_Blend", Float) = 0
        _BlendOp ("_BlendOp", Float) = 0
        _BlendOpAlpha ("_BlendOpAlpha", Float) = 0
        _BumpScale ("_BumpScale", Float) = 1
        _CAMERFLIP ("_CAMERFLIP", Float) = 0
        _Cull ("_Cull", Float) = 2
        _CustomAlpha ("_CustomAlpha", Float) = 0.563
        _CustomBakedShadowScale ("_CustomBakedShadowScale", Float) = 1
        _CustomLightMapNormalBias ("_CustomLightMapNormalBias", Float) = 0
        _CustomLightMapScale ("_CustomLightMapScale", Float) = 1
        _Cutoff ("_Cutoff", Float) = 0.5
        _DepthOffset ("_DepthOffset", Float) = 0
        _DisableCustomShadowCascade ("_DisableCustomShadowCascade", Float) = 0
        _DisableEnvironmentReflection ("_DisableEnvironmentReflection", Float) = 1
        _DisableLegacyGlassLightmap ("_DisableLegacyGlassLightmap", Float) = 0
        _DisableMainCityFogMode ("_DisableMainCityFogMode", Float) = 0
        _DisablePremultipliedAlpha ("_DisablePremultipliedAlpha", Float) = 0
        _DisableReceiveShadow ("_DisableReceiveShadow", Float) = 0
        _DisableShadowCaster ("_DisableShadowCaster", Float) = 0
        _DisableSpecularHighlight ("_DisableSpecularHighlight", Float) = 1
        _DisableURPSpecularEnv ("_DisableURPSpecularEnv", Float) = 1
        _DstBlend ("_DstBlend", Float) = 0
        _DstBlendAlpha ("_DstBlendAlpha", Float) = 0
        _DstBlendFactor ("_DstBlendFactor", Float) = 10
        _DstBlendFactorAlpha ("_DstBlendFactorAlpha", Float) = 10
        _EnvironmentAmbientFloor ("_EnvironmentAmbientFloor", Float) = 0.08
        _EnvironmentLegacyToneMap ("_EnvironmentLegacyToneMap", Float) = 1
        _EnvironmentReflections ("_EnvironmentReflections", Float) = 1
        _FresnelEffectBias ("_FresnelEffectBias", Float) = 0
        _GlassColorAngle ("_GlassColorAngle", Float) = 0.5
        _GlassColorIntensity ("_GlassColorIntensity", Float) = 1
        _GlossMapScale ("_GlossMapScale", Float) = 0
        _Glossiness ("_Glossiness", Float) = 0
        _GlossyReflections ("_GlossyReflections", Float) = 0
        _IFEmiss ("_IFEmiss", Float) = 0
        _IFFLIP ("_IFFLIP", Float) = 0
        _IFMask ("_IFMask", Float) = 0
        _Metallic ("_Metallic", Float) = 0
        _MetallicOffset ("_MetallicOffset", Float) = 0
        _NormalScale ("_NormalScale", Float) = 1
        _NormalStrong ("_NormalStrong", Float) = 1
        _OcclusionScale ("_OcclusionScale", Float) = 1
        _OcclusionStrength ("_OcclusionStrength", Float) = 1
        _QualityLevel ("_QualityLevel", Float) = 0
        _QueueOffset ("_QueueOffset", Float) = 0
        _ReceiveShadows ("_ReceiveShadows", Float) = 1
        _ReflacInLM ("_ReflacInLM", Float) = 0.538
        _ReflecPow ("_ReflecPow", Float) = 0.4
        _ReflectionFade ("_ReflectionFade", Float) = 0
        _ReflectionOcclusion ("_ReflectionOcclusion", Float) = 1
        _ReflectionRoughness ("_ReflectionRoughness", Float) = 0
        _RenderQueueOffset ("_RenderQueueOffset", Float) = 0
        _RenderType ("_RenderType", Float) = 2000
        _RoughNessOffset ("_RoughNessOffset", Float) = 0
        _RoughnessOffset ("_RoughnessOffset", Float) = 0
        _Smoothness ("_Smoothness", Float) = 0.5
        _SmoothnessTextureChannel ("_SmoothnessTextureChannel", Float) = 0
        _SpecularHighlights ("_SpecularHighlights", Float) = 1
        _SpecularInLM ("_SpecularInLM", Float) = 0
        _SrcBlend ("_SrcBlend", Float) = 1
        _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
        _SrcBlendFactor ("_SrcBlendFactor", Float) = 5
        _SrcBlendFactorAlpha ("_SrcBlendFactorAlpha", Float) = 5
        _Surface ("_Surface", Float) = 0
        _UseAlphaClip ("_UseAlphaClip", Float) = 0
        _UseAlphaInput ("_UseAlphaInput", Float) = 0
        _UseCausticMap ("_UseCausticMap", Float) = 0
        _UseCoastMap ("_UseCoastMap", Float) = 1
        _UseCustomLightMap ("_UseCustomLightMap", Float) = 0
        _UseCustomQuality ("_UseCustomQuality", Float) = 0
        _UseCustomRendering ("_UseCustomRendering", Float) = 0
        _UseGlassRendering ("_UseGlassRendering", Float) = 0
        _UseMainMap ("_UseMainMap", Float) = 1
        _UseMixMap ("_UseMixMap", Float) = 0
        _UseNormalMap ("_UseNormalMap", Float) = 0
        _UsePackedNormal ("_UsePackedNormal", Float) = 0
        _UseUnlitMode ("_UseUnlitMode", Float) = 0
        _Use_Reflection_Texture ("_Use_Reflection_Texture", Float) = 0
        _WaterCausticDepthScale ("_WaterCausticDepthScale", Float) = 0
        _WaterCausticDistortion ("_WaterCausticDistortion", Float) = 0.837
        _WaterCoastDistortion ("_WaterCoastDistortion", Float) = 0
        _WaterDepthFadeScale ("_WaterDepthFadeScale", Float) = 1.29
        _WaterFoamCoverage ("_WaterFoamCoverage", Float) = 0.234
        _WaterFoamDistortion ("_WaterFoamDistortion", Float) = 0.01
        _WaterFoamHighlightScale ("_WaterFoamHighlightScale", Float) = 0.116
        _WaterIgnoreMeshTangent ("_WaterIgnoreMeshTangent", Float) = 1
        _WaterRippleHeightScale ("_WaterRippleHeightScale", Float) = 3.62
        _WaterRippleRatio ("_WaterRippleRatio", Float) = 120
        _WaterVertexColorDebugMode ("_WaterVertexColorDebugMode", Float) = 0
        _WaterVertexNormalDebugMode ("_WaterVertexNormalDebugMode", Float) = 0
        _WaterVertexUVDebugMode ("_WaterVertexUVDebugMode", Float) = 0
        _WorkflowMode ("_WorkflowMode", Float) = 1
        _ZTest ("_ZTest", Float) = 4
        _ZWrite ("_ZWrite", Float) = 1
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
            #define ENV_BUILDING 1
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
            #define ENV_BUILDING 1
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
            #define ENV_BUILDING 1
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
            #define ENV_BUILDING 1
            #include "EnvironmentCommon.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
