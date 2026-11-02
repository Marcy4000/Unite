#ifndef CHINESE_LOBBY_ENVIRONMENT_COMMON
#define CHINESE_LOBBY_ENVIRONMENT_COMMON

#if defined(ENV_WINDOW) || defined(ENV_MESHBLEND) || defined(ENV_SEA) || defined(ENV_INTERWATER)
    #ifndef _SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT 1
    #endif
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "EnvironmentInput.hlsl"

float4 _ExposureColor;
float4 _GIDiffuseColor;
float4 _GISpecularColor;
float4 _DarkCorner;
float4 _MainHighLightOffset;
float _PostUVFlip1;
float _EnvironmentGlobalsConfigured;
float3 _LightDirection;
float3 _LightPosition;

struct EnvironmentAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct EnvironmentVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 tangentWS : TEXCOORD2;
    float2 uv : TEXCOORD3;
    float2 uv1 : TEXCOORD4;
    float4 color : TEXCOORD5;
    float fog : TEXCOORD6;
    float3 viewWS : TEXCOORD7;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 8);
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 EnvironmentGlobalOrWhite(float3 value)
{
    return (_EnvironmentGlobalsConfigured > 0.5 || dot(abs(value), 1.0.xxx) > 0.00001) ? value : 1.0.xxx;
}

float EnvironmentHash(float2 position)
{
    return frac(sin(dot(position, float2(12.9898005, 78.2330017))) * 43758.5469);
}

float EnvironmentNoise(float2 position)
{
    float2 cell = floor(position);
    float2 blend = frac(position);
    blend = blend * blend * (3.0 - 2.0 * blend);
    return lerp(lerp(EnvironmentHash(cell), EnvironmentHash(cell + float2(1, 0)), blend.x),
                lerp(EnvironmentHash(cell + float2(0, 1)), EnvironmentHash(cell + 1.0), blend.x), blend.y);
}

float3 EnvironmentDeform(EnvironmentAttributes input)
{
    float3 position = input.positionOS.xyz;
#if defined(ENV_INTERWATER)
    float2 waveUV = (input.uv + _Time.x * _causticscaleoffset.zw) * _WaveScale;
    float wave = EnvironmentNoise(waveUV) * 0.125 + EnvironmentNoise(waveUV * 0.5) * 0.25
               + EnvironmentNoise(waveUV * 0.25) * 0.5;
    position.y += (wave * 2.0 - 1.0) * _WaveHeight;
#elif defined(ENV_BALLOON)
    float phase = _Time.y * _WindSpeed + dot(position, 1.0.xxx) * _SpeedDifference;
    position += sin(phase) * _WindStrength * _WindDirection.xyz * input.color.a;
#elif defined(ENV_FOLIAGE)
    float phase = _Time.y * _WindSpeed + dot(position, _Frequency.xyz) / max(_WaveLength, 0.001);
    float wind = sin(phase) + sin(phase * 1.73) * _WindNoise;
    position += _IFWIND * wind * _Amplitude.xyz * _WindDir.xyz * input.color.a * _WindRang;
#elif defined(ENV_SEA)
    float2 waveUV = TransformObjectToWorld(position).xz / max(abs(_WaterRippleRatio), 0.001);
    float wave = sin(dot(waveUV, _WaterRippleData.xy) + _Time.y * _WaterRippleData.z);
    position.y += wave * _WaterRippleData.w * _WaterRippleHeightScale * input.color.a * 0.01;
#endif
    return position;
}

EnvironmentVaryings EnvironmentVertex(EnvironmentAttributes input)
{
    EnvironmentVaryings output = (EnvironmentVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionOS = EnvironmentDeform(input);
    VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
    VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = positions.positionCS;
    output.positionWS = positions.positionWS;
    output.normalWS = normals.normalWS;
    output.tangentWS = float4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = input.uv;
    output.uv1 = input.uv1;
    output.color = input.color;
    output.viewWS = GetWorldSpaceNormalizeViewDir(positions.positionWS);
    output.fog = ComputeFogFactor(positions.positionCS.z);
    OUTPUT_LIGHTMAP_UV(input.uv1, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(normals.normalWS, output.vertexSH);
#if defined(ENV_SHADOW_PASS)
    float3 lightDirection = _LightDirection;
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        lightDirection = normalize(_LightPosition - positions.positionWS);
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positions.positionWS, normals.normalWS, lightDirection));
    #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
    #else
        output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
    #endif
#endif
    return output;
}

float2 EnvironmentMainUV(EnvironmentVaryings input)
{
#if defined(ENV_LM) || defined(ENV_INTERWATER)
    return input.uv * _UVScallePaner.xy + _UVScallePaner.zw;
#elif defined(ENV_SEA)
    return input.positionWS.xz * _MainTex_ST.xy + _MainTex_ST.zw;
#else
    return input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
#endif
}

half EnvironmentAlpha(EnvironmentVaryings input, half textureAlpha)
{
#if defined(ENV_LM)
    return lerp(1.0h, textureAlpha, saturate(_IFMask));
#elif defined(ENV_MESHBLEND)
    return saturate((textureAlpha + _AlphaOffset) * input.color.a);
#else
    return lerp(1.0h, saturate(textureAlpha + _AlphaOffset), saturate(_UseAlphaInput));
#endif
}

void EnvironmentClip(half alpha)
{
    if (max(_UseAlphaClip, _AlphaClip) > 0.5) clip(alpha - _Cutoff);
}

float3 EnvironmentNormal(EnvironmentVaryings input, half4 normalSample, float strength)
{
    float3 normalTS = normalSample.xyz * 2.0 - 1.0;
    normalTS.xy *= strength;
    float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
    return SafeNormalize(input.tangentWS.xyz * normalTS.x + bitangentWS * normalTS.y + input.normalWS * normalTS.z);
}

half EnvironmentSpecular(float3 normalWS, float3 halfWS, half roughness)
{
    half normalHalf = saturate(dot(normalWS, halfWS));
    half roughness2 = roughness * roughness;
    half denominator = max(normalHalf * normalHalf * (roughness2 * roughness2 - 1.0h) + 1.0h, 0.001h);
    half distribution = roughness2 / denominator;
    return min(distribution * distribution * (roughness * 0.25h + 0.25h), 100.0h);
}

half3 EnvironmentDirect(Light light, float3 normalWS, float3 viewWS, half3 diffuse, half3 specular, half roughness)
{
    float3 highlightWS = SafeNormalize(light.direction + _MainHighLightOffset.xyz + _LocalLightOffset.xyz);
    float3 halfWS = SafeNormalize(highlightWS + viewWS);
    half normalLight = saturate(dot(normalWS, light.direction));
    half3 highlight = EnvironmentSpecular(normalWS, halfWS, roughness) * specular
                    * _SpecularColor.rgb * EnvironmentGlobalOrWhite(_GISpecularColor.rgb);
    highlight *= 1.0h - saturate(_DisableSpecularHighlight);
    return (diffuse + highlight) * normalLight * light.color * light.distanceAttenuation * light.shadowAttenuation;
}

half3 EnvironmentReflection(float3 reflectedWS, float3 positionWS, half roughness)
{
    half mip = roughness * (1.7h - 0.7h * roughness) * 6.0h;
    half4 encoded = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectedWS, mip);
    half3 probe = DecodeHDREnvironment(encoded, unity_SpecCube0_HDR);
#if defined(ENV_MIRROR) || defined(ENV_WINDOW) || defined(ENV_SEA)
    if (_Use_Reflection_Texture > 0.5 && _ReflectionCubeAvailable > 0.5)
    {
        half4 customReflection = SAMPLE_TEXTURECUBE_LOD(_ReflectionCube, sampler_ReflectionCube, reflectedWS, mip);
        probe = customReflection.rgb;
    }
#endif
    return probe;
}

half3 EnvironmentFinish(half3 color, EnvironmentVaryings input)
{
    float3 viewAxis = UNITY_MATRIX_V[2].xyz;
    float corner = pow(saturate(abs(dot(viewAxis, input.viewWS))), 16.0);
    color *= lerp(EnvironmentGlobalOrWhite(_DarkCorner.rgb), 1.0.xxx, corner);
    color *= EnvironmentGlobalOrWhite(_ExposureColor.rgb);
    if (_EnvironmentLegacyToneMap > 0.5)
        color = pow(max(color / (color + 0.155h) * 1.019h, 0.0h), 2.2h);
    if (_DisableMainCityFogMode < 0.5) color = MixFog(color, input.fog);
    return color;
}

half4 EnvironmentSurfaceFragment(EnvironmentVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = EnvironmentMainUV(input);
    half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
#if !defined(ENV_LM)
    if (_UseMainMap < 0.5) main = 1.0h.xxxx;
#endif
    half alpha = EnvironmentAlpha(input, main.a);
    EnvironmentClip(alpha);
    half3 albedo = main.rgb * _MainColor.rgb;
    float3 normalWS = normalize(input.normalWS);
    half roughness = saturate(1.0h + _RoughnessOffset);
    half metallic = saturate(_Metallic + _MetallicOffset);
    half occlusion = 1.0h;
    half3 emission = 0.0h;
#if defined(ENV_LM)
    half4 packed = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, uv);
    half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
    normalWS = EnvironmentNormal(input, normalSample, saturate(_NormalStrong));
    roughness = saturate(packed.g + _RoughNessOffset);
    metallic = saturate(packed.r + _MetallicOffset);
    occlusion = sqrt(max(packed.b, 0.0h));
    half3 maskColor = lerp(_MaskColor.rgb, _MaskColor2.rgb, _PostUVFlip1 * (1.0h - 2.0h * _MaskFlip) + _MaskFlip);
    albedo = main.rgb * lerp(1.0h.xxx, maskColor, saturate(main.a * 2.0h - 1.0h));
    emission = main.rgb * packed.a * _IFEmiss * _EmissColor.rgb;
#elif defined(ENV_METAL)
    half4 packed = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.uv * _MixMap_ST.xy + _MixMap_ST.zw);
    if (_UseMixMap > 0.5)
    {
        roughness = saturate(packed.g + _RoughnessOffset);
        metallic = saturate(packed.r + _MetallicOffset);
        occlusion = lerp(1.0h, sqrt(max(packed.b, 0.0h)), saturate(_OcclusionScale));
        emission = main.rgb * packed.a * _EmissionColor.rgb;
    }
#endif
#if !defined(ENV_LM)
    half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv * _NormalMap_ST.xy + _NormalMap_ST.zw);
    if (_UseNormalMap > 0.5) normalWS = EnvironmentNormal(input, normalSample, _NormalScale);
    #if defined(ENV_GLOSS) || defined(ENV_MIRROR) || defined(ENV_FOLIAGE) || defined(ENV_MIX)
        if (_UseNormalMap > 0.5) roughness = saturate(normalSample.a + _RoughnessOffset);
    #endif
#endif
#if defined(ENV_MIX)
    half4 layer0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * _MainTex_ST.xy + _MainTex_ST.zw);
    half4 layer1 = SAMPLE_TEXTURE2D(_BlendTex, sampler_BlendTex, input.uv * _BlendTex_ST.xy + _BlendTex_ST.zw);
    half2 heights = half2(layer0.a, layer1.a) + input.color.rg;
    half2 weights = max(heights - max(heights.x, heights.y) + max(_HeightBlendWeight, 0.0001h), 0.0h);
    weights /= max(weights.x + weights.y, 0.0001h);
    albedo = (layer0.rgb * _Color0.rgb * weights.x + layer1.rgb * _Color1.rgb * weights.y) * _MainColor.rgb;
    metallic = saturate(dot(weights, half2(_Layer0Metaloffset, _Layer1Metaloffset)));
    roughness = saturate(normalSample.a + dot(weights, half2(_Layer0Roughoffset, _Layer1Roughoffset)) + _RoughnessOffset);
    float2 normalUV = lerp(input.uv * _MainTex_ST.xy + _MainTex_ST.zw, input.uv * _BlendTex_ST.xy + _BlendTex_ST.zw, _IFUseUV1);
    normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV);
    normalWS = EnvironmentNormal(input, normalSample, dot(weights, half2(_Layer1Normal1, _Layer2Normal1)) * _NormalScale);
#endif
#if defined(ENV_FOLIAGE)
    normalWS *= IS_FRONT_VFACE(face, 1.0, -1.0);
    normalWS = SafeNormalize(lerp(normalWS, float3(0, 1, 0), saturate(_TreeSmoothNormalFactor)));
#endif
    roughness = clamp(roughness, 0.04h, 1.0h);
    float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
    if (_DisableReceiveShadow > 0.5) mainLight.shadowAttenuation = 1.0h;
    half3 specular = lerp(0.04h.xxx, albedo, metallic);
    half3 diffuse = albedo * (1.0h - metallic);
    half3 direct = EnvironmentDirect(mainLight, normalWS, viewWS, diffuse, specular, roughness);
    half3 baked = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
    baked = max(baked, _EnvironmentAmbientFloor.xxx);
    half bakedShadow = lerp(1.0h, saturate(input.color.r), saturate(_CustomBakedShadowScale));
    half3 indirect = baked * diffuse * (_GIDiffuseColor.rgb + 1.0h) * _CustomLightMapScale * bakedShadow;
#if defined(ENV_LM)
    half4 lightmap = SAMPLE_TEXTURE2D(_LightmapTex, sampler_LightmapTex, input.uv1);
    half normalBend = clamp(normalWS.y * 0.5h + 0.75h, 0.5h, 1.25h);
    normalBend = lerp(normalBend, 1.0h, lightmap.a);
    direct *= lightmap.a;
    indirect = lightmap.rgb * 4.84h * (_GIDiffuseColor.rgb + 1.0h) * diffuse * normalBend;
#else
    if (_UseCustomLightMap > 0.5)
    {
        half4 lightmap = SAMPLE_TEXTURE2D(_LightmapTex, sampler_LightmapTex, input.uv1);
        indirect = lightmap.rgb * 4.84h * diffuse * _CustomLightMapScale;
        direct *= lightmap.a;
    }
#endif
#if defined(_ADDITIONAL_LIGHTS)
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, input.positionWS);
        direct += EnvironmentDirect(light, normalWS, viewWS, diffuse, specular, roughness);
    LIGHT_LOOP_END
#endif
    half3 reflection = EnvironmentReflection(reflect(-viewWS, normalWS), input.positionWS, saturate(roughness + _ReflectionRoughness));
    reflection *= specular / (pow(roughness, 4.0h) + 1.0h);
#if !defined(ENV_LM)
    reflection *= _ReflectionColor.rgb * _ReflectionOcclusion;
    #if defined(ENV_MIRROR) || defined(ENV_WINDOW)
        reflection *= saturate(1.0h - _DisableEnvironmentReflection + step(0.5h, _Use_Reflection_Texture));
    #else
        reflection *= 1.0h - saturate(_DisableEnvironmentReflection);
    #endif
#endif
    half3 color = (direct + indirect + reflection) * occlusion + emission;
#if defined(ENV_FOLIAGE)
    half normalLight = saturate(dot(normalWS, mainLight.direction));
    half3 treeTint = lerp(_TreeShadowTint.rgb, _TreeHighlightTint.rgb, normalLight);
    color *= lerp(1.0h.xxx, treeTint, saturate(_TreeHighlightTint.a));
    color += albedo * _TreeAmbientTint.rgb * _TreeAmbientTint.a;
    color += baked * albedo * _TreeIndirectTint.rgb * _TreeIndirectTint.a;
    half backlight = saturate(dot(-normalWS, mainLight.direction));
    color += albedo * _SSSColor.rgb * mainLight.color * backlight;
    half rim = pow(1.0h - saturate(dot(normalWS, viewWS)), 4.0h);
    color += rim * _TreeFresnelTint.rgb * _TreeFresnelTint.a;
    if (_VLM > 0.5) color *= input.color.rgb;
#endif
#if defined(ENV_BUILDING)
    color += main.a * _EmissionColor.rgb;
#elif defined(ENV_BILLBOARD)
    color = albedo + main.a * _EmissionColor.rgb;
#elif defined(ENV_FARPLANE)
    color = albedo;
#elif defined(ENV_MIRROR) || defined(ENV_WINDOW)
    half glassFacing = pow(1.0h - saturate(dot(normalWS, viewWS)), max(_GlassColorAngle * 8.0h, 0.001h));
    color = lerp(color, color * _GlassColor.rgb * _GlassColorIntensity, saturate(_UseGlassRendering));
    color += reflection * glassFacing;
#endif
    if (_UseUnlitMode > 0.5) color = albedo + emission;
    color = EnvironmentFinish(color, input);
    if (_SrcBlend == 1.0h && _DstBlend == 10.0h && _DisablePremultipliedAlpha < 0.5h) color *= alpha;
    return half4(color, alpha);
}

half4 EnvironmentDepthFragment(EnvironmentVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    #if defined(ENV_SHADOW_PASS)
        clip(0.5h - _DisableShadowCaster);
    #endif
    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, EnvironmentMainUV(input)).a;
    EnvironmentClip(EnvironmentAlpha(input, alpha));
    return 0;
}

half4 EnvironmentDepthNormalsFragment(EnvironmentVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, EnvironmentMainUV(input)).a;
    EnvironmentClip(EnvironmentAlpha(input, alpha));
    float3 normalWS = normalize(input.normalWS);
#if defined(ENV_LM)
    half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, EnvironmentMainUV(input));
    normalWS = EnvironmentNormal(input, normalSample, saturate(_NormalStrong));
#else
    if (_UseNormalMap > 0.5)
    {
        half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv * _NormalMap_ST.xy + _NormalMap_ST.zw);
        normalWS = EnvironmentNormal(input, normalSample, _NormalScale);
    }
#endif
#if defined(ENV_FOLIAGE)
    normalWS *= IS_FRONT_VFACE(face, 1.0, -1.0);
#endif
#if defined(_GBUFFER_NORMALS_OCT)
    float2 encoded = PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5;
    return half4(PackFloat2To888(saturate(encoded)), 0);
#else
    return half4(normalWS, 0);
#endif
}

#endif
