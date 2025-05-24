//////////////////////////////////////////
//
// NOTE: This is *not* a valid shader file
//
///////////////////////////////////////////
Shader "UI/UI3D_goldcard" {
Properties {
[Header(Tex)] [MainTexture] _MainTexEX ("Diffuse", 2D) = "white" { } // Typo corretto
[NoScaleOffset] _MixMap ("Layermap(RGB)Depth(A)", 2D) = "black" { }
[Header(Layer1)] _Layer1Tex ("Distortion Map", 2D) = "black" { }
_Layer1Vector ("Panner(XY)Distortion(ZW)", Vector) = (0,0,0,0)
_Layer1Rotate ("Rotate", Range(0, 6.28)) = 0
_Layer1PulseClip ("Pulse Min", Range(0, 10)) = 0
_Layer1PulseIntensity ("Pulse Max", Range(0, 10)) = 1
_Layer1PulseRate ("Pulse Rate", Range(0, 30)) = 1
[Header(Layer2)] _Layer2Tex ("Effect Map", 2D) = "black" { }
_Layer2Vector ("Panner(XY)", Vector) = (0,0,0,0)
_Layer2Rotate ("Rotate", Range(0, 6.28)) = 0
_Layer2PulseClip ("Pulse Min", Range(0, 10)) = 0
_Layer2PulseIntensity ("Pulse Max", Range(0, 10)) = 1
_Layer2PulseRate ("Pulse Rate", Range(0, 30)) = 1
[KeywordEnum(ADD, BLEND)] _MODEL2 ("Blend Mode", Float) = 0
[HDR] _Layer2Color ("Color", Color) = (1,1,1,1)
[Header(Layer3)] _Layer3FTex ("Flowmap", 2D) = "black" { }
_Layer3Tex ("Distortion Map", 2D) = "black" { }
_Layer3Vector ("Panner(XY)", Vector) = (0,0,0,0) // ZW non usati nel GLSL originale per Layer3
_Layer3Rotate ("Rotate", Range(0, 6.28)) = 0
_Layer3PulseClip ("Pulse Min", Range(0, 10)) = 0
_Layer3PulseIntensity ("Pulse Max", Range(0, 10)) = 1
_Layer3PulseRate ("Pulse Rate", Range(0, 30)) = 1
[HDR] _Layer3Color ("Color", Color) = (1,1,1,1)
[KeywordEnum(ADD, BLEND)] _MODEL3 ("Blend Mode", Float) = 0
[HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
[HideInInspector] _Stencil ("Stencil ID", Float) = 0
[HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
[HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
[HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
[HideInInspector] _ColorMask ("Color Mask", Float) = 15
[Toggle(UNITY_UI_ALPHACLIP)] [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
}
SubShader {
 Tags { "IGNOREPROJECTOR" = "true" "PreviewType" = "Plane" "QUEUE" = "Transparent" "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" }
 Pass {
  Name "ForwardLit" // o "Unlit" o "Pass"
  Tags { "CanUseSpriteAtlas" = "true" "IGNOREPROJECTOR" = "true" "PreviewType" = "Plane" "QUEUE" = "Transparent" "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  ZTest Always // O [_ZTest] se si vuole configurare, o LEqual per UI standard
  ZWrite Off
  Stencil {
   ReadMask [_StencilReadMask]
   WriteMask [_StencilWriteMask]
   Comp [_StencilComp]
   Pass [_StencilOp]
   Fail Keep
   ZFail Keep
  }

HLSLPROGRAM
#pragma vertex VertexFunction
#pragma fragment FragmentFunction

#pragma multi_compile_local __ _MODEL2_ADD
#pragma multi_compile_local __ _MODEL3_ADD
#pragma shader_feature_local UNITY_UI_ALPHACLIP

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _MainTexEX_ST;
    float4 _Layer1Tex_ST;
    float4 _Layer2Tex_ST;
    float4 _Layer3FTex_ST;
    float4 _Layer3Tex_ST;

    float4 _Layer1Vector;
    float _Layer1Rotate;
    float _Layer1PulseClip;
    float _Layer1PulseIntensity;
    float _Layer1PulseRate;

    float4 _Layer2Vector;
    float _Layer2Rotate;
    float _Layer2PulseClip;
    float _Layer2PulseIntensity;
    float _Layer2PulseRate;
    half4 _Layer2Color;

    float4 _Layer3Vector;
    float _Layer3Rotate;
    float _Layer3PulseClip;
    float _Layer3PulseIntensity;
    float _Layer3PulseRate;
    half4 _Layer3Color;
    
    float _Cutoff;
CBUFFER_END

TEXTURE2D(_MainTexEX);       SAMPLER(sampler_MainTexEX);
TEXTURE2D(_MixMap);          SAMPLER(sampler_MixMap);
TEXTURE2D(_Layer1Tex);       SAMPLER(sampler_Layer1Tex);
TEXTURE2D(_Layer2Tex);       SAMPLER(sampler_Layer2Tex);
TEXTURE2D(_Layer3FTex);      SAMPLER(sampler_Layer3FTex);
TEXTURE2D(_Layer3Tex);       SAMPLER(sampler_Layer3Tex);

struct Attributes
{
    float4 positionOS   : POSITION;
    float2 uv           : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uvMain       : TEXCOORD0;
    float2 uvLayer1     : TEXCOORD1;
    float2 uvLayer2     : TEXCOORD2;
    float2 uvLayer3Flow : TEXCOORD3;
    // float2 uvLayer3Distort : TEXCOORD4; // Se Layer3Tex usa UV diverse da Layer3Flow + panner
    float3 pulseFactors : TEXCOORD5; // x:L1, y:L2, z:L3
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings VertexFunction(Attributes IN)
{
    Varyings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
    OUT.uvMain = IN.uv * _MainTexEX_ST.xy + _MainTexEX_ST.zw;

    float2 centered_uv = IN.uv - 0.5;

    // Layer 1 UVs (Panner + Rotate)
    float2 panned_uv1 = frac(_Time.x * _Layer1Vector.xy);
    float s1 = sin(_Layer1Rotate);
    float c1 = cos(_Layer1Rotate);
    float2x2 rotMatrix1 = float2x2(c1, s1, -s1, c1); // Per x'=x*c+y*s, y'=y*c-x*s
    float2 rotated_uv1 = mul(rotMatrix1, centered_uv) + 0.5;
    OUT.uvLayer1 = rotated_uv1 * _Layer1Tex_ST.xy + _Layer1Tex_ST.zw + panned_uv1;

    // Layer 2 UVs (Panner + Rotate)
    float2 panned_uv2 = frac(_Time.x * _Layer2Vector.xy);
    float s2 = sin(_Layer2Rotate);
    float c2 = cos(_Layer2Rotate);
    float2x2 rotMatrix2 = float2x2(c2, s2, -s2, c2);
    float2 rotated_uv2 = mul(rotMatrix2, centered_uv) + 0.5;
    OUT.uvLayer2 = rotated_uv2 * _Layer2Tex_ST.xy + _Layer2Tex_ST.zw + panned_uv2;
    
    // Layer 3 Flowmap UVs (Rotate) - _Layer3Vector è per il panner della texture disturbata
    float s3 = sin(_Layer3Rotate);
    float c3 = cos(_Layer3Rotate);
    float2x2 rotMatrix3 = float2x2(c3, s3, -s3, c3);
    float2 rotated_uv3_flow = mul(rotMatrix3, centered_uv) + 0.5;
    OUT.uvLayer3Flow = rotated_uv3_flow * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    // OUT.uvLayer3Distort = rotated_uv3_flow * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw; // Se _Layer3Tex usa stesse UV base di flowmap

    // Pulse factors
    float3 pulseTimes = _Time.xxx * float3(_Layer1PulseRate, _Layer2PulseRate, _Layer3PulseRate);
    float3 pulseCos = cos(frac(pulseTimes) * TWO_PI); // TWO_PI = 6.28318530718
    float3 pulse01 = pulseCos * 0.5 + 0.5;
    OUT.pulseFactors.x = lerp(_Layer1PulseClip, _Layer1PulseIntensity, pulse01.x);
    OUT.pulseFactors.y = lerp(_Layer2PulseClip, _Layer2PulseIntensity, pulse01.y);
    OUT.pulseFactors.z = lerp(_Layer3PulseClip, _Layer3PulseIntensity, pulse01.z);
    
    return OUT;
}

half4 FragmentFunction(Varyings IN) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    half4 mixMap = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, IN.uvMain);

    // Layer 1 (Distortion)
    half layer1Sampler = SAMPLE_TEXTURE2D(_Layer1Tex, sampler_Layer1Tex, IN.uvLayer1).x;
    half layer1DistortFactor = layer1Sampler * IN.pulseFactors.x;
    float2 distortedMainUV = IN.uvMain + mixMap.r * layer1DistortFactor * _Layer1Vector.zw;
    half4 mainColor = SAMPLE_TEXTURE2D(_MainTexEX, sampler_MainTexEX, distortedMainUV);

    // Layer 2
    half4 layer2TexColor = SAMPLE_TEXTURE2D(_Layer2Tex, sampler_Layer2Tex, IN.uvLayer2);
    half3 effect2Color = layer2TexColor.rgb * _Layer2Color.rgb;
    half layer2BlendFactor = mixMap.g * IN.pulseFactors.y * layer2TexColor.a; // Usiamo alpha di Layer2Tex
    #if _MODEL2_ADD
        mainColor.rgb += effect2Color * layer2BlendFactor;
    #else // BLEND
        mainColor.rgb = lerp(mainColor.rgb, effect2Color, layer2BlendFactor);
    #endif

    // Layer 3 (Flowmap + Distortion)
    half2 flowmapVal = SAMPLE_TEXTURE2D(_Layer3FTex, sampler_Layer3FTex, IN.uvLayer3Flow).xy;
    // Il GLSL originale applica il panner _Layer3Vector.xy DOPO aver aggiunto flowmapVal.
    // E _Layer3Tex_ST viene applicato a questa UV combinata.
    float2 uvLayer3Distort_panned = frac((_Time.x * _Layer3Vector.xy) + flowmapVal);
    uvLayer3Distort_panned = uvLayer3Distort_panned * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    half4 layer3TexColor = SAMPLE_TEXTURE2D(_Layer3Tex, sampler_Layer3Tex, uvLayer3Distort_panned);
    
    half3 effect3Color = layer3TexColor.rgb * _Layer3Color.rgb;
    half layer3BlendFactor = mixMap.b * IN.pulseFactors.z * layer3TexColor.a; // Usiamo alpha di Layer3Tex
    #if _MODEL3_ADD
        mainColor.rgb += effect3Color * layer3BlendFactor;
    #else // BLEND
        mainColor.rgb = lerp(mainColor.rgb, effect3Color, layer3BlendFactor);
    #endif

    #if UNITY_UI_ALPHACLIP
        clip(mainColor.a - _Cutoff);
    #endif

    return mainColor;
}
ENDHLSL
 }
}
Fallback "Hidden/InternalErrorShader"
}