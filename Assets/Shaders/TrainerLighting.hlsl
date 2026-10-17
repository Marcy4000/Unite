#ifndef TRAINER_LIGHTING_INCLUDED
#define TRAINER_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Shared by all passes of each trainer shader, not by the lobby clothing shader.
CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST, _colorSkin, _SpecularColor;
    float4 _SHTopColor, _SHBotColor, _SHColorScale;
    float4 _AddLightDir, _AddlightColor, _RimLightDir, _RimlightColor;
    float4 _OutlineColor, _lightDir, _ScreenResolution;
    float _actorscale, _ActorScaleAttribute, _UseObjectSpace;
    float _ShadowOffset, _ShadowPow, _ShadowScale, _faceshadowScale;
    float _RoughNessOffset, _MetallicOffset, _NormalScale, _MainlightAttenuation, _PackedNormalEncoding;
    float _SHType, _SHScale, _AtVector, _Debug;
    float _Mask, _Emissive, _EmisssionScale, _Cutoff, _ReceiveShadows;
    float _AddlightLerp, _AddlightAttenuation;
    float _RimlightScale, _RimlightScale2, _RimlightShadowScale, _RimlightAttenuation, _RimEnabled;
    float _VTEX, _VCOLOR2N, _OutlineWidth, _Offset, _OutlineEnabled;
    float _Cull, _ZWrite, _ZTest, _SrcBlendFactor, _DstBlendFactor, _BlendOp;
    float _SrcBlendFactorAlpha, _DstBlendFactorAlpha, _BlendOpAlpha, _AlphaToMask;
    float _RenderType, _BlendMode, _Queue, _QueueOffset;
    float _StencilData, _StencilReadMask, _StencilWriteMask, _StencilComp;
    float _StencilPassOp, _StencilFailOp, _StencilZFailOp;
    #if defined(TRAINER_HAIR)
        float4 _HairMap_ST, _shapeST, _color1, _color2, _color3, _color4;
        float4 _metalv4, _roughv4, _specularColor1, _specularColor2;
        float _SLUV, _SunShift, _SunShiftOffuse1, _SunShiftOffuse2;
        float _glossiness_1X, _glossiness_1Y, _glossiness_2X, _glossiness_2Y;
    #endif
CBUFFER_END

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_BumpMapNr); SAMPLER(sampler_BumpMapNr);
TEXTURE2D(_MixMap); SAMPLER(sampler_MixMap);
TEXTURE2D(_OutlineMap); SAMPLER(sampler_OutlineMap);
#if defined(TRAINER_HAIR)
    TEXTURE2D(_HairMap); SAMPLER(sampler_HairMap);
#endif

struct TrainerAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TrainerVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float3 tangentWS : TEXCOORD3;
    float3 bitangentWS : TEXCOORD4;
    half3 ambient : TEXCOORD5;
    float2 attenuation : TEXCOORD6; // Main height factor, custom add-light factor.
    half fogFactor : TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float2 TrainerUV(TrainerAttributes input)
{
    float2 uv = input.uv0;
    #if defined(TRAINER_HAIR)
        // The saved enum is authoritative, including SetFloat calls made without
        // keyword synchronization. Use the same decision in every auxiliary pass.
        if (_SLUV > 0.5) uv = input.uv1;
    #endif
    return uv * _MainTex_ST.xy + _MainTex_ST.zw;
}

half TrainerAlpha(half4 mask)
{
    half alpha = lerp(1.0h, mask.r, _Mask);
    clip(alpha - _Cutoff);
    return alpha;
}

float3 TrainerAmbient(float3 normalWS)
{
    // Only LERP programs survived in these dumps; CLA is a scene-SH extension.
    float3 ambient = float3(0,0,0);
    if (_SHType < 0.5)
        ambient = SampleSH(normalWS);
    else
    {
        float2 weights = max(float2(normalWS.y + 1.0, normalWS.y - 1.0) *
                            float2(normalWS.y + 1.0, normalWS.y - 1.0) * 0.125, 0.0);
        ambient = weights.x * _SHTopColor.rgb + weights.y * _SHBotColor.rgb;
    }
    return ambient * _SHScale * _SHColorScale.rgb;
}

TrainerVaryings TrainerVertex(TrainerAttributes input)
{
    TrainerVaryings output = (TrainerVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = normal.normalWS;
    output.tangentWS = normal.tangentWS;
    output.bitangentWS = normal.bitangentWS;
    #if defined(TRAINER_HAIR)
        // The hair dump transforms the object-space bitangent as a normal.
        output.bitangentWS = TransformObjectToWorldNormal(cross(input.normalOS, input.tangentOS.xyz)) *
                             input.tangentOS.w * GetOddNegativeScale();
    #endif
    output.uv = TrainerUV(input);
    output.ambient = TrainerAmbient(normal.normalWS);
    // Both recovered X/Y keyword variants use world Y. Do not reinterpret X as
    // world X, which makes hair at x=0 black. Face's object-space option is retained.
    float height = position.positionWS.y;
    #if !defined(TRAINER_HAIR)
        if (_UseObjectSpace > 0.5) height = input.positionOS.y;
    #endif
    // Hair's log2(y) was undefined below zero. Clamp instead of generating NaNs.
    output.attenuation.x = pow(max(height, 0.0), max(_MainlightAttenuation, 0.0001));
    float addHeight = saturate((height + (_AddlightLerp * 2.0 - 1.0) * 5.0) * _AddlightAttenuation);
    output.attenuation.y = addHeight * addHeight;
    output.fogFactor = ComputeFogFactor(position.positionCS.z);
    return output;
}

float3 TrainerNormal(TrainerVaryings input, half4 packed)
{
    float3 normalTS;
    if (_PackedNormalEncoding > 0.5 && _PackedNormalEncoding < 1.5)
    {
        // Compatibility with the project's existing Unity Normal Map imports.
        normalTS = UnpackNormalScale(packed,_NormalScale);
    }
    else
    {
        // Direct RGB decode preserves source alpha. Undo sRGB sampling only when
        // the RGBA data texture was imported with sRGB enabled (encoding 2).
        float3 rgb = _PackedNormalEncoding > 1.5 ? LinearToSRGB(packed.rgb) : packed.rgb;
        normalTS = rgb * 2.0 - 1.0;
        normalTS.xy *= _NormalScale;
    }
    float3 mapped = normalTS.x * input.tangentWS + normalTS.y * input.bitangentWS + normalTS.z * input.normalWS;
    return dot(mapped,mapped) > 1e-8 ? SafeNormalize(mapped) : SafeNormalize(input.normalWS);
}

#if defined(TRAINER_HAIR)
float4 HairColorWeights(float alpha)
{
    return saturate(1.03191495 - abs((alpha + float4(0,-0.333333313,-0.666666687,-1)) * 3.19148946));
}
#endif

void TrainerSurface(half4 main, half4 packed, half4 mask, half alpha,
                    out float3 albedo, out float roughness, out float metallic)
{
    #if defined(TRAINER_HAIR)
        float4 weights = HairColorWeights(mask.a);
        float3 tint = weights.x * _color1.rgb + weights.y * _color2.rgb + weights.z * _color3.rgb + weights.w * _color4.rgb;
        tint = lerp(tint, float3(1,1,1), mask.g); // Not lerp(tint, 1-tint, mask.g).
        albedo = lerp(_colorSkin.rgb, main.rgb * tint, alpha);
        // Unity normal compression may replace alpha with normal X. In that
        // compatibility mode use the scalar metallic control, not the encoded X.
        float packedMetal = (_PackedNormalEncoding > 0.5 && _PackedNormalEncoding < 1.5) ? _MetallicOffset : packed.a;
        metallic = saturate(packedMetal + dot(weights,_metalv4) * (1.0 - mask.g));
        roughness = clamp(main.a + dot(weights,_roughv4) * (1.0 - mask.g),0.04,1.0);
        // Shared _MetallicOffset/_RoughNessOffset are unused by the hair programs.
    #else
        // Skin is dielectric: packed A is the skin-tint bypass, NOT metallic.
        float tintBypass = packed.a;
        if (_PackedNormalEncoding > 0.5 && _PackedNormalEncoding < 1.5)
            tintBypass = mask.a; // Preserve the previous conversion's tint fallback.
        albedo = main.rgb * lerp(_colorSkin.rgb,float3(1,1,1),tintBypass);
        metallic = 0.0;
        roughness = clamp(main.a * main.a + _RoughNessOffset,0.04,1.0);
    #endif
}

float3 TrainerEnvironment(float3 normalWS, float3 viewDir, float roughness)
{
    float mip = roughness * (1.7 - 0.7 * roughness) * 6.0;
    half4 probe = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0,samplerunity_SpecCube0,reflect(-viewDir,normalWS),mip);
    float r2 = roughness * roughness;
    return DecodeHDREnvironment(probe,unity_SpecCube0_HDR) / (r2 * r2 + 1.0);
}

float3 TrainerRim(float3 albedo, float3 normalWS, float ndotv, float roughness, float shadow)
{
    float grazing = 1.0 - ndotv;
    float k = (roughness + 1.0) * (roughness + 1.0) * 0.125;
    float rim = grazing / max(grazing * (1.0 - k) + k,1e-8);
    rim *= saturate(dot(normalWS,SafeNormalize(_RimLightDir.xyz)));
    float range = _RimlightScale2 - _RimlightScale;
    // A reversed range is intentional in some dumped materials; keep its sign.
    rim = abs(range) > 1e-5 ? saturate((rim - _RimlightScale) / range) : step(_RimlightScale,rim);
    rim = rim * rim * (3.0 - 2.0 * rim) * lerp(shadow,1.0,_RimlightShadowScale);
    return clamp(lerp(albedo * _RimlightColor.rgb,_RimlightColor.rgb,_RimlightColor.a) * rim,0.0,100.0);
}

float3 TrainerTone(float3 color)
{
    color = max(color,0.0);
    #if !defined(ENABLE_POST_TONE)
        float3 scaled = color * 0.45;
        color = min(scaled * (color * 1.1295 + 0.03) / (scaled * (color * 1.0935 + 0.59) + 0.08),100.0);
    #endif
    return color;
}

#if defined(TRAINER_HAIR)
float HairDistribution(float3 halfDir, float3 shiftedBitangent, float3 tangent, float3 normalWS,
                       float roughness, float glossX, float glossY)
{
    float2 axes = max(roughness * float2(glossX,glossY),0.0001);
    float x = dot(halfDir,shiftedBitangent) / axes.x;
    float y = dot(halfDir,tangent) / axes.y;
    float z = dot(halfDir,normalWS);
    float d = x * x + y * y + z * z;
    return rcp(max(PI * axes.x * axes.y * d * d,1e-8));
}
#else
float FaceAdditionalSpecular(float roughness, float ndotv, float ndotl, float ndoth, float vdoth)
{
    float r2 = roughness * roughness;
    float r4 = r2 * r2;
    float d = ndoth * ndoth * (r4 - 1.0) + 1.0;
    float distribution = r4 / max(PI * d * d,1e-8);
    float visibility = 0.5 / max(ndotl * (ndotv * (1.0 - r2) + r2) +
                                 ndotv * (ndotl * (1.0 - r2) + r2),1e-8);
    float fresnel = 0.04 + 0.96 * pow(1.0 - vdoth,5.0);
    return clamp(distribution * visibility * fresnel - 0.00006103515625,0.0,100.0);
}
#endif

half4 TrainerFragment(TrainerVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 mask = SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv);
    half alpha = TrainerAlpha(mask);
    half4 main = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    half4 packed = SAMPLE_TEXTURE2D(_BumpMapNr,sampler_BumpMapNr,input.uv);
    float3 albedo;
    float roughness, metallic;
    TrainerSurface(main,packed,mask,alpha,albedo,roughness,metallic);
    float3 normalWS = TrainerNormal(input,packed);
    float3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
    float ndotv = min(abs(dot(normalWS,viewDir)) + 0.004,1.0);
    float4 shadowCoord;
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
    #else
        shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #endif
    Light mainLight = GetMainLight(shadowCoord);
    float shadow = _ReceiveShadows > 0.5 ? mainLight.shadowAttenuation : 1.0;
    float ndotl = saturate(dot(normalWS,mainLight.direction));
    float3 halfDir = SafeNormalize(viewDir + mainLight.direction);
    float ndoth = saturate(dot(normalWS,halfDir));
    float vdoth = saturate(dot(viewDir,halfDir));
    float shapedDiffuse = saturate(pow(ndotl,max(_ShadowPow,0.0001)) + _ShadowOffset);
    float3 diffuse = albedo * (1.0 - metallic);
    float3 lightColor = mainLight.color * mainLight.distanceAttenuation;
    float3 color;
    float3 environment = TrainerEnvironment(normalWS,viewDir,roughness);

    #if defined(TRAINER_HAIR)
        float3 lit = min(lightColor * shadow * input.attenuation.x,1.0);
        float shape = SAMPLE_TEXTURE2D(_HairMap,sampler_HairMap,input.uv * _shapeST.xy + _shapeST.zw).r;
        float3 strand1 = SafeNormalize(input.bitangentWS + input.normalWS * (shape * _SunShift + _SunShiftOffuse1));
        float3 strand2 = SafeNormalize(input.bitangentWS + input.normalWS * (shape * _SunShift * 0.5 + _SunShiftOffuse2));
        float d1 = HairDistribution(halfDir,strand1,input.tangentWS,normalWS,roughness,_glossiness_1X,_glossiness_1Y);
        float d2 = HairDistribution(halfDir,strand2,input.tangentWS,normalWS,roughness,_glossiness_2X,_glossiness_2Y);
        float hairMask = SAMPLE_TEXTURE2D(_HairMap,sampler_HairMap,input.uv * _HairMap_ST.xy + _HairMap_ST.zw).g;
        float3 highlight = (max(diffuse * _specularColor1.rgb * d1,0.0) + max(diffuse * _specularColor2.rgb * d2,0.0)) * hairMask;
        // Angular shadow shaping belongs to diffuse, not the anisotropic lobes.
        color = diffuse * max(lit * shapedDiffuse,_ShadowScale) + highlight * max(lit,_ShadowScale);
        float3 specular = lerp(float3(0.04,0.04,0.04),albedo,metallic);
        float grazingTerm = min(1.0 + 0.04 + 0.96 * metallic - roughness,1.0);
        color += environment * lerp(specular,grazingTerm.xxx,pow(1.0 - ndotv,4.0));
        color += input.ambient * diffuse;
        // The recovered hair variants have no additional-light loop, even with
        // _ADDITIONAL_LIGHTS enabled. Do not add a different hair BRDF here.
    #else
        float r2 = roughness * roughness;
        float denominator = (ndoth * r2) * (ndoth * r2) + 1.0 - ndoth * ndoth;
        float distribution = r2 / max(denominator,1e-8);
        distribution = min(distribution * distribution * (roughness * 0.25 + 0.25),100.0);
        float3 specular = distribution * ndotl * 0.04 * _SpecularColor.rgb * 1.2;
        // Face main height attenuation is present in VS but unused by the dump's FS.
        float3 lit = min(lightColor * saturate(shadow + _faceshadowScale * 0.4),1.0);
        color = albedo * max(lit * shapedDiffuse,_ShadowScale) + specular * max(lit,_ShadowScale);
        color += environment * (0.04 + pow(1.0 - ndotv,4.0) * (1.0 - roughness));
        color += input.ambient * albedo;
    #endif

    float3 addLight = input.attenuation.y * _AddlightColor.rgb * saturate(dot(normalWS,SafeNormalize(_AddLightDir.xyz)));
    color += clamp(color * addLight,0.0,100.0); // Also boosts SH/probe, before rim/emission.
    if (_RimEnabled > 0.5)
        color += TrainerRim(albedo,normalWS,ndotv,roughness,shadow);
    float3 emission = main.rgb * mask.b * _Emissive * _EmisssionScale;
    color += emission;

    #if !defined(TRAINER_HAIR) && defined(_ADDITIONAL_LIGHTS)
        float3 additionalBRDF = albedo * INV_PI + FaceAdditionalSpecular(roughness,ndotv,ndotl,ndoth,vdoth);
        InputData inputData = (InputData)0;
        inputData.positionWS = input.positionWS;
        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
        #if USE_CLUSTER_LIGHT_LOOP
            UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS); ++lightIndex)
            {
                CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                Light light = GetAdditionalLight(lightIndex,input.positionWS,half4(1,1,1,1));
                color += additionalBRDF * light.color * saturate(dot(normalWS,light.direction)) * light.distanceAttenuation *
                         (_ReceiveShadows > 0.5 ? light.shadowAttenuation : 1.0);
            }
        #endif
        uint lightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(lightCount)
            Light light = GetAdditionalLight(lightIndex,input.positionWS,half4(1,1,1,1));
            color += additionalBRDF * light.color * saturate(dot(normalWS,light.direction)) * light.distanceAttenuation *
                     (_ReceiveShadows > 0.5 ? light.shadowAttenuation : 1.0);
        LIGHT_LOOP_END
    #endif

    // Debug variants were not included in the dumps; use raw channel previews.
    if (_Debug > 0.5)
    {
        if (_Debug < 1.5) return half4(albedo,alpha);
        if (_Debug < 2.5) return half4(normalWS * 0.5 + 0.5,alpha);
        if (_Debug < 3.5) return half4(metallic.xxx,alpha);
        if (_Debug < 4.5) return half4(roughness.xxx,alpha);
        if (_Debug < 5.5) return half4(shadow.xxx,alpha);
        if (_Debug < 6.5) return half4(emission,alpha);
        return half4(input.ambient,alpha);
    }
    return half4(MixFog(TrainerTone(color),input.fogFactor),alpha);
}

struct TrainerOutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 color : TEXCOORD1;
    half fogFactor : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TrainerOutlineVaryings TrainerOutlineVertex(TrainerAttributes input)
{
    TrainerOutlineVaryings output = (TrainerOutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 normalOS = input.normalOS;
    if (_VCOLOR2N > 0.5)
    {
        float3 encoded = input.color.rgb * 2.0 - 1.0;
        float3 bitangent = cross(input.normalOS,input.tangentOS.xyz) * input.tangentOS.w * GetOddNegativeScale();
        normalOS = input.tangentOS.xyz * encoded.x + bitangent * encoded.y + input.normalOS * encoded.z;
    }
    float3 geometricNormal = TransformObjectToWorldNormal(input.normalOS);
    float light = saturate(dot(geometricNormal,SafeNormalize(_lightDir.xyz)));
    float width;
    #if defined(TRAINER_HAIR)
        half4 outlineMask = half4(input.color.rgb,1);
        if (_VTEX > 0.5)
            outlineMask = SAMPLE_TEXTURE2D_LOD(_OutlineMap,sampler_OutlineMap,input.uv0,0);
        output.color = (_OutlineColor.rgb + light * (1.0 - _OutlineColor.rgb)) * outlineMask.rgb * _color1.rgb;
        width = _OutlineWidth * outlineMask.a / 216.0;
    #else
        output.color = (light * 0.5 + 0.5) * _OutlineColor.rgb;
        width = _OutlineWidth * input.color.a;
    #endif
    float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
    float2 direction = mul((float3x3)UNITY_MATRIX_VP,TransformObjectToWorldDir(normalOS,false)).xy;
    direction *= rsqrt(max(dot(direction,direction),1e-8));
    float referenceHeight = _ScreenResolution.y > 0.0 ? _ScreenResolution.y : 1080.0;
    positionCS.xy += direction / _ScreenParams.xy * referenceHeight * positionCS.w * width;
    // The dump uses +0.01/w; flip the sign for URP's reversed-Z depth buffer.
    float depthOffset = 0.01 * (-_Offset / 5.0) / max(abs(positionCS.w),0.0001);
    #if UNITY_REVERSED_Z
        positionCS.z -= depthOffset;
    #else
        positionCS.z += depthOffset;
    #endif
    output.positionCS = positionCS;
    output.uv = TrainerUV(input);
    output.fogFactor = ComputeFogFactor(positionCS.z);
    return output;
}

half4 TrainerOutlineFragment(TrainerOutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    clip(_OutlineEnabled - 0.5);
    TrainerAlpha(SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv));
    return half4(MixFog(max(input.color,0.0),input.fogFactor),1);
}

float3 _LightDirection;
float3 _LightPosition;

TrainerVaryings TrainerShadowVertex(TrainerAttributes input)
{
    TrainerVaryings output = TrainerVertex(input);
    float3 direction = _LightDirection;
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        direction = SafeNormalize(_LightPosition - output.positionWS);
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
    output.positionCS = ApplyShadowClamping(output.positionCS);
    return output;
}

half4 TrainerShadowFragment(TrainerVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    TrainerAlpha(SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv));
    return 0;
}

half TrainerDepthFragment(TrainerVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TrainerAlpha(SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv));
    return input.positionCS.z;
}

half4 TrainerDepthNormalsFragment(TrainerVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TrainerAlpha(SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv));
    float3 normalWS = TrainerNormal(input,SAMPLE_TEXTURE2D(_BumpMapNr,sampler_BumpMapNr,input.uv));
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octNormal = PackNormalOctQuadEncode(normalWS);
        return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)),0);
    #else
        return half4(normalWS,0);
    #endif
}

half4 Trainer2DFragment(TrainerVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 mask = SAMPLE_TEXTURE2D(_MixMap,sampler_MixMap,input.uv);
    half alpha = TrainerAlpha(mask);
    float3 albedo;
    float roughness,metallic;
    TrainerSurface(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv),
                   SAMPLE_TEXTURE2D(_BumpMapNr,sampler_BumpMapNr,input.uv),mask,alpha,albedo,roughness,metallic);
    return half4(albedo,alpha);
}

#endif
