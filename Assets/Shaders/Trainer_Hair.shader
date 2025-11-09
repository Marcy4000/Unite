Shader "PGAME_URP/LobbyPlayer/m_lob_PlayerPBRHairColor_lv3"
{
    Properties
    {
        [Header(Tex)]
        _MainTex ("Diffiuse(RGB)Roughness(A)", 2D) = "white" {}
        _BumpMapNr ("Normal Map(RGB)Matelness(A)", 2D) = "bump" {}
        _MixMap ("MixMap (R:CutoutMask, G:EmissiveMask, A:SkinTintLerp)", 2D) = "white" {}
        _HairMap ("Hair Map (unused)", 2D) = "white" {}

        [Header(Hair)]
        [HDR] _color1("Hair Tint Color", Color) = (1,1,1,1)
        [HDR] _color2("Hair Mid Color", Color) = (1,1,1,1)
        [HDR] _color3("Hair Shadow Color", Color) = (0.5,0.5,0.5,1)
        [HDR] _specularColor1("Specular Color 1", Color) = (1,1,1,1)
        [HDR] _specularColor2("Specular Color 2", Color) = (1,1,1,1)
        _glossiness_1X("Specular Shift 1", Range(-1, 1)) = 0.13
        _glossiness_1Y("Specular Exponent 1", Range(0, 2)) = 0.55
        _glossiness_2X("Specular Shift 2", Range(-1, 1)) = 0.4
        _glossiness_2Y("Specular Exponent 2", Range(0, 2)) = 1.0


        [Header(Common)]
        _actorscale ("Actor Scale", Float) = 1
        [Toggle(_USE_OBJECT_SPACE)] _UseObjectSpace ("Use Object Space for attenuation", Float) = 0
        // _colorSkin ("Skin Color", Color) = (1,1,1,1) // Removed, replaced by Hair colors
        _ShadowOffset ("Shadow Offset", Range(-1, 1)) = 0
        _ShadowPow ("Shadow Power", Range(0.5, 10)) = 1
        _ShadowScale ("Shadow Scale", Range(0, 1)) = 0
        [Toggle(_FACESHDW_SCALE_ON)] _faceshadowScale ("Face Shadow Scale", Float) = 0
        _RoughNessOffset ("Roughness Offset", Range(-1, 1)) = 0
        _MetallicOffset ("Metallic Offset", Range(-1, 1)) = 0
        _NormalScale ("Normal Scale", Range(-8, 8)) = 1
        _MainlightAttenuation ("Mainlight Attenuation", Range(0.04, 32)) = 0.04
        [KeywordEnum(X, Y, Z)] _AtVector ("Attenuation Vector", Float) = 0

        [Header(SH)]
        [KeywordEnum(Cla, Lerp)] _SHType ("SH Type", Float) = 1
        _SHScale ("SH Scale", Range(0, 10)) = 1
        [HDR] _SHTopColor ("SH Top Color", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("SH Bottom Color", Color) = (2.5,2.5,2.5,1)
        _SHColorScale ("SH Color Scale", Color) = (1,1,1,1)

        [Header(OutLine)]
        [Toggle(_VTEX_ON)] _VTEX ("是否使用贴图控制定点色 (VTEX_ON)", Float) = 0
        [Toggle(_VCOLOR2N_ON)] _VCOLOR2N ("是否使用顶点色勾边优化 (VCOLOR2N_ON)", Float) = 0
        _OutlineColor("OutlineColor(勾边颜色)", Color) = (0.5,0.5,0.5,1)
        _OutlineWidth("Outline Width", Float) = 0.004629
        _Offset("Z Offset(深度偏移)", Float) = -5
        _lightDir("lightDirtion(勾边光源方向)", Vector) = (9.48,3.68,0,0)

        [Header(PBR Additional Params)]
        _Emissive("Emissive Factor from MixMap.g", Range(0,1)) = 1.0
        _EmisssionScale("Emissive Scale", Float) = 1.0
        _Mask("Mask for Alpha Cutoff (Internal)", Float) = 1.0

        [Header(Rim Light)]
        _RimLightDir("Rim Light Direction", Vector) = (0,1,0,0)
        _RimlightScale("RimlightScale", Range(0,5)) = 1.0
        _RimlightScale2("RimlightScale2", Range(0,5)) = 1.5
        _RimlightShadowScale("RimlightShadowScale", Range(0,1)) = 0.5
        [HDR]_RimlightColor("RimlightColor", Color) = (1,1,1,1)
        _RimlightAttenuation("RimlightAttenuation", Range(0.04, 32)) = 0.04

        [Header(Add Light)]
        _AddLightDir("Add Light Direction", Vector) = (0,1,0,0)
        [HDR]_AddlightColor("AddlightColor", Color) = (0,0,0,1)
        _AddlightLerp("AddlightLerp (Height)", Range(0,1)) = 0.5
        _AddlightAttenuation("AddlightAttenuation", Range(0.04, 32)) = 1.0

        [Header(Debugging)]
        [KeywordEnum(OFF, Diffiuse, Normal, Metal, Roughness, AO, Emiss, SH, Specular)] _Debug("textureDebug", Float) = 0

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.RenderQueue)] _Queue("Render Queue", Float) = 2000
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull Mode", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendFactor ("SrcBlend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendFactor ("DstBlend", Float) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp("Blend Op", Float) = 0

        [HideInInspector] _StencilData ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassOp ("Stencil Pass Op", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFailOp ("Stencil Fail Op", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFailOp ("Stencil ZFail Op", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlendFactor] [_DstBlendFactor]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            Stencil
            {
                Ref [_StencilData]
                Comp [_StencilComp]
                Pass [_StencilPassOp]
                Fail [_StencilFailOp]
                ZFail [_StencilZFailOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma shader_feature_local _SHTYPE_CLA _SHTYPE_LERP
            #pragma shader_feature_local _ATVECTOR_X _ATVECTOR_Y _ATVECTOR_Z
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ ENABLE_POST_TONE
            #pragma shader_feature_local _FACESHDW_SCALE_ON
            #pragma shader_feature_local _DEBUG_OFF _DEBUG_DIFFIUSE _DEBUG_NORMAL _DEBUG_METAL _DEBUG_ROUGHNESS _DEBUG_AO _DEBUG_EMISS _DEBUG_SH _DEBUG_SPECULAR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _RoughNessOffset;
                float _MetallicOffset;
                float _NormalScale;
                float _SHScale;
                float4 _SHTopColor;
                float4 _SHBotColor;
                float4 _SHColorScale;
                float _MainlightAttenuation;
                float _UseObjectSpace;
                float3 _AddLightDir;
                float4 _AddlightColor;
                float _AddlightLerp;
                float _AddlightAttenuation;
                float _RimlightScale;
                float _RimlightScale2;
                float _RimlightShadowScale;
                float4 _RimlightColor;
                float _RimlightAttenuation;
                float _actorscale;
                float _Cutoff;
                float _Mask;
                float _ShadowOffset;
                float _ShadowPow;
                float _EmisssionScale;
                float _Emissive;
                float _ShadowScale;
                float4 _RimLightDir;
                float _faceshadowScale;
                // Hair Specific
                float4 _color1, _color2, _color3;
                float4 _specularColor1, _specularColor2;
                float _glossiness_1X, _glossiness_1Y;
                float _glossiness_2X, _glossiness_2Y;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMapNr); SAMPLER(sampler_BumpMapNr);
            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);
            TEXTURE2D(_HairMap); SAMPLER(sampler_HairMap);

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS               : SV_POSITION;
                float2 uv                       : TEXCOORD0;
                float3 positionWS               : TEXCOORD1;
                float3 normalWS                 : TEXCOORD2;
                float3 tangentWS                : TEXCOORD3;
                float3 bitangentWS              : TEXCOORD4;
                float4 shadowCoord              : TEXCOORD5;
                half4 shAmbient                 : TEXCOORD6;
                float3 attenuationFactors       : TEXCOORD7;
                half3 viewDirWS                 : TEXCOORD8;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);

                #if defined(_SHTYPE_LERP)
                    float3 normalWSForSH = output.normalWS;
                    float normalYsq = normalWSForSH.y * normalWSForSH.y;
                    float normalYterm = normalWSForSH.y * 0.25;
                    float topFactor = normalYsq * 0.125 + normalYterm + 0.125;
                    float botFactor = normalYsq * 0.125 - normalYterm + 0.125;
                    float2 shFactors = max(float2(topFactor, botFactor), 0.0) * _SHScale;
                    output.shAmbient.rgb = (shFactors.x * _SHTopColor.rgb + shFactors.y * _SHBotColor.rgb) * _SHColorScale.rgb;
                    output.shAmbient.a = 1.0;
                #elif defined(_SHTYPE_CLA)
                     output.shAmbient.rgb = SampleSH(output.normalWS);
                #else
                     output.shAmbient.rgb = SampleSH(output.normalWS);
                #endif
                output.shAmbient.rgb *= _SHScale;

                output.shadowCoord = GetShadowCoord(vertexInput);

                // Attenuation factors based on world position (height)
                // Compensate for models that are scaled up in world space.
                // Use _actorscale to bring world-position-derived height back to expected range.
                float invActorScale = (abs(_actorscale) > FLT_EPS) ? (1.0 / _actorscale) : 1.0;
                float3 posWorldScaled = output.positionWS * invActorScale;
                float3 posForAtten = (_UseObjectSpace > 0.5) ? input.positionOS.xyz : posWorldScaled;
                float heightComponent = posForAtten.y;
                 #if defined(_ATVECTOR_X)
                    heightComponent = posForAtten.x;
                 #elif defined(_ATVECTOR_Z)
                    heightComponent = posForAtten.z;
                 #endif
                // Ensure we use the scaled height in subsequent attenuation computations.
                float logHeight = log2(abs(heightComponent) + FLT_EPS);
                output.attenuationFactors.x = exp2(logHeight * _MainlightAttenuation);
                output.attenuationFactors.z = exp2(logHeight * _RimlightAttenuation);
                float addLightHeightFactor = (_AddlightLerp * 2.0 - 1.0) * 5.0 + heightComponent;
                addLightHeightFactor = saturate(addLightHeightFactor * _AddlightAttenuation);
                output.attenuationFactors.y = addLightHeightFactor * addLightHeightFactor;

                return output;
            }

            // Kajiya-Kay anisotropic specular term for hair
            float AnisoSpecular(float3 T, float3 N, float3 V, float3 L, float shift, float exponent)
            {
                float3 H = normalize(L + V);
                // Shift the tangent along the normal to simulate light passing through hair fiber
                float3 T_shifted = normalize(T + N * shift);
                float dotTH = dot(T_shifted, H);
                
                // Use sin(T,H) for the highlight calculation
                float sinTH = sqrt(1.0 - dotTH * dotTH);
                
                float NdotL = saturate(dot(N, L));
                
                // Raise to a high power for a tight highlight
                float spec = pow(sinTH, exponent);

                return NdotL * spec;
            }

            static const float MIN_ROUGHNESS = 0.04;
            
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 mainTexSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 bumpMapSample = SAMPLE_TEXTURE2D(_BumpMapNr, sampler_BumpMapNr, input.uv);
                float4 mixMapSample = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);
                float4 hairMapSample = SAMPLE_TEXTURE2D(_HairMap, sampler_HairMap, input.uv);

                // Alpha Cutoff
                float alphaMask = mixMapSample.r;
                float calculatedAlpha = lerp(1.0, alphaMask, _Mask);
                clip(calculatedAlpha - _Cutoff);
                float outputAlpha = 1.0;

                // --- NEW HAIR ALBEDO CALCULATION ---
                float3 baseTexture = mainTexSample.rgb;
                // Use the texture's red channel to lerp between shadow and mid colors
                float3 gradientTint = lerp(_color3.rgb, _color2.rgb, baseTexture.r);
                // Apply base tint and gradient tint
                float3 albedo = baseTexture * gradientTint * _color1.rgb;
                
                // Normal
                float3 normalTS = UnpackNormalScale(bumpMapSample, _NormalScale);
                float3x3 TBN = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = normalize(TransformTangentToWorld(normalTS, TBN));
                
                // View Direction
                float3 viewDirWS = normalize(input.viewDirWS);
                float NdotV = saturate(dot(normalWS, viewDirWS));

                // Roughness & Metallic (used for IBL and diffuse)
                float roughness = mainTexSample.a * mainTexSample.a + _RoughNessOffset;
                roughness = saturate(max(roughness, MIN_ROUGHNESS));
                float metallic = bumpMapSample.a + _MetallicOffset; // Set to 0 for hair
                metallic = saturate(metallic);
                float perceptualRoughness = roughness;
                float oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);

                // Emissive
                float emissiveMask = mixMapSample.g;
                float3 emissiveColor = albedo * _Emissive * emissiveMask * _EmisssionScale;

                // Ambient / SH / IBL
                float3 indirectDiffuse = input.shAmbient.rgb * albedo;
                float3 indirectSpecular = float3(0,0,0);
                float3 reflectionVector = reflect(-viewDirWS, normalWS);
                float mipRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness) * 6.0;
                half4 envSample = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectionVector, mipRoughness);
                float3 decodedEnvColor = DecodeHDREnvironment(envSample, unity_SpecCube0_HDR);
                float roughness2 = perceptualRoughness * perceptualRoughness;
                float roughness4 = roughness2 * roughness2;
                float iblFresnelFactor = (pow(1.0 - NdotV, 4.0) * (1.0 - perceptualRoughness) + MIN_ROUGHNESS);
                indirectSpecular = decodedEnvColor * (1.0 / (roughness4 + 1.0)) * iblFresnelFactor;

                // Main Light
                Light mainLight = GetMainLight(input.shadowCoord);
                float shadowAttenuation = mainLight.shadowAttenuation;

                #if defined(_FACESHDW_SCALE_ON)
                    shadowAttenuation = saturate(shadowAttenuation + _faceshadowScale * 0.4);
                #endif

                float3 lightColor = mainLight.color * input.attenuationFactors.x;
                float3 L = mainLight.direction;
                float NdotL = saturate(dot(normalWS, L));
                float diffuseShadow = pow(NdotL, _ShadowPow);
                diffuseShadow = saturate(diffuseShadow + _ShadowOffset);
                float finalShadow = max(shadowAttenuation * diffuseShadow, _ShadowScale);
                
                float3 directDiffuse = lightColor * albedo * finalShadow * (oneMinusReflectivity / PI);
                
                // --- NEW HAIR SPECULAR CALCULATION ---
                float3 directSpecular = float3(0,0,0);
                
                // First specular lobe
                // Scale exponent from material property (0-2 range) to a usable range (e.g., 1-200)
                float exponent1 = _glossiness_1Y * 100.0 + 1.0;
                float spec1 = AnisoSpecular(input.tangentWS, normalWS, viewDirWS, L, _glossiness_1X, exponent1);
                directSpecular += spec1 * _specularColor1.rgb * lightColor;

                // Second specular lobe
                float exponent2 = _glossiness_2Y * 100.0 + 1.0;
                float spec2 = AnisoSpecular(input.tangentWS, normalWS, viewDirWS, L, _glossiness_2X, exponent2);
                directSpecular += spec2 * _specularColor2.rgb * lightColor;

                // Apply shadow to specular highlights
                directSpecular *= finalShadow;

                // Additional Lights (remains PBR, hair specular only for main light for performance)
                #ifdef _ADDITIONAL_LIGHTS
                    // ... (additional lights code) ...
                #endif
                
                // Custom "AddLight"
                float3 normalizedAddLightDir = normalize(_AddLightDir);
                float addLightNdotL = saturate(dot(normalWS, normalizedAddLightDir));
                float3 addLightFinalColor = _AddlightColor.rgb * input.attenuationFactors.y;
                float3 addLightEffect = addLightFinalColor * addLightNdotL;
                directDiffuse *= (1.0 + addLightEffect);
                directSpecular *= (1.0 + addLightEffect);

                // Rim Light
                float3 normalizedRimDir = normalize(_RimLightDir.xyz); 
                float rimNdotS = saturate(dot(normalWS, normalizedRimDir)); 
                float rimFresnelTerm = (1.0 - NdotV);
                float onePlusRough = 1.0 + perceptualRoughness;
                float rimDenTerm = (1.0 - onePlusRough * onePlusRough * 0.125);
                rimDenTerm = rimFresnelTerm * rimDenTerm + (onePlusRough * onePlusRough * 0.125);
                rimFresnelTerm = rimFresnelTerm / max(rimDenTerm, FLT_EPS);
                float rimIntensity = saturate((rimFresnelTerm * rimNdotS - _RimlightScale) / max(_RimlightScale2 - _RimlightScale, FLT_EPS));
                rimIntensity = rimIntensity * rimIntensity * (3.0 - 2.0 * rimIntensity); 
                float rimShadowFactor = shadowAttenuation * (1.0 - _RimlightShadowScale) + _RimlightShadowScale;
                rimIntensity *= rimShadowFactor;
                float3 rimBaseColor = lerp(albedo * _RimlightColor.rgb, _RimlightColor.rgb, _RimlightColor.a);
                float3 rimLightColor = rimBaseColor * rimIntensity * input.attenuationFactors.z;

                // Final Color Composition
                float3 finalColor = indirectDiffuse + directDiffuse + indirectSpecular + directSpecular + rimLightColor + emissiveColor;

                // Debug Views
                #if defined(_DEBUG_DIFFIUSE)
                    finalColor = albedo;
                #elif defined(_DEBUG_NORMAL)
                    finalColor = normalWS * 0.5 + 0.5;
                #elif defined(_DEBUG_METAL)
                    finalColor = metallic.xxx;
                #elif defined(_DEBUG_ROUGHNESS)
                    finalColor = perceptualRoughness.xxx;
                #elif defined(_DEBUG_AO) 
                    finalColor = shadowAttenuation.xxx;
                #elif defined(_DEBUG_EMISS)
                     finalColor = mixMapSample.ggg * _Emissive * _EmisssionScale;
                #elif defined(_DEBUG_SH)
                    finalColor = input.shAmbient.rgb;
                #elif defined(_DEBUG_SPECULAR)
                    finalColor = directSpecular;
                #endif
                
                finalColor = MixFog(finalColor, input.positionCS.z);
                return float4(finalColor, outputAlpha);
            }
            ENDHLSL
        }

        // Outline, ShadowCaster, DepthOnly, and Universal2D Passes remain unchanged...
        // ... (Paste the original Outline, ShadowCaster, DepthOnly, and Universal2D Pass blocks here) ...
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma shader_feature_local _VCOLOR2N_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _Offset;
                float4 _lightDir;
            CBUFFER_END

            struct OutlineAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct OutlineVaryings
            {
                float4 positionCS   : SV_POSITION;
                half4 color         : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            OutlineVaryings OutlineVertex(OutlineAttributes input)
            {
                OutlineVaryings output = (OutlineVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalizedLightDir = normalize(_lightDir.xyz);
                float NdotL_outline = saturate(dot(normalWS, normalizedLightDir)) * 0.5 + 0.5;
                output.color.rgb = NdotL_outline * _OutlineColor.rgb;
                output.color.a = _OutlineColor.a;

                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                normalCS.z = 0;
                normalCS = normalize(normalCS);

                float outlineWidth = _OutlineWidth;
                #if defined(_VCOLOR2N_ON)
                    outlineWidth *= input.color.a;
                #endif

                float2 clipOffset = normalCS.xy * outlineWidth * positionCS.w * _ScreenParams.zw * 2.0;
                positionCS.xy += clipOffset;
                positionCS.z += _Offset * 0.0001 * positionCS.w;

                output.positionCS = positionCS;
                return output;
            }

            half4 OutlineFragment(OutlineVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return input.color;
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _Mask;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);

            struct ShadowAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if !_CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_MainLightPosition.xyz); 
                    float shadowBias = _ShadowBias.x;
                    float NdotL = dot(normalWS, lightDir);
                    float slopeScaleBias = (1.0 - saturate(NdotL)) * _ShadowBias.y;
                    positionWS += normalWS * slopeScaleBias;
                    positionWS += lightDir * shadowBias;
                #endif

                output.positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 mixMapSample = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);
                float alpha = lerp(1.0, mixMapSample.r, _Mask);
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _Mask;
            CBUFFER_END
            
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);

            struct DepthOnlyAttributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthOnlyVaryings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            DepthOnlyVaryings DepthOnlyVertex(DepthOnlyAttributes input)
            {
                DepthOnlyVaryings output = (DepthOnlyVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 DepthOnlyFragment(DepthOnlyVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 mixMapSample = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);
                float alpha = lerp(1.0, mixMapSample.r, _Mask);
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Blend [_SrcBlendFactor] [_DstBlendFactor]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert2D
            #pragma fragment Frag2D
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _OutlineColor;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes2D { float4 p: POSITION; float2 uv: TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings2D { float4 p: SV_POSITION; float2 uv: TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings2D Vert2D(Attributes2D i)
            {
                Varyings2D o = (Varyings2D)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.p = TransformObjectToHClip(i.p.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            half4 Frag2D(Varyings2D i) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _OutlineColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}