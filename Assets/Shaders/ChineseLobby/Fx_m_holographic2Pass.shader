Shader "==PGAME==/Fx2/m_holographic2Pass"
{
    Properties
            {
        [Header(Tex)] _MainTex ("细节扰动贴图", 2D) = "white" { }
        _MixMap ("基础贴图", 2D) = "white" { }
        [HDR] _BaseColor ("基础颜色", Color) = (0,0,0,0.1)
        [HDR] _RimColor ("边缘颜色", Color) = (0,0,0,1)
        _MainTexST ("细节图缩放和流动", Vector) = (1,1,0,0)
        _MixMapST ("基础贴图缩放和流动", Vector) = (1,1,0,0)
        _BlockSize ("扰动方块的大小", Range(0, 10)) = 1
        _minlight ("调整闪烁的最低亮度", Range(0, 1)) = 0.65
        [KeywordEnum(OFF, ON)] _TEX ("是否使用贴图控制基础色", Float) = 0
        [KeywordEnum(OFF, ON)] _WSUV ("细节扰动贴图是否跟随UV", Float) = 0
        [HideInInspector] _RenderType ("Vector1 ", Float) = 0
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
        [HideInInspector] _QueueOffset ("Queue offset", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "IgnoreProjector" = "true" }
        LOD 300
        Blend [_SrcBlendFactor] [_DstBlendFactor], [_SrcBlendFactorAlpha] [_DstBlendFactorAlpha]
        BlendOp [_BlendOp], [_BlendOpAlpha]
        AlphaToMask [_AlphaToMask]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull Back
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _RimColor;
        float4 _MainTexST;
        float4 _MixMapST;
        float _BlockSize;
        float _minlight;
        float _TEX;
        float _WSUV;
        float _RenderType;
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
        float _QueueOffset;
        float _Cutoff;
        float4 _BaseMap_ST;
        CBUFFER_END
        #include "FxHolographic.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Stencil { Ref 1 Comp Equal Pass Keep Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FxHolographicVertex
            #pragma fragment FxHolographicFragment
            #pragma shader_feature_local _TEX_OFF _TEX_ON
            #pragma shader_feature_local _WSUV_OFF _WSUV_ON
            #pragma multi_compile _ _VROTATE_ON
            #pragma multi_compile _ FOG_LINEAR
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "HolographicStencil"
            Tags { "LightMode" = "FurPass1" }
            ColorMask 0
            Stencil { Ref 1 Comp Always Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FxHolographicStencilVertex
            #pragma fragment FxHolographicStencilFragment
            #pragma multi_compile _ _VROTATE_ON
            #pragma multi_compile _ FOG_LINEAR
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ZTest LEqual
            Cull Off
            Blend One Zero
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FxHolographicDepthVertex
            #pragma fragment FxHolographicDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "Lightweight2D"
            Tags { "LightMode" = "Universal2D" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FxHolographic2DVertex
            #pragma fragment FxHolographic2DFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback Off
}
