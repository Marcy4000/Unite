Shader "==PGAME==/Fx/m_2P_fresnel_dissolve"
{
    Properties
            {
        [ToggleUI] _IF_ADD_MODE ("ADD 模式", Float) = 0
        [NoScaleOffset] _Tex1 ("MainTex", 2D) = "white" { }
        _tex1_st ("MainTex_ST", Vector) = (1,1,0,0)
        _Tex1_Speed ("MainTex Speed", Vector) = (0,0,0,0)
        [HDR] _Color1 ("MainTex Color", Color) = (0,0,1,1)
        _Emission1 ("MainTex Emission", Float) = 0.5
        [NoScaleOffset] _Tex2 ("Dissolve Tex", 2D) = "white" { }
        _tex2_st ("DissolveTex_ST", Vector) = (1,1,0,0)
        _Tex2_Speed ("DissolveTex_Speed", Vector) = (0,0,0,0)
        [HDR] _FresnelColor1 ("Fresnel Color", Color) = (1,0,0,1)
        _FresnelPower1 ("Fresnel Power", Float) = 2
        _FresnelEmission1 ("Fresnel Emission", Float) = 2
        _Alpha1 ("Alpha", Range(0, 1)) = 1
        _DissolveEdge1 ("Dissolve Edge", Range(0.5, 1)) = 1
        _DissolveOffset1 ("Dissolve Offset", Range(0, 1)) = 0.5
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
        float4 _Tex1_Speed;
        float4 _Color1;
        float _Emission1;
        float4 _tex2_st;
        float4 _Tex2_Speed;
        float4 _FresnelColor1;
        float _FresnelPower1;
        float _FresnelEmission1;
        float _Alpha1;
        float _DissolveEdge1;
        float _DissolveOffset1;
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
        #define FX_SECOND_TEXTURE 1
        #define FX_FRESNEL 1
        #define FX_DISSOLVE 1
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
