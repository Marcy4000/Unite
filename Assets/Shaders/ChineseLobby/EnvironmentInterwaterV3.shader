Shader "==PGAME==/BattleV3/m_bat_interwaterV3_lv3"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("水滩Alpha贴图R(边缘)G(Alpha)", 2D) = "white" { }
        _WaveScale ("海浪密度 ", Range(1, 50)) = 20
        _WaveHeight ("海浪高度 ", Range(0, 1)) = 0.5
        _foam1 ("浪边厚度2", Range(0, 1)) = 0.55
        _foam2 ("浪边厚度2", Range(0, 1)) = 0.6
        _UVScallePaner ("UVScallePaner", Vector) = (1,1,0,0)
        [NoScaleOffset] _BumpMap ("NormalMap", 2D) = "bump" { }
        _NormalScale ("NormalScale", Range(-4, 4)) = 1
        _BumpTiling ("Bump Tiling", Vector) = (1,-1,1.5,2)
        _BumpDirection ("Bump Direction & Speed", Vector) = (0.5,-0.5,-0.35,-0.35)
        [NoScaleOffset] _Caustic ("水滩底部贴图", 2D) = "white" { }
        [HDR] _CausticColor ("水滩底部颜色", Color) = (1,1,1,1)
        [HDR] _FoamColor ("水边颜色", Color) = (1,1,1,1)
        _causticscaleoffset ("水滩底部缩放与偏移", Vector) = (1,1,0,0)
        _CausticScale ("水底扰动强度 ", Range(0.004, 0.5)) = 0.1
        _HeightBlendWeight ("HeightBlendWeight ", Range(0.004, 1)) = 0.5
        _FrontDualParaboloidMap ("环境光贴图", 2D) = "white" { }
        [HDR] _SpecularColor ("环境光颜色", Color) = (1,1,1,0)
        [HideInInspector] _RenderType ("Vector1 ", Float) = 0
        [HideInInspector] _Cull ("Vector1 ", Float) = 2
        [HideInInspector] _ZWrite ("Vector1 ", Float) = 1
        [HideInInspector] _ZTest ("Vector1 ", Float) = 4
        [HideInInspector] _AlphaToMask ("Vector1 ", Float) = 0
        [HideInInspector] _SrcBlendFactor ("Vector1 ", Float) = 5
        [HideInInspector] _DstBlendFactor ("Vector1 ", Float) = 10
        [HideInInspector] _SrcBlendFactorAlpha ("Vector1 ", Float) = 5
        [HideInInspector] _DstBlendFactorAlpha ("Vector1 ", Float) = 10
        [HideInInspector] _BlendOp ("Vector1 ", Float) = 0
        [HideInInspector] _BlendOpAlpha ("Vector1 ", Float) = 0
        [HideInInspector] _BlendMode ("Vector1 ", Float) = 0
        _EmissionColor ("_EmissionColor", Color) = (0,0,0,1)
        _GlassColor ("_GlassColor", Color) = (1,1,1,1)
        _LocalLightOffset ("_LocalLightOffset", Vector) = (0,0,0,0)
        _MainColor ("_MainColor", Color) = (1,1,1,0)
        _ReflectionColor ("_ReflectionColor", Color) = (1,1,1,1)
        _AlphaClip ("_AlphaClip", Float) = 0
        _AlphaOffset ("_AlphaOffset", Float) = 0
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
        _EnvironmentLegacyToneMap ("_EnvironmentLegacyToneMap", Float) = 1
        _GlassColorAngle ("_GlassColorAngle", Float) = 0.5
        _GlassColorIntensity ("_GlassColorIntensity", Float) = 1
        _Metallic ("_Metallic", Float) = 0
        _MetallicOffset ("_MetallicOffset", Float) = 0
        _OcclusionScale ("_OcclusionScale", Float) = 1
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
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Geometry+300" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            AlphaToMask [_AlphaToMask]
            Blend [_SrcBlendFactor] [_DstBlendFactor], [_SrcBlendFactorAlpha] [_DstBlendFactorAlpha]
            BlendOp [_BlendOp], [_BlendOpAlpha]
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
            #define ENV_INTERWATER 1
            #include "EnvironmentWater.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
