Shader "==PGAME==/Fx/m_1P_Base_Half"
{
    Properties
            {
        [ToggleUI] _IF_ADD_MODE ("ADD 模式", Float) = 1
        [NoScaleOffset] _Tex1 ("MainTex", 2D) = "white" { }
        _tex1_st ("MainTex_ST", Vector) = (1,1,0,0)
        _Power1 ("TexPower", Float) = 1
        [HDR] _Color1 ("Color", Color) = (1,1,1,1)
        _Emission1 ("Emission", Float) = 2
        _Alpha1 ("Alpha", Range(0, 1)) = 1
        _Zoffset ("Zoffset", Range(-5, 5)) = 0
        [HideInInspector] _RenderType ("Vector1", Float) = 0
        [HideInInspector] _Cull ("Vector1", Float) = 2
        [HideInInspector] _ZWrite ("Vector1", Float) = 1
        [HideInInspector] _ZTest ("Vector1", Float) = 4
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
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float _IF_ADD_MODE;
        float4 _tex1_st;
        float _Power1;
        float4 _Color1;
        float _Emission1;
        float _Alpha1;
        float _Zoffset;
        float _RenderType;
        float _Cull;
        float _ZWrite;
        float _ZTest;
        float _AlphaToMask;
        float _SrcBlendFactor;
        float _DstBlendFactor;
        float _SrcBlendFactorAlpha;
        float _DstBlendFactorAlpha;
        float _BlendOp;
        float _BlendOpAlpha;
        float _BlendMode;
        CBUFFER_END
        #define FX_BASE 1
        #define FX_BASE_HALF 1
        #include "FxCommon.hlsl"
        ENDHLSL
        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" "ColorOver" = "true" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FxVertex
            #pragma fragment FxFragment
            #pragma multi_compile _ FOG_LINEAR
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback Off
}
