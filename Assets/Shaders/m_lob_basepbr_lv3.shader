Shader "PGAME/LobbyPokemon/m_lob_basepbr_lv3"
{
    Properties
    {
        [Header(Tex)]
        [MainTexture] _MainTex ("Diffuse", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _MixMap ("MROE (Metallic | Roughness | AO | Emissive Mask)", 2D) = "white" {}
        
        [Header(Emission)]
        [Toggle] _Emissive ("Enable Emission", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmisssionScale ("Emission Scale", Range(0, 10)) = 1
        [Toggle] _IFSSSEmission ("Emission Changes Base Color", Float) = 0
        [HideInInspector] _EmisAreaDiffuse ("New Diffuse of Emission Area", Color) = (1,1,1,1)

        [Header(Subsurface Scattering)]
        [Toggle] _IFSSS ("Enable SSS", Float) = 0
        _SSSColor ("SSS Color", Color) = (1,1,1,1)
        _SSSScale ("SSS Scale", Range(0, 1)) = 0.5

        [Header(PBR Control)]
        _MetallicOffset ("Metallic Offset", Range(-1, 1)) = 0
        _RoughNessOffset ("Roughness Offset", Range(-1, 1)) = 0
        _AOScale ("AO Scale", Range(0.04, 20)) = 1
        _NormalScale ("Normal Scale", Range(-8, 8)) = 1
        
        [Header(Cel Shading)]
        [Toggle] _IFRamp ("Enable Ramp Shading", Float) = 1
        _RampThreshold ("Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmooth ("Ramp Smoothness", Range(0.01, 1)) = 0.1
        _SketchColor ("Sketch Color", Color) = (0,0,0,1)

        [Header(Fur)]
        [Toggle] _IFFur ("Enable Fur", Float) = 0
        _FurTex ("Fur (Shape R | Length G | HighlightMask B)", 2D) = "white" { }
        _FurShape ("Fur Shape Scale", Range(0, 0.1)) = 0.05
        _FurTilling ("Fur Tilling", Float) = 1

        [Header(Fur Highlight)]
        [HDR] _specularColor1 ("Specular 1 Color", Color) = (0.2,0.2,0.2,1)
        _glossiness_1X ("Specular 1 X Axis", Range(0, 1)) = 0.1
        _glossiness_1Y ("Specular 1 Y Axis", Range(0, 1)) = 0.8
        _SunShiftOffuse1 ("Sun Shift Offset 1", Range(-1, 1)) = 0
        
        [HDR] _specularColor2 ("Specular 2 Color", Color) = (0.3,0.2,0.1,1)
        _glossiness_2X ("Specular 2 X Axis", Range(0, 1)) = 0.4
        _glossiness_2Y ("Specular 2 Y Axis", Range(0, 1)) = 1
        _SunShiftOffuse2 ("Sun Shift Offset 2", Range(-1, 1)) = 0
        _SunShift ("Sun Shift", Range(-1, 1)) = 0

        [Header(SH Lighting)]
        [KeywordEnum(Cla, Lerp)] _SHType ("SH Type", Float) = 1
        _SHScale ("SH Scale", Range(0, 10)) = 1
        _SHshadow ("SH in Shadow", Range(0, 1)) = 1
        [HDR] _SHTopColor ("SH Top Color", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("SH Bottom Color", Color) = (2.5,2.5,2.5,1)
        
        [Header(Outline)]
        [KeywordEnum(OFF, ON)] _VCOLOR2N ("Use Vertex Color for Outline", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0.5,0.5,0.5,1)
        _Offset ("Z Offset", Float) = -5
        [HideInInspector] _OutlineMap ("OutlineMap", 2D) = "white" { }
        [HideInInspector] [KeywordEnum(OFF, ON)] _VTEX ("Use Outline Map", Float) = 0

        [Header(Advanced)]
        [Toggle(_ENVIRONMENTREFLECTIONS_OFF)] _DisableEnvironmentReflection ("Disable Environment Reflection", Float) = 0
        [HideInInspector] _actorscale ("actor Scale", Float) = 1
        [HideInInspector] _ActorScaleAttribute ("_ActorScaleAttribute", Float) = 1
        [HideInInspector] _RenderType ("Vector1 ", Float) = 0
        [HideInInspector] _Cull ("Cull Mode", Float) = 2
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [HideInInspector] _AlphaToMask ("Vector1 ", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            
            HLSLPROGRAM
            #pragma exclude_renderers gles
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            #pragma shader_feature _IFFUR_OFF _IFFUR_ON
            #pragma shader_feature _IFSSS_OFF _IFSSS_ON
            #pragma shader_feature _IFRAMP_OFF _IFRAMP_ON
            #pragma shader_feature _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature _SHTYPE_CLA _SHTYPE_LERP
            #pragma shader_feature _IFSSSEMISSION_OFF _IFSSSEMISSION_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
                float4 shadowCoord  : TEXCOORD5;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _NormalScale;
                float _MetallicOffset;
                float _RoughNessOffset;
                float _AOScale;
                float _Emissive;
                float4 _EmissionColor;
                float _EmisssionScale;
                float _IFSSSEmission;
                float3 _EmisAreaDiffuse;
                float _IFSSS;
                float _SSSScale;
                float4 _SSSColor;
                float _IFRamp;
                float _RampThreshold;
                float _RampSmooth;
                float3 _SketchColor;
                float _IFFur;
                float _FurTilling;
                float _FurShape;
                float4 _specularColor1;
                float _glossiness_1X;
                float _glossiness_1Y;
                float _SunShiftOffuse1;
                float4 _specularColor2;
                float _glossiness_2X;
                float _glossiness_2Y;
                float _SunShiftOffuse2;
                float _SunShift;
                float _SHType;
                float _SHScale;
                float _SHshadow;
                float4 _SHTopColor;
                float4 _SHBotColor;
                float _Cutoff;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MixMap);         SAMPLER(sampler_MixMap);
            TEXTURE2D(_FurTex);         SAMPLER(sampler_FurTex);

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangentOS.w;
                o.shadowCoord = GetShadowCoord(GetVertexPositionInputs(v.positionOS.xyz));
                return o;
            }
            
            // Custom Anisotropic Specular Distribution Function
            float AnisotropicD(float3 normal, float3 halfDir, float3 tangent, float3 bitangent, float roughnessX, float roughnessY)
{
    // avoid zero roughness causing div-by-zero
    roughnessX = max(roughnessX, 1e-4);
    roughnessY = max(roughnessY, 1e-4);

    float h_dot_n = dot(halfDir, normal);
    float h_dot_t = dot(halfDir, tangent);
    float h_dot_b = dot(halfDir, bitangent);

    float term1 = h_dot_t / roughnessX;
    float term2 = h_dot_b / roughnessY;

    float distribution = h_dot_n * h_dot_n + (term1 * term1 + term2 * term2);
    distribution = distribution * distribution;

    float denom = PI * roughnessX * roughnessY * distribution;

    return 1.0 / max(denom, 1e-6);
}

            half4 frag(Varyings i) : SV_Target
            {
                // -------------------------------------------------
                // 1. Unpack Material Properties & Sample Textures
                // -------------------------------------------------
                float2 uv = i.uv;

                #if _IFFUR_ON
                    float3 furTex = SAMPLE_TEXTURE2D(_FurTex, sampler_FurTex, i.uv * _FurTilling).rgb;
                    float furShapeScale = (furTex.r * 2.0 - 1.0) * _FurShape * furTex.g + 1.0;
                    uv *= furShapeScale;
                #else
                    float3 furTex = float3(0.5, 0, 0); // Neutral values when fur is off
                #endif

                half4 albedoAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half3 albedo = albedoAlpha.rgb;
                half alpha = albedoAlpha.a;

                clip(alpha - _Cutoff);
                
                half4 mixSample = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, i.uv);
                half metallic = mixSample.r + _MetallicOffset;
                half roughness = mixSample.g + _RoughNessOffset;
                half ao = pow(max(mixSample.b, 0.0), _AOScale); // clamp negative base

                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv));
                normalTS.xy *= _NormalScale;
                normalTS.z = sqrt(1.0 - saturate(dot(normalTS.xy, normalTS.xy)));
                
                float3 normalWS = TransformTangentToWorld(normalTS, float3x3(i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz));
                normalWS = normalize(normalWS);
                
                half emissionMask = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv).a;
                half3 emission = 0;
                if (_Emissive > 0)
                {
                    emission = _EmissionColor.rgb * emissionMask * _EmisssionScale;
                }
                
                #if _IFSSSEMISSION_ON
                    albedo = lerp(albedo, _EmisAreaDiffuse.rgb, emissionMask);
                #endif
                
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - i.positionWS);
                
                // -------------------------------------------------
                // 2. Lighting Calculation
                // -------------------------------------------------
                Light mainLight = GetMainLight(i.shadowCoord);
                half shadowAttenuation = mainLight.shadowAttenuation;
                
                // --- Ambient / Spherical Harmonics ---
                half3 ambient = 0;
                #if _SHTYPE_LERP
                    half shLerp = normalWS.y * 0.5 + 0.5;
                    ambient = lerp(_SHBotColor.rgb, _SHTopColor.rgb, shLerp);
                #else // CLA
                    ambient = max(0, normalWS.y) * _SHTopColor.rgb + max(0, -normalWS.y) * _SHBotColor.rgb;
                #endif
                
                half shShadow = lerp(1.0, _SHshadow, 1.0 - shadowAttenuation);
                ambient *= _SHScale * shShadow;

                // --- BRDF Data Setup ---
                // FIX: Manually initialize BRDFData for modern URP
                BRDFData brdfData = (BRDFData)0;
                brdfData.diffuse = albedo * (1.0 - metallic);
                brdfData.specular = lerp(0.04, albedo, metallic);
                brdfData.roughness = roughness;
                brdfData.perceptualRoughness = roughness;
                brdfData.roughness2 = roughness * roughness;
                brdfData.grazingTerm = saturate(roughness + 0.04);
                
                // --- Main Light Calculation ---
                half3 gi = (half3)GlobalIllumination(brdfData, 0, normalWS, viewDirWS, ao, ambient); // explicit cast
                half3 color = gi;
                
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lightColor = mainLight.color;

                half ramp = 1.0;
                #if _IFRAMP_ON
                    float rampMin = _RampThreshold - _RampSmooth * 0.5;
                    float rampMax = _RampThreshold + _RampSmooth * 0.5;
                    ramp = smoothstep(rampMin, rampMax, ndotl);

                    half sketch = lerp(1.0, furTex.r + (ramp * 2.0 - 1.0), 0.5); // Approximation of original sketch logic
                    sketch = saturate(sketch);
                    lightColor *= lerp(_SketchColor.rgb, float3(1,1,1), sketch);
                #endif
                
                half3 diffuse = lightColor * ramp;
                
                // --- Custom Specular Calculation ---
                half3 customSpecular = 0;
                #if _IFFUR_ON
    float3 halfDir = SafeNormalize(viewDirWS + mainLight.direction);

    // Lobe 1
    // Use tangent/bitangent varyings from Varyings (i.tangentWS / i.bitangentWS)
    float3 normal1 = normalize(i.tangentWS * (furTex.r * _SunShift + _SunShiftOffuse1) + i.bitangentWS);
    float spec1 = AnisotropicD(normalWS, halfDir, i.tangentWS, i.bitangentWS, max(_glossiness_1X, 1e-4), max(_glossiness_1Y, 1e-4));
    customSpecular += spec1 * _specularColor1.rgb;

    // Lobe 2
    float3 normal2 = normalize(i.tangentWS * (furTex.r * _SunShift * 0.5 + _SunShiftOffuse2) + i.bitangentWS);
    float spec2 = AnisotropicD(normalWS, halfDir, i.tangentWS, i.bitangentWS, max(_glossiness_2X, 1e-4), max(_glossiness_2Y, 1e-4));
    customSpecular += spec2 * _specularColor2.rgb;

    customSpecular = max(0, customSpecular);
    customSpecular *= lerp(float3(1.2,1.2,1.2), float3(0,0,0), furTex.b);
                #endif

                // Combine custom lighting with URP's BRDF
                color += LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDirWS) * ramp;
                color += customSpecular * lightColor * shadowAttenuation;

                // --- Additional Lights ---
                #ifdef _ADDITIONAL_LIGHTS
                    int additionalLightsCount = GetAdditionalLightsCount();
                    for (int j = 0; j < additionalLightsCount; ++j)
                    {
                        Light additionalLight = GetAdditionalLight(j, i.positionWS);
                        half additionalShadow = additionalLight.shadowAttenuation;
                        half3 addLightColor = additionalLight.color;
                        half addNdotL = saturate(dot(normalWS, additionalLight.direction));
                        
                        #if _IFRAMP_ON
                            float addRampMin = _RampThreshold - _RampSmooth * 0.5;
                            float addRampMax = _RampThreshold + _RampSmooth * 0.5;
                            addNdotL *= smoothstep(addRampMin, addRampMax, addNdotL);
                        #endif

                        color += LightingPhysicallyBased(brdfData, additionalLight, normalWS, viewDirWS) * addNdotL;
                    }
                #endif

                color += emission;

                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // FIX: Include Lighting.hlsl for GetVertexPositionInputs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl" // FIX: Include Shadows.hlsl for shadow bias functions

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _IFFur;
                float _FurShape;
                float _FurTilling;
            CBUFFER_END
            
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FurTex); SAMPLER(sampler_FurTex);

            Varyings ShadowPassVertex(Attributes v)
{
    Varyings o;
    // Simpler transform for shadow caster (removed undefined GetShadowBias / ApplyShadowBias)
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    // normal is present if needed for bias calculations elsewhere
    // float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

    o.positionCS = TransformWorldToHClip(positionWS);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

            half4 ShadowPassFragment(Varyings i) : SV_TARGET
            {
                float2 uv = i.uv;
                #if _IFFUR_ON
                    float3 furTex = SAMPLE_TEXTURE2D(_FurTex, sampler_FurTex, i.uv * _FurTilling).rgb;
                    float furShapeScale = (furTex.r * 2.0 - 1.0) * _FurShape * furTex.g + 1.0;
                    uv *= furShapeScale;
                #endif
                
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ColorMask 0
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _IFFur;
                float _FurShape;
                float _FurTilling;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FurTex); SAMPLER(sampler_FurTex);

            Varyings DepthOnlyVertex(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 DepthOnlyFragment(Varyings i) : SV_TARGET
            {
                float2 uv = i.uv;
                #if _IFFUR_ON
                    float3 furTex = SAMPLE_TEXTURE2D(_FurTex, sampler_FurTex, i.uv * _FurTilling).rgb;
                    float furShapeScale = (furTex.r * 2.0 - 1.0) * _FurShape * furTex.g + 1.0;
                    uv *= furShapeScale;
                #endif
                
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            
            #pragma shader_feature _VCOLOR2N_OFF _VCOLOR2N_ON
            #pragma shader_feature _VTEX_OFF _VTEX_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _Offset;
                float4 _lightDir;
            CBUFFER_END

            TEXTURE2D(_OutlineMap); SAMPLER(sampler_OutlineMap);

            Varyings OutlineVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                
                float3 normal = v.normalOS;

                #if _VCOLOR2N_ON
                    float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
                    float3x3 worldToObject = (float3x3)GetWorldToObjectMatrix();

                    float3 tangent = v.tangentOS.xyz;
                    float3 bitangent = cross(v.normalOS, tangent) * v.tangentOS.w;

                    float3 modNormal = float3(v.color.r * 2 - 1, v.color.g * 2 - 1, v.color.b * 2 - 1);
                    normal = normalize(mul(float3x3(tangent, bitangent, v.normalOS), modNormal));
                #endif

                float4 posCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 normalCS = normalize(TransformWorldToHClipDir(TransformObjectToWorldNormal(normal)));
                
                float outlineMapWidth = 1.0;
                #if _VTEX_ON
                    outlineMapWidth = SAMPLE_TEXTURE2D_LOD(_OutlineMap, sampler_OutlineMap, v.uv, 0).w;
                #endif

                float outlineWidth = _Offset * 0.001 * outlineMapWidth;
                posCS.xy += normalize(normalCS.xy) * outlineWidth * posCS.w;
                o.positionCS = posCS;

                float3 worldNormal = normalize(mul((float3x3)GetWorldToObjectMatrix(), v.normalOS));
                float lightFactor = saturate(dot(worldNormal, normalize(_lightDir.xyz)));
                float3 finalColor = lerp(_OutlineColor.rgb, float3(1,1,1) - _OutlineColor.rgb, lightFactor);

                #if _VTEX_ON
                    float3 mapColor = SAMPLE_TEXTURE2D_LOD(_OutlineMap, sampler_OutlineMap, v.uv, 0).rgb;
                    o.color = float4(finalColor * mapColor, 1.0);
                #else
                    o.color = float4(finalColor * v.color.rgb, 1.0);
                #endif

                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_TARGET
            {
                return i.color;
            }
            ENDHLSL
        }
        
        // This pass is a simple conversion of the "Lightweight2D" pass.
        // In a real project, a dedicated 2D or UI shader would be more appropriate.
        Pass
        {
            Name "Lightweight2D"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            Blend Off
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert2D
            #pragma fragment frag2D
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _OutlineColor; // Reusing a property for color tint
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            
            Varyings vert2D(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag2D(Varyings i) : SV_TARGET
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _OutlineColor;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}