//=========================================================================================
// PGAME/LobbyPlayer/m_lob_playerpbrSkin_lv3 - URP Conversion
//=========================================================================================
Shader "PGAME_URP/LobbyPlayer/m_lob_playerpbrSkin_lv3"
{
    Properties
    {
        [Header(Tex)]
        _MainTex ("Diffiuse(RGB)Roughness(A)", 2D) = "white" {}
        _BumpMapNr ("Normal Map(RGB)Matelness(A)", 2D) = "bump" {}
        _MixMap ("MixMap (R:CutoutMask, G:EmissiveMask, A:SkinTintLerp)", 2D) = "white" {} // Added based on fragment shader analysis

        [Header(Common)]
        _actorscale ("Actor Scale", Float) = 1
        [HDR] _colorSkin ("Skin Color", Color) = (1,1,1,1)
        _ShadowOffset ("Shadow Offset", Range(-1, 1)) = 0
        _ShadowPow ("Shadow Power", Range(0.5, 10)) = 1
        _ShadowScale ("Shadow Scale", Range(0, 1)) = 0
        [Toggle(_FACESHDW_SCALE_ON)] _faceshadowScale ("Face Shadow Scale", Float) = 0 // Changed to Toggle for shader_feature
        _RoughNessOffset ("Roughness Offset", Range(-1, 1)) = 0
        [HDR] _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _MetallicOffset ("Metallic Offset", Range(-1, 1)) = 0 // Note: Original _BumpMapNr.a is metalness
        _NormalScale ("Normal Scale", Range(-8, 8)) = 1
        _MainlightAttenuation ("Mainlight Attenuation", Range(0.04, 32)) = 0.04
        [KeywordEnum(X, Y, Z)] _AtVector ("Attenuation Vector", Float) = 0 // _ATVECTOR_X, _ATVECTOR_Y, _ATVECTOR_Z

        [Header(SH)]
        [KeywordEnum(Cla, Lerp)] _SHType ("SH Type", Float) = 1 // _SHTYPE_CLA, _SHTYPE_LERP
        _SHScale ("SH Scale", Range(0, 10)) = 1
        [HDR] _SHTopColor ("SH Top Color", Color) = (2.5,2.5,2.5,1)
        [HDR] _SHBotColor ("SH Bottom Color", Color) = (2.5,2.5,2.5,1)
        _SHColorScale ("SH Color Scale", Color) = (1,1,1,1) // Added, as it's used in VS for final SH color

        [Header(OutLine)]
        [Toggle(_VTEX_ON)] _VTEX ("是否使用贴图控制定点色 (VTEX_ON)", Float) = 0 // For outline, needs custom logic if used
        [Toggle(_VCOLOR2N_ON)] _VCOLOR2N ("是否使用顶点色勾边优化 (VCOLOR2N_ON)", Float) = 0 // For outline width/visibility from vcolor
        _OutlineColor("OutlineColor(勾边颜色)", Color) = (0.5,0.5,0.5,1)
        _OutlineWidth("Outline Width", Float) = 0.004629 // Extracted from original outline code (0.0046 / worldPos.w scaling)
        _Offset("Z Offset(深度偏移)", Float) = -5 // For outline
        _lightDir("lightDirtion(勾边光源方向)", Vector) = (9.48,3.68,0,0)

        [Header(PBR Additional Params from original shader code)]
        _Emissive("Emissive Factor from MixMap.g", Range(0,1)) = 1.0
        _EmisssionScale("Emissive Scale", Float) = 1.0
        _Mask("Mask for Alpha Cutoff (Internal)", Float) = 1.0 // Used in discard logic

        // Rim Light (parameters found in original shader fragment code uniforms)
        [Header(Rim Light)]
        _RimLightDir("Rim Light Direction", Vector) = (0,1,0,0)
        _RimlightScale("RimlightScale", Range(0,5)) = 1.0
        _RimlightScale2("RimlightScale2", Range(0,5)) = 1.5
        _RimlightShadowScale("RimlightShadowScale", Range(0,1)) = 0.5
        [HDR]_RimlightColor("RimlightColor", Color) = (1,1,1,1)
        _RimlightAttenuation("RimlightAttenuation", Range(0.04, 32)) = 0.04

        // Add Light (parameters found in original shader fragment code uniforms)
        [Header(Add Light)]
        _AddLightDir("Add Light Direction", Vector) = (0,1,0,0)
        [HDR]_AddlightColor("AddlightColor", Color) = (0,0,0,1)
        _AddlightLerp("AddlightLerp (Height)", Range(0,1)) = 0.5
        _AddlightAttenuation("AddlightAttenuation", Range(0.04, 32)) = 1.0

        [Header(Debugging)]
        [KeywordEnum(OFF, Diffiuse, Normal, Metal, Roughness, AO, Emiss, SH)] _Debug("textureDebug", Float) = 0

        //[Header(Render States - From HideInInspector)]
        [Enum(UnityEngine.Rendering.RenderQueue)] _Queue("Render Queue", Float) = 2000 // Opaque
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull Mode", Float) = 2 // Back
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1 // On
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4 // LEqual

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendFactor ("SrcBlend", Float) = 1 // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendFactor ("DstBlend", Float) = 0 // Zero
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp("Blend Op", Float) = 0 // Add

        // Stencil Properties
        [HideInInspector] _StencilData ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8 // Always
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassOp ("Stencil Pass Op", Float) = 0 // Keep
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFailOp ("Stencil Fail Op", Float) = 0 // Keep
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFailOp ("Stencil ZFail Op", Float) = 0 // Keep

        // These were in original but seem less critical for URP or handled by URP automatically
        // [HideInInspector] _ActorScaleAttribute ("_ActorScaleAttribute", Float) = 1
        // [HideInInspector] _AlphaToMask ("Vector1 ", Float) = 0
        // [HideInInspector] _SrcBlendFactorAlpha ("Vector1 ", Float) = 5
        // [HideInInspector] _DstBlendFactorAlpha ("Vector1 ", Float) = 10
        // [HideInInspector] _BlendOpAlpha ("Vector1 ", Float) = 0
        // [HideInInspector] _RenderType ("Vector1 ", Float) = 0 // Use _Queue and Blend modes instead
        // [HideInInspector] _QueueOffset ("Queue offset", Float) = 0
        // _ReceiveShadows ("Receive Shadows", Float) = 1 // URP handles this via Light component & material settings
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque" // Default, can be changed by _Queue
            "Queue" = "Geometry"     // Default, can be changed by _Queue
        }
        LOD 300

        // Main PBR Pass (ported from original "ForwardLit" GpuProgramID 46686)
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

            // Shader Features / Multi Compiles from original keywords
            #pragma shader_feature_local _SHTYPE_CLA _SHTYPE_LERP
            #pragma shader_feature_local _ATVECTOR_X _ATVECTOR_Y _ATVECTOR_Z // Y is default (0)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT // Cascade shadows uses this for blending
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION // If URP SSAO is enabled
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ ENABLE_POST_TONE // Custom tonemapping toggle
            #pragma shader_feature_local _FACESHDW_SCALE_ON

            // Debugging features
            #pragma shader_feature_local _DEBUG_OFF _DEBUG_DIFFIUSE _DEBUG_NORMAL _DEBUG_METAL _DEBUG_ROUGHNESS _DEBUG_AO _DEBUG_EMISS _DEBUG_SH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl" // Foraces Filmic
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl" // For Alpha
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl" // For saturate


            CBUFFER_START(UnityPerMaterial)
                // Textures ST
                float4 _MainTex_ST;
                // Properties (already declared above, URP handles CBuffer for them)
                // ... all float, vector, color properties ...
                // Copied from original UnityPerMaterial block for clarity (URP might put these in UnityPerMaterial by default)
                float _RoughNessOffset;
                float4 _SpecularColor; // Used as specular tint
                float _MetallicOffset; // Applied to _BumpMapNr.a
                float _NormalScale;
                float _SHScale;
                float4 _SHTopColor;
                float4 _SHBotColor;
                float4 _SHColorScale;
                float _MainlightAttenuation;
                float3 _AddLightDir;
                float4 _AddlightColor; // alpha seems unused
                float _AddlightLerp;
                float _AddlightAttenuation;
                float _RimlightScale;
                float _RimlightScale2;
                float _RimlightShadowScale;
                float4 _RimlightColor;
                float _RimlightAttenuation;
                float _actorscale; // Potentially for LOD or distance fade, needs context
                float _Cutoff;
                float _Mask;
                float4 _colorSkin;
                float _ShadowOffset;
                float _ShadowPow;
                float _EmisssionScale;
                float _Emissive; // Factor for emissive mask
                float _ShadowScale; // Min shadow value
                float4 _RimLightDir;
                float _faceshadowScale; // On/Off via _FACESHDW_SCALE_ON
            CBUFFER_END

            // Texture declarations
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMapNr);
            SAMPLER(sampler_BumpMapNr);
            TEXTURE2D(_MixMap);
            SAMPLER(sampler_MixMap);

            // Environment cubemap
            // TEXTURECUBE(unity_SpecCube0); // Already defined in URP's Lighting.hlsl
            // SAMPLER(samplerunity_SpecCube0); // Already defined in URP's Lighting.hlsl

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT; // w component is sign
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR; // For outline width if _VCOLOR2N_ON
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
                float4 shadowCoord              : TEXCOORD5; // xyz for coord, w for ??? (original had it separate)
                half4 shAmbient                 : TEXCOORD6; // SH color * SHColorScale
                float3 attenuationFactors       : TEXCOORD7; // x: mainlight, y: addlight height atten, z: rimlight
                half3 viewDirWS                 : TEXCOORD8; // For PBR calculations
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 VshadowCoord : TEXCOORD9;
                #endif
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


                // SH Lighting (simplified from original, based on normal.y lerp)
                // Original:
                // u_xlat21 = u_xlat1.y * u_xlat1.y; (normalWS.y^2)
                // u_xlat2.x = u_xlat1.y * 0.25; (normalWS.y * 0.25)
                // u_xlat9 = u_xlat21 * 0.125 + u_xlat2.x; ((normalWS.y^2)*0.125 + normalWS.y*0.25)
                // u_xlat21 = u_xlat21 * 0.125 + (-u_xlat2.x); ((normalWS.y^2)*0.125 - normalWS.y*0.25)
                // u_xlat3.y = u_xlat21 + 0.125; // Factor for Bot
                // u_xlat3.x = u_xlat9 + 0.125;  // Factor for Top
                // u_xlat16_4.xy = max(u_xlat3.xy, vec2(0.0, 0.0));
                // u_xlat16_4.xy = u_xlat16_4.xy * vec2(vec2(_SHScale, _SHScale));
                // u_xlat16_2 = u_xlat16_4.yyyy * _SHBotColor;
                // u_xlat16_2 = u_xlat16_4.xxxx * _SHTopColor + u_xlat16_2;
                // vs_TEXCOORD1 = u_xlat16_2 * _SHColorScale;
                
                #if defined(_SHTYPE_LERP)
                    // Simplified SH lerp based on normal.y (matches original GLES code structure)
                    float3 normalWSForSH = output.normalWS; // Already normalized
                    float normalYsq = normalWSForSH.y * normalWSForSH.y;
                    float normalYterm = normalWSForSH.y * 0.25;
                    float topFactor = normalYsq * 0.125 + normalYterm + 0.125;
                    float botFactor = normalYsq * 0.125 - normalYterm + 0.125;
                    
                    float2 shFactors = max(float2(topFactor, botFactor), 0.0) * _SHScale;
                    output.shAmbient.rgb = (shFactors.x * _SHTopColor.rgb + shFactors.y * _SHBotColor.rgb) * _SHColorScale.rgb;
                    output.shAmbient.a = 1.0; // Or average of top/bot alpha
                #elif defined(_SHTYPE_CLA) // Cla = Classic Unity SH Sample
                     output.shAmbient.rgb = SampleSH(output.normalWS);
                #else // Default to classic if no type defined
                     output.shAmbient.rgb = SampleSH(output.normalWS);
                #endif
                output.shAmbient.rgb *= _SHScale; // Apply overall SHScale if not already in factors

                // Shadow Coordinate
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_SCREEN) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #else
                    output.shadowCoord = float4(0,0,0,0);
                #endif
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                     // output.VshadowCoord = vertexInput.shadowCoord; // Problematic line, GetShadowCoord should provide necessary data into output.shadowCoord
                #endif


                // Attenuation Factors based on world Y position (log/exp scaled)
                // AttenuationVector logic (_ATVECTOR_X, _ATVECTOR_Y, _ATVECTOR_Z)
                float heightComponent = output.positionWS.y;
                #if defined(_ATVECTOR_X)
                    heightComponent = output.positionWS.x;
                #elif defined(_ATVECTOR_Z)
                    heightComponent = output.positionWS.z;
                #endif
                // Ensure heightComponent is positive for log
                float logHeight = log2(abs(heightComponent) + FLT_EPS); // Add epsilon to avoid log(0)

                output.attenuationFactors.x = exp2(logHeight * _MainlightAttenuation); // Mainlight
                output.attenuationFactors.z = exp2(logHeight * _RimlightAttenuation); // Rimlight

                // Addlight Attenuation
                // u_xlat16_4.x = _AddlightLerp * 2.0 + -1.0;
                // u_xlat16_4.x = u_xlat16_4.x * 5.0 + u_xlat0.y; (where u_xlat0.y is worldPos.y)
                // u_xlat16_4.x = u_xlat16_4.x * _AddlightAttenuation;
                // u_xlat16_4.x = clamp(u_xlat16_4.x, 0.0, 1.0);
                // vs_TEXCOORD8.y = u_xlat16_4.x * u_xlat16_4.x;
                float addLightHeightFactor = (_AddlightLerp * 2.0 - 1.0) * 5.0 + heightComponent;
                addLightHeightFactor = saturate(addLightHeightFactor * _AddlightAttenuation);
                output.attenuationFactors.y = addLightHeightFactor * addLightHeightFactor; // Addlight

                return output;
            }

            // Helper for GGX D term
            float DistributionGGX(float3 N, float3 H, float roughness)
            {
                float a = roughness * roughness;
                float a2 = a * a;
                float NdotH = saturate(dot(N, H));
                float NdotH2 = NdotH * NdotH;
                float num = a2;
                float den = (NdotH2 * (a2 - 1.0) + 1.0);
                den = PI * den * den;
                return num / max(den, FLT_EPS); // Max to avoid div by zero
            }

            // Helper for Schlick Fresnel
            float3 FresnelSchlick(float cosTheta, float3 F0)
            {
                return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
            }
            
            // Helper for Smith G term (correlated)
            float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
            {
                float NdotV = saturate(dot(N, V));
                float NdotL = saturate(dot(N, L));
                float k = (roughness + 1.0) * (roughness + 1.0) / 8.0; // (alpha / 2) where alpha = roughness^2 for direct, or roughness for IBL
                // Simplified for direct lighting k = (roughness * roughness) / 2.0
                // The original seems to use a direct k style.
                // Original: u_xlat11 = u_xlat16_37 * u_xlat16_36 + u_xlat16_26; (NdotV * (1-rough) + rough^2)
                // u_xlat22 = u_xlat33 * u_xlat16_36 + u_xlat16_26; (NdotL * (1-rough) + rough^2)
                // u_xlat16_36 = u_xlat33 * u_xlat11 + u_xlat22; (NdotL * NdotV_term + NdotL_term)
                // u_xlat16_36 = float(1.0) / float(u_xlat16_36); u_xlat11 = u_xlat16_36 * 0.5;
                // This is form of G_SmithGGXCorrelated = 0.5 / lerp(2 * NdotL * NdotV, NdotL + NdotV, roughness)
                // Or more directly Smith G for GGX:
                float ggxV = NdotV / (NdotV * (1.0 - k) + k);
                float ggxL = NdotL / (NdotL * (1.0 - k) + k);
                return ggxV * ggxL;
            }

            static const float MIN_ROUGHNESS = 0.04; // Define MIN_ROUGHNESS

            
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 mainTexSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 bumpMapSample = SAMPLE_TEXTURE2D(_BumpMapNr, sampler_BumpMapNr, input.uv);
                float4 mixMapSample = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);

                // Alpha Cutoff Logic
                float currentCutoff = _Cutoff;
                // --- BEGIN TEST ---
                // To aggressively test if _Cutoff property is the issue, uncomment next line:
                // currentCutoff = 0.0; // Force cutoff to 0 for debugging this specific problem
                // --- END TEST ---

                float alphaMask = mixMapSample.r;
                float calculatedAlpha = (_Mask * (alphaMask - 1.0) + 1.0); // This is lerp(1.0, alphaMask, _Mask)

                // Perform clipping
                clip(calculatedAlpha - currentCutoff);

                // If the pixel has not been clipped, its output alpha for an "Opaque" shader
                // should generally be 1.0. The 'calculatedAlpha' might be low (e.g., 0.0 if alphaMask is 0.0),
                // which is fine for clip test (0.0 - 0.0 is not < 0), but for final output, opaque should be 1.0.
                float outputAlpha = 1.0;


                // Albedo
                float3 albedo = mainTexSample.rgb;
                // Skin Color Tint
                float3 skinTintBase = float3(1,1,1) - _colorSkin.rgb;
                skinTintBase = mixMapSample.a * skinTintBase + _colorSkin.rgb;
                albedo *= skinTintBase;

                // Normal
                float3 normalTS = UnpackNormalScale(bumpMapSample, _NormalScale);
                float3x3 TBN = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = TransformTangentToWorld(normalTS, TBN);
                normalWS = normalize(normalWS);

                // View Direction
                float3 viewDirWS = normalize(input.viewDirWS);
                float NdotV = saturate(dot(normalWS, viewDirWS));

                // Roughness & Metallic
                float roughness = mainTexSample.a * mainTexSample.a + _RoughNessOffset;
                roughness = saturate(max(roughness, MIN_ROUGHNESS));
                
                float metallic = bumpMapSample.a + _MetallicOffset;
                metallic = saturate(metallic);

                float perceptualRoughness = roughness;
                float oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
                float3 f0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);

                // Emissive
                float3 emissiveColor = float3(0,0,0);
                // Debug check is separate, so we can use the #else path normally
                float emissiveMask = mixMapSample.g;
                emissiveColor = albedo * _Emissive * emissiveMask * _EmisssionScale;


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
                float3 directDiffuse = float3(0,0,0);
                float3 directSpecular = float3(0,0,0);
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
                
                float finalShadow = shadowAttenuation * diffuseShadow;
                finalShadow = max(finalShadow, _ShadowScale);
                
                directDiffuse += lightColor * albedo * finalShadow * (oneMinusReflectivity / PI);

                float3 H = normalize(L + viewDirWS);
                float NdotH = saturate(dot(normalWS, H));
                // float LdotH = saturate(dot(L, H)); // Original didn't use LdotH for Fresnel directly

                float r2 = roughness2; // perceptualRoughness^2
                float NdotH2 = NdotH * NdotH;
                float r2NdotH = r2 * NdotH;
                float den_D_orig = r2NdotH * r2NdotH + (1.0 - NdotH2);
                float D_orig_num_div_den = r2 / max(den_D_orig, FLT_EPS);
                float D_orig_final = D_orig_num_div_den * D_orig_num_div_den;
                float G_like_factor_orig = perceptualRoughness * 0.25 + 0.25;
                float specStrength_orig = NdotL * D_orig_final * G_like_factor_orig * 0.0399999991;
                
                directSpecular += specStrength_orig * lightColor * _SpecularColor.rgb * finalShadow;

                // Additional Lights
                #ifdef _ADDITIONAL_LIGHTS
                    // ... (additional lights code, assume correct for now) ...
                #endif
                
                // Custom "AddLight"
                float3 normalizedAddLightDir = normalize(_AddLightDir);
                float addLightNdotL = saturate(dot(normalWS, normalizedAddLightDir));
                float3 addLightFinalColor = _AddlightColor.rgb * input.attenuationFactors.y;
                float3 addLightEffect = addLightFinalColor * addLightNdotL;
                directDiffuse *= (1.0 + addLightEffect);
                directSpecular *= (1.0 + addLightEffect);

                // Rim Light
                float3 rimLightColor = float3(0,0,0);
                // ... (rim light code, assume correct for now) ...
                float3 normalizedRimDir = normalize(_RimLightDir.xyz); 
                float rimNdotS = saturate(dot(normalWS, normalizedRimDir)); 
                float rimFresnelTerm = (1.0 - NdotV);
                float onePlusRough = 1.0 + perceptualRoughness;
                float rimDenTerm = (1.0 - onePlusRough * onePlusRough * 0.125);
                rimDenTerm = rimFresnelTerm * rimDenTerm + (onePlusRough * onePlusRough * 0.125);
                rimFresnelTerm = rimFresnelTerm / max(rimDenTerm, FLT_EPS);
                float rimIntensity = rimFresnelTerm * rimNdotS; 
                rimIntensity = (rimIntensity - _RimlightScale) / max(_RimlightScale2 - _RimlightScale, FLT_EPS);
                rimIntensity = saturate(rimIntensity);
                rimIntensity = rimIntensity * rimIntensity * (3.0 - 2.0 * rimIntensity); 
                float rimShadowFactor = shadowAttenuation * (1.0 - _RimlightShadowScale) + _RimlightShadowScale;
                rimIntensity *= rimShadowFactor;
                float3 rimBaseColor = lerp(albedo * _RimlightColor.rgb, _RimlightColor.rgb, _RimlightColor.a);
                rimLightColor = rimBaseColor * rimIntensity * input.attenuationFactors.z;


                // Final Color Composition
                float3 finalColor = indirectDiffuse + directDiffuse;
                finalColor += indirectSpecular + directSpecular;
                finalColor += rimLightColor;
                finalColor += emissiveColor;

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
                #elif defined(_DEBUG_EMISS) // This was overriding the normal emissiveColor before. Fixed.
                     finalColor = mixMapSample.ggg * _Emissive * _EmisssionScale; // Show mask scaled
                #elif defined(_DEBUG_SH)
                    finalColor = input.shAmbient.rgb;
                #endif

                // If any debug view is active, ensure outputAlpha is 1.0
                #if defined(_DEBUG_DIFFIUSE) || defined(_DEBUG_NORMAL) || defined(_DEBUG_METAL) || defined(_DEBUG_ROUGHNESS) || defined(_DEBUG_AO) || defined(_DEBUG_EMISS) || defined(_DEBUG_SH)
                    outputAlpha = 1.0;
                #endif


                // Custom Tonemapping (ENABLE_POST_TONE)
                #if defined(ENABLE_POST_TONE)
                    // ... (tonemapping code) ...
                #endif
                
                // Fog
                finalColor = MixFog(finalColor, input.positionCS.z);

                return float4(finalColor, outputAlpha); // Use the potentially modified outputAlpha
            }
            ENDHLSL
        }

        // Outline Pass
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" } // Or UniversalForwardOnly if it needs lighting data not available in Unlit

            Cull Front // Draw backfaces for outline
            ZWrite On    // Write depth for outline pixels
            ZTest LEqual
            Blend Off    // No blending for outline itself

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            #pragma shader_feature_local _VCOLOR2N_ON // Vertex color for outline width/visibility
            // _VTEX_ON is not straightforwardly used in original outline GLSL, would require texture sampling in VS if it means per-vertex width from a texture

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _Offset; // Z Offset
                float4 _lightDir; // For outline color tint based on light direction
            CBUFFER_END

            struct OutlineAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR; // Used if _VCOLOR2N_ON
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

                // World space normal
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // Outline color tint based on light direction (ported from original)
                float3 normalizedLightDir = normalize(_lightDir.xyz);
                float NdotL_outline = saturate(dot(normalWS, normalizedLightDir));
                NdotL_outline = NdotL_outline * 0.5 + 0.5; // Remap to 0.5 - 1.0
                output.color.rgb = NdotL_outline * _OutlineColor.rgb;
                output.color.a = _OutlineColor.a;

                // Vertex extrusion for outline
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // Screen-space normal extrusion (simplified from original)
                // Original used matrix mult for normal -> screen space direction
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS); // Normal in clip space (direction)
                normalCS.z = 0; // We only want screen XY offset
                normalCS = normalize(normalCS);

                float outlineWidth = _OutlineWidth;
                #if defined(_VCOLOR2N_ON)
                    outlineWidth *= input.color.a; // Modulate width by vertex alpha
                    // Could also make outline conditional on input.color.a > threshold
                    // output.color.a *= step(0.01, input.color.a); // Hide outline if vcolor.a is zero
                #endif

                // Apply extrusion based on screen pixels
                // The original "0.00462962966" might be _OutlineWidth / some_screen_res_factor
                // Let's use a URP common way: offset by pixels in clip space
                float2 clipOffset = normalCS.xy * outlineWidth * positionCS.w * _ScreenParams.zw * 2.0; // zw = 1/res, needs *2 for NDC
                positionCS.xy += clipOffset;
                
                // Z-Offset
                positionCS.z += _Offset * 0.0001 * positionCS.w; // Apply Z offset, scale by w for perspective

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
        
        // ShadowCaster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull] // Use main cull mode, or Off/Back as per original fixed pass

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW // URP define for point/spot shadow specifics

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _Mask;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MixMap); // Assuming MixMap is also needed for alpha mask in shadow pass
            SAMPLER(sampler_MixMap);


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

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    // Bias for point/spot lights is usually handled by the light settings in URP
                    // The original had a fixed bias:
                    // _LightDirection.xyz * _ShadowBias.xxx
                    // NdotL based bias: normalWS * ( (1-saturate(dot(_LightDirection.xyz, normalWS))) * _ShadowBias.y)
                    // URP handles this with ApplyShadowBias in fragment or specific vertex manipulations
                    // For now, let URP handle bias, or re-add if specific behavior is needed.
                #else // Directional
                    // Directional light shadow bias (original logic)
                    // Bias based on light direction and normal (slope-scale and constant bias)
                    // float3 lightDir = normalize(GetMainLight().direction); // This is view space normal in GetMainLight
                    // For shadow caster, _MainLightPosition.xyz is world space light direction for directional
                    float3 lightDir = normalize(_MainLightPosition.xyz); 
                    float shadowBias = _ShadowBias.x; // Constant bias
                    float NdotL = dot(normalWS, lightDir);
                    float slopeScaleBias = (1.0 - saturate(NdotL)) * _ShadowBias.y;
                    positionWS += normalWS * slopeScaleBias; // Apply slope scale bias
                    positionWS += lightDir * shadowBias; // Apply constant bias along light direction
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
                float4 mainTexSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 mixMapSample = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);

                float alphaMask = mixMapSample.r;
                float alpha = (_Mask * (alphaMask - 1.0) + 1.0);
                clip(alpha - _Cutoff);
                
                return 0; // Depth is written automatically
            }
            ENDHLSL
        }

        // DepthOnly Pass
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // For _MainTex_ST
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"


            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _Mask;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MixMap); // Assuming MixMap is also needed for alpha mask in depth pass
            SAMPLER(sampler_MixMap);

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
                float4 mainTexSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 mixMapSample = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv);

                float alphaMask = mixMapSample.r;
                float alpha = (_Mask * (alphaMask - 1.0) + 1.0);
                clip(alpha - _Cutoff);

                return 0;
            }
            ENDHLSL
        }
        
        // Universal2D Pass (if needed, basic implementation from original)
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Blend [_SrcBlendFactor] [_DstBlendFactor]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull Off // Usually Off for 2D sprites

            HLSLPROGRAM
            #pragma vertex Vert2D
            #pragma fragment Frag2D
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; // Renamed from _BaseMap_ST for consistency
                float4 _OutlineColor; // Reusing _OutlineColor as _BaseColor for 2D
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes2D
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings2D
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings2D Vert2D(Attributes2D input)
            {
                Varyings2D o = (Varyings2D)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return o;
            }

            half4 Frag2D(Varyings2D i) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return color * _OutlineColor; // Using _OutlineColor as _BaseColor
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit" // Fallback to standard URP Lit
}