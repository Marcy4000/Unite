Shader "==PGAME==/LobbyPlayer/m_lob_PlayerPBRColorEye" {
    // Properties remain the same
    Properties {
        [Header(Tex)] 
        [MainTexture] _MainTex ("Diffuse", 2D) = "white" { }
        _MixMap ("Alpha(R) Mask(G) Emiss(B) Color(A)", 2D) = "black" { }
        [Toggle] _Emissive ("If Emissive", Float) = 0
        
        [Header(MaskColor)] 
        [HDR] _color1 ("_color1", Color) = (1,1,1,1)
        [HDR] _color2 ("_color2", Color) = (1,1,1,1)
        [HDR] _color3 ("_color3", Color) = (1,1,1,1)
        [HDR] _color4 ("_color4", Color) = (1,1,1,1)
        
        [Header(OutLine)] 
        _OutlineColor ("OutlineColor", Color) = (0.5,0.5,0.5,1)
        _Offset ("Z Offset", Float) = -5
        
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        _Cull ("__cull", Float) = 2
        [HideInInspector] _QueueOffset ("Queue offset", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        _BaseColor("Base Color", Color) = (1,1,1,1)
    }
    
    SubShader {
        Tags { 
            "RenderPipeline" = "UniversalPipeline" 
            "RenderType" = "Opaque" 
            "IGNOREPROJECTOR" = "true"
        }
        LOD 300

        // Main Forward Lit Pass
        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend Off
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ENABLE_POST_TONE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS   : POSITION;
                // FIX: Changed from TEXCOORD1 to TEXCOORD0. This instructs the shader to use the
                // primary UV channel from the mesh, fixing the stretching and orientation issue.
                float2 uv           : TEXCOORD0; 
            };

            struct Varyings {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MixMap);
            SAMPLER(sampler_MixMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _color1;
                float4 _color2;
                float4 _color3;
                float4 _color4;
            CBUFFER_END

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                half4 mixMap = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, IN.uv);
                half3 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;
                
                half4 weights = mixMap.a + half4(0.0, -0.333333, -0.666667, -1.0);
                weights = -abs(weights * 3.191489) + 1.031915;
                weights = saturate(weights);

                half3 blendedColor = weights.x * _color1.rgb + 
                                     weights.y * _color2.rgb + 
                                     weights.z * _color3.rgb + 
                                     weights.w * _color4.rgb;

                half mask = mixMap.g;
                half3 maskedBlendedColor = lerp(blendedColor * 1.2, half3(1.0, 1.0, 1.0), mask);
                
                half3 baseColor = maskedBlendedColor * mainTex;
                
                half emission = mixMap.b;
                half3 finalColor = baseColor * (1.0 + emission);
                
                #ifndef ENABLE_POST_TONE
                    const half k1 = 0.449999;
                    const half k2 = 0.155000;
                    const half k3 = 1.019000;
                    const half k4 = 2.200000;

                    half3 toned = (finalColor * k1) / (finalColor * k1 + k2);
                    toned *= k3;
                    
                    finalColor = pow(saturate(toned), k4);
                    
                    finalColor = min(finalColor, half3(100.0, 100.0, 100.0));
                #endif

                half alpha = mixMap.r;
                
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
        
        // Shadow Caster Pass (no changes needed here)
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                // It's good practice to declare the UVs here as well, matching the original.
                float2 uv           : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS   : SV_POSITION;
            };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                Light mainLight = GetMainLight();
                
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                
                positionWS = ApplyShadowBias(positionWS, normalWS, mainLight.direction);
                
                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                return 0;
            }
            ENDHLSL
        }

        // Depth Only Pass (no changes needed here)
        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS   : SV_POSITION;
            };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                return 0;
            }
            ENDHLSL
        }
        
        // 2D Pass (no changes needed here)
        Pass {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Blend Off
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
            CBUFFER_END
            
            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return texColor * _BaseColor;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/InternalErrorShader"
}