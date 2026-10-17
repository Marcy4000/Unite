// Recovered from Compiled/==PGAME==_LobbyPlayer_m_lob_PlayerPBRHairColor_lv3.txt.
Shader "==PGAME==/LOBBYPLAYER/m_lob_PlayerPBRHairColor_lv3_URP"
{
    Properties
    {
        [Header(Textures)]
        [MainTexture] _MainTex ("Diffuse (RGB) Roughness (A)", 2D) = "white" {}
        [HDR] _colorSkin ("Skin Color in Cutout Area", Color) = (1,1,1,1)
        _BumpMapNr ("Normal (RGB) Metallic (A) - Linear RGBA", 2D) = "bump" {}
        [Enum(LinearRGBA,0,UnityNormalMap,1,SRGBRGBA,2)] _PackedNormalEncoding ("Packed Normal Encoding", Float) = 1
        _MixMap ("Alpha (R) Color Bypass (G) Emission (B) Color Index (A)", 2D) = "black" {}
        [Toggle] _Mask ("Use Cutout Mask", Float) = 0
        [Toggle] _Emissive ("Enable Emission", Float) = 0
        [KeywordEnum(UV0, UV1)] _SLUV ("Texture UV Set", Float) = 0
        [Header(Lighting)]
        _ShadowOffset ("Diffuse Shadow Offset", Range(-1,1)) = 0
        _ShadowPow ("Diffuse Shadow Power", Range(0.5,10)) = 1
        _ShadowScale ("Minimum Lighting", Range(0,1)) = 0
        _NormalScale ("Normal Strength", Range(-8,8)) = 1
        _EmisssionScale ("Emission Scale", Range(0,10)) = 0
        _MainlightAttenuation ("World Height Attenuation", Range(0.04,32)) = 0.04
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        [Header(Hair Highlights)]
        _HairMap ("Strand Shape (R) Highlight Mask (G)", 2D) = "black" {}
        _shapeST ("Strand Shape Tiling and Offset", Vector) = (1,1,0,0)
        _SunShiftOffuse1 ("First Strand Shift", Range(-1,1)) = 0
        _SunShiftOffuse2 ("Second Strand Shift", Range(-1,1)) = 0
        _SunShift ("Shape Shift Strength", Range(-1,1)) = 0
        [HDR] _specularColor1 ("First Highlight Color", Color) = (0.2,0.2,0.2,1)
        _glossiness_1X ("First Highlight X", Range(0,1)) = 0.1
        _glossiness_1Y ("First Highlight Y", Range(0,1)) = 0.8
        [HDR] _specularColor2 ("Second Highlight Color", Color) = (0.3,0.2,0.1,1)
        _glossiness_2X ("Second Highlight X", Range(0,1)) = 0.4
        _glossiness_2Y ("Second Highlight Y", Range(0,1)) = 1
        [Header(Ambient)]
        [KeywordEnum(Cla, Lerp)] _SHType ("SH Type", Float) = 1
        _SHScale ("SH Strength", Range(0,10)) = 1
        [HDR] _SHTopColor ("SH Top Color", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("SH Bottom Color", Color) = (2.5,2.5,2.5,1)
        _SHColorScale ("SH Color Scale", Color) = (1,1,1,1)
        [Header(Extra Light)]
        _AddLightDir ("Extra Light Direction", Vector) = (0,1,0,0)
        [HDR] _AddlightColor ("Extra Light Color", Color) = (0,0,0,1)
        _AddlightLerp ("Extra Light Height Offset", Range(0,1)) = 0.5
        _AddlightAttenuation ("Extra Light Height Scale", Float) = 1
        [Header(Rim)]
        [Toggle] _RimEnabled ("Enable Recovered Rim Lighting", Float) = 0
        _RimLightDir ("Rim Direction", Vector) = (0,1,0,0)
        _RimlightScale ("Rim Start", Float) = 1
        _RimlightScale2 ("Rim End", Float) = 1.5
        _RimlightShadowScale ("Rim in Shadow", Range(0,1)) = 0.5
        [HDR] _RimlightColor ("Rim Color (A = Ignore Albedo)", Color) = (0,0,0,0)
        [Header(Outline)]
        [Toggle] _OutlineEnabled ("Enable Outline", Float) = 1
        [KeywordEnum(OFF, ON)] _VTEX ("Use Outline Texture", Float) = 0
        [KeywordEnum(OFF, ON)] _VCOLOR2N ("Vertex RGB Encodes Smooth Outline Normal", Float) = 0
        _OutlineMap ("Outline Color (RGB) Width (A)", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.5,0.5,0.5,1)
        _OutlineWidth ("Outline Width Multiplier", Float) = 1
        _Offset ("Outline Depth Offset", Float) = -5
        _lightDir ("Outline Light Direction", Vector) = (9.48,3.68,0,0)
        _ScreenResolution ("Outline Reference Resolution", Vector) = (1920,1080,0,0)
        [Header(Color Regions)]
        [HDR] _color1 ("Region 1 Color", Color) = (1,1,1,1)
        [HDR] _color2 ("Region 2 Color", Color) = (1,1,1,1)
        [HDR] _color3 ("Region 3 Color", Color) = (1,1,1,1)
        [HDR] _color4 ("Region 4 Color", Color) = (1,1,1,1)
        _metalv4 ("Region Metallic Offsets", Vector) = (0,0,0,0)
        _roughv4 ("Region Roughness Offsets", Vector) = (1,1,1,1)
        [Header(Debug)]
        [KeywordEnum(OFF, Deffiuse, Normal, Metal, Roughness, AO, Emiss, SH)] _Debug ("Texture Debug", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _RenderType ("Legacy Render Type", Float) = 0
        [HideInInspector] _BlendMode ("Legacy Blend Mode", Float) = 0
        [HideInInspector] _Queue ("Legacy Queue", Float) = 3000
        [HideInInspector] _QueueOffset ("Legacy Queue Offset", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _ZWrite ("Depth Write", Float) = 1
        [HideInInspector] _ZTest ("Depth Test", Float) = 4
        [HideInInspector] _SrcBlendFactor ("Source Blend", Float) = 5
        [HideInInspector] _DstBlendFactor ("Destination Blend", Float) = 10
        [HideInInspector] _SrcBlendFactorAlpha ("Source Alpha Blend", Float) = 5
        [HideInInspector] _DstBlendFactorAlpha ("Destination Alpha Blend", Float) = 10
        [HideInInspector] _BlendOp ("Blend Operation", Float) = 0
        [HideInInspector] _BlendOpAlpha ("Alpha Blend Operation", Float) = 0
        [HideInInspector] _AlphaToMask ("Alpha to Coverage", Float) = 0
        [HideInInspector] _StencilData ("Stencil Reference", Float) = 0
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilComp ("Stencil Compare", Float) = 8
        [HideInInspector] _StencilPassOp ("Stencil Pass", Float) = 0
        [HideInInspector] _StencilFailOp ("Stencil Fail", Float) = 0
        [HideInInspector] _StencilZFailOp ("Stencil Depth Fail", Float) = 0
        // Retained shared material parameters; unused in the recovered hair FS.
        [HideInInspector] _actorscale ("Legacy Actor Scale", Float) = 1
        [HideInInspector] _ActorScaleAttribute ("Legacy Actor Scale Attribute", Float) = 1
        [HideInInspector] _UseObjectSpace ("Legacy Object Space Flag", Float) = 0
        [HideInInspector] _faceshadowScale ("Legacy Face Shadow Flag", Float) = 0
        [HideInInspector] _RoughNessOffset ("Legacy Roughness Offset", Range(-1,1)) = 0
        _MetallicOffset ("Metallic for Unity Normal Map Compatibility", Range(-1,1)) = 0
        [HideInInspector] _SpecularColor ("Legacy Specular Color", Color) = (1,1,1,1)
        [HideInInspector] _RimlightAttenuation ("Legacy Rim Height Attenuation", Float) = 0.04
        [HideInInspector] [KeywordEnum(X, Y, Z)] _AtVector ("Legacy Attenuation Vector", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 300
        HLSLINCLUDE
        #define TRAINER_HAIR 1
        #include "TrainerLighting.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            // Keep legacy RGB blending, but accumulate coverage (not alpha squared).
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
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrainerMetaVertex
            #pragma fragment TrainerMetaFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            TrainerVaryings TrainerMetaVertex(TrainerAttributes input)
            {
                TrainerVaryings output = (TrainerVaryings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz,input.uv1,input.uv2);
                output.uv = TrainerUV(input);
                return output;
            }
            half4 TrainerMetaFragment(TrainerVaryings input) : SV_Target
            {
                half4 mask = SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv);
                half alpha = TrainerAlpha(mask);
                half4 main = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
                float3 albedo;
                float roughness,metallic;
                TrainerSurface(main,SAMPLE_TEXTURE2D(_BumpMapNr,sampler_BumpMapNr,input.uv),mask,alpha,albedo,roughness,metallic);
                MetaInput meta = (MetaInput)0;
                meta.Albedo = albedo;
                meta.Emission = main.rgb * mask.b * _Emissive * _EmisssionScale;
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
