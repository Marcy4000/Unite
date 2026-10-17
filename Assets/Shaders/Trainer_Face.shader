// Recovered from Compiled/==PGAME==_LobbyPlayer_m_lob_playerpbrSkin_lv3.txt.
Shader "PGAME_URP/LobbyPlayer/m_lob_playerpbrSkin_lv3"
{
    Properties
    {
        [Header(Textures)]
        [MainTexture] _MainTex ("Diffuse (RGB) Roughness (A)", 2D) = "white" {}
        _BumpMapNr ("Normal (RGB) Skin Tint Bypass (A) - Linear RGBA", 2D) = "bump" {}
        [Enum(LinearRGBA,0,UnityNormalMap,1,SRGBRGBA,2)] _PackedNormalEncoding ("Packed Normal Encoding", Float) = 1
        _MixMap ("Cutout (R) Unused (G) Emission (B) Unused (A)", 2D) = "white" {}
        [Header(Skin)]
        [HDR] _colorSkin ("Skin Color", Color) = (1,1,1,1)
        _RoughNessOffset ("Roughness Offset", Range(-1,1)) = 0
        [HDR] _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _NormalScale ("Normal Strength", Range(-8,8)) = 1
        _ShadowOffset ("Diffuse Shadow Offset", Range(-1,1)) = 0
        _ShadowPow ("Diffuse Shadow Power", Range(0.5,10)) = 1
        _ShadowScale ("Minimum Lighting", Range(0,1)) = 0
        [Toggle(_FACESHDW_SCALE_ON)] _faceshadowScale ("Lighten Face Projection Shadows", Float) = 0
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        [Header(Ambient)]
        [KeywordEnum(Cla, Lerp)] _SHType ("SH Type", Float) = 1
        _SHScale ("SH Strength", Range(0,10)) = 1
        [HDR] _SHTopColor ("SH Top Color", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("SH Bottom Color", Color) = (2.5,2.5,2.5,1)
        _SHColorScale ("SH Color Scale", Color) = (1,1,1,1)
        [Header(Emission and Cutout)]
        _Emissive ("Emission Factor (Mask B)", Range(0,1)) = 1
        _EmisssionScale ("Emission Scale", Float) = 1
        _Mask ("Cutout Mask Strength", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Header(Extra Light)]
        _AddLightDir ("Extra Light Direction", Vector) = (0,1,0,0)
        [HDR] _AddlightColor ("Extra Light Color", Color) = (0,0,0,1)
        _AddlightLerp ("Extra Light Height Offset", Range(0,1)) = 0.5
        _AddlightAttenuation ("Extra Light Height Scale", Range(0.04,32)) = 1
        [Header(Rim)]
        [Toggle] _RimEnabled ("Enable Rim Lighting", Float) = 1
        _RimLightDir ("Rim Direction", Vector) = (0,1,0,0)
        _RimlightScale ("Rim Start", Float) = 1
        _RimlightScale2 ("Rim End", Float) = 1.5
        _RimlightShadowScale ("Rim in Shadow", Range(0,1)) = 0.5
        [HDR] _RimlightColor ("Rim Color (A = Ignore Albedo)", Color) = (1,1,1,1)
        [Header(Outline)]
        [Toggle] _OutlineEnabled ("Enable Outline", Float) = 1
        [Toggle(_VTEX_ON)] _VTEX ("Legacy Outline Texture Flag", Float) = 0
        [Toggle(_VCOLOR2N_ON)] _VCOLOR2N ("Vertex RGB Encodes Smooth Outline Normal", Float) = 0
        _OutlineMap ("Legacy Outline Map", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.5,0.5,0.5,1)
        _OutlineWidth ("Outline Width (Vertex Alpha Masked)", Float) = 0.004629
        _Offset ("Outline Depth Offset", Float) = -5
        _lightDir ("Outline Light Direction", Vector) = (9.48,3.68,0,0)
        _ScreenResolution ("Outline Reference Resolution", Vector) = (1920,1080,0,0)
        [Header(Debug)]
        [KeywordEnum(OFF, Diffiuse, Normal, Metal, Roughness, AO, Emiss, SH)] _Debug ("Texture Debug", Float) = 0
        [Header(Render State)]
        [Enum(UnityEngine.Rendering.RenderQueue)] _Queue ("Legacy Queue Value (Set Material Queue)", Float) = 2000
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("Depth Write", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendFactor ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendFactor ("Destination Blend", Float) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Operation", Float) = 0
        [HideInInspector] _SrcBlendFactorAlpha ("Source Alpha Blend", Float) = 1
        [HideInInspector] _DstBlendFactorAlpha ("Destination Alpha Blend", Float) = 0
        [HideInInspector] _BlendOpAlpha ("Alpha Blend Operation", Float) = 0
        [HideInInspector] _AlphaToMask ("Alpha to Coverage", Float) = 0
        [HideInInspector] _RenderType ("Legacy Render Type", Float) = 0
        [HideInInspector] _BlendMode ("Legacy Blend Mode", Float) = 0
        [HideInInspector] _QueueOffset ("Legacy Queue Offset", Float) = 0
        [Header(Stencil)]
        [HideInInspector] _StencilData ("Stencil Reference", Float) = 0
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassOp ("Stencil Pass", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFailOp ("Stencil Fail", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFailOp ("Stencil Depth Fail", Float) = 0
        // Kept for preset/material compatibility; these are unused in the recovered FS.
        [HideInInspector] _MetallicOffset ("Legacy Metallic Offset", Range(-1,1)) = 0
        [HideInInspector] _actorscale ("Legacy Actor Scale", Float) = 1
        [HideInInspector] _ActorScaleAttribute ("Legacy Actor Scale Attribute", Float) = 1
        [Toggle(_USE_OBJECT_SPACE)] _UseObjectSpace ("Extra Light Uses Object Space Height", Float) = 0
        [HideInInspector] _MainlightAttenuation ("Legacy Main Height Attenuation", Range(0.04,32)) = 0.04
        [HideInInspector] _RimlightAttenuation ("Legacy Rim Height Attenuation", Range(0.04,32)) = 0.04
        [HideInInspector] [KeywordEnum(X, Y, Z)] _AtVector ("Legacy Attenuation Vector", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        HLSLINCLUDE
        #include "TrainerLighting.hlsl"
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_SrcBlendFactor] [_DstBlendFactor], One OneMinusSrcAlpha
            BlendOp [_BlendOp], Add
            AlphaToMask [_AlphaToMask]
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
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrainerVertex
            #pragma fragment TrainerFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ ENABLE_POST_TONE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            ENDHLSL
        }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="TrainerOutline" }
            Cull Front
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrainerOutlineVertex
            #pragma fragment TrainerOutlineFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
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
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrainerShadowVertex
            #pragma fragment TrainerShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrainerVertex
            #pragma fragment TrainerDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrainerVertex
            #pragma fragment TrainerDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }
            Cull Off
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_SrcBlendFactor] [_DstBlendFactor], One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrainerVertex
            #pragma fragment Trainer2DFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
