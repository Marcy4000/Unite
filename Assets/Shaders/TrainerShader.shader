Shader "Converted/ComplexURPCharacter"
{
    Properties
    {
        [Header(Main Maps)]
        _MainTex("Main Texture (Albedo)", 2D) = "white" {}
        _BumpMapNr("Normal Map", 2D) = "bump" {}
        _MixMap("Mix Map (R=Cutout, G=Metallic, B=Emission)", 2D) = "white" {}

        [Header(Cutout Settings)]
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Mask("Alpha Mask", Range(0, 1)) = 1

        [Header(PBR and Color)]
        _MatColor("Material Color", Color) = (1,1,1,1)
        _colorSkin("Skin Color", Color) = (1,1,1,1)
        _RoughNessOffset("Roughness Offset", Range(0, 1)) = 0
        _MetallicOffset("Metallic Offset", Range(0, 1)) = 0
        _SpecularColor("Specular Color", Color) = (1,1,1,1)

        [Header(Main Light Shadows)]
        _ShadowScale("Shadow Falloff Scale", Range(0, 1)) = 0
        _faceshadowScale("Face Shadow Scale", Range(0, 1)) = 0.5
        _ShadowOffset("Shadow Offset", Range(-1, 1)) = 0
        _ShadowPow("Shadow Power", Range(0, 5)) = 1

        [Header(Emission)]
        _Emissive("Emissive Mask Scale", Float) = 1
        _EmisssionScale("Emission Intensity", Float) = 1

        [Header(Additional Light)]
        _AddLightDir("Add Light Direction", Vector) = (0,1,0,0)
        _AddlightColor("Add Light Color", Color) = (1,1,1,1)
        
        [Header(Rim Light)]
        _RimLightDir("Rim Light Direction", Vector) = (0,1,0,0)
        _RimlightColor("Rim Light Color", Color) = (1,1,1,1)
        _RimlightScale("Rim Light Start", Range(-1, 1)) = 0
        _RimlightScale2("Rim Light End", Range(-1, 1)) = 1
        _RimlightShadowScale("Rim Shadow Influence", Range(0, 1)) = 0.5

        [Header(Fresnel)]
        _FresnelColor("Fresnel Color", Color) = (1,1,1,1)
        _FresnelFactor("Fresnel Factor", Range(0, 5)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            // --- FIX: Added necessary render states for a cutout shader ---
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

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
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
                float4 shadowCoord  : TEXCOORD5;
            };

            // --- FIX: Samplers are now declared OUTSIDE the CBUFFER ---
            sampler2D _MainTex;
            sampler2D _BumpMapNr;
            sampler2D _MixMap;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                
                half _Cutoff;
                half _Mask;
                half4 _MatColor;
                half4 _colorSkin;
                half _RoughNessOffset;
                half _MetallicOffset;
                half _ShadowScale;
                half _faceshadowScale;
                half _ShadowOffset;
                half _ShadowPow;
                half _Emissive;
                half _EmisssionScale;
                half3 _AddLightDir;
                half4 _AddlightColor;
                half4 _RimLightDir;
                half4 _RimlightColor;
                half _RimlightScale;
                half _RimlightScale2;
                half _RimlightShadowScale;
                half4 _FresnelColor;
                half _FresnelFactor;
                half4 _SpecularColor;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 mainTex = tex2D(_MainTex, input.uv);
                half4 mixMap = tex2D(_MixMap, input.uv);

                half alpha = mixMap.r * _Mask;
                clip(alpha - _Cutoff);

                half3 normalTS = UnpackNormal(tex2D(_BumpMapNr, input.uv));
                float3x3 tbn = float3x3(normalize(input.tangentWS), normalize(input.bitangentWS), normalize(input.normalWS));
                half3 normalWS = normalize(mul(normalTS, tbn));
                
                half3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                half3 albedo = mainTex.rgb * _MatColor.rgb;
                
                half metallic = mixMap.g + _MetallicOffset;
                half roughness = mainTex.a * mainTex.a + _RoughNessOffset;
                roughness = clamp(roughness, 0.04, 1.0);
                
                half3 emission = mixMap.b * _Emissive * _EmisssionScale * albedo;

                Light mainLight = GetMainLight(input.shadowCoord);
                half shadowAttenuation = mainLight.shadowAttenuation;
                shadowAttenuation = clamp(shadowAttenuation + (_faceshadowScale * 0.4), 0.0, 1.0);

                half3 lightDirWS = mainLight.direction;
                half NdotL = saturate(dot(normalWS, lightDirWS));

                half shadowPower = pow(NdotL, _ShadowPow) + _ShadowOffset;
                shadowPower = saturate(shadowPower);
                
                half attenuation = max(shadowAttenuation * shadowPower, _ShadowScale);
                half3 diffuse = albedo * mainLight.color * attenuation;

                half3 halfwayDir = normalize(lightDirWS + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfwayDir));
                half specTerm = pow(NdotH, (1.0 - roughness) * 128);
                half3 specular = _SpecularColor.rgb * specTerm * attenuation * 1.2;
                
                half3 addLightDir = normalize(_AddLightDir);
                half addNdotL = saturate(dot(normalWS, addLightDir));
                half3 addLightColor = addNdotL * _AddlightColor.rgb * albedo;
                
                half rimDot = 1.0 - saturate(dot(viewDirWS, normalWS));
                half rimIntensity = smoothstep(_RimlightScale, _RimlightScale2, rimDot);
                rimIntensity *= pow(saturate(dot(normalWS, mainLight.direction)) * shadowAttenuation, _RimlightShadowScale);
                half3 rimColor = _RimlightColor.rgb * rimIntensity;

                half3 reflectionDir = reflect(-viewDirWS, normalWS);
                half4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectionDir, roughness * 6.0);
                half3 skyColor = DecodeHDREnvironment(environment, unity_SpecCube0_HDR);

                half fresnelDot = saturate(dot(viewDirWS, normalWS));
                half fresnel = pow(1.0 - fresnelDot, _FresnelFactor);
                half3 fresnelColor = _FresnelColor.rgb * fresnel;

                half3 finalColor = diffuse + specular + addLightColor + rimColor + fresnelColor;
                finalColor += skyColor * metallic;
                finalColor += emission;
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
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
            
            sampler2D _MixMap;
            float4 _MainTex_ST;
            half _Cutoff;
            half _Mask;

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = tex2D(_MixMap, input.uv).r * _Mask;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}