Shader "==PGAME==/Battle/m_bat_actorbase_lv3" {
    Properties {
        [Header(Tex)]
        [MainTexture] _MainTex ("Diffuse", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _MixMap ("MROE (Metallic-Roughness-Occlusion-Emission)", 2D) = "white" {}
        [Toggle] _Emissive ("If Emissive", Float) = 0

        [Header(Common)]
        _RoughNessOffset ("RoughNessOffset", Range(-1, 1)) = 0
        [HDR] _SpecularColor ("SpecularColor", Color) = (1,1,1,1)
        _MetallicOffset ("MetallicOffset", Range(-1, 1)) = 0
        _NormalScale ("NormalScale", Range(-8, 8)) = 1
        _EmisssionScale ("EmisssionScale", Range(0, 10)) = 0
        _EmisssionColor ("EmisssionColor", Color) = (1,1,1,1)
        _CustomAlpha ("Custom Alpha", Range(0, 1)) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(SH)]
        _SHScale ("SHScale", Range(0, 10)) = 1
        [HDR] _SHTopColor ("SHTopColor", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("SHBotColor", Color) = (2.5,2.5,2.5,1)

        [Header(Addlight)]
        _AddLightDir ("AddLightDir", Vector) = (1.22,0,2,0)
        [HDR] _AddlightColor ("AddlightColor", Color) = (0,0,0,0)

        [Header(Extra)]
        [MaterialToggle(_COLOR_MASK)] _ColorMask ("Enable Color Mask", Float) = 0
        [HideIfDisabled(_COLOR_MASK)] [HDR] _color1 ("_color1", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_MASK)] [HDR] _color2 ("_color2", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_MASK)] [HDR] _color3 ("_color3", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_MASK)] [HDR] _color4 ("_color4", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_MASK)] _metalv4 ("_metalv4", Vector) = (0,0,0,0)
        [HideIfDisabled(_COLOR_MASK)] _roughv4 ("_roughv4", Vector) = (0,0,0,0)

        [Header(COLOR_CHANGING)]
        [MaterialToggle(_COLOR_CHANGING)] _COLOR_CHANGING ("Enable Color Changing", Float) = 0
        [HideIfDisabled(_COLOR_CHANGING)] _ChangingMask ("ChangingMask", 2D) = "black" {}
        [HideIfDisabled(_COLOR_CHANGING)] _Color01 ("Mask_1.0", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color02 ("Mask_0.9", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color03 ("Mask_0.8", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color04 ("Mask_0.7", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color05 ("Mask_0.6", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color06 ("Mask_0.5", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color07 ("Mask_0.4", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color08 ("Mask_0.3", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color09 ("Mask_0.2", Color) = (1,1,1,1)
        [HideIfDisabled(_COLOR_CHANGING)] _Color10 ("Mask_0.1", Color) = (1,1,1,1)

        [Header(FX UNIFORM)]
        [MaterialToggle(_FXUNIF)] _FXUNI ("Enable Hit Effects", Float) = 0
        [HideIfDisabled(_FXUNIF)] _Rim ("UBuff Rim", Range(0, 10)) = 2
        [HideIfDisabled(_FXUNIF)] [Toggle] _TexorColor ("Use Texture for Line Color", Float) = 0
        [HideIfDisabled(_FXUNIF)] _LineConstColor ("UBuff Const Color", Color) = (0.8980393,0.3960784,1,0)
        [HideIfDisabled(_FXUNIF)] _LineColor ("UBuff Color", 2D) = "black" {}
        [HideIfDisabled(_FXUNIF)] _LineColorPosi ("UBuff Color Position", Range(0, 1)) = 0
        [HideIfDisabled(_FXUNIF)] _LineColorSpeed ("UBuff Color Change Speed", Range(0, 1)) = 0
        [HideIfDisabled(_FXUNIF)] _UBuffWidth ("UBuffOutLineWidth", Range(0, 0.1)) = 0.04
        [HideIfDisabled(_FXUNIF)] _Offset2 ("OutLineZOffSet", Float) = 0
        [HideIfDisabled(_FXUNIF)] _Ratio_SteadyColorEffect ("_RatioSteadyColorEffect", Float) = 0
        [HideIfDisabled(_FXUNIF)] [HDR] _SteadyColor ("_SteadyColor", Color) = (0,0,0,0)
        [HideIfDisabled(_FXUNIF)] _Ratio_FlashColorEffect ("_Ratio_FlashColorEffect", Float) = 0
        [HideIfDisabled(_FXUNIF)] _DOTEmissionQuency ("_DOTEmissionQuency", Float) = 5
        [HideIfDisabled(_FXUNIF)] _DOTColor ("_DOTColor", Color) = (0.8584906,0.125534,0.3327915,1)
        [HideIfDisabled(_FXUNIF)] _DOTFresnelPower ("_DOTFresnelPower", Float) = 1
        [HideIfDisabled(_FXUNIF)] _DOTEmissionMax ("_DOTEmissionMax", Float) = 1
        [HideIfDisabled(_FXUNIF)] _DOTEmissionMin ("_DOTEmissionMin", Float) = 0
        [HideIfDisabled(_FXUNIF)] _OverlayColor ("_OverlayColor", Color) = (0,0,0,0)
        [HideIfDisabled(_FXUNIF)] _OverlayColorQuency ("_OverlayColorQuency", Float) = 0
        [HideIfDisabled(_FXUNIF)] _OverlayColorFlicker ("_OverlayColorFlicker", Float) = 0
        [HideIfDisabled(_FXUNIF)] _Ratio_OverlayColor ("_Ratio_OverlayColor", Float) = 0
        [HideIfDisabled(_FXUNIF)] _DOTEmissionScale ("_DOTEmissionScale", Float) = 1
        [HideIfDisabled(_FXUNIF)] _Ratio_OverlayColorEffect ("_Ratio_OverlayColorEffect", Float) = 1
        [HideIfDisabled(_FXUNIF)] _AdditionalTexture ("_AdditionalTexture", 2D) = "white" {}
        [HideIfDisabled(_FXUNIF)] _AddTexST ("_AddTexST", Vector) = (1,1,0,0)
        [HideIfDisabled(_FXUNIF)] _AddTexColor ("_AddTexColor", Color) = (1,1,1,1)
        [HideIfDisabled(_FXUNIF)] _WhiteFresnelAlpha ("_WhiteFresnelAlpha", Range(0, 1)) = 0
        [HideIfDisabled(_FXUNIF)] _AddTexScale ("_AddTexScale", Float) = 0.7
        [HideIfDisabled(_FXUNIF)] _Ratio_ExtraTexture ("_Ratio_ExtraTexture", Float) = 0
        [HideIfDisabled(_FXUNIF)] [HDR] _flowColor ("_flowColor RGB(color) A(strength)", Color) = (1,1,1,0.3)
        [HideIfDisabled(_FXUNIF)] [HDR] _flowFresnelColor ("_flowFresnelColor", Color) = (1,1,1,1)
        [HideIfDisabled(_FXUNIF)] _flowFresnelPower ("_flowFresnelPower", Float) = 1
        [HideIfDisabled(_FXUNIF)] _flowPosition ("_flowPosition XY(offset) ZW(timeflow)", Vector) = (0,0,0,0)
        [HideIfDisabled(_FXUNIF)] _flowFilp ("_flowFilp (-1 to reverse)", Float) = 1
        [HideIfDisabled(_FXUNIF)] _Ratio_flowColor ("_Ratio_flowColor", Float) = 0

        [HideInInspector] _Cull ("Cull Mode", Float) = 2 // Default to Back
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1 // Default to On
        [HideInInspector] _AlphaToMask ("AlphaToMask", Float) = 0
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        LOD 300

        // Main Forward Lit Pass
        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Keywords from original shader
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _COLOR_MASK
            #pragma shader_feature_local _COLOR_CHANGING
            #pragma shader_feature_local _FXUNIF
            
            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 T           : TEXCOORD1;
                float3 B           : TEXCOORD2;
                float3 N           : TEXCOORD3;
                float3 positionWS   : TEXCOORD4;
                half4 sh            : TEXCOORD5;
                
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                float fogCoord      : TEXCOORD6;
                #endif
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _RoughNessOffset;
                half4 _SpecularColor;
                float _MetallicOffset;
                float _NormalScale;
                float _EmisssionScale;
                half4 _EmisssionColor;
                half _CustomAlpha;
                float _SHScale;
                half4 _SHTopColor;
                half4 _SHBotColor;
                float _Emissive;
                half _Cutoff;
                
                // FX Uniforms
                half _Ratio_SteadyColorEffect;
                half4 _SteadyColor;
                half _Ratio_FlashColorEffect;
                float _DOTEmissionQuency;
                half4 _DOTColor;
                float _DOTFresnelPower;
                float _DOTEmissionMax;
                float _DOTEmissionMin;
                half4 _OverlayColor;
                float _OverlayColorQuency;
                float _OverlayColorFlicker;
                float _DOTEmissionScale;
                half _Ratio_OverlayColorEffect;
                float4 _AddTexST;
                half4 _AddTexColor;
                float _AddTexScale;
                half _Ratio_ExtraTexture;
                half4 _flowColor;
                float4 _flowPosition;
                float _flowFresnelPower;
                half4 _flowFresnelColor;
                half _Ratio_flowColor;
                half _WhiteFresnelAlpha;
                float _flowFilp;
                
                // Properties for merged Rim Effect
                half _Rim;
                float _LineColorPosi;
                float _LineColorSpeed;
                half3 _LineConstColor;
                float _TexorColor;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MixMap);         SAMPLER(sampler_MixMap);
            TEXTURE2D(_AdditionalTexture); SAMPLER(sampler_AdditionalTexture);
            TEXTURE2D(_LineColor);      SAMPLER(sampler_LineColor);

            Varyings Vert(Attributes v) {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float3 tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
                float3 bitangentWS = cross(normalWS, tangentWS) * v.tangentOS.w;

                o.N = normalize(normalWS);
                o.T = normalize(tangentWS);
                o.B = normalize(bitangentWS);
                
                half3 worldNormal = normalize(o.N);
                half yCoeff = worldNormal.y * 0.5 + 0.5;
                o.sh.rgb = lerp(_SHBotColor.rgb, _SHTopColor.rgb, yCoeff) * _SHScale;
                o.sh.a = 1.0;

                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                #endif
                return o;
            }

            half4 Frag(Varyings i) : SV_Target {
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 mixMap = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, i.uv);

                #if _ALPHATEST_ON
                    clip(mainTex.a * _CustomAlpha - _Cutoff);
                #endif

                float3x3 TBN = float3x3(normalize(i.T), normalize(i.B), normalize(i.N));
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv));
                normalTS.xy *= _NormalScale;
                normalTS.z = sqrt(1.0 - saturate(dot(normalTS.xy, normalTS.xy)));
                float3 worldNormal = normalize(mul(TBN, normalTS));
                
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                
                float metallic = saturate(mixMap.r + _MetallicOffset);
                float roughness = saturate(mixMap.g + _RoughNessOffset);
                float occlusion = mixMap.b;
                float emissionMask = mixMap.a;
                
                half3 albedo = mainTex.rgb;
                half alpha = mainTex.a * _CustomAlpha;
                
                half3 specColor = _SpecularColor.rgb;
                half3 diffuseColor = albedo * (1.0 - metallic);
                half3 F0 = lerp(0.04, albedo, metallic);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lightColor = mainLight.color;
                half shadowAtten = mainLight.shadowAttenuation;
                half3 lightDir = mainLight.direction;

                half3 indirectDiffuse = i.sh.rgb * diffuseColor * occlusion;
                
                float3 reflectionDir = reflect(-viewDir, worldNormal);
                float roughnessMIP = roughness * (1 + 7 * roughness);
                half4 indirectSpecTex = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectionDir, roughnessMIP * 6.0);
                half3 indirectSpecular = DecodeHDREnvironment(indirectSpecTex, unity_SpecCube0_HDR);

                float VdotH_refl = saturate(dot(viewDir, reflectionDir));
                float fresnelTerm_refl = pow(1.0 - VdotH_refl, 5.0);
                half3 reflectionBRDF = F0 + (1.0 - F0) * fresnelTerm_refl;
                indirectSpecular *= reflectionBRDF * occlusion;

                half3 halfDir = normalize(lightDir + viewDir);
                float NdotL = saturate(dot(worldNormal, lightDir));
                float NdotH = saturate(dot(worldNormal, halfDir));
                
                float roughness2 = roughness * roughness;
                float d_denominator = (NdotH * NdotH * (roughness2 - 1.0) + 1.0);
                float distribution = roughness2 / (PI * d_denominator * d_denominator);
                
                float LdotH = saturate(dot(lightDir, halfDir));
                half3 fresnel = F0 + (1.0 - F0) * pow(1.0 - LdotH, 5.0);
                
                float k = (roughness + 1.0) * (roughness + 1.0) / 8.0;
                float NdotV = saturate(dot(worldNormal, viewDir));
                float geometry = (NdotL / lerp(NdotL, 1.0, k)) * (NdotV / lerp(NdotV, 1.0, k));
                
                half3 directSpecular = (distribution * fresnel * geometry) / (4.0 * NdotL * NdotV + 0.001) * specColor;
                
                half3 directDiffuse = diffuseColor / PI;
                
                half3 directLighting = (directDiffuse + directSpecular) * NdotL * lightColor * shadowAtten;

                half3 finalColor = indirectDiffuse + indirectSpecular + directLighting;

                #if _Emissive
                    finalColor += albedo * emissionMask * _EmisssionColor.rgb * _EmisssionScale;
                #endif
                
                #if _FXUNIF
                    float NdotV_Fresnel = 1.0 - saturate(dot(worldNormal, viewDir));
                
                    float flicker = sin(_Time.y * _OverlayColorQuency) * _OverlayColorFlicker;
                    float overlayRatio = saturate(_OverlayColor.a + flicker) * _Ratio_OverlayColorEffect;
                    finalColor = lerp(finalColor, _OverlayColor.rgb, overlayRatio);
                
                    float flashPulse = lerp(_DOTEmissionMin, _DOTEmissionMax, (sin(_Time.y * _DOTEmissionQuency) + 1.0) * 0.5);
                    float flashFresnel = pow(NdotV_Fresnel, _DOTFresnelPower);
                    half3 flashColor = _DOTColor.rgb * flashFresnel * flashPulse * _DOTEmissionScale * _Ratio_FlashColorEffect;
                    finalColor += flashColor;

                    float2 flowUV = (i.positionWS.xy * _AddTexST.xy + _AddTexST.zw) + (_Time.x * _flowPosition.zw * _flowFilp);
                    half3 flowTex = SAMPLE_TEXTURE2D(_AdditionalTexture, sampler_AdditionalTexture, flowUV).rgb;
                    float flowFresnel = pow(NdotV_Fresnel, _flowFresnelPower);
                    half3 flowEffect = (flowTex * _flowColor.rgb * _flowColor.a) + (_flowFresnelColor.rgb * flowFresnel);
                    finalColor += flowEffect * _Ratio_flowColor;

                    half3 extraTex = SAMPLE_TEXTURE2D(_AdditionalTexture, sampler_AdditionalTexture, flowUV).rgb * _AddTexColor.rgb;
                    finalColor += extraTex * _AddTexScale * _Ratio_ExtraTexture;
                    
                    float steadyFresnel = NdotV_Fresnel * _SteadyColor.a;
                    half3 steadyEffect = _SteadyColor.rgb * steadyFresnel;
                    finalColor = lerp(finalColor, steadyEffect, _Ratio_SteadyColorEffect);

                    float whiteFresnel = pow(1.0 - saturate(dot(worldNormal, viewDir)), 1.5);
                    finalColor = lerp(finalColor, finalColor * 0.3 + 0.7, whiteFresnel * _WhiteFresnelAlpha);
                    
                    // --- Merged UBuffEffect (Rim Light) ---
                    half rimIntensity = pow(NdotV_Fresnel, _Rim);
                    half2 rimUV = float2(frac(_Time.y * _LineColorSpeed + _LineColorPosi), 0.5);
                    half3 rimTexColor = SAMPLE_TEXTURE2D(_LineColor, sampler_LineColor, rimUV).rgb;
                    half3 rimFinalColor = lerp(_LineConstColor, rimTexColor, _TexorColor);
                    finalColor = lerp(finalColor, rimFinalColor, rimIntensity);
                #endif

                float3 x = finalColor;
                float3 num = (x * (x * 1.1295 + 0.03)) * 0.45;
                float3 den = (x * (x * 1.0935 + 0.59)) * 0.45 + 0.08;
                finalColor = min(num / den, 100.0);
                
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                finalColor = MixFog(finalColor, i.fogCoord);
                #endif

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
        
        // Pass for Outline (formerly UBuffOutLine)
        // This pass runs automatically without a custom renderer feature.
        Pass {
            Name "Outline"

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local _FXUNIF

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                half fresnelSq : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _LineColorPosi;
                float _LineColorSpeed;
                half3 _LineConstColor;
                float _TexorColor;
                float _UBuffWidth;
                float _Offset2;
            CBUFFER_END
            
            TEXTURE2D(_LineColor); SAMPLER(sampler_LineColor);

            Varyings OutlineVert(Attributes v) {
                Varyings o;
                float3 posOS = v.positionOS.xyz + normalize(v.normalOS) * _UBuffWidth;
                float3 posWS = TransformObjectToWorld(posOS);
                o.positionCS = TransformWorldToHClip(posWS);
                o.positionCS.z -= _Offset2 * 0.001 * o.positionCS.w;
                
                float3 viewDir = normalize(GetCameraPositionWS() - posWS);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                half fresnel = 1.0 - saturate(dot(viewDir, -normalWS));
                o.fresnelSq = fresnel * fresnel;
                
                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_Target {
                #if !_FXUNIF
                    return 0;
                #endif

                half2 uv = float2(frac(_Time.y * _LineColorSpeed + _LineColorPosi), 0.5);
                half3 texColor = SAMPLE_TEXTURE2D(_LineColor, sampler_LineColor, uv).rgb;
                half3 finalColor = lerp(_LineConstColor, texColor, _TexorColor);
                
                return half4(finalColor, i.fresnelSq);
            }
            ENDHLSL
        }

        // Shadow Caster Pass
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings ShadowPassVertex(Attributes input) {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                Light mainLight = GetMainLight();
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, mainLight.direction));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET {
                #if _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                    clip(alpha - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // Depth Only Pass
        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings DepthOnlyVertex(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET {
                #if _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                    clip(alpha - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}