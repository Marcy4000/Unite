//////////////////////////////////////////
//
// NOTE: This is *not* a valid shader file
//
///////////////////////////////////////////
Shader "UI/UI3D_goldcard" {
Properties {
[Header(Tex)] [MainTexture] _MainTexEX ("Diffiuse", 2D) = "white" { }
[NoScaleOffset] _MixMap ("Layermap(RGB)Depth(A)", 2D) = "black" { }
[Header(Layer1)] _Layer1Tex ("扰动图", 2D) = "black" { }
_Layer1Vector ("Panner(XY)Distortion(ZW)", Vector) = (0,0,0,0)
_Layer1Rotate ("Rotate", Range(0, 6.28)) = 0
_Layer1PulseClip ("闪烁最小值", Range(0, 10)) = 0
_Layer1PulseIntensity ("闪烁最大值", Range(0, 10)) = 1
_Layer1PulseRate ("闪烁频率", Range(0, 30)) = 1
[Header(Layer2)] _Layer2Tex ("特效图", 2D) = "black" { }
_Layer2Vector ("Panner(XY)", Vector) = (0,0,0,0)
_Layer2Rotate ("Rotate", Range(0, 6.28)) = 0
_Layer2PulseClip ("闪烁最小值", Range(0, 10)) = 0
_Layer2PulseIntensity ("闪烁最大值", Range(0, 10)) = 1
_Layer2PulseRate ("闪烁频率", Range(0, 30)) = 1
[KeywordEnum(ADD, BLEND)] _MODEL2 ("混合模式", Float) = 0
[HDR] _Layer2Color ("Color", Color) = (1,1,1,1)
[Header(Layer3)] _Layer3FTex ("Flowmap", 2D) = "black" { }
_Layer3Tex ("扰动图", 2D) = "black" { }
_Layer3Vector ("Panner(XY)", Vector) = (0,0,0,0)
_Layer3Rotate ("Rotate", Range(0, 6.28)) = 0
_Layer3PulseClip ("闪烁最小值", Range(0, 10)) = 0
_Layer3PulseIntensity ("闪烁最大值", Range(0, 10)) = 1
_Layer3PulseRate ("闪烁频率", Range(0, 30)) = 1
[HDR] _Layer3Color ("Color", Color) = (1,1,1,1)
[KeywordEnum(ADD, BLEND)] _MODEL3 ("混合模式", Float) = 0
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
 LOD 300
 Tags { "IGNOREPROJECTOR" = "true" "PreviewType" = "Plane" "QUEUE" = "Transparent" "RenderPipeline" = "LightweightPipeline" "RenderType" = "Transparent" }
 Pass {
  Name "ForwardLit"
  LOD 300
  Tags { "CanUseSpriteAtlas" = "true" "IGNOREPROJECTOR" = "true" "PreviewType" = "Plane" "QUEUE" = "Transparent" "RenderPipeline" = "LightweightPipeline" "RenderType" = "Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
  ColorMask 0 0
  ZTest Always
  ZWrite Off
  Stencil {
   ReadMask 0
   WriteMask 0
   Comp Disabled
   Pass Keep
   Fail Keep
   ZFail Keep
  }
  GpuProgramID 61739
Program "vp" {
SubProgram "gles " {
"// hash: f9d457058ec85b38
#ifdef VERTEX
#version 100

uniform 	vec4 _Time;
uniform 	vec3 _WorldSpaceCameraPos;
uniform 	vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
uniform 	mediump vec4 _MainTexEX_ST;
uniform 	mediump vec4 _Layer1Tex_ST;
uniform 	mediump vec4 _Layer2Tex_ST;
uniform 	mediump vec4 _Layer3FTex_ST;
uniform 	mediump vec4 _Layer1Vector;
uniform 	float _Layer1Rotate;
uniform 	mediump float _Layer1PulseRate;
uniform 	mediump float _Layer1PulseClip;
uniform 	mediump float _Layer1PulseIntensity;
uniform 	mediump vec4 _Layer2Vector;
uniform 	float _Layer2Rotate;
uniform 	mediump float _Layer2PulseRate;
uniform 	mediump float _Layer2PulseClip;
uniform 	mediump float _Layer2PulseIntensity;
uniform 	float _Layer3Rotate;
uniform 	mediump float _Layer3PulseRate;
uniform 	mediump float _Layer3PulseClip;
uniform 	mediump float _Layer3PulseIntensity;
attribute highp vec4 in_POSITION0;
attribute highp vec2 in_TEXCOORD0;
varying highp vec4 vs_TEXCOORD0;
varying highp vec4 vs_TEXCOORD1;
varying mediump vec4 vs_TEXCOORD2;
varying mediump vec3 vs_TEXCOORD3;
vec4 u_xlat0;
vec4 u_xlat1;
vec3 u_xlat2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
vec2 u_xlat10;
vec2 u_xlat11;
void main()
{
    u_xlat0.xy = _Time.xx * _Layer1Vector.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat1.x = sin(_Layer1Rotate);
    u_xlat2.x = cos(_Layer1Rotate);
    u_xlat2.yz = u_xlat1.xx;
    u_xlat1.xyz = u_xlat2.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat10.xy = in_TEXCOORD0.xy + vec2(-0.5, -0.5);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat1.xy = u_xlat11.xy + vec2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * _Layer1Tex_ST.xy + u_xlat0.xy;
    vs_TEXCOORD0.zw = u_xlat0.xy + _Layer1Tex_ST.zw;
    vs_TEXCOORD0.xy = in_TEXCOORD0.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;
    u_xlat0.x = sin(_Layer2Rotate);
    u_xlat1.x = cos(_Layer2Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat0.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat0.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy + vec2(0.5, 0.5);
    u_xlat1.xy = _Time.xx * _Layer2Vector.xy;
    u_xlat1.xy = fract(u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer2Tex_ST.xy + u_xlat1.xy;
    vs_TEXCOORD1.xy = u_xlat0.xy + _Layer2Tex_ST.zw;
    u_xlat0.x = sin(_Layer3Rotate);
    u_xlat1.x = cos(_Layer3Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat11.xy + vec2(0.5, 0.5);
    vs_TEXCOORD1.zw = u_xlat0.xy * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    vs_TEXCOORD2.w = 1.0;
    u_xlat0.xyz = in_POSITION0.yyy * hlslcc_mtx4x4unity_ObjectToWorld[1].xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[0].xyz * in_POSITION0.xxx + u_xlat0.xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[2].xyz * in_POSITION0.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + hlslcc_mtx4x4unity_ObjectToWorld[3].xyz;
    u_xlat1.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    vs_TEXCOORD2.xyz = u_xlat1.xyz;
    u_xlat1.x = _Time.x * _Layer1PulseRate;
    u_xlat1.y = _Time.x * _Layer2PulseRate;
    u_xlat1.z = _Time.x * _Layer3PulseRate;
    u_xlat1.xyz = fract(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(6.23999977, 6.23999977, 6.23999977);
    u_xlat1.xyz = cos(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, 0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat16_3.x = _Layer1PulseClip;
    u_xlat16_4.x = (-u_xlat16_3.x) + _Layer1PulseIntensity;
    u_xlat16_3.y = _Layer2PulseClip;
    u_xlat16_4.y = (-u_xlat16_3.y) + _Layer2PulseIntensity;
    u_xlat16_3.z = _Layer3PulseClip;
    u_xlat16_4.z = (-u_xlat16_3.z) + _Layer3PulseIntensity;
    vs_TEXCOORD3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat1 = u_xlat0.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
    u_xlat1 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat0.zzzz + u_xlat1;
    gl_Position = u_xlat0 + hlslcc_mtx4x4unity_MatrixVP[3];
    return;
}

#endif
#ifdef FRAGMENT
#version 100

#ifdef GL_FRAGMENT_PRECISION_HIGH
    precision highp float;
#else
    precision mediump float;
#endif
precision highp int;
uniform 	vec4 _Time;
uniform 	vec4 _Layer3Tex_ST;
uniform 	mediump vec4 _Layer1Vector;
uniform 	mediump vec4 _Layer2Color;
uniform 	mediump vec4 _Layer3Color;
uniform 	vec4 _Layer3Vector;
uniform lowp sampler2D _MixMap;
uniform lowp sampler2D _Layer1Tex;
uniform lowp sampler2D _Layer2Tex;
uniform lowp sampler2D _Layer3FTex;
uniform lowp sampler2D _Layer3Tex;
uniform lowp sampler2D _MainTexEX;
varying highp vec4 vs_TEXCOORD0;
varying highp vec4 vs_TEXCOORD1;
varying mediump vec3 vs_TEXCOORD3;
#define SV_Target0 gl_FragData[0]
vec2 u_xlat0;
lowp vec4 u_xlat10_0;
vec2 u_xlat1;
lowp vec4 u_xlat10_1;
mediump vec4 u_xlat16_2;
lowp vec4 u_xlat10_3;
mediump vec3 u_xlat16_4;
lowp vec3 u_xlat10_6;
mediump vec2 u_xlat16_7;
float u_xlat10;
void main()
{
    u_xlat10_0.xy = texture2D(_Layer3FTex, vs_TEXCOORD1.zw).xy;
    u_xlat10 = fract(_Time.x);
    u_xlat0.xy = _Layer3Vector.xy * vec2(u_xlat10) + u_xlat10_0.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    u_xlat10_0 = texture2D(_Layer3Tex, u_xlat0.xy);
    u_xlat10_1.x = texture2D(_Layer1Tex, vs_TEXCOORD0.zw).x;
    u_xlat1.x = u_xlat10_1.x * vs_TEXCOORD3.x;
    u_xlat10_6.xyz = texture2D(_MixMap, vs_TEXCOORD0.xy).xyz;
    u_xlat16_2.x = u_xlat10_6.x * u_xlat1.x;
    u_xlat16_7.xy = u_xlat10_6.yz * vs_TEXCOORD3.yz;
    u_xlat1.xy = u_xlat16_2.xx * _Layer1Vector.zw + vs_TEXCOORD0.xy;
    u_xlat10_1 = texture2D(_MainTexEX, u_xlat1.xy);
    u_xlat10_3 = texture2D(_Layer2Tex, vs_TEXCOORD1.xy);
    u_xlat16_4.xyz = u_xlat10_3.xyz * _Layer2Color.xyz + (-u_xlat10_1.xyz);
    u_xlat16_2.x = u_xlat16_7.x * u_xlat10_3.w;
    u_xlat16_7.x = u_xlat10_0.w * u_xlat16_7.y;
    u_xlat16_2.xzw = u_xlat16_2.xxx * u_xlat16_4.xyz + u_xlat10_1.xyz;
    SV_Target0.w = u_xlat10_1.w;
    u_xlat16_4.xyz = u_xlat10_0.xyz * _Layer3Color.xyz + (-u_xlat16_2.xzw);
    SV_Target0.xyz = u_xlat16_7.xxx * u_xlat16_4.xyz + u_xlat16_2.xzw;
    return;
}

#endif
"
}
SubProgram "gles3 " {
"// hash: 7d30ace151a0a4a8
#ifdef VERTEX
#version 300 es

#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
#define UNITY_UNIFORM
#else
#define UNITY_UNIFORM uniform
#endif
#define UNITY_SUPPORTS_UNIFORM_LOCATION 1
#if UNITY_SUPPORTS_UNIFORM_LOCATION
#define UNITY_LOCATION(x) layout(location = x)
#define UNITY_BINDING(x) layout(binding = x, std140)
#else
#define UNITY_LOCATION(x)
#define UNITY_BINDING(x) layout(std140)
#endif
uniform 	vec4 _Time;
uniform 	vec3 _WorldSpaceCameraPos;
uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(1) uniform UnityPerDraw {
#endif
	UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
	UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_WorldToObject[4];
	UNITY_UNIFORM vec4 unity_LODFade;
	UNITY_UNIFORM mediump vec4 unity_WorldTransformParams;
	UNITY_UNIFORM mediump vec4 unity_LightData;
	UNITY_UNIFORM mediump vec4 unity_LightIndices[2];
	UNITY_UNIFORM vec4 unity_ProbesOcclusion;
	UNITY_UNIFORM mediump vec4 unity_SpecCube0_HDR;
	UNITY_UNIFORM vec4 unity_LightmapST;
	UNITY_UNIFORM vec4 unity_DynamicLightmapST;
	UNITY_UNIFORM mediump vec4 unity_SHAr;
	UNITY_UNIFORM mediump vec4 unity_SHAg;
	UNITY_UNIFORM mediump vec4 unity_SHAb;
	UNITY_UNIFORM mediump vec4 unity_SHBr;
	UNITY_UNIFORM mediump vec4 unity_SHBg;
	UNITY_UNIFORM mediump vec4 unity_SHBb;
	UNITY_UNIFORM mediump vec4 unity_SHC;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(0) uniform UnityPerMaterial {
#endif
	UNITY_UNIFORM mediump vec4 _MainTexEX_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer2Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer3FTex_ST;
	UNITY_UNIFORM vec4 _Layer3Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Vector;
	UNITY_UNIFORM float _Layer1Rotate;
	UNITY_UNIFORM mediump float _Layer1PulseRate;
	UNITY_UNIFORM mediump float _Layer1PulseClip;
	UNITY_UNIFORM mediump float _Layer1PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer2Vector;
	UNITY_UNIFORM float _Layer2Rotate;
	UNITY_UNIFORM mediump vec4 _Layer2Color;
	UNITY_UNIFORM mediump float _Layer2PulseRate;
	UNITY_UNIFORM mediump float _Layer2PulseClip;
	UNITY_UNIFORM mediump float _Layer2PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer3Color;
	UNITY_UNIFORM mediump vec4 _Layer3Offset;
	UNITY_UNIFORM vec4 _Layer3Vector;
	UNITY_UNIFORM float _Layer3Rotate;
	UNITY_UNIFORM mediump float _Layer3PulseRate;
	UNITY_UNIFORM mediump float _Layer3PulseClip;
	UNITY_UNIFORM mediump float _Layer3PulseIntensity;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
in highp vec4 in_POSITION0;
in highp vec2 in_TEXCOORD0;
out highp vec4 vs_TEXCOORD0;
out highp vec4 vs_TEXCOORD1;
out mediump vec4 vs_TEXCOORD2;
out mediump vec3 vs_TEXCOORD3;
vec4 u_xlat0;
vec4 u_xlat1;
vec3 u_xlat2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
vec2 u_xlat10;
vec2 u_xlat11;
void main()
{
    u_xlat0.xy = _Time.xx * _Layer1Vector.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat1.x = sin(_Layer1Rotate);
    u_xlat2.x = cos(_Layer1Rotate);
    u_xlat2.yz = u_xlat1.xx;
    u_xlat1.xyz = u_xlat2.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat10.xy = in_TEXCOORD0.xy + vec2(-0.5, -0.5);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat1.xy = u_xlat11.xy + vec2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * _Layer1Tex_ST.xy + u_xlat0.xy;
    vs_TEXCOORD0.zw = u_xlat0.xy + _Layer1Tex_ST.zw;
    vs_TEXCOORD0.xy = in_TEXCOORD0.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;
    u_xlat0.x = sin(_Layer2Rotate);
    u_xlat1.x = cos(_Layer2Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat0.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat0.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy + vec2(0.5, 0.5);
    u_xlat1.xy = _Time.xx * _Layer2Vector.xy;
    u_xlat1.xy = fract(u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer2Tex_ST.xy + u_xlat1.xy;
    vs_TEXCOORD1.xy = u_xlat0.xy + _Layer2Tex_ST.zw;
    u_xlat0.x = sin(_Layer3Rotate);
    u_xlat1.x = cos(_Layer3Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat11.xy + vec2(0.5, 0.5);
    vs_TEXCOORD1.zw = u_xlat0.xy * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    vs_TEXCOORD2.w = 1.0;
    u_xlat0.xyz = in_POSITION0.yyy * hlslcc_mtx4x4unity_ObjectToWorld[1].xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[0].xyz * in_POSITION0.xxx + u_xlat0.xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[2].xyz * in_POSITION0.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + hlslcc_mtx4x4unity_ObjectToWorld[3].xyz;
    u_xlat1.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    vs_TEXCOORD2.xyz = u_xlat1.xyz;
    u_xlat1.x = _Time.x * _Layer1PulseRate;
    u_xlat1.y = _Time.x * _Layer2PulseRate;
    u_xlat1.z = _Time.x * _Layer3PulseRate;
    u_xlat1.xyz = fract(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(6.23999977, 6.23999977, 6.23999977);
    u_xlat1.xyz = cos(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, 0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat16_3.x = _Layer1PulseClip;
    u_xlat16_4.x = (-u_xlat16_3.x) + _Layer1PulseIntensity;
    u_xlat16_3.y = _Layer2PulseClip;
    u_xlat16_4.y = (-u_xlat16_3.y) + _Layer2PulseIntensity;
    u_xlat16_3.z = _Layer3PulseClip;
    u_xlat16_4.z = (-u_xlat16_3.z) + _Layer3PulseIntensity;
    vs_TEXCOORD3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat1 = u_xlat0.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
    u_xlat1 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat0.zzzz + u_xlat1;
    gl_Position = u_xlat0 + hlslcc_mtx4x4unity_MatrixVP[3];
    return;
}

#endif
#ifdef FRAGMENT
#version 300 es

precision highp float;
precision highp int;
#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
#define UNITY_UNIFORM
#else
#define UNITY_UNIFORM uniform
#endif
#define UNITY_SUPPORTS_UNIFORM_LOCATION 1
#if UNITY_SUPPORTS_UNIFORM_LOCATION
#define UNITY_LOCATION(x) layout(location = x)
#define UNITY_BINDING(x) layout(binding = x, std140)
#else
#define UNITY_LOCATION(x)
#define UNITY_BINDING(x) layout(std140)
#endif
uniform 	vec4 _Time;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(0) uniform UnityPerMaterial {
#endif
	UNITY_UNIFORM mediump vec4 _MainTexEX_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer2Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer3FTex_ST;
	UNITY_UNIFORM vec4 _Layer3Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Vector;
	UNITY_UNIFORM float _Layer1Rotate;
	UNITY_UNIFORM mediump float _Layer1PulseRate;
	UNITY_UNIFORM mediump float _Layer1PulseClip;
	UNITY_UNIFORM mediump float _Layer1PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer2Vector;
	UNITY_UNIFORM float _Layer2Rotate;
	UNITY_UNIFORM mediump vec4 _Layer2Color;
	UNITY_UNIFORM mediump float _Layer2PulseRate;
	UNITY_UNIFORM mediump float _Layer2PulseClip;
	UNITY_UNIFORM mediump float _Layer2PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer3Color;
	UNITY_UNIFORM mediump vec4 _Layer3Offset;
	UNITY_UNIFORM vec4 _Layer3Vector;
	UNITY_UNIFORM float _Layer3Rotate;
	UNITY_UNIFORM mediump float _Layer3PulseRate;
	UNITY_UNIFORM mediump float _Layer3PulseClip;
	UNITY_UNIFORM mediump float _Layer3PulseIntensity;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
UNITY_LOCATION(0) uniform mediump sampler2D _MainTexEX;
UNITY_LOCATION(1) uniform mediump sampler2D _MixMap;
UNITY_LOCATION(2) uniform mediump sampler2D _Layer1Tex;
UNITY_LOCATION(3) uniform mediump sampler2D _Layer2Tex;
UNITY_LOCATION(4) uniform mediump sampler2D _Layer3FTex;
UNITY_LOCATION(5) uniform mediump sampler2D _Layer3Tex;
in highp vec4 vs_TEXCOORD0;
in highp vec4 vs_TEXCOORD1;
in mediump vec3 vs_TEXCOORD3;
layout(location = 0) out mediump vec4 SV_Target0;
vec2 u_xlat0;
mediump vec4 u_xlat16_0;
vec2 u_xlat1;
mediump vec4 u_xlat16_1;
mediump vec4 u_xlat16_2;
mediump vec4 u_xlat16_3;
mediump vec3 u_xlat16_4;
mediump vec3 u_xlat16_6;
mediump vec2 u_xlat16_7;
float u_xlat10;
void main()
{
    u_xlat16_0.xy = texture(_Layer3FTex, vs_TEXCOORD1.zw).xy;
    u_xlat10 = fract(_Time.x);
    u_xlat0.xy = _Layer3Vector.xy * vec2(u_xlat10) + u_xlat16_0.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    u_xlat16_0 = texture(_Layer3Tex, u_xlat0.xy);
    u_xlat16_1.x = texture(_Layer1Tex, vs_TEXCOORD0.zw).x;
    u_xlat1.x = u_xlat16_1.x * vs_TEXCOORD3.x;
    u_xlat16_6.xyz = texture(_MixMap, vs_TEXCOORD0.xy).xyz;
    u_xlat16_2.x = u_xlat16_6.x * u_xlat1.x;
    u_xlat16_7.xy = u_xlat16_6.yz * vs_TEXCOORD3.yz;
    u_xlat1.xy = u_xlat16_2.xx * _Layer1Vector.zw + vs_TEXCOORD0.xy;
    u_xlat16_1 = texture(_MainTexEX, u_xlat1.xy);
    u_xlat16_3 = texture(_Layer2Tex, vs_TEXCOORD1.xy);
    u_xlat16_4.xyz = u_xlat16_3.xyz * _Layer2Color.xyz + (-u_xlat16_1.xyz);
    u_xlat16_2.x = u_xlat16_7.x * u_xlat16_3.w;
    u_xlat16_7.x = u_xlat16_0.w * u_xlat16_7.y;
    u_xlat16_2.xzw = u_xlat16_2.xxx * u_xlat16_4.xyz + u_xlat16_1.xyz;
    SV_Target0.w = u_xlat16_1.w;
    u_xlat16_4.xyz = u_xlat16_0.xyz * _Layer3Color.xyz + (-u_xlat16_2.xzw);
    SV_Target0.xyz = u_xlat16_7.xxx * u_xlat16_4.xyz + u_xlat16_2.xzw;
    return;
}

#endif
"
}
SubProgram "gles " {
Local Keywords { "_MODEL3_ADD" }
"// hash: 9e92daed92a60e10
#ifdef VERTEX
#version 100

uniform 	vec4 _Time;
uniform 	vec3 _WorldSpaceCameraPos;
uniform 	vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
uniform 	mediump vec4 _MainTexEX_ST;
uniform 	mediump vec4 _Layer1Tex_ST;
uniform 	mediump vec4 _Layer2Tex_ST;
uniform 	mediump vec4 _Layer3FTex_ST;
uniform 	mediump vec4 _Layer1Vector;
uniform 	float _Layer1Rotate;
uniform 	mediump float _Layer1PulseRate;
uniform 	mediump float _Layer1PulseClip;
uniform 	mediump float _Layer1PulseIntensity;
uniform 	mediump vec4 _Layer2Vector;
uniform 	float _Layer2Rotate;
uniform 	mediump float _Layer2PulseRate;
uniform 	mediump float _Layer2PulseClip;
uniform 	mediump float _Layer2PulseIntensity;
uniform 	float _Layer3Rotate;
uniform 	mediump float _Layer3PulseRate;
uniform 	mediump float _Layer3PulseClip;
uniform 	mediump float _Layer3PulseIntensity;
attribute highp vec4 in_POSITION0;
attribute highp vec2 in_TEXCOORD0;
varying highp vec4 vs_TEXCOORD0;
varying highp vec4 vs_TEXCOORD1;
varying mediump vec4 vs_TEXCOORD2;
varying mediump vec3 vs_TEXCOORD3;
vec4 u_xlat0;
vec4 u_xlat1;
vec3 u_xlat2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
vec2 u_xlat10;
vec2 u_xlat11;
void main()
{
    u_xlat0.xy = _Time.xx * _Layer1Vector.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat1.x = sin(_Layer1Rotate);
    u_xlat2.x = cos(_Layer1Rotate);
    u_xlat2.yz = u_xlat1.xx;
    u_xlat1.xyz = u_xlat2.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat10.xy = in_TEXCOORD0.xy + vec2(-0.5, -0.5);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat1.xy = u_xlat11.xy + vec2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * _Layer1Tex_ST.xy + u_xlat0.xy;
    vs_TEXCOORD0.zw = u_xlat0.xy + _Layer1Tex_ST.zw;
    vs_TEXCOORD0.xy = in_TEXCOORD0.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;
    u_xlat0.x = sin(_Layer2Rotate);
    u_xlat1.x = cos(_Layer2Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat0.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat0.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy + vec2(0.5, 0.5);
    u_xlat1.xy = _Time.xx * _Layer2Vector.xy;
    u_xlat1.xy = fract(u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer2Tex_ST.xy + u_xlat1.xy;
    vs_TEXCOORD1.xy = u_xlat0.xy + _Layer2Tex_ST.zw;
    u_xlat0.x = sin(_Layer3Rotate);
    u_xlat1.x = cos(_Layer3Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat11.xy + vec2(0.5, 0.5);
    vs_TEXCOORD1.zw = u_xlat0.xy * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    vs_TEXCOORD2.w = 1.0;
    u_xlat0.xyz = in_POSITION0.yyy * hlslcc_mtx4x4unity_ObjectToWorld[1].xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[0].xyz * in_POSITION0.xxx + u_xlat0.xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[2].xyz * in_POSITION0.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + hlslcc_mtx4x4unity_ObjectToWorld[3].xyz;
    u_xlat1.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    vs_TEXCOORD2.xyz = u_xlat1.xyz;
    u_xlat1.x = _Time.x * _Layer1PulseRate;
    u_xlat1.y = _Time.x * _Layer2PulseRate;
    u_xlat1.z = _Time.x * _Layer3PulseRate;
    u_xlat1.xyz = fract(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(6.23999977, 6.23999977, 6.23999977);
    u_xlat1.xyz = cos(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, 0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat16_3.x = _Layer1PulseClip;
    u_xlat16_4.x = (-u_xlat16_3.x) + _Layer1PulseIntensity;
    u_xlat16_3.y = _Layer2PulseClip;
    u_xlat16_4.y = (-u_xlat16_3.y) + _Layer2PulseIntensity;
    u_xlat16_3.z = _Layer3PulseClip;
    u_xlat16_4.z = (-u_xlat16_3.z) + _Layer3PulseIntensity;
    vs_TEXCOORD3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat1 = u_xlat0.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
    u_xlat1 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat0.zzzz + u_xlat1;
    gl_Position = u_xlat0 + hlslcc_mtx4x4unity_MatrixVP[3];
    return;
}

#endif
#ifdef FRAGMENT
#version 100

#ifdef GL_FRAGMENT_PRECISION_HIGH
    precision highp float;
#else
    precision mediump float;
#endif
precision highp int;
uniform 	vec4 _Time;
uniform 	vec4 _Layer3Tex_ST;
uniform 	mediump vec4 _Layer1Vector;
uniform 	mediump vec4 _Layer2Color;
uniform 	mediump vec4 _Layer3Color;
uniform 	vec4 _Layer3Vector;
uniform lowp sampler2D _MixMap;
uniform lowp sampler2D _Layer1Tex;
uniform lowp sampler2D _Layer2Tex;
uniform lowp sampler2D _Layer3FTex;
uniform lowp sampler2D _Layer3Tex;
uniform lowp sampler2D _MainTexEX;
varying highp vec4 vs_TEXCOORD0;
varying highp vec4 vs_TEXCOORD1;
varying mediump vec3 vs_TEXCOORD3;
#define SV_Target0 gl_FragData[0]
vec2 u_xlat0;
lowp vec4 u_xlat10_0;
mediump vec4 u_xlat16_1;
lowp vec4 u_xlat10_2;
mediump vec3 u_xlat16_3;
lowp vec3 u_xlat10_4;
mediump vec2 u_xlat16_5;
float u_xlat8;
void main()
{
    u_xlat10_0.x = texture2D(_Layer1Tex, vs_TEXCOORD0.zw).x;
    u_xlat0.x = u_xlat10_0.x * vs_TEXCOORD3.x;
    u_xlat10_4.xyz = texture2D(_MixMap, vs_TEXCOORD0.xy).xyz;
    u_xlat16_1.x = u_xlat10_4.x * u_xlat0.x;
    u_xlat16_5.xy = u_xlat10_4.yz * vs_TEXCOORD3.yz;
    u_xlat0.xy = u_xlat16_1.xx * _Layer1Vector.zw + vs_TEXCOORD0.xy;
    u_xlat10_0 = texture2D(_MainTexEX, u_xlat0.xy);
    u_xlat10_2 = texture2D(_Layer2Tex, vs_TEXCOORD1.xy);
    u_xlat16_3.xyz = u_xlat10_2.xyz * _Layer2Color.xyz + (-u_xlat10_0.xyz);
    u_xlat16_1.x = u_xlat16_5.x * u_xlat10_2.w;
    u_xlat16_1.xyw = u_xlat16_1.xxx * u_xlat16_3.xyz + u_xlat10_0.xyz;
    SV_Target0.w = u_xlat10_0.w;
    u_xlat10_0.xy = texture2D(_Layer3FTex, vs_TEXCOORD1.zw).xy;
    u_xlat8 = fract(_Time.x);
    u_xlat0.xy = _Layer3Vector.xy * vec2(u_xlat8) + u_xlat10_0.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    u_xlat10_0.xyz = texture2D(_Layer3Tex, u_xlat0.xy).xyz;
    u_xlat16_3.xyz = u_xlat16_5.yyy * u_xlat10_0.xyz;
    SV_Target0.xyz = u_xlat16_3.xyz * _Layer3Color.xyz + u_xlat16_1.xyw;
    return;
}

#endif
"
}
SubProgram "gles3 " {
Local Keywords { "_MODEL3_ADD" }
"// hash: 4bc5cefc13bc4f7f
#ifdef VERTEX
#version 300 es

#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
#define UNITY_UNIFORM
#else
#define UNITY_UNIFORM uniform
#endif
#define UNITY_SUPPORTS_UNIFORM_LOCATION 1
#if UNITY_SUPPORTS_UNIFORM_LOCATION
#define UNITY_LOCATION(x) layout(location = x)
#define UNITY_BINDING(x) layout(binding = x, std140)
#else
#define UNITY_LOCATION(x)
#define UNITY_BINDING(x) layout(std140)
#endif
uniform 	vec4 _Time;
uniform 	vec3 _WorldSpaceCameraPos;
uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(1) uniform UnityPerDraw {
#endif
	UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
	UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_WorldToObject[4];
	UNITY_UNIFORM vec4 unity_LODFade;
	UNITY_UNIFORM mediump vec4 unity_WorldTransformParams;
	UNITY_UNIFORM mediump vec4 unity_LightData;
	UNITY_UNIFORM mediump vec4 unity_LightIndices[2];
	UNITY_UNIFORM vec4 unity_ProbesOcclusion;
	UNITY_UNIFORM mediump vec4 unity_SpecCube0_HDR;
	UNITY_UNIFORM vec4 unity_LightmapST;
	UNITY_UNIFORM vec4 unity_DynamicLightmapST;
	UNITY_UNIFORM mediump vec4 unity_SHAr;
	UNITY_UNIFORM mediump vec4 unity_SHAg;
	UNITY_UNIFORM mediump vec4 unity_SHAb;
	UNITY_UNIFORM mediump vec4 unity_SHBr;
	UNITY_UNIFORM mediump vec4 unity_SHBg;
	UNITY_UNIFORM mediump vec4 unity_SHBb;
	UNITY_UNIFORM mediump vec4 unity_SHC;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(0) uniform UnityPerMaterial {
#endif
	UNITY_UNIFORM mediump vec4 _MainTexEX_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer2Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer3FTex_ST;
	UNITY_UNIFORM vec4 _Layer3Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Vector;
	UNITY_UNIFORM float _Layer1Rotate;
	UNITY_UNIFORM mediump float _Layer1PulseRate;
	UNITY_UNIFORM mediump float _Layer1PulseClip;
	UNITY_UNIFORM mediump float _Layer1PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer2Vector;
	UNITY_UNIFORM float _Layer2Rotate;
	UNITY_UNIFORM mediump vec4 _Layer2Color;
	UNITY_UNIFORM mediump float _Layer2PulseRate;
	UNITY_UNIFORM mediump float _Layer2PulseClip;
	UNITY_UNIFORM mediump float _Layer2PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer3Color;
	UNITY_UNIFORM mediump vec4 _Layer3Offset;
	UNITY_UNIFORM vec4 _Layer3Vector;
	UNITY_UNIFORM float _Layer3Rotate;
	UNITY_UNIFORM mediump float _Layer3PulseRate;
	UNITY_UNIFORM mediump float _Layer3PulseClip;
	UNITY_UNIFORM mediump float _Layer3PulseIntensity;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
in highp vec4 in_POSITION0;
in highp vec2 in_TEXCOORD0;
out highp vec4 vs_TEXCOORD0;
out highp vec4 vs_TEXCOORD1;
out mediump vec4 vs_TEXCOORD2;
out mediump vec3 vs_TEXCOORD3;
vec4 u_xlat0;
vec4 u_xlat1;
vec3 u_xlat2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
vec2 u_xlat10;
vec2 u_xlat11;
void main()
{
    u_xlat0.xy = _Time.xx * _Layer1Vector.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat1.x = sin(_Layer1Rotate);
    u_xlat2.x = cos(_Layer1Rotate);
    u_xlat2.yz = u_xlat1.xx;
    u_xlat1.xyz = u_xlat2.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat10.xy = in_TEXCOORD0.xy + vec2(-0.5, -0.5);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat1.xy = u_xlat11.xy + vec2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * _Layer1Tex_ST.xy + u_xlat0.xy;
    vs_TEXCOORD0.zw = u_xlat0.xy + _Layer1Tex_ST.zw;
    vs_TEXCOORD0.xy = in_TEXCOORD0.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;
    u_xlat0.x = sin(_Layer2Rotate);
    u_xlat1.x = cos(_Layer2Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat0.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat0.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy + vec2(0.5, 0.5);
    u_xlat1.xy = _Time.xx * _Layer2Vector.xy;
    u_xlat1.xy = fract(u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer2Tex_ST.xy + u_xlat1.xy;
    vs_TEXCOORD1.xy = u_xlat0.xy + _Layer2Tex_ST.zw;
    u_xlat0.x = sin(_Layer3Rotate);
    u_xlat1.x = cos(_Layer3Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat11.xy + vec2(0.5, 0.5);
    vs_TEXCOORD1.zw = u_xlat0.xy * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    vs_TEXCOORD2.w = 1.0;
    u_xlat0.xyz = in_POSITION0.yyy * hlslcc_mtx4x4unity_ObjectToWorld[1].xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[0].xyz * in_POSITION0.xxx + u_xlat0.xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[2].xyz * in_POSITION0.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + hlslcc_mtx4x4unity_ObjectToWorld[3].xyz;
    u_xlat1.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    vs_TEXCOORD2.xyz = u_xlat1.xyz;
    u_xlat1.x = _Time.x * _Layer1PulseRate;
    u_xlat1.y = _Time.x * _Layer2PulseRate;
    u_xlat1.z = _Time.x * _Layer3PulseRate;
    u_xlat1.xyz = fract(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(6.23999977, 6.23999977, 6.23999977);
    u_xlat1.xyz = cos(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, 0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat16_3.x = _Layer1PulseClip;
    u_xlat16_4.x = (-u_xlat16_3.x) + _Layer1PulseIntensity;
    u_xlat16_3.y = _Layer2PulseClip;
    u_xlat16_4.y = (-u_xlat16_3.y) + _Layer2PulseIntensity;
    u_xlat16_3.z = _Layer3PulseClip;
    u_xlat16_4.z = (-u_xlat16_3.z) + _Layer3PulseIntensity;
    vs_TEXCOORD3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat1 = u_xlat0.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
    u_xlat1 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat0.zzzz + u_xlat1;
    gl_Position = u_xlat0 + hlslcc_mtx4x4unity_MatrixVP[3];
    return;
}

#endif
#ifdef FRAGMENT
#version 300 es

precision highp float;
precision highp int;
#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
#define UNITY_UNIFORM
#else
#define UNITY_UNIFORM uniform
#endif
#define UNITY_SUPPORTS_UNIFORM_LOCATION 1
#if UNITY_SUPPORTS_UNIFORM_LOCATION
#define UNITY_LOCATION(x) layout(location = x)
#define UNITY_BINDING(x) layout(binding = x, std140)
#else
#define UNITY_LOCATION(x)
#define UNITY_BINDING(x) layout(std140)
#endif
uniform 	vec4 _Time;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(0) uniform UnityPerMaterial {
#endif
	UNITY_UNIFORM mediump vec4 _MainTexEX_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer2Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer3FTex_ST;
	UNITY_UNIFORM vec4 _Layer3Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Vector;
	UNITY_UNIFORM float _Layer1Rotate;
	UNITY_UNIFORM mediump float _Layer1PulseRate;
	UNITY_UNIFORM mediump float _Layer1PulseClip;
	UNITY_UNIFORM mediump float _Layer1PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer2Vector;
	UNITY_UNIFORM float _Layer2Rotate;
	UNITY_UNIFORM mediump vec4 _Layer2Color;
	UNITY_UNIFORM mediump float _Layer2PulseRate;
	UNITY_UNIFORM mediump float _Layer2PulseClip;
	UNITY_UNIFORM mediump float _Layer2PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer3Color;
	UNITY_UNIFORM mediump vec4 _Layer3Offset;
	UNITY_UNIFORM vec4 _Layer3Vector;
	UNITY_UNIFORM float _Layer3Rotate;
	UNITY_UNIFORM mediump float _Layer3PulseRate;
	UNITY_UNIFORM mediump float _Layer3PulseClip;
	UNITY_UNIFORM mediump float _Layer3PulseIntensity;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
UNITY_LOCATION(0) uniform mediump sampler2D _MainTexEX;
UNITY_LOCATION(1) uniform mediump sampler2D _MixMap;
UNITY_LOCATION(2) uniform mediump sampler2D _Layer1Tex;
UNITY_LOCATION(3) uniform mediump sampler2D _Layer2Tex;
UNITY_LOCATION(4) uniform mediump sampler2D _Layer3FTex;
UNITY_LOCATION(5) uniform mediump sampler2D _Layer3Tex;
in highp vec4 vs_TEXCOORD0;
in highp vec4 vs_TEXCOORD1;
in mediump vec3 vs_TEXCOORD3;
layout(location = 0) out mediump vec4 SV_Target0;
vec2 u_xlat0;
mediump vec4 u_xlat16_0;
mediump vec4 u_xlat16_1;
mediump vec4 u_xlat16_2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
mediump vec2 u_xlat16_5;
float u_xlat8;
void main()
{
    u_xlat16_0.x = texture(_Layer1Tex, vs_TEXCOORD0.zw).x;
    u_xlat0.x = u_xlat16_0.x * vs_TEXCOORD3.x;
    u_xlat16_4.xyz = texture(_MixMap, vs_TEXCOORD0.xy).xyz;
    u_xlat16_1.x = u_xlat16_4.x * u_xlat0.x;
    u_xlat16_5.xy = u_xlat16_4.yz * vs_TEXCOORD3.yz;
    u_xlat0.xy = u_xlat16_1.xx * _Layer1Vector.zw + vs_TEXCOORD0.xy;
    u_xlat16_0 = texture(_MainTexEX, u_xlat0.xy);
    u_xlat16_2 = texture(_Layer2Tex, vs_TEXCOORD1.xy);
    u_xlat16_3.xyz = u_xlat16_2.xyz * _Layer2Color.xyz + (-u_xlat16_0.xyz);
    u_xlat16_1.x = u_xlat16_5.x * u_xlat16_2.w;
    u_xlat16_1.xyw = u_xlat16_1.xxx * u_xlat16_3.xyz + u_xlat16_0.xyz;
    SV_Target0.w = u_xlat16_0.w;
    u_xlat16_0.xy = texture(_Layer3FTex, vs_TEXCOORD1.zw).xy;
    u_xlat8 = fract(_Time.x);
    u_xlat0.xy = _Layer3Vector.xy * vec2(u_xlat8) + u_xlat16_0.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    u_xlat16_0.xyz = texture(_Layer3Tex, u_xlat0.xy).xyz;
    u_xlat16_3.xyz = u_xlat16_5.yyy * u_xlat16_0.xyz;
    SV_Target0.xyz = u_xlat16_3.xyz * _Layer3Color.xyz + u_xlat16_1.xyw;
    return;
}

#endif
"
}
SubProgram "gles " {
Local Keywords { "_MODEL2_ADD" }
"// hash: f13aaeb3c06a171b
#ifdef VERTEX
#version 100

uniform 	vec4 _Time;
uniform 	vec3 _WorldSpaceCameraPos;
uniform 	vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
uniform 	mediump vec4 _MainTexEX_ST;
uniform 	mediump vec4 _Layer1Tex_ST;
uniform 	mediump vec4 _Layer2Tex_ST;
uniform 	mediump vec4 _Layer3FTex_ST;
uniform 	mediump vec4 _Layer1Vector;
uniform 	float _Layer1Rotate;
uniform 	mediump float _Layer1PulseRate;
uniform 	mediump float _Layer1PulseClip;
uniform 	mediump float _Layer1PulseIntensity;
uniform 	mediump vec4 _Layer2Vector;
uniform 	float _Layer2Rotate;
uniform 	mediump float _Layer2PulseRate;
uniform 	mediump float _Layer2PulseClip;
uniform 	mediump float _Layer2PulseIntensity;
uniform 	float _Layer3Rotate;
uniform 	mediump float _Layer3PulseRate;
uniform 	mediump float _Layer3PulseClip;
uniform 	mediump float _Layer3PulseIntensity;
attribute highp vec4 in_POSITION0;
attribute highp vec2 in_TEXCOORD0;
varying highp vec4 vs_TEXCOORD0;
varying highp vec4 vs_TEXCOORD1;
varying mediump vec4 vs_TEXCOORD2;
varying mediump vec3 vs_TEXCOORD3;
vec4 u_xlat0;
vec4 u_xlat1;
vec3 u_xlat2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
vec2 u_xlat10;
vec2 u_xlat11;
void main()
{
    u_xlat0.xy = _Time.xx * _Layer1Vector.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat1.x = sin(_Layer1Rotate);
    u_xlat2.x = cos(_Layer1Rotate);
    u_xlat2.yz = u_xlat1.xx;
    u_xlat1.xyz = u_xlat2.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat10.xy = in_TEXCOORD0.xy + vec2(-0.5, -0.5);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat1.xy = u_xlat11.xy + vec2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * _Layer1Tex_ST.xy + u_xlat0.xy;
    vs_TEXCOORD0.zw = u_xlat0.xy + _Layer1Tex_ST.zw;
    vs_TEXCOORD0.xy = in_TEXCOORD0.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;
    u_xlat0.x = sin(_Layer2Rotate);
    u_xlat1.x = cos(_Layer2Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat0.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat0.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy + vec2(0.5, 0.5);
    u_xlat1.xy = _Time.xx * _Layer2Vector.xy;
    u_xlat1.xy = fract(u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer2Tex_ST.xy + u_xlat1.xy;
    vs_TEXCOORD1.xy = u_xlat0.xy + _Layer2Tex_ST.zw;
    u_xlat0.x = sin(_Layer3Rotate);
    u_xlat1.x = cos(_Layer3Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat11.xy + vec2(0.5, 0.5);
    vs_TEXCOORD1.zw = u_xlat0.xy * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    vs_TEXCOORD2.w = 1.0;
    u_xlat0.xyz = in_POSITION0.yyy * hlslcc_mtx4x4unity_ObjectToWorld[1].xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[0].xyz * in_POSITION0.xxx + u_xlat0.xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[2].xyz * in_POSITION0.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + hlslcc_mtx4x4unity_ObjectToWorld[3].xyz;
    u_xlat1.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    vs_TEXCOORD2.xyz = u_xlat1.xyz;
    u_xlat1.x = _Time.x * _Layer1PulseRate;
    u_xlat1.y = _Time.x * _Layer2PulseRate;
    u_xlat1.z = _Time.x * _Layer3PulseRate;
    u_xlat1.xyz = fract(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(6.23999977, 6.23999977, 6.23999977);
    u_xlat1.xyz = cos(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, 0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat16_3.x = _Layer1PulseClip;
    u_xlat16_4.x = (-u_xlat16_3.x) + _Layer1PulseIntensity;
    u_xlat16_3.y = _Layer2PulseClip;
    u_xlat16_4.y = (-u_xlat16_3.y) + _Layer2PulseIntensity;
    u_xlat16_3.z = _Layer3PulseClip;
    u_xlat16_4.z = (-u_xlat16_3.z) + _Layer3PulseIntensity;
    vs_TEXCOORD3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat1 = u_xlat0.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
    u_xlat1 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat0.zzzz + u_xlat1;
    gl_Position = u_xlat0 + hlslcc_mtx4x4unity_MatrixVP[3];
    return;
}

#endif
#ifdef FRAGMENT
#version 100

#ifdef GL_FRAGMENT_PRECISION_HIGH
    precision highp float;
#else
    precision mediump float;
#endif
precision highp int;
uniform 	vec4 _Time;
uniform 	vec4 _Layer3Tex_ST;
uniform 	mediump vec4 _Layer1Vector;
uniform 	mediump vec4 _Layer2Color;
uniform 	mediump vec4 _Layer3Color;
uniform 	vec4 _Layer3Vector;
uniform lowp sampler2D _MixMap;
uniform lowp sampler2D _Layer1Tex;
uniform lowp sampler2D _Layer2Tex;
uniform lowp sampler2D _Layer3FTex;
uniform lowp sampler2D _Layer3Tex;
uniform lowp sampler2D _MainTexEX;
varying highp vec4 vs_TEXCOORD0;
varying highp vec4 vs_TEXCOORD1;
varying mediump vec3 vs_TEXCOORD3;
#define SV_Target0 gl_FragData[0]
vec2 u_xlat0;
lowp vec4 u_xlat10_0;
vec2 u_xlat1;
lowp vec4 u_xlat10_1;
mediump vec4 u_xlat16_2;
lowp vec3 u_xlat10_3;
mediump vec3 u_xlat16_4;
lowp vec3 u_xlat10_6;
mediump vec2 u_xlat16_7;
float u_xlat10;
mediump float u_xlat16_12;
void main()
{
    u_xlat10_0.xy = texture2D(_Layer3FTex, vs_TEXCOORD1.zw).xy;
    u_xlat10 = fract(_Time.x);
    u_xlat0.xy = _Layer3Vector.xy * vec2(u_xlat10) + u_xlat10_0.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    u_xlat10_0 = texture2D(_Layer3Tex, u_xlat0.xy);
    u_xlat10_1.x = texture2D(_Layer1Tex, vs_TEXCOORD0.zw).x;
    u_xlat1.x = u_xlat10_1.x * vs_TEXCOORD3.x;
    u_xlat10_6.xyz = texture2D(_MixMap, vs_TEXCOORD0.xy).xyz;
    u_xlat16_2.x = u_xlat10_6.x * u_xlat1.x;
    u_xlat16_7.xy = u_xlat10_6.yz * vs_TEXCOORD3.yz;
    u_xlat1.xy = u_xlat16_2.xx * _Layer1Vector.zw + vs_TEXCOORD0.xy;
    u_xlat10_1 = texture2D(_MainTexEX, u_xlat1.xy);
    u_xlat10_3.xyz = texture2D(_Layer2Tex, vs_TEXCOORD1.xy).xyz;
    u_xlat16_2.xyw = u_xlat16_7.xxx * u_xlat10_3.xyz;
    u_xlat16_12 = u_xlat10_0.w * u_xlat16_7.y;
    u_xlat16_2.xyw = u_xlat16_2.xyw * _Layer2Color.xyz + u_xlat10_1.xyz;
    SV_Target0.w = u_xlat10_1.w;
    u_xlat16_4.xyz = u_xlat10_0.xyz * _Layer3Color.xyz + (-u_xlat16_2.xyw);
    SV_Target0.xyz = vec3(u_xlat16_12) * u_xlat16_4.xyz + u_xlat16_2.xyw;
    return;
}

#endif
"
}
SubProgram "gles3 " {
Local Keywords { "_MODEL2_ADD" }
"// hash: 46189bb883ec9295
#ifdef VERTEX
#version 300 es

#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
#define UNITY_UNIFORM
#else
#define UNITY_UNIFORM uniform
#endif
#define UNITY_SUPPORTS_UNIFORM_LOCATION 1
#if UNITY_SUPPORTS_UNIFORM_LOCATION
#define UNITY_LOCATION(x) layout(location = x)
#define UNITY_BINDING(x) layout(binding = x, std140)
#else
#define UNITY_LOCATION(x)
#define UNITY_BINDING(x) layout(std140)
#endif
uniform 	vec4 _Time;
uniform 	vec3 _WorldSpaceCameraPos;
uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(1) uniform UnityPerDraw {
#endif
	UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
	UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_WorldToObject[4];
	UNITY_UNIFORM vec4 unity_LODFade;
	UNITY_UNIFORM mediump vec4 unity_WorldTransformParams;
	UNITY_UNIFORM mediump vec4 unity_LightData;
	UNITY_UNIFORM mediump vec4 unity_LightIndices[2];
	UNITY_UNIFORM vec4 unity_ProbesOcclusion;
	UNITY_UNIFORM mediump vec4 unity_SpecCube0_HDR;
	UNITY_UNIFORM vec4 unity_LightmapST;
	UNITY_UNIFORM vec4 unity_DynamicLightmapST;
	UNITY_UNIFORM mediump vec4 unity_SHAr;
	UNITY_UNIFORM mediump vec4 unity_SHAg;
	UNITY_UNIFORM mediump vec4 unity_SHAb;
	UNITY_UNIFORM mediump vec4 unity_SHBr;
	UNITY_UNIFORM mediump vec4 unity_SHBg;
	UNITY_UNIFORM mediump vec4 unity_SHBb;
	UNITY_UNIFORM mediump vec4 unity_SHC;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(0) uniform UnityPerMaterial {
#endif
	UNITY_UNIFORM mediump vec4 _MainTexEX_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer2Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer3FTex_ST;
	UNITY_UNIFORM vec4 _Layer3Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Vector;
	UNITY_UNIFORM float _Layer1Rotate;
	UNITY_UNIFORM mediump float _Layer1PulseRate;
	UNITY_UNIFORM mediump float _Layer1PulseClip;
	UNITY_UNIFORM mediump float _Layer1PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer2Vector;
	UNITY_UNIFORM float _Layer2Rotate;
	UNITY_UNIFORM mediump vec4 _Layer2Color;
	UNITY_UNIFORM mediump float _Layer2PulseRate;
	UNITY_UNIFORM mediump float _Layer2PulseClip;
	UNITY_UNIFORM mediump float _Layer2PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer3Color;
	UNITY_UNIFORM mediump vec4 _Layer3Offset;
	UNITY_UNIFORM vec4 _Layer3Vector;
	UNITY_UNIFORM float _Layer3Rotate;
	UNITY_UNIFORM mediump float _Layer3PulseRate;
	UNITY_UNIFORM mediump float _Layer3PulseClip;
	UNITY_UNIFORM mediump float _Layer3PulseIntensity;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
in highp vec4 in_POSITION0;
in highp vec2 in_TEXCOORD0;
out highp vec4 vs_TEXCOORD0;
out highp vec4 vs_TEXCOORD1;
out mediump vec4 vs_TEXCOORD2;
out mediump vec3 vs_TEXCOORD3;
vec4 u_xlat0;
vec4 u_xlat1;
vec3 u_xlat2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
vec2 u_xlat10;
vec2 u_xlat11;
void main()
{
    u_xlat0.xy = _Time.xx * _Layer1Vector.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat1.x = sin(_Layer1Rotate);
    u_xlat2.x = cos(_Layer1Rotate);
    u_xlat2.yz = u_xlat1.xx;
    u_xlat1.xyz = u_xlat2.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat10.xy = in_TEXCOORD0.xy + vec2(-0.5, -0.5);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat1.xy = u_xlat11.xy + vec2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * _Layer1Tex_ST.xy + u_xlat0.xy;
    vs_TEXCOORD0.zw = u_xlat0.xy + _Layer1Tex_ST.zw;
    vs_TEXCOORD0.xy = in_TEXCOORD0.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;
    u_xlat0.x = sin(_Layer2Rotate);
    u_xlat1.x = cos(_Layer2Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat0.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat0.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy + vec2(0.5, 0.5);
    u_xlat1.xy = _Time.xx * _Layer2Vector.xy;
    u_xlat1.xy = fract(u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer2Tex_ST.xy + u_xlat1.xy;
    vs_TEXCOORD1.xy = u_xlat0.xy + _Layer2Tex_ST.zw;
    u_xlat0.x = sin(_Layer3Rotate);
    u_xlat1.x = cos(_Layer3Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat11.xy + vec2(0.5, 0.5);
    vs_TEXCOORD1.zw = u_xlat0.xy * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    vs_TEXCOORD2.w = 1.0;
    u_xlat0.xyz = in_POSITION0.yyy * hlslcc_mtx4x4unity_ObjectToWorld[1].xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[0].xyz * in_POSITION0.xxx + u_xlat0.xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[2].xyz * in_POSITION0.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + hlslcc_mtx4x4unity_ObjectToWorld[3].xyz;
    u_xlat1.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    vs_TEXCOORD2.xyz = u_xlat1.xyz;
    u_xlat1.x = _Time.x * _Layer1PulseRate;
    u_xlat1.y = _Time.x * _Layer2PulseRate;
    u_xlat1.z = _Time.x * _Layer3PulseRate;
    u_xlat1.xyz = fract(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(6.23999977, 6.23999977, 6.23999977);
    u_xlat1.xyz = cos(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, 0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat16_3.x = _Layer1PulseClip;
    u_xlat16_4.x = (-u_xlat16_3.x) + _Layer1PulseIntensity;
    u_xlat16_3.y = _Layer2PulseClip;
    u_xlat16_4.y = (-u_xlat16_3.y) + _Layer2PulseIntensity;
    u_xlat16_3.z = _Layer3PulseClip;
    u_xlat16_4.z = (-u_xlat16_3.z) + _Layer3PulseIntensity;
    vs_TEXCOORD3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat1 = u_xlat0.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
    u_xlat1 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat0.zzzz + u_xlat1;
    gl_Position = u_xlat0 + hlslcc_mtx4x4unity_MatrixVP[3];
    return;
}

#endif
#ifdef FRAGMENT
#version 300 es

precision highp float;
precision highp int;
#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
#define UNITY_UNIFORM
#else
#define UNITY_UNIFORM uniform
#endif
#define UNITY_SUPPORTS_UNIFORM_LOCATION 1
#if UNITY_SUPPORTS_UNIFORM_LOCATION
#define UNITY_LOCATION(x) layout(location = x)
#define UNITY_BINDING(x) layout(binding = x, std140)
#else
#define UNITY_LOCATION(x)
#define UNITY_BINDING(x) layout(std140)
#endif
uniform 	vec4 _Time;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(0) uniform UnityPerMaterial {
#endif
	UNITY_UNIFORM mediump vec4 _MainTexEX_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer2Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer3FTex_ST;
	UNITY_UNIFORM vec4 _Layer3Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Vector;
	UNITY_UNIFORM float _Layer1Rotate;
	UNITY_UNIFORM mediump float _Layer1PulseRate;
	UNITY_UNIFORM mediump float _Layer1PulseClip;
	UNITY_UNIFORM mediump float _Layer1PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer2Vector;
	UNITY_UNIFORM float _Layer2Rotate;
	UNITY_UNIFORM mediump vec4 _Layer2Color;
	UNITY_UNIFORM mediump float _Layer2PulseRate;
	UNITY_UNIFORM mediump float _Layer2PulseClip;
	UNITY_UNIFORM mediump float _Layer2PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer3Color;
	UNITY_UNIFORM mediump vec4 _Layer3Offset;
	UNITY_UNIFORM vec4 _Layer3Vector;
	UNITY_UNIFORM float _Layer3Rotate;
	UNITY_UNIFORM mediump float _Layer3PulseRate;
	UNITY_UNIFORM mediump float _Layer3PulseClip;
	UNITY_UNIFORM mediump float _Layer3PulseIntensity;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
UNITY_LOCATION(0) uniform mediump sampler2D _MainTexEX;
UNITY_LOCATION(1) uniform mediump sampler2D _MixMap;
UNITY_LOCATION(2) uniform mediump sampler2D _Layer1Tex;
UNITY_LOCATION(3) uniform mediump sampler2D _Layer2Tex;
UNITY_LOCATION(4) uniform mediump sampler2D _Layer3FTex;
UNITY_LOCATION(5) uniform mediump sampler2D _Layer3Tex;
in highp vec4 vs_TEXCOORD0;
in highp vec4 vs_TEXCOORD1;
in mediump vec3 vs_TEXCOORD3;
layout(location = 0) out mediump vec4 SV_Target0;
vec2 u_xlat0;
mediump vec4 u_xlat16_0;
vec2 u_xlat1;
mediump vec4 u_xlat16_1;
mediump vec4 u_xlat16_2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
mediump vec3 u_xlat16_6;
mediump vec2 u_xlat16_7;
float u_xlat10;
mediump float u_xlat16_12;
void main()
{
    u_xlat16_0.xy = texture(_Layer3FTex, vs_TEXCOORD1.zw).xy;
    u_xlat10 = fract(_Time.x);
    u_xlat0.xy = _Layer3Vector.xy * vec2(u_xlat10) + u_xlat16_0.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    u_xlat16_0 = texture(_Layer3Tex, u_xlat0.xy);
    u_xlat16_1.x = texture(_Layer1Tex, vs_TEXCOORD0.zw).x;
    u_xlat1.x = u_xlat16_1.x * vs_TEXCOORD3.x;
    u_xlat16_6.xyz = texture(_MixMap, vs_TEXCOORD0.xy).xyz;
    u_xlat16_2.x = u_xlat16_6.x * u_xlat1.x;
    u_xlat16_7.xy = u_xlat16_6.yz * vs_TEXCOORD3.yz;
    u_xlat1.xy = u_xlat16_2.xx * _Layer1Vector.zw + vs_TEXCOORD0.xy;
    u_xlat16_1 = texture(_MainTexEX, u_xlat1.xy);
    u_xlat16_3.xyz = texture(_Layer2Tex, vs_TEXCOORD1.xy).xyz;
    u_xlat16_2.xyw = u_xlat16_7.xxx * u_xlat16_3.xyz;
    u_xlat16_12 = u_xlat16_0.w * u_xlat16_7.y;
    u_xlat16_2.xyw = u_xlat16_2.xyw * _Layer2Color.xyz + u_xlat16_1.xyz;
    SV_Target0.w = u_xlat16_1.w;
    u_xlat16_4.xyz = u_xlat16_0.xyz * _Layer3Color.xyz + (-u_xlat16_2.xyw);
    SV_Target0.xyz = vec3(u_xlat16_12) * u_xlat16_4.xyz + u_xlat16_2.xyw;
    return;
}

#endif
"
}
SubProgram "gles " {
Local Keywords { "_MODEL2_ADD" "_MODEL3_ADD" }
"// hash: 14732cb07319710c
#ifdef VERTEX
#version 100

uniform 	vec4 _Time;
uniform 	vec3 _WorldSpaceCameraPos;
uniform 	vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
uniform 	mediump vec4 _MainTexEX_ST;
uniform 	mediump vec4 _Layer1Tex_ST;
uniform 	mediump vec4 _Layer2Tex_ST;
uniform 	mediump vec4 _Layer3FTex_ST;
uniform 	mediump vec4 _Layer1Vector;
uniform 	float _Layer1Rotate;
uniform 	mediump float _Layer1PulseRate;
uniform 	mediump float _Layer1PulseClip;
uniform 	mediump float _Layer1PulseIntensity;
uniform 	mediump vec4 _Layer2Vector;
uniform 	float _Layer2Rotate;
uniform 	mediump float _Layer2PulseRate;
uniform 	mediump float _Layer2PulseClip;
uniform 	mediump float _Layer2PulseIntensity;
uniform 	float _Layer3Rotate;
uniform 	mediump float _Layer3PulseRate;
uniform 	mediump float _Layer3PulseClip;
uniform 	mediump float _Layer3PulseIntensity;
attribute highp vec4 in_POSITION0;
attribute highp vec2 in_TEXCOORD0;
varying highp vec4 vs_TEXCOORD0;
varying highp vec4 vs_TEXCOORD1;
varying mediump vec4 vs_TEXCOORD2;
varying mediump vec3 vs_TEXCOORD3;
vec4 u_xlat0;
vec4 u_xlat1;
vec3 u_xlat2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
vec2 u_xlat10;
vec2 u_xlat11;
void main()
{
    u_xlat0.xy = _Time.xx * _Layer1Vector.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat1.x = sin(_Layer1Rotate);
    u_xlat2.x = cos(_Layer1Rotate);
    u_xlat2.yz = u_xlat1.xx;
    u_xlat1.xyz = u_xlat2.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat10.xy = in_TEXCOORD0.xy + vec2(-0.5, -0.5);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat1.xy = u_xlat11.xy + vec2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * _Layer1Tex_ST.xy + u_xlat0.xy;
    vs_TEXCOORD0.zw = u_xlat0.xy + _Layer1Tex_ST.zw;
    vs_TEXCOORD0.xy = in_TEXCOORD0.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;
    u_xlat0.x = sin(_Layer2Rotate);
    u_xlat1.x = cos(_Layer2Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat0.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat0.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy + vec2(0.5, 0.5);
    u_xlat1.xy = _Time.xx * _Layer2Vector.xy;
    u_xlat1.xy = fract(u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer2Tex_ST.xy + u_xlat1.xy;
    vs_TEXCOORD1.xy = u_xlat0.xy + _Layer2Tex_ST.zw;
    u_xlat0.x = sin(_Layer3Rotate);
    u_xlat1.x = cos(_Layer3Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat11.xy + vec2(0.5, 0.5);
    vs_TEXCOORD1.zw = u_xlat0.xy * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    vs_TEXCOORD2.w = 1.0;
    u_xlat0.xyz = in_POSITION0.yyy * hlslcc_mtx4x4unity_ObjectToWorld[1].xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[0].xyz * in_POSITION0.xxx + u_xlat0.xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[2].xyz * in_POSITION0.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + hlslcc_mtx4x4unity_ObjectToWorld[3].xyz;
    u_xlat1.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    vs_TEXCOORD2.xyz = u_xlat1.xyz;
    u_xlat1.x = _Time.x * _Layer1PulseRate;
    u_xlat1.y = _Time.x * _Layer2PulseRate;
    u_xlat1.z = _Time.x * _Layer3PulseRate;
    u_xlat1.xyz = fract(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(6.23999977, 6.23999977, 6.23999977);
    u_xlat1.xyz = cos(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, 0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat16_3.x = _Layer1PulseClip;
    u_xlat16_4.x = (-u_xlat16_3.x) + _Layer1PulseIntensity;
    u_xlat16_3.y = _Layer2PulseClip;
    u_xlat16_4.y = (-u_xlat16_3.y) + _Layer2PulseIntensity;
    u_xlat16_3.z = _Layer3PulseClip;
    u_xlat16_4.z = (-u_xlat16_3.z) + _Layer3PulseIntensity;
    vs_TEXCOORD3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat1 = u_xlat0.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
    u_xlat1 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat0.zzzz + u_xlat1;
    gl_Position = u_xlat0 + hlslcc_mtx4x4unity_MatrixVP[3];
    return;
}

#endif
#ifdef FRAGMENT
#version 100

#ifdef GL_FRAGMENT_PRECISION_HIGH
    precision highp float;
#else
    precision mediump float;
#endif
precision highp int;
uniform 	vec4 _Time;
uniform 	vec4 _Layer3Tex_ST;
uniform 	mediump vec4 _Layer1Vector;
uniform 	mediump vec4 _Layer2Color;
uniform 	mediump vec4 _Layer3Color;
uniform 	vec4 _Layer3Vector;
uniform lowp sampler2D _MixMap;
uniform lowp sampler2D _Layer1Tex;
uniform lowp sampler2D _Layer2Tex;
uniform lowp sampler2D _Layer3FTex;
uniform lowp sampler2D _Layer3Tex;
uniform lowp sampler2D _MainTexEX;
varying highp vec4 vs_TEXCOORD0;
varying highp vec4 vs_TEXCOORD1;
varying mediump vec3 vs_TEXCOORD3;
#define SV_Target0 gl_FragData[0]
vec2 u_xlat0;
lowp vec4 u_xlat10_0;
mediump vec4 u_xlat16_1;
lowp vec3 u_xlat10_2;
mediump vec3 u_xlat16_3;
lowp vec3 u_xlat10_4;
mediump vec2 u_xlat16_5;
float u_xlat8;
void main()
{
    u_xlat10_0.x = texture2D(_Layer1Tex, vs_TEXCOORD0.zw).x;
    u_xlat0.x = u_xlat10_0.x * vs_TEXCOORD3.x;
    u_xlat10_4.xyz = texture2D(_MixMap, vs_TEXCOORD0.xy).xyz;
    u_xlat16_1.x = u_xlat10_4.x * u_xlat0.x;
    u_xlat16_5.xy = u_xlat10_4.yz * vs_TEXCOORD3.yz;
    u_xlat0.xy = u_xlat16_1.xx * _Layer1Vector.zw + vs_TEXCOORD0.xy;
    u_xlat10_0 = texture2D(_MainTexEX, u_xlat0.xy);
    u_xlat10_2.xyz = texture2D(_Layer2Tex, vs_TEXCOORD1.xy).xyz;
    u_xlat16_1.xyw = u_xlat16_5.xxx * u_xlat10_2.xyz;
    u_xlat16_1.xyw = u_xlat16_1.xyw * _Layer2Color.xyz + u_xlat10_0.xyz;
    SV_Target0.w = u_xlat10_0.w;
    u_xlat10_0.xy = texture2D(_Layer3FTex, vs_TEXCOORD1.zw).xy;
    u_xlat8 = fract(_Time.x);
    u_xlat0.xy = _Layer3Vector.xy * vec2(u_xlat8) + u_xlat10_0.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    u_xlat10_0.xyz = texture2D(_Layer3Tex, u_xlat0.xy).xyz;
    u_xlat16_3.xyz = u_xlat16_5.yyy * u_xlat10_0.xyz;
    SV_Target0.xyz = u_xlat16_3.xyz * _Layer3Color.xyz + u_xlat16_1.xyw;
    return;
}

#endif
"
}
SubProgram "gles3 " {
Local Keywords { "_MODEL2_ADD" "_MODEL3_ADD" }
"// hash: 190d92f54e608e65
#ifdef VERTEX
#version 300 es

#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
#define UNITY_UNIFORM
#else
#define UNITY_UNIFORM uniform
#endif
#define UNITY_SUPPORTS_UNIFORM_LOCATION 1
#if UNITY_SUPPORTS_UNIFORM_LOCATION
#define UNITY_LOCATION(x) layout(location = x)
#define UNITY_BINDING(x) layout(binding = x, std140)
#else
#define UNITY_LOCATION(x)
#define UNITY_BINDING(x) layout(std140)
#endif
uniform 	vec4 _Time;
uniform 	vec3 _WorldSpaceCameraPos;
uniform 	vec4 hlslcc_mtx4x4unity_MatrixVP[4];
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(1) uniform UnityPerDraw {
#endif
	UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
	UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_WorldToObject[4];
	UNITY_UNIFORM vec4 unity_LODFade;
	UNITY_UNIFORM mediump vec4 unity_WorldTransformParams;
	UNITY_UNIFORM mediump vec4 unity_LightData;
	UNITY_UNIFORM mediump vec4 unity_LightIndices[2];
	UNITY_UNIFORM vec4 unity_ProbesOcclusion;
	UNITY_UNIFORM mediump vec4 unity_SpecCube0_HDR;
	UNITY_UNIFORM vec4 unity_LightmapST;
	UNITY_UNIFORM vec4 unity_DynamicLightmapST;
	UNITY_UNIFORM mediump vec4 unity_SHAr;
	UNITY_UNIFORM mediump vec4 unity_SHAg;
	UNITY_UNIFORM mediump vec4 unity_SHAb;
	UNITY_UNIFORM mediump vec4 unity_SHBr;
	UNITY_UNIFORM mediump vec4 unity_SHBg;
	UNITY_UNIFORM mediump vec4 unity_SHBb;
	UNITY_UNIFORM mediump vec4 unity_SHC;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(0) uniform UnityPerMaterial {
#endif
	UNITY_UNIFORM mediump vec4 _MainTexEX_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer2Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer3FTex_ST;
	UNITY_UNIFORM vec4 _Layer3Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Vector;
	UNITY_UNIFORM float _Layer1Rotate;
	UNITY_UNIFORM mediump float _Layer1PulseRate;
	UNITY_UNIFORM mediump float _Layer1PulseClip;
	UNITY_UNIFORM mediump float _Layer1PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer2Vector;
	UNITY_UNIFORM float _Layer2Rotate;
	UNITY_UNIFORM mediump vec4 _Layer2Color;
	UNITY_UNIFORM mediump float _Layer2PulseRate;
	UNITY_UNIFORM mediump float _Layer2PulseClip;
	UNITY_UNIFORM mediump float _Layer2PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer3Color;
	UNITY_UNIFORM mediump vec4 _Layer3Offset;
	UNITY_UNIFORM vec4 _Layer3Vector;
	UNITY_UNIFORM float _Layer3Rotate;
	UNITY_UNIFORM mediump float _Layer3PulseRate;
	UNITY_UNIFORM mediump float _Layer3PulseClip;
	UNITY_UNIFORM mediump float _Layer3PulseIntensity;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
in highp vec4 in_POSITION0;
in highp vec2 in_TEXCOORD0;
out highp vec4 vs_TEXCOORD0;
out highp vec4 vs_TEXCOORD1;
out mediump vec4 vs_TEXCOORD2;
out mediump vec3 vs_TEXCOORD3;
vec4 u_xlat0;
vec4 u_xlat1;
vec3 u_xlat2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
vec2 u_xlat10;
vec2 u_xlat11;
void main()
{
    u_xlat0.xy = _Time.xx * _Layer1Vector.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat1.x = sin(_Layer1Rotate);
    u_xlat2.x = cos(_Layer1Rotate);
    u_xlat2.yz = u_xlat1.xx;
    u_xlat1.xyz = u_xlat2.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat10.xy = in_TEXCOORD0.xy + vec2(-0.5, -0.5);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat1.xy = u_xlat11.xy + vec2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * _Layer1Tex_ST.xy + u_xlat0.xy;
    vs_TEXCOORD0.zw = u_xlat0.xy + _Layer1Tex_ST.zw;
    vs_TEXCOORD0.xy = in_TEXCOORD0.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;
    u_xlat0.x = sin(_Layer2Rotate);
    u_xlat1.x = cos(_Layer2Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat0.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat0.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy + vec2(0.5, 0.5);
    u_xlat1.xy = _Time.xx * _Layer2Vector.xy;
    u_xlat1.xy = fract(u_xlat1.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer2Tex_ST.xy + u_xlat1.xy;
    vs_TEXCOORD1.xy = u_xlat0.xy + _Layer2Tex_ST.zw;
    u_xlat0.x = sin(_Layer3Rotate);
    u_xlat1.x = cos(_Layer3Rotate);
    u_xlat1.yz = u_xlat0.xx;
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, -0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat1.xyz = u_xlat1.xyz * vec3(2.0, 2.0, 2.0) + vec3(-1.0, -1.0, -1.0);
    u_xlat11.x = dot(u_xlat10.xy, u_xlat1.xz);
    u_xlat11.y = dot(u_xlat10.yx, u_xlat1.xy);
    u_xlat0.xy = u_xlat11.xy + vec2(0.5, 0.5);
    vs_TEXCOORD1.zw = u_xlat0.xy * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;
    vs_TEXCOORD2.w = 1.0;
    u_xlat0.xyz = in_POSITION0.yyy * hlslcc_mtx4x4unity_ObjectToWorld[1].xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[0].xyz * in_POSITION0.xxx + u_xlat0.xyz;
    u_xlat0.xyz = hlslcc_mtx4x4unity_ObjectToWorld[2].xyz * in_POSITION0.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + hlslcc_mtx4x4unity_ObjectToWorld[3].xyz;
    u_xlat1.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    vs_TEXCOORD2.xyz = u_xlat1.xyz;
    u_xlat1.x = _Time.x * _Layer1PulseRate;
    u_xlat1.y = _Time.x * _Layer2PulseRate;
    u_xlat1.z = _Time.x * _Layer3PulseRate;
    u_xlat1.xyz = fract(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(6.23999977, 6.23999977, 6.23999977);
    u_xlat1.xyz = cos(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * vec3(0.5, 0.5, 0.5) + vec3(0.5, 0.5, 0.5);
    u_xlat16_3.x = _Layer1PulseClip;
    u_xlat16_4.x = (-u_xlat16_3.x) + _Layer1PulseIntensity;
    u_xlat16_3.y = _Layer2PulseClip;
    u_xlat16_4.y = (-u_xlat16_3.y) + _Layer2PulseIntensity;
    u_xlat16_3.z = _Layer3PulseClip;
    u_xlat16_4.z = (-u_xlat16_3.z) + _Layer3PulseIntensity;
    vs_TEXCOORD3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat1 = u_xlat0.yyyy * hlslcc_mtx4x4unity_MatrixVP[1];
    u_xlat1 = hlslcc_mtx4x4unity_MatrixVP[0] * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = hlslcc_mtx4x4unity_MatrixVP[2] * u_xlat0.zzzz + u_xlat1;
    gl_Position = u_xlat0 + hlslcc_mtx4x4unity_MatrixVP[3];
    return;
}

#endif
#ifdef FRAGMENT
#version 300 es

precision highp float;
precision highp int;
#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
#define UNITY_UNIFORM
#else
#define UNITY_UNIFORM uniform
#endif
#define UNITY_SUPPORTS_UNIFORM_LOCATION 1
#if UNITY_SUPPORTS_UNIFORM_LOCATION
#define UNITY_LOCATION(x) layout(location = x)
#define UNITY_BINDING(x) layout(binding = x, std140)
#else
#define UNITY_LOCATION(x)
#define UNITY_BINDING(x) layout(std140)
#endif
uniform 	vec4 _Time;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
UNITY_BINDING(0) uniform UnityPerMaterial {
#endif
	UNITY_UNIFORM mediump vec4 _MainTexEX_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer2Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer3FTex_ST;
	UNITY_UNIFORM vec4 _Layer3Tex_ST;
	UNITY_UNIFORM mediump vec4 _Layer1Vector;
	UNITY_UNIFORM float _Layer1Rotate;
	UNITY_UNIFORM mediump float _Layer1PulseRate;
	UNITY_UNIFORM mediump float _Layer1PulseClip;
	UNITY_UNIFORM mediump float _Layer1PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer2Vector;
	UNITY_UNIFORM float _Layer2Rotate;
	UNITY_UNIFORM mediump vec4 _Layer2Color;
	UNITY_UNIFORM mediump float _Layer2PulseRate;
	UNITY_UNIFORM mediump float _Layer2PulseClip;
	UNITY_UNIFORM mediump float _Layer2PulseIntensity;
	UNITY_UNIFORM mediump vec4 _Layer3Color;
	UNITY_UNIFORM mediump vec4 _Layer3Offset;
	UNITY_UNIFORM vec4 _Layer3Vector;
	UNITY_UNIFORM float _Layer3Rotate;
	UNITY_UNIFORM mediump float _Layer3PulseRate;
	UNITY_UNIFORM mediump float _Layer3PulseClip;
	UNITY_UNIFORM mediump float _Layer3PulseIntensity;
#if HLSLCC_ENABLE_UNIFORM_BUFFERS
};
#endif
UNITY_LOCATION(0) uniform mediump sampler2D _MainTexEX;
UNITY_LOCATION(1) uniform mediump sampler2D _MixMap;
UNITY_LOCATION(2) uniform mediump sampler2D _Layer1Tex;
UNITY_LOCATION(3) uniform mediump sampler2D _Layer2Tex;
UNITY_LOCATION(4) uniform mediump sampler2D _Layer3FTex;
UNITY_LOCATION(5) uniform mediump sampler2D _Layer3Tex;
in highp vec4 vs_TEXCOORD0;
in highp vec4 vs_TEXCOORD1;
in mediump vec3 vs_TEXCOORD3;
layout(location = 0) out mediump vec4 SV_Target0;
vec2 u_xlat0;
mediump vec4 u_xlat16_0;
mediump vec4 u_xlat16_1;
mediump vec3 u_xlat16_2;
mediump vec3 u_xlat16_3;
mediump vec3 u_xlat16_4;
mediump vec2 u_xlat16_5;
float u_xlat8;
void main()
{
    u_xlat16_0.x = texture(_Layer1Tex, vs_TEXCOORD0.zw).x;
    u_xlat0.x = u_xlat16_0.x * vs_TEXCOORD3.x;
    u_xlat16_4.xyz = texture(_MixMap, vs_TEXCOORD0.xy).xyz;
    u_xlat16_1.x = u_xlat16_4.x * u_xlat0.x;
    u_xlat16_5.xy = u_xlat16_4.yz * vs_TEXCOORD3.yz;
    u_xlat0.xy = u_xlat16_1.xx * _Layer1Vector.zw + vs_TEXCOORD0.xy;
    u_xlat16_0 = texture(_MainTexEX, u_xlat0.xy);
    u_xlat16_2.xyz = texture(_Layer2Tex, vs_TEXCOORD1.xy).xyz;
    u_xlat16_1.xyw = u_xlat16_5.xxx * u_xlat16_2.xyz;
    u_xlat16_1.xyw = u_xlat16_1.xyw * _Layer2Color.xyz + u_xlat16_0.xyz;
    SV_Target0.w = u_xlat16_0.w;
    u_xlat16_0.xy = texture(_Layer3FTex, vs_TEXCOORD1.zw).xy;
    u_xlat8 = fract(_Time.x);
    u_xlat0.xy = _Layer3Vector.xy * vec2(u_xlat8) + u_xlat16_0.xy;
    u_xlat0.xy = fract(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;
    u_xlat16_0.xyz = texture(_Layer3Tex, u_xlat0.xy).xyz;
    u_xlat16_3.xyz = u_xlat16_5.yyy * u_xlat16_0.xyz;
    SV_Target0.xyz = u_xlat16_3.xyz * _Layer3Color.xyz + u_xlat16_1.xyw;
    return;
}

#endif
"
}
}
Program "fp" {
SubProgram "gles " {
""
}
SubProgram "gles3 " {
""
}
SubProgram "gles " {
Local Keywords { "_MODEL3_ADD" }
""
}
SubProgram "gles3 " {
Local Keywords { "_MODEL3_ADD" }
""
}
SubProgram "gles " {
Local Keywords { "_MODEL2_ADD" }
""
}
SubProgram "gles3 " {
Local Keywords { "_MODEL2_ADD" }
""
}
SubProgram "gles " {
Local Keywords { "_MODEL2_ADD" "_MODEL3_ADD" }
""
}
SubProgram "gles3 " {
Local Keywords { "_MODEL2_ADD" "_MODEL3_ADD" }
""
}
}
}
}
Fallback "Hidden/InternalErrorShader"
}