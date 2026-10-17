// Forward programs recovered from Compiled/==PGAME==_LobbyPlayer_m_lob_PlayerPBRColorEye.txt.
Shader "==PGAME==/LobbyPlayer/m_lob_PlayerPBRColorEye"
{
    Properties
    {
        [Header(Textures)]
        [MainTexture] _MainTex ("Diffuse (RGB)", 2D) = "white" {}
        _MixMap ("Alpha (R) Tint Bypass (G) Brightening (B) Region Index (A)", 2D) = "black" {}
        [Enum(UV0,0,UV1,1)] _EyeUV ("Eye UV Set (Original Uses UV1)", Float) = 1
        [Toggle] _Emissive ("Legacy Emissive Flag (Unused by Original Eye Programs)", Float) = 0

        [Header(Region Colors)]
        [HDR] _color1 ("Region 1", Color) = (1,1,1,1)
        [HDR] _color2 ("Region 2", Color) = (1,1,1,1)
        [HDR] _color3 ("Region 3", Color) = (1,1,1,1)
        [HDR] _color4 ("Region 4", Color) = (1,1,1,1)

        // Kept for existing materials/presets; the dump has no outline pass.
        [Header(Legacy Outline Parameters)]
        _OutlineColor ("Outline Color (Unused)", Color) = (0.5,0.5,0.5,1)
        _Offset ("Z Offset (Unused)", Float) = -5
        _lightDir ("Outline Light Direction (Unused)", Vector) = (9.48,3.68,0,0)

        [Header(Render State)]
        [HideInInspector] _Surface ("Legacy Surface", Float) = 0
        [HideInInspector] _Blend ("Legacy Blend Mode", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip (Compatibility Extension)", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0
        [Toggle] _ZWrite ("Depth Write", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [HideInInspector] _QueueOffset ("Queue Offset", Float) = 0

        // The surviving 2D program is a generic BaseMap * BaseColor fallback.
        // Preserve the previous conversion's MainTex fallback unless explicitly selected.
        [Header(Generic 2D Fallback)]
        [Toggle] _UseBaseMap2D ("Use Original 2D BaseMap Instead of MainTex", Float) = 0
        _BaseMap ("2D Base Map", 2D) = "white" {}
        _BaseColor ("2D Base Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        // The same buffer is used by every pass for SRP Batcher compatibility.
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _BaseMap_ST;
            float4 _color1, _color2, _color3, _color4;
            float4 _OutlineColor, _lightDir, _BaseColor;
            float _EyeUV, _Emissive, _Offset, _Surface, _Blend, _AlphaClip, _Cutoff;
            float _SrcBlend, _DstBlend, _ZWrite, _ZTest, _Cull, _QueueOffset, _UseBaseMap2D;
        CBUFFER_END

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv0 : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float2 EyeUV(Attributes input)
        {
            float2 uv = _EyeUV > 0.5 ? input.uv1 : input.uv0;
            // The original applies MainTex ST to BOTH maps, not separate MixMap ST.
            return uv * _MainTex_ST.xy + _MainTex_ST.zw;
        }

        Varyings EyeVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = EyeUV(input);
            return output;
        }

        void ClipEye(float alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #else
                if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
            #endif
        }

        float3 EyeColor(float3 diffuse, float4 mask)
        {
            float4 weights = saturate(1.03191495 - abs((mask.a +
                float4(0,-0.333333313,-0.666666687,-1)) * 3.19148946));
            // Do not normalize the narrow overlapping weights at region boundaries.
            float3 tint = weights.x * _color1.rgb + weights.y * _color2.rgb +
                          weights.z * _color3.rgb + weights.w * _color4.rgb;
            float3 color = diffuse * lerp(tint * 1.2,float3(1,1,1),mask.g);
            // Both recovered variants ignore _Emissive: B always brightens the tinted base.
            return color * (1.0 + mask.b);
        }

        float3 EyeTone(float3 color)
        {
            // Guard the log/power domain at black/negative input, but do not saturate HDR.
            color = max(color,0.0);
            float3 scaled = color * 0.45;
            float3 mapped = scaled / (scaled + 0.155) * 1.019;
            return min(pow(mapped,2.2),100.0);
        }

        float4 EyeFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 mask = SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv);
            ClipEye(mask.r);
            float3 color = EyeColor(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).rgb,mask);
            #if !defined(ENABLE_POST_TONE)
                color = EyeTone(color);
            #endif
            // Main texture alpha is not opacity, and BaseColor is only a 2D fallback tint.
            return float4(color,mask.r);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EyeVertex
            #pragma fragment EyeFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ ENABLE_POST_TONE
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EyeShadowVertex
            #pragma fragment EyeShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings EyeShadowVertex(Attributes input)
            {
                Varyings output = EyeVertex(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 direction = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    direction = SafeNormalize(_LightPosition - positionWS);
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS,output.normalWS,direction));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            float4 EyeShadowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ClipEye(SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv).r);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EyeVertex
            #pragma fragment EyeDepthFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON

            float EyeDepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipEye(SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv).r);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EyeVertex
            #pragma fragment EyeNormalsFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            float4 EyeNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipEye(SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv).r);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return float4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5)),0);
                #else
                    return float4(normalWS,0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Eye2DVertex
            #pragma fragment Eye2DFragment
            #pragma multi_compile_instancing

            Varyings Eye2DVertex(Attributes input)
            {
                Varyings output = EyeVertex(input);
                output.uv = _UseBaseMap2D > 0.5 ? input.uv0 * _BaseMap_ST.xy + _BaseMap_ST.zw :
                                                input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }
            float4 Eye2DFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 color;
                if (_UseBaseMap2D > 0.5) color = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv);
                else color = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
                return color * _BaseColor;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
