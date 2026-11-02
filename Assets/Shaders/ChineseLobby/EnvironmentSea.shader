Shader "==MAIN==/Environment_Variant_Sea"
{
    Properties
    {
        [Toggle] _ReflectionCubeAvailable ("Assigned reflection cube contains data", Float) = 0
        _CausticMap ("_CausticMap", 2D) = "white" {}
        _MainTex ("_MainTex", 2D) = "white" {}
        _NormalMap ("_NormalMap", 2D) = "bump" {}
        _ReflectionCube ("_ReflectionCube", Cube) = "black" {}
        _BaseColor ("_BaseColor", Color) = (1,1,1,1)
        _Color ("_Color", Color) = (1,1,1,1)
        _EmissionColor ("_EmissionColor", Color) = (0,0,0,1)
        _GlassColor ("_GlassColor", Color) = (1,1,1,1)
        _LocalLightOffset ("_LocalLightOffset", Vector) = (0,0,0,0)
        _MainColor ("_MainColor", Color) = (1,1,1,0)
        _ReflectionColor ("_ReflectionColor", Color) = (1,1,1,1)
        _SpecColor ("_SpecColor", Color) = (0.19999996,0.19999996,0.19999996,1)
        _SpecularColor ("_SpecularColor", Color) = (1,1,1,0)
        _WaterCausticChannel ("_WaterCausticChannel", Vector) = (1,0,0,0)
        _WaterCausticColor ("_WaterCausticColor", Color) = (0,1.1603823,2.3831515,1)
        _WaterCoastChannel ("_WaterCoastChannel", Vector) = (0,0,0,1)
        _WaterDepthColor ("_WaterDepthColor", Color) = (0,0.6320754,0.21609415,1)
        _WaterFoamChannel ("_WaterFoamChannel", Vector) = (0,0,0,0)
        _WaterFoamColor ("_WaterFoamColor", Color) = (7.999999,7.999999,7.999999,1)
        _WaterRippleData ("_WaterRippleData", Vector) = (0.76,2.4,-0.2,1.1)
        _WaterVPMatrixm0 ("_WaterVPMatrixm0", Vector) = (-1.732051,0,0,-20.7846)
        _WaterVPMatrixm1 ("_WaterVPMatrixm1", Vector) = (0,0,-1.732051,-66.5108)
        _WaterVPMatrixm2 ("_WaterVPMatrixm2", Vector) = (0,0,0,0.04163513)
        _WaterVPMatrixm3 ("_WaterVPMatrixm3", Vector) = (0,-0.9999999,0,583.9999)
        _AlphaClip ("_AlphaClip", Float) = 0
        _AlphaOffset ("_AlphaOffset", Float) = 0
        _Blend ("_Blend", Float) = 0
        _BumpScale ("_BumpScale", Float) = 1
        _Cull ("_Cull", Float) = 2
        _CustomBakedShadowScale ("_CustomBakedShadowScale", Float) = 1
        _CustomLightMapNormalBias ("_CustomLightMapNormalBias", Float) = 0
        _CustomLightMapScale ("_CustomLightMapScale", Float) = 1
        _Cutoff ("_Cutoff", Float) = 0.5
        _DepthOffset ("_DepthOffset", Float) = 0
        _DisableEnvironmentReflection ("_DisableEnvironmentReflection", Float) = 0
        _DisableMainCityFogMode ("_DisableMainCityFogMode", Float) = 0
        _DisablePremultipliedAlpha ("_DisablePremultipliedAlpha", Float) = 0
        _DisableReceiveShadow ("_DisableReceiveShadow", Float) = 0
        _DisableShadowCaster ("_DisableShadowCaster", Float) = 0
        _DisableSpecularHighlight ("_DisableSpecularHighlight", Float) = 0
        _DstBlend ("_DstBlend", Float) = 10
        _DstBlendAlpha ("_DstBlendAlpha", Float) = 1
        _EnvironmentAmbientFloor ("_EnvironmentAmbientFloor", Float) = 0.08
        _EnvironmentLegacyToneMap ("_EnvironmentLegacyToneMap", Float) = 1
        _EnvironmentReflections ("_EnvironmentReflections", Float) = 1
        _FresnelEffectBias ("_FresnelEffectBias", Float) = 0.169
        _GlassColorAngle ("_GlassColorAngle", Float) = 0.5
        _GlassColorIntensity ("_GlassColorIntensity", Float) = 1
        _GlossMapScale ("_GlossMapScale", Float) = 0
        _Glossiness ("_Glossiness", Float) = 0
        _GlossyReflections ("_GlossyReflections", Float) = 0
        _Metallic ("_Metallic", Float) = 0
        _MetallicOffset ("_MetallicOffset", Float) = 0
        _NormalScale ("_NormalScale", Float) = 1
        _OcclusionScale ("_OcclusionScale", Float) = 1
        _OcclusionStrength ("_OcclusionStrength", Float) = 1
        _QualityLevel ("_QualityLevel", Float) = 0
        _QueueOffset ("_QueueOffset", Float) = 0
        _ReceiveShadows ("_ReceiveShadows", Float) = 1
        _ReflectionFade ("_ReflectionFade", Float) = 0
        _ReflectionOcclusion ("_ReflectionOcclusion", Float) = 1
        _ReflectionRoughness ("_ReflectionRoughness", Float) = 0
        _RenderQueueOffset ("_RenderQueueOffset", Float) = -510
        _RenderType ("_RenderType", Float) = 3000
        _RoughNessOffset ("_RoughNessOffset", Float) = 0
        _RoughnessOffset ("_RoughnessOffset", Float) = 0
        _Smoothness ("_Smoothness", Float) = 0.5
        _SmoothnessTextureChannel ("_SmoothnessTextureChannel", Float) = 0
        _SpecularHighlights ("_SpecularHighlights", Float) = 1
        _SrcBlend ("_SrcBlend", Float) = 5
        _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
        _Surface ("_Surface", Float) = 0
        _UseAlphaClip ("_UseAlphaClip", Float) = 0
        _UseAlphaInput ("_UseAlphaInput", Float) = 1
        _UseCausticMap ("_UseCausticMap", Float) = 1
        _UseCoastMap ("_UseCoastMap", Float) = 1
        _UseCustomLightMap ("_UseCustomLightMap", Float) = 0
        _UseCustomQuality ("_UseCustomQuality", Float) = 0
        _UseCustomRendering ("_UseCustomRendering", Float) = 0
        _UseGlassRendering ("_UseGlassRendering", Float) = 0
        _UseMainMap ("_UseMainMap", Float) = 1
        _UseMixMap ("_UseMixMap", Float) = 0
        _UseNormalMap ("_UseNormalMap", Float) = 1
        _UseUnlitMode ("_UseUnlitMode", Float) = 0
        _Use_Reflection_Texture ("_Use_Reflection_Texture", Float) = 3
        _WaterCausticDepthScale ("_WaterCausticDepthScale", Float) = 0
        _WaterCausticDistortion ("_WaterCausticDistortion", Float) = 0.837
        _WaterCoastDistortion ("_WaterCoastDistortion", Float) = 0
        _WaterDepthFadeScale ("_WaterDepthFadeScale", Float) = 0.35
        _WaterFoamCoverage ("_WaterFoamCoverage", Float) = 0.66
        _WaterFoamDistortion ("_WaterFoamDistortion", Float) = 0.01
        _WaterFoamHighlightScale ("_WaterFoamHighlightScale", Float) = 0.116
        _WaterIgnoreMeshTangent ("_WaterIgnoreMeshTangent", Float) = 1
        _WaterRippleHeightScale ("_WaterRippleHeightScale", Float) = 10
        _WaterRippleRatio ("_WaterRippleRatio", Float) = 165
        _WaterVertexColorDebugMode ("_WaterVertexColorDebugMode", Float) = 0
        _WaterVertexNormalDebugMode ("_WaterVertexNormalDebugMode", Float) = 0
        _WaterVertexUVDebugMode ("_WaterVertexUVDebugMode", Float) = 0
        _WorkflowMode ("_WorkflowMode", Float) = 1
        _ZTest ("_ZTest", Float) = 4
        _ZWrite ("_ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
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
            #pragma fragment EnvironmentWaterFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #define ENV_SEA 1
            #include "EnvironmentWater.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
