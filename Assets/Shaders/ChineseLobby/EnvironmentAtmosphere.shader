Shader "==PGAME==/MainCity/atmosphere"
{
    Properties
    {
        _CloudTex ("_CloudTex", 2D) = "black" {}
        _NoiseTex ("_NoiseTex", 2D) = "gray" {}
        _Color0 ("_Color0", Color) = (0.43529415,0.6569937,1,1)
        _Color1 ("_Color1", Color) = (1,1,1,1)
        _Color10 ("_Color10", Color) = (0.8705883,0.9333334,0.9333334,1)
        _Color11 ("_Color11", Color) = (0.46226418,0.8684662,1,1)
        _Color12 ("_Color12", Color) = (1.4142135,1.3353387,0.25349113,1)
        _Color2 ("_Color2", Color) = (0.34665364,0.68567586,0.8962264,1)
        _Color3 ("_Color3", Color) = (0.49341398,0.7923257,0.9339623,1)
        _Color4 ("_Color4", Color) = (0.5849056,0.9059119,1,1)
        _Color5 ("_Color5", Color) = (0.98541766,1,0.745283,1)
        _Color6 ("_Color6", Color) = (0.85039157,0.92786014,0.990566,1)
        _Color7 ("_Color7", Color) = (0.77254903,0.77254903,0.93334,1)
        _Color8 ("_Color8", Color) = (0.90588236,0.90588236,0.90588236,1)
        _Color9 ("_Color9", Color) = (0.87055105,0.8773585,0.78631186,1)
        _EmissionColor ("_EmissionColor", Color) = (0,0,0,1)
        _GlassColor ("_GlassColor", Color) = (1,1,1,1)
        _LocalLightOffset ("_LocalLightOffset", Vector) = (0,0,0,0)
        _MainColor ("_MainColor", Color) = (1,1,1,0)
        _ReflectionColor ("_ReflectionColor", Color) = (1,1,1,1)
        _SpecularColor ("_SpecularColor", Color) = (1,1,1,0)
        _fogRange ("_fogRange", Vector) = (34.1,5,22.1,1)
        _horizonAdjust ("_horizonAdjust", Vector) = (221.17,0.9,0,0)
        _rimAdjust ("_rimAdjust", Vector) = (212,0,0,0)
        _AlphaClip ("_AlphaClip", Float) = 0
        _AlphaOffset ("_AlphaOffset", Float) = 0
        _CloudRange ("_CloudRange", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 1
        _CustomBakedShadowScale ("_CustomBakedShadowScale", Float) = 1
        _CustomLightMapScale ("_CustomLightMapScale", Float) = 1
        _Cutoff ("_Cutoff", Float) = 0.5
        _DepthOffset ("_DepthOffset", Float) = 0
        _DisableEnvironmentReflection ("_DisableEnvironmentReflection", Float) = 0
        _DisableMainCityFogMode ("_DisableMainCityFogMode", Float) = 0
        _DisablePremultipliedAlpha ("_DisablePremultipliedAlpha", Float) = 0
        _DisableReceiveShadow ("_DisableReceiveShadow", Float) = 0
        _DisableShadowCaster ("_DisableShadowCaster", Float) = 0
        _DisableSpecularHighlight ("_DisableSpecularHighlight", Float) = 0
        _DstBlend ("_DstBlend", Float) = 0
        _DstBlendAlpha ("_DstBlendAlpha", Float) = 0
        _EnvironmentAmbientFloor ("_EnvironmentAmbientFloor", Float) = 0.08
        _EnvironmentLegacyToneMap ("_EnvironmentLegacyToneMap", Float) = 0
        _FrontBack ("_FrontBack", Float) = 0
        _GlassColorAngle ("_GlassColorAngle", Float) = 0.5
        _GlassColorIntensity ("_GlassColorIntensity", Float) = 1
        _Intensity ("_Intensity", Float) = 1
        _LayerCloud ("_LayerCloud", Float) = 0
        _LayerCloudMask ("_LayerCloudMask", Float) = 0.66
        _Metallic ("_Metallic", Float) = 0
        _MetallicOffset ("_MetallicOffset", Float) = 0
        _NoiseIntensity ("_NoiseIntensity", Float) = 0.0036
        _NoiseMoveX ("_NoiseMoveX", Float) = -0.0367
        _NoiseMoveY ("_NoiseMoveY", Float) = 0.0259
        _NoiseTilling ("_NoiseTilling", Float) = 9.92
        _NormalScale ("_NormalScale", Float) = 1
        _OcclusionScale ("_OcclusionScale", Float) = 1
        _QualityLevel ("_QualityLevel", Float) = 0
        _ReflectionOcclusion ("_ReflectionOcclusion", Float) = 1
        _ReflectionRoughness ("_ReflectionRoughness", Float) = 0
        _RoughNessOffset ("_RoughNessOffset", Float) = 0
        _RoughnessOffset ("_RoughnessOffset", Float) = 0
        _SrcBlend ("_SrcBlend", Float) = 1
        _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
        _UseAlphaClip ("_UseAlphaClip", Float) = 0
        _UseAlphaInput ("_UseAlphaInput", Float) = 0
        _UseCustomLightMap ("_UseCustomLightMap", Float) = 0
        _UseGlassRendering ("_UseGlassRendering", Float) = 0
        _UseMixMap ("_UseMixMap", Float) = 0
        _UseNormalMap ("_UseNormalMap", Float) = 0
        _UseUnlitMode ("_UseUnlitMode", Float) = 0
        _Use_Reflection_Texture ("_Use_Reflection_Texture", Float) = 0
        _ZTest ("_ZTest", Float) = 4
        _ZWrite ("_ZWrite", Float) = 1
        _heightAdjust ("_heightAdjust", Float) = 0.84
        _horizonWidth ("_horizonWidth", Float) = 7
        _rimRange ("_rimRange", Float) = 0.392
        _rimSoft ("_rimSoft", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Background" "Queue"="Background" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            Blend One Zero
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EnvironmentVertex
            #pragma fragment EnvironmentSkyFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #define ENV_SKY 1
            #include "EnvironmentSky.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
