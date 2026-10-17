Shader "==PGAME==/LobbyPlayer/m_lob_playerpbr_lv3"
{
    Properties
    {
        [Header(Common)]
        [MainTexture] _MainTex ("Diffuse (RGB) Roughness (A)", 2D) = "white" {}
        [Toggle(_BUMP_ON)] _BumpOn ("Use Packed Normal", Float) = 1
        [NoScaleOffset] _BumpMapNr ("Normal (RGB) Metallic (A) - Linear Texture", 2D) = "bump" {}
        [Enum(LinearRGBA,0,UnityNormalMap,1,SRGBRGBA,2)] _PackedNormalEncoding ("Packed Normal Encoding", Float) = 0
        _NormalScale ("Normal Strength", Range(-4,4)) = 1
        [Toggle(_BUMP2_ON)] _BumpOn2 ("Use Detail Normal", Float) = 0
        _BumpMap2 ("Detail Normal (RGB) - Linear Texture", 2D) = "bump" {}
        _Normal2Scale ("Detail Normal Strength", Range(-4,4)) = 1
        [Enum(LinearRGBA,0,UnityNormalMap,1,SRGBRGBA,2)] _DetailNormalEncoding ("Detail Normal Encoding", Float) = 0
        [Toggle(_Mask_ON)] _MaskOn ("Use Mask Texture", Float) = 1
        _MixMap ("Alpha (R) Ramp or Matcap Mask (G) Emission (B) Reserved (A)", 2D) = "white" {}
        _RoughNessOffset ("Roughness Offset", Range(-1,1)) = 0
        [Toggle] _RoughnessAlphaSquared ("Legacy Squared Roughness Alpha", Float) = 0
        _MetallicOffset ("Metallic Offset", Range(-1,1)) = 0
        [HDR] _SpecularColor ("Specular Color", Color) = (1,1,1,0)
        [Toggle(_EMISS_ON)] _Emiss ("Emission Strength", Float) = 0
        [HDR] _EmissColor ("Emission Color", Color) = (1,1,1,0)

        [Header(Ramp and Matcap)]
        [Toggle(_IFRAMP_ON)] _IFRAMP ("Use Ramp (Takes Priority over Matcap)", Float) = 0
        _RampTex ("View Angle Ramp", 2D) = "white" {}
        [HDR] _RampTexColor ("Ramp Color", Color) = (1,1,1,0)
        [Toggle(_IFMAT_ON)] _IFMAT ("Use Matcap Highlight", Float) = 0
        [NoScaleOffset] _MatTex ("Matcap Highlight", 2D) = "white" {}
        [HDR] _MatTexColor ("Matcap Highlight Color", Color) = (2,2,2,0)
        [Toggle(_MATCAPENV_ON)] _Matcap ("Use Matcap Environment", Float) = 0
        [NoScaleOffset] _MatcapTex ("Matcap Environment (Requires Mipmaps)", 2D) = "black" {}
        [HDR] _MatcapColor ("Matcap Environment Color", Color) = (4.84,4.84,4.84,0)

        [Header(Bag Deformation)]
        [Toggle(_BAG_ON)] _IsBag ("Enable Bag Deformation", Float) = 0
        _MainPlayerPositionWS ("Bag Center in Object Space (XYZ) Radius (W)", Vector) = (0,0,0.1,0)

        [Header(Ambient Lighting)]
        [KeywordEnum(Cla, Lerp)] _SHType ("SH Type", Float) = 1
        _SHScale ("SH Scale", Range(0,10)) = 1
        _SHshadow ("SH in Shadow", Range(0,1)) = 1
        [HDR] _SHTopColor ("SH Top Color", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("SH Bottom Color", Color) = (2.5,2.5,2.5,1)
        _SHColorScale ("SH Color Scale", Color) = (1,1,1,1)

        [Header(Rim Light)]
        _RimLightDir ("Rim Light Direction", Vector) = (0,0,1,0)
        _RimlightScale ("Rim Start", Float) = 1
        _RimlightScale2 ("Rim End", Float) = 1
        _RimlightShadowScale ("Rim in Shadow", Range(0,1)) = 0
        [HDR] _RimlightColor ("Rim Color (A = Ignore Albedo)", Color) = (1,1,1,1)

        [Header(Outline)]
        [Toggle] _OutlineEnabled ("Enable Outline", Float) = 1
        [Toggle(_VTEX_ON)] _VTEX ("Use Outline Texture", Float) = 0
        _OutlineMap ("Outline Color (RGB) Width (A)", 2D) = "white" {}
        [Toggle(_VCOLOR2N_ON)] _VCOLOR2N ("Vertex Color Encodes Smooth Outline Normal", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0.5,0.5,0.5,1)
        _Offset ("Outline Depth Offset", Float) = -5
        _lightDir ("Outline Light Direction", Vector) = (9.48,3.68,0,0)
        _ScreenResolution ("Outline Reference Resolution", Vector) = (1920,1080,0,0)

        [Header(Debug)]
        [KeywordEnum(OFF, Deffiuse, Normal, Metal, Roughness, AO, Emiss, SH)] _Debug ("Texture Debug", Float) = 0

        [Header(Render State)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip (Uses Mask R, Not Roughness)", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(Opaque,0,Transparent,1)] _BlendMode ("Alpha Output Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("Depth Write", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        [Toggle] _AlphaToMask ("Alpha to Coverage", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendFactor ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendFactor ("Destination Blend", Float) = 0
        [HideInInspector] _SrcBlendFactorAlpha ("Legacy Source Alpha Blend", Float) = 5
        [HideInInspector] _DstBlendFactorAlpha ("Legacy Destination Alpha Blend", Float) = 10
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Operation", Float) = 0
        [HideInInspector] _BlendOpAlpha ("Legacy Alpha Blend Operation", Float) = 0
        [HideInInspector] _RenderType ("Legacy Render Type", Float) = 0
        [HideInInspector] _E1 ("Legacy Ramp and Matcap Warning", Color) = (1,0,0,0)

        // Neutral-by-default extensions retained from the previous reimplementation.
        [Header(Legacy Lighting Controls)]
        _ShadowScale ("Minimum Diffuse Lighting", Range(0,1)) = 0
        _ShadowOffset ("Diffuse Lighting Offset", Float) = 0
        _ShadowPow ("Diffuse Lighting Power", Float) = 1
        _AddLightDir ("Extra Light Direction", Vector) = (0,1,0,0)
        [HDR] _AddlightColor ("Extra Light Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        // Identical layout in every pass keeps the shader SRP Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _BumpMap2_ST, _RampTex_ST;
            float4 _SpecularColor, _EmissColor, _RampTexColor, _MatTexColor, _MatcapColor;
            float4 _MainPlayerPositionWS, _SHTopColor, _SHBotColor, _SHColorScale;
            float4 _RimLightDir, _RimlightColor, _OutlineColor, _lightDir, _ScreenResolution;
            float4 _AddLightDir, _AddlightColor, _E1;
            float _BumpOn, _NormalScale, _BumpOn2, _Normal2Scale;
            float _MaskOn, _RoughNessOffset, _MetallicOffset, _Emiss;
            float _IFRAMP, _IFMAT, _Matcap, _IsBag;
            float _SHType, _SHScale, _SHshadow;
            float _RimlightScale, _RimlightScale2, _RimlightShadowScale;
            float _OutlineEnabled, _VTEX, _VCOLOR2N, _Offset, _Debug;
            float _AlphaClip, _Cutoff, _BlendMode, _Cull, _ZWrite, _ZTest, _AlphaToMask;
            float _SrcBlendFactor, _DstBlendFactor, _SrcBlendFactorAlpha, _DstBlendFactorAlpha;
            float _BlendOp, _BlendOpAlpha, _RenderType;
            float _ShadowScale, _ShadowOffset, _ShadowPow;
            float _PackedNormalEncoding, _DetailNormalEncoding, _RoughnessAlphaSquared;
        CBUFFER_END

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMapNr); SAMPLER(sampler_BumpMapNr);
        TEXTURE2D(_BumpMap2); SAMPLER(sampler_BumpMap2);
        TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);
        TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);
        TEXTURE2D(_MatTex); SAMPLER(sampler_MatTex);
        TEXTURE2D(_MatcapTex); SAMPLER(sampler_MatcapTex);
        TEXTURE2D(_OutlineMap); SAMPLER(sampler_OutlineMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float4 tangentWS : TEXCOORD3;
            half fogFactor : TEXCOORD4;
            half3 vertexLighting : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 DeformBag(float3 positionOS)
        {
            // Despite its WS suffix, the dump subtracts this center from POSITION0.
            float radius = _MainPlayerPositionWS.w;
            if (_IsBag > 0.5 && radius > 0.0001)
            {
                float weight = 1.0 - saturate(distance(positionOS, _MainPlayerPositionWS.xyz) / radius);
                positionOS *= 1.0 + radius * weight * weight;
            }
            return positionOS;
        }

        half4 SampleMask(float2 uv)
        {
            half4 mask = half4(1,1,1,1);
            if (_MaskOn > 0.5)
                mask = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, uv);
            return mask;
        }

        half SurfaceAlpha(half4 mask)
        {
            // Clipping is independent of the opaque/transparent output alpha.
            #if defined(_ALPHATEST_ON)
                clip(mask.r - _Cutoff);
            #else
                if (_AlphaClip > 0.5)
                    clip(mask.r - _Cutoff);
            #endif
            return _BlendMode == 0.0 ? 1.0h : mask.r;
        }

        float3 SurfaceNormal(Varyings input, out half metallic)
        {
            float3 normalTS = float3(0,0,1);
            metallic = saturate(_MetallicOffset);
            if (_BumpOn > 0.5)
            {
                half4 packed = SAMPLE_TEXTURE2D(_BumpMapNr, sampler_BumpMapNr, input.uv.xy);
                if (_PackedNormalEncoding > 0.5 && _PackedNormalEncoding < 1.5)
                {
                    normalTS = UnpackNormalScale(packed, _NormalScale);
                    // Normal-map compression can replace alpha; use the scalar fallback.
                    metallic = saturate(_MetallicOffset);
                }
                else
                {
                    float3 rgb = _PackedNormalEncoding > 1.5 ? LinearToSRGB(packed.rgb) : packed.rgb;
                    normalTS = rgb * 2.0 - 1.0;
                    normalTS.xy *= _NormalScale;
                    metallic = saturate(packed.a + _MetallicOffset);
                }
            }
            if (_BumpOn2 > 0.5)
            {
                half4 packedDetail = SAMPLE_TEXTURE2D(_BumpMap2, sampler_BumpMap2, input.uv.zw);
                float3 detail;
                if (_DetailNormalEncoding > 0.5 && _DetailNormalEncoding < 1.5)
                    detail = UnpackNormalScale(packedDetail, _Normal2Scale);
                else
                {
                    float3 rgb = _DetailNormalEncoding > 1.5 ? LinearToSRGB(packedDetail.rgb) : packedDetail.rgb;
                    detail = rgb * 2.0 - 1.0;
                    detail.xy *= _Normal2Scale;
                }
                // Reoriented normal mapping, matching the _BUMP2_ON variant.
                float3 baseNormal = normalTS + float3(0,0,1);
                detail *= float3(-1,-1,1);
                normalTS = baseNormal * dot(baseNormal, detail) / max(baseNormal.z, 0.0001) - detail;
            }
            float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
            float3 mapped = normalTS.x * input.tangentWS.xyz + normalTS.y * bitangentWS + normalTS.z * input.normalWS;
            return dot(mapped, mapped) > 1e-8 ? SafeNormalize(mapped) : SafeNormalize(input.normalWS);
        }

        float3 LobbyAmbient(float3 normalWS)
        {
            if (_SHType > 0.5)
            {
                // The LERP variant is a quadratic hemisphere, not a linear lerp.
                float2 weights = max(0.125 * float2((normalWS.y + 1.0) * (normalWS.y + 1.0),
                                                  (normalWS.y - 1.0) * (normalWS.y - 1.0)), 0.0);
                return (weights.x * _SHTopColor.rgb + weights.y * _SHBotColor.rgb) * _SHScale * _SHColorScale.rgb;
            }
            // Fixed lobby SH coefficients recovered from the _SHTYPE_CLA program.
            float4 n = float4(normalWS, 1);
            const float4x4 red = float4x4(
                0.029173,-0.001093,0.001014,0.000121,
                -0.001093,-0.029173,-0.027876,0.034602,
                0.001014,-0.027876,0.039470,-0.018654,
                0.000121,0.034602,-0.018654,0.219935);
            const float4x4 green = float4x4(
                0.045281,-0.000962,0.000811,-0.000138,
                -0.000962,-0.045281,-0.021259,0.105730,
                0.000811,-0.021259,-0.011407,-0.000754,
                -0.000138,0.105730,-0.000754,0.397244);
            const float4x4 blue = float4x4(
                0.032648,-0.000876,0.000726,-0.000198,
                -0.000876,-0.032648,-0.013036,0.163064,
                0.000726,-0.013036,-0.038254,0.011892,
                -0.000198,0.163064,0.011892,0.530796);
            return max(float3(dot(n, mul(red,n)), dot(n, mul(green,n)), dot(n, mul(blue,n))), 0.0) * _SHScale * _SHColorScale.rgb;
        }

        float2 MatcapUV(float3 normalWS, float3 viewDirWS)
        {
            float3 cameraUp = UNITY_MATRIX_I_V._m01_m11_m21;
            float3 right = cross(cameraUp, -viewDirWS);
            if (dot(right,right) < 1e-8)
                right = UNITY_MATRIX_I_V._m00_m10_m20;
            right = SafeNormalize(right);
            float3 up = SafeNormalize(cross(-viewDirWS, right));
            float3 n = SafeNormalize(float3(dot(right,normalWS), dot(up,normalWS), dot(-viewDirWS,normalWS)));
            return n.xy * 0.5 + 0.5;
        }

        float3 LobbyToneMap(float3 color)
        {
            color = max(color, 0.0);
            // Both 0.45 factors and the 1.0935 coefficient occur in the dump.
            float3 scaled = color * 0.45;
            return min(scaled * (color * 1.1295 + 0.03) /
                       (scaled * (color * 1.0935 + 0.59) + 0.08), 100.0);
        }

        Varyings LobbyVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(DeformBag(input.positionOS.xyz));
            VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = normal.normalWS;
            output.tangentWS = float4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            output.uv.xy = TRANSFORM_TEX(input.uv, _MainTex);
            output.uv.zw = TRANSFORM_TEX(input.uv, _BumpMap2);
            output.fogFactor = ComputeFogFactor(position.positionCS.z);
            output.vertexLighting = VertexLighting(position.positionWS, normal.normalWS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_SrcBlendFactor] [_DstBlendFactor], One OneMinusSrcAlpha
            BlendOp [_BlendOp], Add
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LobbyVertex
            #pragma fragment LobbyFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            half4 LobbyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 mask = SampleMask(input.uv.xy);
                half alpha = SurfaceAlpha(mask);
                half4 albedoRoughness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
                half metallic;
                float3 normalWS = SurfaceNormal(input, metallic);
                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float roughnessAlpha = _RoughnessAlphaSquared > 0.5 ? albedoRoughness.a * albedoRoughness.a : albedoRoughness.a;
                float roughness = clamp(roughnessAlpha + _RoughNessOffset, 0.04, 1.0);
                float roughness2 = roughness * roughness;
                float ndotv = min(abs(dot(normalWS, viewDirWS)) + 0.004, 1.0);
                float3 diffuse = albedoRoughness.rgb * (1.0 - metallic);
                float3 specular = lerp(float3(0.04,0.04,0.04), albedoRoughness.rgb, metallic);

                float4 shadowCoord;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    // Per-fragment selection is necessary for cascade boundaries.
                    shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float shadow = mainLight.shadowAttenuation;
                float3 ambient = LobbyAmbient(normalWS) * saturate(shadow + _SHshadow);
                float3 color = diffuse * ambient;

                // The original highlight uses N.V, not N.H (deliberate lobby style).
                float denominator = (ndotv * roughness2) * (ndotv * roughness2) + 1.0 - ndotv * ndotv;
                float distribution = roughness2 / max(denominator, 1e-8);
                distribution = min(distribution * distribution * (roughness * 0.25 + 0.25), 100.0);
                float3 directSpecular = distribution * specular * ndotl;
                float diffuseLight = max(saturate(pow(max(ndotl,1e-8), max(_ShadowPow,0.0001)) + _ShadowOffset), _ShadowScale);
                // A matcap environment replaces BOTH the direct specular and probe.
                float3 mainBRDF = diffuse * diffuseLight;
                if (_Matcap < 0.5)
                    mainBRDF += directSpecular * _SpecularColor.rgb;
                color += mainBRDF * mainLight.color * (mainLight.distanceAttenuation * shadow);

                float2 matcapUV = MatcapUV(normalWS, viewDirWS);
                float mip = roughness * (1.7 - 0.7 * roughness) * 6.0;
                float matcapLight = ndotl * 0.75 + 0.25;
                if (_Matcap > 0.5)
                {
                    color += SAMPLE_TEXTURE2D_LOD(_MatcapTex, sampler_MatcapTex, matcapUV, mip).rgb *
                             _MatcapColor.rgb * matcapLight * specular;
                }
                else
                {
                    // Legacy probe mip range is six; do not substitute URP's BRDF.
                    half4 probe = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0,
                                                        reflect(-viewDirWS, normalWS), mip);
                    float3 environment = DecodeHDREnvironment(probe, unity_SpecCube0_HDR);
                    color += environment * specular / (roughness2 * roughness2 + 1.0);
                }

                float grazing = 1.0 - ndotv;
                float rimK = (roughness + 1.0) * (roughness + 1.0) * 0.125;
                float rim = grazing / max(grazing * (1.0 - rimK) + rimK, 1e-8);
                rim *= saturate(dot(SafeNormalize(_RimLightDir.xyz), normalWS));
                float rimRange = _RimlightScale2 - _RimlightScale;
                // Preserve reversed ramps; equal endpoints become a finite step.
                rim = abs(rimRange) > 1e-5 ? saturate((rim - _RimlightScale) / rimRange) : step(_RimlightScale, rim);
                rim = rim * rim * (3.0 - 2.0 * rim);
                rim *= lerp(shadow, 1.0, _RimlightShadowScale);
                float3 rimColor = lerp(albedoRoughness.rgb * _RimlightColor.rgb, _RimlightColor.rgb, _RimlightColor.a);
                color += clamp(rimColor * rim, 0.0, 100.0);
                float3 emission = albedoRoughness.rgb * mask.b * _EmissColor.rgb * _Emiss;
                color += emission;

                if (_IFRAMP > 0.5)
                {
                    float2 rampUV = float2(min(ndotv * ndotv, 0.96) * _RampTex_ST.x + _RampTex_ST.z, 0.0);
                    color += SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, rampUV).rgb * _RampTexColor.rgb * mask.g * matcapLight;
                }
                else if (_IFMAT > 0.5)
                {
                    color += SAMPLE_TEXTURE2D(_MatTex, sampler_MatTex, matcapUV).rgb * _MatTexColor.rgb * mask.g * matcapLight;
                }

                color += diffuse * saturate(dot(normalWS, SafeNormalize(_AddLightDir.xyz))) * _AddlightColor.rgb;
                // The dump reuses the main-light specular term for additional lights.
                float3 additionalBRDF = diffuse;
                if (_Matcap < 0.5)
                    additionalBRDF += clamp(directSpecular - 0.00006103515625, 0.0, 100.0);
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                            color += additionalBRDF * light.color * saturate(dot(normalWS,light.direction)) *
                                     (light.distanceAttenuation * light.shadowAttenuation);
                        }
                    #endif
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        color += additionalBRDF * light.color * saturate(dot(normalWS,light.direction)) *
                                 (light.distanceAttenuation * light.shadowAttenuation);
                    LIGHT_LOOP_END
                #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
                    color += additionalBRDF * input.vertexLighting;
                #endif

                // Missing debug variants are reconstructed from their property names.
                if (_Debug > 0.5)
                {
                    if (_Debug < 1.5) return half4(albedoRoughness.rgb, alpha);
                    if (_Debug < 2.5) return half4(normalWS * 0.5 + 0.5, alpha);
                    if (_Debug < 3.5) return half4(metallic.xxx, alpha);
                    if (_Debug < 4.5) return half4(roughness.xxx, alpha);
                    if (_Debug < 5.5) return half4(1,1,1,alpha); // No AO channel in this shader.
                    if (_Debug < 6.5) return half4(emission, alpha);
                    return half4(ambient, alpha);
                }
                return half4(MixFog(LobbyToneMap(color), input.fogFactor), alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="TrainerOutline" }
            Cull Front
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_SrcBlendFactor] [_DstBlendFactor], One OneMinusSrcAlpha
            BlendOp [_BlendOp], Add
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 color : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            OutlineVaryings OutlineVertex(Attributes input)
            {
                OutlineVaryings output = (OutlineVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(DeformBag(input.positionOS.xyz));
                float3 normalOS = input.normalOS;
                if (_VCOLOR2N > 0.5)
                {
                    float3 encoded = input.color.rgb * 2.0 - 1.0;
                    float3 bitangentOS = cross(input.normalOS,input.tangentOS.xyz) * input.tangentOS.w * GetOddNegativeScale();
                    normalOS = input.tangentOS.xyz * encoded.x + bitangentOS * encoded.y + input.normalOS * encoded.z;
                }
                half4 outlineMask = half4(input.color.rgb,1);
                if (_VTEX > 0.5)
                    outlineMask = SAMPLE_TEXTURE2D_LOD(_OutlineMap,sampler_OutlineMap,input.uv,0);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float lighting = saturate(dot(normalWS,SafeNormalize(_lightDir.xyz)));
                output.color = (_OutlineColor.rgb + lighting * (1.0 - _OutlineColor.rgb)) * outlineMask.rgb;

                // Reconstruct the dump's constant-screen-width extrusion and ray depth bias.
                float4 positionCS = TransformWorldToHClip(positionWS);
                float2 directionCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldDir(normalOS,false)).xy;
                directionCS *= rsqrt(max(dot(directionCS,directionCS),1e-8));
                float referenceHeight = _ScreenResolution.y > 0.0 ? _ScreenResolution.y : 1080.0;
                float2 offsetCS = directionCS / _ScreenParams.xy * referenceHeight * positionCS.w *
                                  (1.0 / 216.0) * outlineMask.a;
                float2 offsetVS = offsetCS / float2(UNITY_MATRIX_P._m00,UNITY_MATRIX_P._m11);
                float3 positionVS = TransformWorldToView(positionWS);
                positionVS.xy += offsetVS;
                float depthBias = max(length(offsetVS),0.001) * abs(_Offset) * outlineMask.a * 15.0;
                positionVS += positionVS / max(abs(positionVS.z),0.0001) * depthBias;
                output.positionCS = mul(UNITY_MATRIX_P,float4(positionVS,1));
                output.uv = TRANSFORM_TEX(input.uv,_MainTex);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 OutlineFragment(OutlineVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_OutlineEnabled - 0.5);
                half alpha = SurfaceAlpha(SampleMask(input.uv));
                return half4(MixFog(max(input.color,0.0),input.fogFactor),alpha);
            }
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
            #pragma vertex LobbyShadowVertex
            #pragma fragment LobbyShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings LobbyShadowVertex(Attributes input)
            {
                Varyings output = LobbyVertex(input);
                float3 direction = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    direction = SafeNormalize(_LightPosition - output.positionWS);
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            half4 LobbyShadowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SurfaceAlpha(SampleMask(input.uv.xy));
                return 0;
            }
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
            #pragma vertex LobbyVertex
            #pragma fragment LobbyDepthFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON

            half LobbyDepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceAlpha(SampleMask(input.uv.xy));
                return input.positionCS.z;
            }
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
            #pragma vertex LobbyVertex
            #pragma fragment LobbyDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            half4 LobbyDepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceAlpha(SampleMask(input.uv.xy));
                half metallic;
                float3 normalWS = SurfaceNormal(input,metallic);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)),0);
                #else
                    return half4(normalWS,0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
