Shader "==PGAME==/Fx/m_1P_scroll"
{
    Properties
            {
        [ToggleUI] _IF_ADD_MODE ("ADD 模式", Float) = 0
        [NoScaleOffset] _Tex1 ("MainTex", 2D) = "white" { }
        _tex1_st ("MainTex_ST", Vector) = (1,1,0,0)
        _Tex1_Scroll ("MainTex_Scroll", Vector) = (0,0,0,0)
        [HDR] _Color1 ("Color", Color) = (1,1,1,1)
        Vector1_AC3F40EB ("Emission", Float) = 2
        _Alpha1 ("Alpha", Range(0, 1)) = 1
        [ToggleUI] _use_mask ("使用快捷MASK", Float) = 0
        [ToggleUI] _Vmask ("Vmask", Float) = 0
        _maskoffset ("Mask offset", Range(-1, 1)) = 1
        _maskedge ("Mask Edge", Range(0.5, 1)) = 0.5
        _Zoffset ("Zoffset", Range(-3, 3)) = 0
        [HideInInspector] _RenderType ("Vector1", Float) = 0
        [HideInInspector] _Cull ("Vector1", Float) = 0
        [HideInInspector] _ZWrite ("Vector1", Float) = 1
        [HideInInspector] _ZTest ("Vector1", Float) = 4
        [HideInInspector] _StencilData ("Vector1", Float) = 0
        [HideInInspector] _StencilReadMask ("Vector1", Float) = 255
        [HideInInspector] _StencilWriteMask ("Vector1", Float) = 255
        [HideInInspector] _StencilComp ("Vector1", Float) = 8
        [HideInInspector] _StencilPassOp ("Vector1", Float) = 0
        [HideInInspector] _StencilFailOp ("Vector1", Float) = 0
        [HideInInspector] _StencilZFailOp ("Vector1", Float) = 0
        [HideInInspector] _AlphaToMask ("Vector1", Float) = 0
        [HideInInspector] _SrcBlendFactor ("Vector1", Float) = 5
        [HideInInspector] _DstBlendFactor ("Vector1", Float) = 10
        [HideInInspector] _SrcBlendFactorAlpha ("Vector1", Float) = 5
        [HideInInspector] _DstBlendFactorAlpha ("Vector1", Float) = 10
        [HideInInspector] _BlendOp ("Vector1", Float) = 0
        [HideInInspector] _BlendOpAlpha ("Vector1", Float) = 0
        [HideInInspector] _BlendMode ("Vector1", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Blend [_SrcBlendFactor] [_DstBlendFactor], [_SrcBlendFactorAlpha] [_DstBlendFactorAlpha]
        BlendOp [_BlendOp], [_BlendOpAlpha]
        AlphaToMask [_AlphaToMask]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull [_Cull]
        Stencil
        {
            Ref [_StencilData]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
            Comp [_StencilComp]
            Pass [_StencilPassOp]
            Fail [_StencilFailOp]
            ZFail [_StencilZFailOp]
        }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float _IF_ADD_MODE;
        float4 _tex1_st;
        float4 _Tex1_Scroll;
        float4 _Color1;
        float Vector1_AC3F40EB;
        float _Alpha1;
        float _use_mask;
        float _Vmask;
        float _maskoffset;
        float _maskedge;
        float _Zoffset;
        float _RenderType;
        float _Cull;
        float _ZWrite;
        float _ZTest;
        float _StencilData;
        float _StencilReadMask;
        float _StencilWriteMask;
        float _StencilComp;
        float _StencilPassOp;
        float _StencilFailOp;
        float _StencilZFailOp;
        float _AlphaToMask;
        float _SrcBlendFactor;
        float _DstBlendFactor;
        float _SrcBlendFactorAlpha;
        float _DstBlendFactorAlpha;
        float _BlendOp;
        float _BlendOpAlpha;
        float _BlendMode;
        CBUFFER_END
        #define FX_SCROLL 1
        #define FX_SCROLL_EMISSION 1
        #define FX_QUICK_MASK 1
        #include "FxCommon.hlsl"
        ENDHLSL
        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FxVertex
            #pragma fragment FxFragment
            #pragma multi_compile _ FOG_LINEAR
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FxShadowVertex
            #pragma fragment FxShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "FxShadow.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
