//////////////////////////////////////////
//
// NOTE: This is a valid URP shader file
// Converted from the original legacy shader.
//
///////////////////////////////////////////
Shader "==PGAME==/LOBBYPLAYER/m_lob_PlayerPBRHairColor_lv3_URP" {
    Properties {
        [Header(Tex)] [MainTexture] _MainTex ("Diffiuse(RGB)Roughness(A)", 2D) = "white" { }
        [HDR] _colorSkin ("_colorSkin(皮肤颜色)", Color) = (1,1,1,1)
        _BumpMapNr ("Normal Map(RGB)Matelness(A)", 2D) = "bump" { }
        _MixMap ("Alpha(R)Mask(G)Emiss(B)Color(A)", 2D) = "black" { }
        [Toggle] _Mask ("If Mask(打开遮罩)", Float) = 0
        [Toggle] _Emissive ("If Emissive(打开自发光)", Float) = 0
        [KeywordEnum(UV0, UV1)] _SLUV ("贴图采样UV选择", Float) = 0
        [Header(Common)] _actorscale ("actor Scale(角色衰减缩放)", Float) = 1
        [HideInInspector] _ActorScaleAttribute ("_ActorScaleAttribute", Float) = 1
        _ShadowOffset ("_ShadowOffset(阴影偏移)", Range(-1, 1)) = 0
        _ShadowPow ("_ShadowPow(阴影过渡)", Range(0.5, 10)) = 1
        _ShadowScale ("阴影减弱", Range(0, 1)) = 0
        _RoughNessOffset ("RoughNessOffset(粗糙度偏移)", Range(-1, 1)) = 0
        [HDR] _SpecularColor ("SpecularColor(高光颜色)", Color) = (1,1,1,1)
        _MetallicOffset ("MetallicOffset(金属度偏移)", Range(-1, 1)) = 0
        _NormalScale ("NormalScale(法线强度)", Range(-8, 8)) = 1
        _EmisssionScale ("EmisssionScale(自发光强度)", Range(0, 10)) = 0
        _MainlightAttenuation ("MainlightAttenuation(主光源衰减)", Range(0.04, 32)) = 0.04
        [KeywordEnum(X, Y, Z)] _AtVector ("AttenuationVector(衰减方向)", Float) = 0
        [Header(HairHighlight)] _HairMap ("HairShape(R)Mask(G)", 2D) = "black" { }
        _shapeST ("shapeST", Vector) = (1,1,0,0)
        _SunShiftOffuse1 ("SunShiftOffuse1", Range(-1, 1)) = 0
        _SunShiftOffuse2 ("SunShiftOffuse2", Range(-1, 1)) = 0
        _SunShift ("SunShift", Range(-1, 1)) = 0
        [HDR] _specularColor1 ("Specular1 Color", Color) = (0.2,0.2,0.2,1)
        _glossiness_1X ("Specular1 X Axis", Range(0, 1)) = 0.1
        _glossiness_1Y ("Specular1 Y Axis", Range(0, 1)) = 0.8
        [HDR] _specularColor2 ("Specular2 Color", Color) = (0.3,0.2,0.1,1)
        _glossiness_2X ("Specular2 X Axis", Range(0, 1)) = 0.4
        _glossiness_2Y ("Specular2 Y Axis", Range(0, 1)) = 1
        [Header(SH)] [KeywordEnum(Cla, Lerp)] _SHType ("SH Type(SH类型)", Float) = 1
        _SHScale ("SHScale(SH强弱)", Range(0, 10)) = 1
        [HDR] _SHTopColor ("SHTopColor(亮部颜色)", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("SHBotColor(暗部颜色)", Color) = (2.5,2.5,2.5,1)
        [Header(OutLine)] [KeywordEnum(OFF, ON)] _VTEX ("是否使用贴图控制定点色", Float) = 0
        [KeywordEnum(OFF, ON)] _VCOLOR2N ("是否使用顶点色勾边优化", Float) = 0
        _OutlineMap ("OutlineMap(勾边贴图)", 2D) = "white" { }
        _OutlineColor ("OutlineColor(勾边颜色)", Color) = (0.5,0.5,0.5,1)
        _Offset ("Z Offset(深度偏移)", Float) = -5
        _lightDir ("lightDirtion(勾边光源方向)", Vector) = (9.48,3.68,0,0)
        [Header(MaskColor)] [HDR] _color1 ("_color1", Color) = (1,1,1,1)
        [HDR] _color2 ("_color2", Color) = (1,1,1,1)
        [HDR] _color3 ("_color3", Color) = (1,1,1,1)
        [HDR] _color4 ("_color4", Color) = (1,1,1,1)
        _metalv4 ("_metalv4", Vector) = (0,0,0,0)
        _roughv4 ("_roughv4", Vector) = (1,1,1,1)
        [KeywordEnum(OFF, Deffiuse, Normal, Metal, Roughness, AO, Emiss, SH)] _Debug ("textureDebug", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _OutlineWidth("Outline Width", Float) = 1.0

        // Original hidden properties for render state
        [HideInInspector] _RenderType ("Vector1 ", Float) = 0
        [HideInInspector] _Cull ("Cull Mode", Float) = 2 // 2 is Back
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1 // On
        [HideInInspector] _ZTest ("ZTest", Float) = 4 // LEqual
        [HideInInspector] _SrcBlendFactor ("SrcBlend", Float) = 5 // SrcAlpha
        [HideInInspector] _DstBlendFactor ("DstBlend", Float) = 10 // OneMinusSrcAlpha
    }
    SubShader {
        Tags {
            "IGNOREPROJECTOR" = "true"
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        LOD 300

        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Render states from original shader
            Blend [_SrcBlendFactor] [_DstBlendFactor]
            ZWrite [_ZWrite]
            Cull [_Cull]
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma exclude_renderers gles
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            // Custom Keywords from original shader
            #pragma multi_compile _SHTYPE_CLA _SHTYPE_LERP
            #pragma multi_compile _SLUV_UV0 _SLUV_UV1
            #pragma multi_compile _ATVECTOR_X _ATVECTOR_Y _ATVECTOR_Z
            #pragma multi_compile _VTEX_OFF _VTEX_ON
            #pragma multi_compile _VCOLOR2N_OFF _VCOLOR2N_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _HairMap_ST;
                float4 _shapeST;
                float4 _colorSkin;
                float4 _SpecularColor;
                float4 _specularColor1;
                float4 _specularColor2;
                float4 _SHTopColor;
                float4 _SHBotColor;
                float4 _OutlineColor;
                float4 _lightDir;
                float4 _color1;
                float4 _color2;
                float4 _color3;
                float4 _color4;
                float4 _metalv4;
                float4 _roughv4;
                float4 _AddlightColor;
                float4 _AddlightDarkcolor;
                float4 _RimlightColor;
                float4 _RimLightDir;
                float4 _FresnelColor;
                
                float _Mask;
                float _Emissive;
                float _actorscale;
                float _ActorScaleAttribute;
                float _ShadowOffset;
                float _ShadowPow;
                float _ShadowScale;
                float _RoughNessOffset;
                float _MetallicOffset;
                float _NormalScale;
                float _EmisssionScale;
                float _MainlightAttenuation;
                float _SunShiftOffuse1;
                float _SunShiftOffuse2;
                float _SunShift;
                float _glossiness_1X;
                float _glossiness_1Y;
                float _glossiness_2X;
                float _glossiness_2Y;
                float _SHScale;
                float _InvRim;
                float3 _AddLightDir;
                float _AddlightScale;
                float _AddlightAttenuation;
                float _AddlightLerp;
                float _AddlightShadowScale;
                float _RimlightScale;
                float _RimlightScale2;
                float _RimlightShadowScale;
                float _RimlightAttenuation;
                float _Cutoff;
                float _BumpScale;
                float _FresnelFactor;
                float _faceshadowScale;
            CBUFFER_END
            
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMapNr); SAMPLER(sampler_BumpMapNr);
            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);
            TEXTURE2D(_HairMap); SAMPLER(sampler_HairMap);


            struct Attributes_ForwardLit {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv0          : TEXCOORD0;
                float2 uv1          : TEXCOORD1;
            };

            struct Varyings_ForwardLit {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 sh           : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
                float3 normalWS     : TEXCOORD3;
                float3 tangentWS    : TEXCOORD4;
                float3 bitangentWS  : TEXCOORD5;
                float4 shadowCoord  : TEXCOORD7;
                float4 attenuation  : TEXCOORD8; // x: main light, y: add light, z: rim light
            };

            Varyings_ForwardLit vert(Attributes_ForwardLit input) {
                Varyings_ForwardLit output = (Varyings_ForwardLit)0;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;

                #if _SLUV_UV1
                    output.uv = input.uv1 * _MainTex_ST.xy + _MainTex_ST.zw;
                #else
                    output.uv = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
                #endif

                // Custom SH calculation from original shader
                float3 normalWS = output.normalWS;
                float normalY2 = normalWS.y * normalWS.y;
                float normalY_25 = normalWS.y * 0.25;
                float sh_x = normalY2 * 0.125 + normalY_25 + 0.125;
                float sh_y = normalY2 * 0.125 - normalY_25 + 0.125;
                float2 sh_final = max(float2(sh_x, sh_y), 0.0) * _SHScale;
                output.sh = sh_final.xxxx * _SHTopColor + sh_final.yyyy * _SHBotColor;

                // Attenuation factors based on world position
                float pos_y_log = log2(output.positionWS.y);
                float mainLightAtten = exp2(pos_y_log * _MainlightAttenuation);
                float rimLightAtten = exp2(pos_y_log * _RimlightAttenuation);
                
                float addLightPosFactor = (_AddlightLerp * 2.0 - 1.0) * 5.0 + output.positionWS.y;
                float addLightAtten = saturate(addLightPosFactor * _AddlightAttenuation);
                addLightAtten *= addLightAtten;

                output.attenuation = float4(mainLightAtten, addLightAtten, rimLightAtten, 1.0);

                #if defined(_MAIN_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
                    output.shadowCoord = GetShadowCoord(positionInputs);
                #else
                    output.shadowCoord = float4(0,0,0,0);
                #endif

                return output;
            }

            half4 frag(Varyings_ForwardLit input) : SV_Target {
                half4 mixMap = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);
                
                half maskValue = _Mask * (mixMap.r - 1.0) + 1.0;
                clip(maskValue - _Cutoff);

                Light mainLight = GetMainLight(input.shadowCoord);
                half shadow = mainLight.shadowAttenuation;
                
                half3 lightColor = mainLight.color * mainLight.distanceAttenuation;
                lightColor *= input.attenuation.x;
                lightColor = min(lightColor, mainLight.color);
                
                half3 finalShadowColor = max(lightColor * shadow, _ShadowScale);

                // Color Masking
                half4 colorSteps = mixMap.a + half4(0.0, -0.3333, -0.6667, -1.0);
                colorSteps = -abs(colorSteps * 3.191489);
                colorSteps = saturate(colorSteps + 1.031915);

                half3 colorMasked = colorSteps.xxx * _color1.rgb + colorSteps.yyy * _color2.rgb + colorSteps.zzz * _color3.rgb + colorSteps.www * _color4.rgb;
                colorMasked = lerp(colorMasked, 1.0 - colorMasked, mixMap.g);

                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 albedo = lerp(_colorSkin.rgb, mainTex.rgb * colorMasked, maskValue);

                half metalMask = dot(colorSteps, _metalv4);
                half roughMask = dot(colorSteps, _roughv4);

                half4 bumpMap = SAMPLE_TEXTURE2D(_BumpMapNr, sampler_BumpMapNr, input.uv);
                half roughness = roughMask + mainTex.a;
                half metallic = metalMask + bumpMap.a;
                metallic = lerp(metallic, bumpMap.a, 1.0 - mixMap.g);
                metallic = saturate(metallic);

                half3 normalTS = UnpackNormal(bumpMap);
                float3 normalWS = TransformTangentToWorld(normalTS, float3x3(input.tangentWS, input.bitangentWS, input.normalWS));
                
                half3 diffuseColor = albedo * (1.0 - metallic);
                half3 specularColor = _specularColor1.rgb * albedo;

                // Hair Highlight (custom anisotropic-like shading)
                half hairShape = SAMPLE_TEXTURE2D(_HairMap, sampler_HairMap, input.uv * _shapeST.xy + _shapeST.zw).r;
                float shift1 = hairShape * _SunShift + _SunShiftOffuse1;
                float shift2 = hairShape * _SunShift * 0.5 + _SunShiftOffuse2;

                half3 tangent1 = normalize(input.tangentWS + normalWS * shift1);
                half3 tangent2 = normalize(input.tangentWS + normalWS * shift2);

                half3 viewDir = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                half3 lightDir = mainLight.direction;
                half3 halfDir = SafeNormalize(lightDir + viewDir);

                float dot_lh1 = dot(halfDir, tangent1);
                float dot_lh2 = dot(halfDir, tangent2);
                float dot_nh = dot(normalWS, halfDir);
                float dot_nv = dot(normalWS, viewDir);
                float dot_lt = dot(lightDir, input.tangentWS);

                roughness = lerp(roughness, mainTex.a, 1.0 - mixMap.y);
                roughness = max(roughness, 0.04);
                roughness = min(roughness, 1.0);

                float2 gloss1 = roughness * float2(_glossiness_1X, _glossiness_1Y);
                float2 gloss2 = roughness * float2(_glossiness_2X, _glossiness_2Y);
                
                float spec1 = pow(dot_lh1, 2) / (gloss1.x * gloss1.x) + pow(dot_lt, 2) / (gloss1.y * gloss1.y) + pow(dot_nh, 2);
                spec1 = 1.0 / (3.14159 * gloss1.x * gloss1.y * pow(spec1, 2));
                
                float spec2 = pow(dot_lh2, 2) / (gloss2.x * gloss2.x) + pow(dot_lt, 2) / (gloss2.y * gloss2.y) + pow(dot_nh, 2);
                spec2 = 1.0 / (3.14159 * gloss2.x * gloss2.y * pow(spec2, 2));
                
                half3 hairSpecular = max(0, spec1 * specularColor) + max(0, spec2 * _specularColor2.rgb * albedo);
                half hairMask = SAMPLE_TEXTURE2D(_HairMap, sampler_HairMap, input.uv * _HairMap_ST.xy + _HairMap_ST.zw).g;
                hairSpecular *= hairMask;

                half NdotL = saturate(dot(normalWS, lightDir));
                half shadowTerm = pow(NdotL, _ShadowPow) + _ShadowOffset;
                shadowTerm = saturate(shadowTerm);

                half3 lighting = finalShadowColor * shadowTerm;
                lighting = max(lighting, _ShadowScale);
                
                half3 finalColor = lighting * diffuseColor + hairSpecular * lighting;

                // Reflection
                half3 reflection = reflect(-viewDir, normalWS);
                half roughnessPixels = roughness * (1.7 - 0.7 * roughness);
                roughnessPixels *= 6.0;
                half4 envSample = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflection, roughnessPixels);
                half3 envColor = DecodeHDREnvironment(envSample, unity_SpecCube0_HDR);

                half smoothness = 1.0 - roughness;
                half oneMinusReflectivity = 1.0 - (0.04 * (1 - metallic));
                half3 fresnel = oneMinusReflectivity * pow(1.0 - saturate(dot_nv), 5) + (0.04 * (1-metallic));

                finalColor += envColor * fresnel;
                finalColor += input.sh.rgb * albedo;
                
                // Additional Light (Custom)
                half3 addLightDir = normalize(_AddLightDir);
                half addNdotL = saturate(dot(normalWS, addLightDir));
                half3 addLightColor = input.attenuation.y * _AddlightColor.rgb * addNdotL;
                finalColor += finalColor * addLightColor;

                // Rim Light (Custom)
                half3 rimDir = normalize(_RimLightDir.xyz);
                half rimDot = saturate(dot(rimDir, normalWS));
                half rimFresnel = pow(1.0 - saturate(dot_nv), 2);
                half rimTerm = saturate((rimFresnel * rimDot - _RimlightScale) / (_RimlightScale2 - _RimlightScale));
                rimTerm = rimTerm * rimTerm * (3.0 - 2.0 * rimTerm); // Smoothstep
                rimTerm *= shadow;
                half3 rimColor = lerp(_RimlightColor.rgb * albedo, _RimlightColor.rgb, _RimlightColor.a);
                finalColor += rimColor * rimTerm;

                // Emissive
                half emissiveMask = mixMap.b * _Emissive;
                finalColor += mainTex.rgb * emissiveMask * _EmisssionScale;

                // Final Tonemapping-like adjustment from original shader
                half3 filmic = finalColor * 1.1295 + 0.03;
                half3 filmic_denom = (finalColor * 0.45) * (finalColor * 1.0935 + 0.59) + 0.08;
                finalColor = (filmic * (finalColor * 0.45)) / filmic_denom;
                finalColor = min(finalColor, 100.0);

                return half4(finalColor, maskValue);
            }
            ENDHLSL
        }
        
        // Outline Pass
        Pass {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" } 

            Cull Front
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _VCOLOR2N_OFF _VCOLOR2N_ON
            #pragma multi_compile _VTEX_OFF _VTEX_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _Offset;
                float4 _lightDir;
                float4 _color1;
            CBUFFER_END
            
            TEXTURE2D(_OutlineMap); SAMPLER(sampler_OutlineMap);

            struct Attributes_Outline {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float4 color        : COLOR;
                float2 uv0          : TEXCOORD0;
            };

            struct Varyings_Outline {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings_Outline vert(Attributes_Outline v)
            {
                Varyings_Outline o;
                
                float3 normal = v.normalOS;
                #if _VCOLOR2N_ON
                    float3 bitangent = cross(v.normalOS, v.tangentOS.xyz) * v.tangentOS.w * GetOddNegativeScale();
                    float3x3 tbn = float3x3(v.tangentOS.xyz, bitangent, v.normalOS);
                    normal = mul(tbn, v.color.xyz * 2 - 1);
                #endif
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                float4 posCS = positionInputs.positionCS;

                float3 normalWS = TransformObjectToWorldNormal(normal);
                float4 normalCS = mul(GetWorldToHClipMatrix(), float4(normalWS, 0.0));
                
                float outlineWidth = 0.00463 * _OutlineWidth;
                
                #if _VTEX_ON
                    outlineWidth *= SAMPLE_TEXTURE2D_LOD(_OutlineMap, sampler_OutlineMap, v.uv0, 0).w;
                #endif

                posCS.xy += normalize(normalCS.xy) * outlineWidth * posCS.w;
                posCS.z += _Offset * 0.00001;

                o.positionCS = posCS;

                float lightFactor = 1.0;
                #if _VTEX_OFF
                    float3 worldNormal = normalize(mul((float3x3)GetObjectToWorldMatrix(), normal));
                    lightFactor = saturate(dot(worldNormal, normalize(_lightDir.xyz)));
                #endif
                
                half3 outlineColor = lerp(_OutlineColor.rgb, float3(1,1,1), 1.0 - lightFactor);
                outlineColor *= v.color.rgb;

                #if _VTEX_ON
                    outlineColor *= SAMPLE_TEXTURE2D_LOD(_OutlineMap, sampler_OutlineMap, v.uv0, 0).rgb;
                #endif

                o.color.rgb = outlineColor * _color1.rgb;
                o.color.a = 1.0;

                return o;
            }

            half4 frag(Varyings_Outline i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }

        // Shadow Caster Pass
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _Mask;
            CBUFFER_END

            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes_Shadow
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv0          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings_Shadow
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Varyings_Shadow ShadowPassVertex(Attributes_Shadow input)
            {
                Varyings_Shadow output;
                UNITY_SETUP_INSTANCE_ID(input);

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
                output.uv = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }

            half4 ShadowPassFragment(Varyings_Shadow input) : SV_TARGET
            {
                half4 mixMap = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);
                half maskValue = _Mask * (mixMap.r - 1.0) + 1.0;
                clip(maskValue - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
        
        // Depth Only Pass
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _Mask;
            CBUFFER_END

            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);

            struct Attributes_Depth
            {
                float4 positionOS   : POSITION;
                float2 uv0          : TEXCOORD0;
            };

            struct Varyings_Depth
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Varyings_Depth DepthOnlyVertex(Attributes_Depth input)
            {
                Varyings_Depth output = (Varyings_Depth)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }

            half4 DepthOnlyFragment(Varyings_Depth input) : SV_TARGET
            {
                half4 mixMap = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);
                half maskValue = _Mask * (mixMap.r - 1.0) + 1.0;
                clip(maskValue - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
        
        // Meta Pass for Light Baking
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMeta
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _colorSkin;
                float4 _color1, _color2, _color3, _color4;
                float _Cutoff;
                float _Mask;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);

            struct Attributes_Meta
            {
                float4 positionOS       : POSITION;
                float2 uv0              : TEXCOORD0;
                float2 lightmapUV       : TEXCOORD1;
                float2 dynamicLightmapUV: TEXCOORD2;
            };

            struct Varyings_Meta
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Varyings_Meta UniversalVertexMeta(Attributes_Meta input)
            {
                Varyings_Meta o;
                o.positionCS = UnityMetaVertexPosition(input.positionOS, input.lightmapUV, input.dynamicLightmapUV);
                o.uv = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            half4 UniversalFragmentMeta(Varyings_Meta i) : SV_Target
            {
                half4 mixMap = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, i.uv);
                half maskValue = _Mask * (mixMap.r - 1.0) + 1.0;
                clip(maskValue - _Cutoff);

                half4 colorSteps = mixMap.a + half4(0.0, -0.3333, -0.6667, -1.0);
                colorSteps = -abs(colorSteps * 3.191489);
                colorSteps = saturate(colorSteps + 1.031915);

                half3 colorMasked = colorSteps.xxx * _color1.rgb + colorSteps.yyy * _color2.rgb + colorSteps.zzz * _color3.rgb + colorSteps.www * _color4.rgb;
                colorMasked = lerp(colorMasked, 1.0 - colorMasked, mixMap.g);

                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 albedo = lerp(_colorSkin.rgb, mainTex.rgb * colorMasked, maskValue);
                
                MetaInput metaInput;
                metaInput.Albedo = albedo;
                metaInput.Emission = float3(0,0,0);

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/InternalErrorShader"
}