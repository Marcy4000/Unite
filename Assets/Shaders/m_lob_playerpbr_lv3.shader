Shader "==PGAME==/LobbyPlayer/m_lob_playerpbr_lv3_URP_Standard"
{
    Properties
    {
        [MainTexture] _MainTex ("Main Texture", 2D) = "white" {}
        _BumpMapNr ("Normal Map", 2D) = "bump" {}
        
        [Header(Surface Options)]
        _RoughNessOffset ("Roughness Offset", Range(0,1)) = 0.5
        _MetallicOffset ("Metallic Offset", Range(0,1)) = 0.0
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _NormalScale ("Normal Scale", Float) = 1.0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        
        [Header(Spherical Harmonics)]
        _SHScale ("SH Scale", Float) = 1.0
        _SHTopColor ("SH Top Color", Color) = (1,1,1,1)
        _SHBotColor ("SH Bot Color", Color) = (0,0,0,1)
        _SHColorScale ("SH Color Scale", Color) = (1,1,1,1)

        [Header(Main Light Settings)]
        _ShadowScale ("Shadow Scale", Range(0,1)) = 0.0
        _ShadowOffset ("Shadow Offset", Float) = 0.0
        _ShadowPow ("Shadow Pow", Float) = 1.0

        [Header(Add Light)]
        _AddLightDir ("Add Light Dir", Vector) = (0,1,0,0)
        _AddlightColor ("Add Light Color", Color) = (1,1,1,1)

        [Header(Rim Light)]
        _RimlightColor ("Rim Light Color", Color) = (1,1,1,1)
        _RimlightScale ("Rim Scale", Float) = 1.0
        _RimlightScale2 ("Rim Scale 2", Float) = 1.0
        _RimlightShadowScale ("Rim Shadow Scale", Float) = 0.0
        _RimLightDir ("Rim Light Dir", Vector) = (0,0,1,0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry" 
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            
            // Required URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD3;
                float3 tangentWS  : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                float3 shColor    : TEXCOORD6;
                float3 viewDirWS  : TEXCOORD7;
                // Using standard macro for shadow coord compatibility
                float4 shadowCoord : TEXCOORD8;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _RoughNessOffset;
                float4 _SpecularColor;
                float _MetallicOffset;
                float _NormalScale;
                float _SHScale;
                float4 _SHTopColor;
                float4 _SHBotColor;
                float4 _SHColorScale;
                float3 _AddLightDir;
                float4 _AddlightColor;
                float _RimlightScale;
                float _RimlightScale2;
                float _RimlightShadowScale;
                float4 _RimlightColor;
                float _ShadowOffset;
                float _ShadowPow;
                float _ShadowScale;
                float4 _RimLightDir;
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMapNr); SAMPLER(sampler_BumpMapNr);

            float3 CustomToneMap(float3 x)
            {
                x = max(x, 0.0);
                float3 a = x * (1.1294999 * x + 0.029999999);
                float3 b = x * (0.44999999 * x + 0.58999997) + 0.079999998; 
                return min(a / b, 100.0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;

                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                
                // Manual Shadow Coord calculation for maximum compatibility
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    output.shadowCoord = ComputeScreenPos(output.positionCS);
                #else
                    output.shadowCoord = TransformWorldToShadowCoord(vertexInput.positionWS);
                #endif

                // Spherical Harmonics
                float3 worldNormal = output.normalWS;
                float sqrY = worldNormal.y * worldNormal.y;
                float term = worldNormal.y * 0.25;
                float shMix = (sqrY * 0.125) + term + 0.125;
                float shMix2 = (sqrY * 0.125) - term + 0.125;
                float2 shFactors = max(float2(shMix, shMix2), 0.0) * _SHScale;
                output.shColor = ((shFactors.y * _SHBotColor.rgb) + (shFactors.x * _SHTopColor.rgb)) * _SHColorScale.rgb;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Normal Mapping
                float4 packedNormal = SAMPLE_TEXTURE2D(_BumpMapNr, sampler_BumpMapNr, input.uv);
                float3 normalTS = packedNormal.xyz * 2.0 + float3(-1.0, -1.0, -2.0); 
                normalTS = normalTS * _NormalScale + float3(0.0, 0.0, 1.0);
                float3 normalWS = normalize(normalTS.x * input.tangentWS + normalTS.y * input.bitangentWS + normalTS.z * input.normalWS);

                // 2. Material Prep
                float4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float roughness = clamp(saturate((mainTexColor.w * mainTexColor.w) + _RoughNessOffset), 0.04, 0.99);
                float metallic = saturate(packedNormal.w + _MetallicOffset);
                float3 viewDir = normalize(input.viewDirWS);
                float NdotV = max(dot(normalWS, viewDir), 0.0001);

                // 3. Environment
                float3 reflectDir = reflect(-viewDir, normalWS);
                float3 environment = GlossyEnvironmentReflection(reflectDir, roughness, 1.0);
                float oneMinusMet = 1.0 - (metallic * 0.96);
                float3 reflColor = lerp(mainTexColor.rgb, float3(0.04, 0.04, 0.04), metallic);
                float3 reflectionMask = (float3(saturate((1.0 - roughness) + NdotV), saturate((1.0 - roughness) + NdotV), saturate((1.0 - roughness) + NdotV)) - reflColor) * (oneMinusMet * oneMinusMet) + reflColor;
                
                environment *= reflectionMask;
                environment += input.shColor * (mainTexColor.rgb * (1.0 - metallic)); 

                // 4. Main Light
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDir = mainLight.direction;
                float3 lightColor = mainLight.color;
                float shadowAtten = mainLight.shadowAttenuation;

                float NdotL = saturate(dot(normalWS, lightDir));
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));

                float specTerm = pow(NdotH, (1.0/roughness)*20.0);
                float3 specular = min(specTerm, 100.0) * reflColor * NdotL * _SpecularColor.rgb * 1.2;
                
                float shadowVal = saturate(exp2(log2(NdotL + 0.001) * _ShadowPow) + _ShadowOffset);
                float3 lightResult = lightColor * max(shadowAtten * shadowVal, _ShadowScale);
                float3 finalColor = environment + (specular * lightResult);

                // 5. Add Light & Rim
                float3 addLightDirNorm = normalize(_AddLightDir);
                finalColor += min(saturate(dot(normalWS, addLightDirNorm)) * _AddlightColor.rgb * lightResult, 100.0);

                float rimDot = saturate(dot(normalize(_RimLightDir.xyz), normalWS));
                float rimFactor = saturate(NdotV * rimDot - _RimlightScale);
                rimFactor *= (1.0 / max((_RimlightScale2 - _RimlightScale), 0.0001));
                rimFactor = saturate(rimFactor * rimFactor * (3.0 - 2.0 * rimFactor));
                finalColor += min(_RimlightColor.rgb * rimFactor * saturate(shadowAtten * (1.0 - _RimlightShadowScale) + _RimlightShadowScale), 100.0);

                return float4(CustomToneMap(finalColor), 1.0);
            }
            ENDHLSL
        }

        // Mandatory Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Vary { float4 positionCS : SV_POSITION; };
            Vary vert(Attr input) {
                Vary output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs norm = GetVertexNormalInputs(input.normalOS, float4(0,0,0,0));
                output.positionCS = GetShadowPositionHClip(pos, norm);
                return output;
            }
            float4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}