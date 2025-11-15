// Converted to Unity URP v17 by Gemini - CORRECTED v4
// Original Shader: ==PGAME==/LobbyPlayer/m_lob_playerpbr_lv3
// Manually implemented missing URP functions and added safeguards to prevent NaN values.

Shader "==PGAME==/LobbyPlayer/m_lob_playerpbr_lv3_URP" {
    Properties {
        [Header(Common)]
        _MainTex ("     混合基础色贴图Diffiuse(RGB)|Roughness(A)", 2D) = "white" { }
        [MaterialToggle(_BUMP_ON)] _BumpOn ("是否使用法线贴图 (Use Normal Map)", Float) = 1
        [HideIfDisabled(_BUMP_ON)] [NoScaleOffset] _BumpMapNr ("     混合法线贴图Normal|Metal (Normal(RGB)|Metallic(A))", 2D) = "bump" { }
        [HideIfDisabled(_BUMP_ON)] _NormalScale ("     法线强度 (Normal Scale)", Range(-4, 4)) = 1
        [MaterialToggle(_BUMP2_ON)] _BumpOn2 ("是否使用法线贴图2 (Use Normal Map 2)", Float) = 0
        [HideIfDisabled(_BUMP2_ON)] _BumpMap2 ("     法线贴图2 (Normal Map 2)", 2D) = "bump" { }
        [HideIfDisabled(_BUMP2_ON)] _Normal2Scale ("     法线2强度 (Normal 2 Scale)", Range(-4, 4)) = 1
        [MaterialToggle(_MASK_ON)] _MaskOn ("是否使用额外遮罩贴图 (Use Mask Map)", Float) = 1
        [HideIfDisabled(_MASK_ON)] _MixMap ("     Alpha(R)|RampMask(G)|Emiss(B)", 2D) = "white" { }
        _RoughNessOffset ("     粗糙度偏移 (Roughness Offset)", Range(-1, 1)) = 0
        [HDR] _SpecularColor ("     高光颜色 (Specular Color)", Color) = (1,1,1,0)
        _MetallicOffset ("     金属度偏移 (Metallic Offset)", Range(-1, 1)) = 0
        [MaterialToggle(_EMISS_ON)] _EmissOn("是否开启自发光 (Enable Emission)", Float) = 0
        [HideIfDisabled(_EmissOn)] [HDR] _EmissColor ("     自发光颜色 (Emission Color)", Color) = (1,1,1,0)
        [HideIfDisabled(_EmissOn)] _Emiss ("     自发光强度 (Emission Strength)", Range(0, 10)) = 1

        [HideIfDisabled(_IFRAMP_ON, _IFMAT_ON)] _E1 ("Matcap和Ramp不兼容 (Matcap & Ramp are incompatible)", Color) = (1,0,0,0)
        [Header(RampTex)]
        [MaterialToggle(_IFRAMP_ON)] _IFRAMP ("是否使用Ramp贴图 (Use Ramp)", Float) = 0
        [HideIfDisabled(_IFRAMP_ON)] _RampTex ("     RampTex", 2D) = "white" { }
        [HideIfDisabled(_IFRAMP_ON)] [HDR] _RampTexColor ("     Ramp强度 (Ramp Color)", Color) = (1,1,1,0)

        [Header(MatTex)]
        [MaterialToggle(_IFMAT_ON)] _IFMAT ("是否使用Matcap贴图 (Use Matcap)", Float) = 0
        [HideIfDisabled(_IFMAT_ON)] [NoScaleOffset] _MatTex ("     MatcapTex", 2D) = "white" { }
        [HideIfDisabled(_IFMAT_ON)] [HDR] _MatTexColor ("     Matcap强度 (Matcap Color)", Color) = (2,2,2,0)
        
        [Header(MatEnv)]
        [MaterialToggle(_MATCAPENV_ON)] _Matcap ("是否使用自定义Matcap环境光 (Use Matcap Environment)", Float) = 0
        [HideIfDisabled(_MATCAPENV_ON)] [NoScaleOffset] _MatcapTexEnv ("     Matcap环境光贴图 (Matcap Env)", 2D) = "black" { }
        [HideIfDisabled(_MATCAPENV_ON)] [HDR] _MatcapColor ("     环境光强度 (Matcap Env Color)", Color) = (4.84,4.84,4.84,0)

        [MaterialToggle(_BAG_ON)] _IsBag ("是否是背包 (Is Bag)", Float) = 0
        [HideIfDisabled(_BAG_ON)] _MainPlayerPositionWS ("Position(RGB)range(A)", Vector) = (0,0,0.1,0)

        [Header(SH)]
        [KeywordEnum(Cla, Lerp)] _SHType ("SH Type", Float) = 1
        _SHScale ("     SH强度 (SH Scale)", Range(0, 10)) = 1
        _SHshadow ("     SH阴影 (SH in shadow)", Range(0, 1)) = 1
        [HDR] _SHTopColor ("     SH顶部颜色 (SH Top Color)", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("     SH底部颜色 (SH Bottom Color)", Color) = (2.5,2.5,2.5,1)

        [Header(Rim Light)]
        _RimLightDir("Rim Light Direction", Vector) = (0.0, 0.0, 1.0, 0.0)
        _RimlightScale("Rim Light Scale 1", Range(-1,1)) = 0.0
        _RimlightScale2("Rim Light Scale 2", Range(-1,1)) = 0.0
        [HDR] _RimlightColor("Rim Light Color", Color) = (1,1,1,1)

        [Header(OutLine)]
        [MaterialToggle(_VTEX_ON)] _VTEX ("是否使用贴图控制顶点色 (Use Texture for Outline)", Float) = 0
        [HideIfDisabled(_VTEX_ON)] _OutlineMap ("OutlineMap", 2D) = "white" { }
        [MaterialToggle(_VCOLOR2N_ON)] _VCOLOR2N ("是否使用顶点色勾边优化 (Use Vertex Color for Outline)", Float) = 0
        _OutlineColor ("OutlineColor", Color) = (0.5,0.5,0.5,1)
        _Offset ("Z Offset", Float) = -5
        _lightDir ("lightDirtion", Vector) = (9.48,3.68,0,0)
        
        [Header(Debug)]
        [KeywordEnum(OFF, Deffiuse, Normal, Metal, Roughness, AO, Emiss, SH)] _Debug ("Debug模式 (Debug Mode)", Float) = 0
        
        [HideIfDisabled(_ALPHATEST_ON)] _Cutoff ("Masked混合模式裁切值 (Alpha Clip Threshold)", Range(0, 1)) = 0.5

        [HideInInspector] _RenderType ("RenderType", Float) = 0 // Opaque=0, Transparent=1
        [HideInInspector] _Cull ("Cull Mode", Float) = 2 // Off=0, Front=1, Back=2
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _ZTest ("ZTest", Float) = 4 // LEqual
        [HideInInspector] _AlphaToMask ("AlphaToMask", Float) = 0
        [HideInInspector] _SrcBlendFactor ("SrcBlend", Float) = 1 // One
        [HideInInspector] _DstBlendFactor ("DstBlend", Float) = 0 // Zero
        [HideInInspector] _BlendMode ("Blend Mode", Float) = 0 // 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply
    }
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Blend [_SrcBlendFactor] [_DstBlendFactor]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            #pragma shader_feature_local _BUMP_ON
            #pragma shader_feature_local _BUMP2_ON
            #pragma shader_feature_local _MASK_ON
            #pragma shader_feature_local _EMISS_ON
            #pragma shader_feature_local _IFRAMP_ON
            #pragma shader_feature_local _IFMAT_ON
            #pragma shader_feature_local _MATCAPENV_ON
            #pragma shader_feature_local _BAG_ON
            #pragma shader_feature_local _ALPHATEST_ON
            
            #pragma multi_compile_local _SHTYPE_CLA _SHTYPE_LERP
            #pragma multi_compile_local _DEBUG_OFF _DEBUG_DEFFIUSE _DEBUG_NORMAL _DEBUG_METAL _DEBUG_ROUGHNESS _DEBUG_AO _DEBUG_EMISS _DEBUG_SH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap2_ST;
                float _NormalScale;
                float _Normal2Scale;
                float _RoughNessOffset;
                float4 _SpecularColor;
                float _MetallicOffset;
                float _Emiss;
                float4 _EmissColor;
                float _SHScale;
                float4 _SHTopColor;
                float4 _SHBotColor;
                float _SHshadow;
                float3 _RimLightDir;
                float _RimlightScale;
                float _RimlightScale2;
                float4 _RimlightColor;
                float _Cutoff;
                float4 _MainPlayerPositionWS;
                float4 _RampTex_ST;
                float4 _RampTexColor;
                float4 _MatTexColor;
                float4 _MatcapColor;
                float _BlendMode;
            CBUFFER_END
            
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMapNr); SAMPLER(sampler_BumpMapNr);
            TEXTURE2D(_BumpMap2); SAMPLER(sampler_BumpMap2);
            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);
            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);
            TEXTURE2D(_MatTex); SAMPLER(sampler_MatTex);
            TEXTURE2D(_MatcapTexEnv); SAMPLER(sampler_MatcapTexEnv);

            struct Attributes {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
                #if defined(_BUMP2_ON)
                float2 uv2          : TEXCOORD5;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float3 SafeNormalizeOutline(float3 inVec)
            {
                float dp3 = dot(inVec, inVec);
                if (dp3 > 1e-6f)
                {
                    return rsqrt(dp3) * inVec;
                }
                return float3(0.0, 0.0, 1.0);
            }
            
            half3 TonemapFilmic(half3 color)
            {
                half3 numerator = color * (color * 1.1295 + 0.03) * 0.45;
                half3 denominator = (color * 0.45) * (color * 1.0935 + 0.59) + 0.08;
                return numerator / max(denominator, 1e-6);
            }

            Varyings vert(Attributes input) {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if defined(_BAG_ON)
                    float3 pos_offset = _MainPlayerPositionWS.xyz - input.positionOS.xyz;
                    float dist = length(pos_offset);
                    float falloff = 1.0 - saturate(dist / max(_MainPlayerPositionWS.w, 1e-6));
                    falloff *= falloff;
                    float scale = falloff * _MainPlayerPositionWS.w + 1.0;
                    input.positionOS.xyz *= scale;
                #endif
                
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;
                
                #if defined(_BUMP2_ON)
                output.uv2 = TRANSFORM_TEX(input.uv, _BumpMap2);
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedoAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 albedo = albedoAlpha.rgb;

                #if defined(_MASK_ON)
                    half4 mixMap = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);
                    #if defined(_ALPHATEST_ON)
                        clip(mixMap.r - _Cutoff);
                    #endif
                    if (_BlendMode != 0.0) {
                        albedoAlpha.a = mixMap.r;
                    }
                #else
                    #if defined(_ALPHATEST_ON)
                        clip(albedoAlpha.a - _Cutoff);
                    #endif
                #endif

                half roughness = albedoAlpha.a + _RoughNessOffset;
                half metallic = 0.0;
                half3 normalTS = float3(0.5, 0.5, 1);

                #if defined(_BUMP_ON)
                    half4 packedNormalMetallic = SAMPLE_TEXTURE2D(_BumpMapNr, sampler_BumpMapNr, input.uv);
                    normalTS = UnpackNormalScale(packedNormalMetallic, _NormalScale);
                    metallic = packedNormalMetallic.a + _MetallicOffset;
                #endif

                #if defined(_BUMP2_ON)
                    half3 normal2TS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap2, sampler_BumpMap2, input.uv2), _Normal2Scale);
                    normalTS = BlendNormals(normalTS, normal2TS);
                #endif
                
                float3x3 tbn = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = TransformTangentToWorld(normalTS, tbn);
                normalWS = normalize(normalWS);
                
                metallic = saturate(metallic);
                roughness = saturate(roughness);
                half occlusion = 1.0; 
                half3 emission = 0;

                #if defined(_EMISS_ON)
                    emission = _EmissColor.rgb * _Emiss;
                    #if defined(_MASK_ON)
                        emission *= mixMap.b;
                    #endif
                    emission *= albedo;
                #endif
                
                float3 viewDirWS = GetWorldSpaceViewDir(input.positionWS);
                
                BRDFData brdfData;
                brdfData = (BRDFData)0;
                half oneMinusMetallic = 1.0h - metallic;
                brdfData.diffuse = albedo * oneMinusMetallic;
                brdfData.specular = lerp(kDielectricSpec.rgb, albedo, metallic);
                brdfData.grazingTerm = saturate(roughness + metallic);
                brdfData.perceptualRoughness = roughness;
                brdfData.roughness = max(0.002, roughness * roughness);
                
                Light mainLight = GetMainLight();
                float3 totalLight = float3(0,0,0);
                
                totalLight += LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDirWS);

                #ifdef _ADDITIONAL_LIGHTS
                    int additionalLightsCount = GetAdditionalLightsCount();
                    for (int i = 0; i < additionalLightsCount; ++i) {
                        Light additionalLight = GetAdditionalLight(i, input.positionWS);
                        totalLight += LightingPhysicallyBased(brdfData, additionalLight, normalWS, viewDirWS);
                    }
                #endif
                
                half3 ambient = 0;
                #if defined(_SHTYPE_CLA)
                     ambient = SampleSH(normalWS);
                #elif defined(_SHTYPE_LERP)
                    half shLerp = normalWS.y * 0.5 + 0.5;
                    ambient = lerp(_SHBotColor.rgb, _SHTopColor.rgb, shLerp);
                #endif
                
                ambient *= _SHScale * (_SHshadow + 1.0);
                ambient *= brdfData.diffuse;
                totalLight += ambient;

                float3 rimDir = SafeNormalize(_RimLightDir);
                float rimDot = 1.0 - saturate(dot(viewDirWS, normalWS));
                float rimIntensity = smoothstep(_RimlightScale, _RimlightScale2, rimDot);
                half3 rimColor = _RimlightColor.rgb * _RimlightColor.a * rimIntensity;
                totalLight += rimColor * albedo;
                
                half3 finalColor = totalLight + emission;
                
                #if defined(_IFRAMP_ON)
                    float rampCoord = 1.0 - saturate(dot(viewDirWS, normalWS));
                    half4 rampColor = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(rampCoord, 0.5));
                    half3 rampContribution = rampColor.rgb * _RampTexColor.rgb;
                    #if defined(_MASK_ON)
                        rampContribution *= mixMap.g;
                    #endif
                    finalColor += rampContribution;
                #endif

                #if defined(_IFMAT_ON)
                    float3 worldCamUp = GetCameraUp();
                    float3 worldCamRight = GetCameraRight();
                    float3 matcapNormal = mul((float3x3)UNITY_MATRIX_V, normalWS);
                    float2 matcapUV = matcapNormal.xy * 0.5 + 0.5;
                    half3 matcapColor = SAMPLE_TEXTURE2D(_MatTex, sampler_MatTex, matcapUV).rgb * _MatTexColor.rgb;
                     #if defined(_MASK_ON)
                        matcapColor *= mixMap.g;
                    #endif
                    finalColor += matcapColor;
                #endif
                
                // Apply original filmic tonemapping
                finalColor = TonemapFilmic(finalColor);
                
                #if defined(_DEBUG_DEFFIUSE)
                    finalColor = albedo;
                #elif defined(_DEBUG_NORMAL)
                    finalColor = normalWS * 0.5 + 0.5;
                #elif defined(_DEBUG_METAL)
                    finalColor = metallic;
                #elif defined(_DEBUG_ROUGHNESS)
                    finalColor = roughness;
                #elif defined(_DEBUG_AO)
                    finalColor = occlusion;
                #elif defined(_DEBUG_EMISS)
                    finalColor = emission;
                #elif defined(_DEBUG_SH)
                    finalColor = ambient;
                #endif

                return half4(finalColor, albedoAlpha.a);
            }
            ENDHLSL
        }

        // 2. ShadowCaster Pass
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _BlendMode;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowPassVertex(Attributes input) {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #ifdef _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                    #ifdef _MASK_ON
                        if(_BlendMode != 0.0) {
                           alpha = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv).r;
                        }
                    #endif
                    clip(alpha - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // 3. DepthOnly Pass
        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _BlendMode;
            CBUFFER_END
            
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);

             struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings DepthOnlyVertex(Attributes input) {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET {
                 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                 #ifdef _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                    #ifdef _MASK_ON
                         if(_BlendMode != 0.0) {
                           alpha = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv).r;
                        }
                    #endif
                    clip(alpha - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
        
        // 4. Outline Pass
        Pass {
            Name "OUTLINE"
            Tags { "LightMode" = "SRPDefaultUnlit" } 
            
            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline

            #pragma shader_feature_local _VTEX_ON
            #pragma shader_feature_local _VCOLOR2N_ON
            #pragma shader_feature_local _BAG_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _Offset;
                float4 _lightDir;
                float4 _MainPlayerPositionWS;
            CBUFFER_END
            
            TEXTURE2D(_OutlineMap); SAMPLER(sampler_OutlineMap);

            struct Attributes {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionHCS  : SV_POSITION;
                float4 color        : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertOutline(Attributes input) {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normal = input.normalOS;

                #if defined(_VCOLOR2N_ON)
                    float3 bitangent = cross(normal, input.tangentOS.xyz) * input.tangentOS.w;
                    float3 modifiedNormal = float3(0,0,0);
                    modifiedNormal += input.tangentOS.xyz * (input.color.r * 2 - 1);
                    modifiedNormal += bitangent * (input.color.g * 2 - 1);
                    modifiedNormal += normal * (input.color.b * 2 - 1);
                    normal = normalize(modifiedNormal);
                #endif
                
                float offset = _Offset * 0.001;

                #if defined(_VTEX_ON)
                    offset *= SAMPLE_TEXTURE2D_LOD(_OutlineMap, sampler_OutlineMap, input.uv, 0).w;
                #endif
                
                #if defined(_BAG_ON)
                     float3 pos_offset = _MainPlayerPositionWS.xyz - input.positionOS.xyz;
                    float dist = length(pos_offset);
                    float falloff = 1.0 - saturate(dist / max(_MainPlayerPositionWS.w, 1e-6));
                    falloff *= falloff;
                    float scale = falloff * _MainPlayerPositionWS.w + 1.0;
                    input.positionOS.xyz *= scale;
                #endif

                float3 pos = input.positionOS.xyz + normal * offset;

                output.positionHCS = TransformObjectToHClip(pos);
                
                float3 normalWS = normalize(TransformObjectToWorldNormal(normal));
                float3 lightDir = SafeNormalize(_lightDir.xyz);
                float nDotL = saturate(dot(normalWS, lightDir));
                
                float3 finalOutlineColor = lerp(_OutlineColor.rgb, float3(1,1,1), nDotL);
                output.color = float4(finalOutlineColor * input.color.rgb, _OutlineColor.a);
                
                return output;
            }

            half4 fragOutline(Varyings input) : SV_TARGET {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.color;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}