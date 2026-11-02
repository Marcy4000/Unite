#ifndef CHINESE_LOBBY_FX_COMMON_INCLUDED
#define CHINESE_LOBBY_FX_COMMON_INCLUDED

TEXTURE2D(_Tex1);
SAMPLER(sampler_Tex1);
#if defined(FX_SECOND_TEXTURE)
TEXTURE2D(_Tex2);
SAMPLER(sampler_Tex2);
#endif
#if defined(FX_THIRD_TEXTURE)
TEXTURE2D(_Tex3);
SAMPLER(sampler_Tex3);
#endif

struct FxAttributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FxVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv1 : TEXCOORD0;
    float2 uv2 : TEXCOORD1;
    float2 uv3 : TEXCOORD2;
    float2 rawUV : TEXCOORD3;
    half4 color : TEXCOORD4;
#if defined(FX_FRESNEL)
    float3 normalWS : TEXCOORD5;
    float3 viewDirectionWS : TEXCOORD6;
#endif
    UNITY_VERTEX_OUTPUT_STEREO
};

float2 FxScrollUV(float2 uv, float4 transform, float2 speed)
{
    return uv * transform.xy + transform.zw + frac(_TimeParameters.x * speed);
}

FxVaryings FxVertex(FxAttributes input)
{
    FxVaryings output = (FxVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
#if UNITY_REVERSED_Z
    output.positionCS.z += _Zoffset / output.positionCS.w;
#else
    output.positionCS.z -= _Zoffset / output.positionCS.w;
#endif
    output.rawUV = input.uv.xy;
    output.color = input.color;
#if defined(FX_BASE)
    output.uv1 = input.uv.xy * _tex1_st.xy + _tex1_st.zw;
#elif defined(FX_SCROLL)
    output.uv1 = FxScrollUV(input.uv.xy, _tex1_st, _Tex1_Scroll.xy);
#else
    output.uv1 = FxScrollUV(input.uv.xy, _tex1_st, _Tex1_Speed.xy);
#endif
#if defined(FX_SECOND_TEXTURE)
    output.uv2 = FxScrollUV(input.uv.xy, _tex2_st, _Tex2_Speed.xy);
#endif
#if defined(FX_THIRD_TEXTURE)
    output.uv3 = FxScrollUV(input.uv.xy, _tex3_st, _Tex3_Speed.xy);
#endif
#if defined(FX_FRESNEL)
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.viewDirectionWS = GetWorldSpaceViewDir(positionWS);
#endif
    return output;
}

#if defined(FX_DISSOLVE) || defined(FX_QUICK_MASK)
half FxCubicRamp(half value, half edge)
{
    half denominator = 2.0h * edge - 1.0h;
    half ramp = denominator == 0.0h ? step(1.0h - edge, value)
        : saturate((value - (1.0h - edge)) / denominator);
    return ramp * ramp * (3.0h - 2.0h * ramp);
}
#endif

half4 FxFragment(FxVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 mainUV = input.uv1;
#if defined(FX_NOISE)
    half2 noiseSample = SAMPLE_TEXTURE2D(_Tex2, sampler_Tex2, input.uv2).rg;
    mainUV = FxScrollUV(input.rawUV, _tex1_st, _Tex1_Speed.xy)
        + half2(noiseSample.r, 1.0h - noiseSample.g) * _Noise1;
#elif defined(FX_NOISE_DISSOLVE)
    half2 noiseSample = SAMPLE_TEXTURE2D(_Tex3, sampler_Tex3, input.uv3).rg;
    mainUV = FxScrollUV(input.rawUV, _tex1_st, _Tex1_Speed.xy)
        + half2(noiseSample.r, 1.0h - noiseSample.g) * _noise1;
#endif
    half4 mainSample = SAMPLE_TEXTURE2D(_Tex1, sampler_Tex1, mainUV);
    half3 textureColor = mainSample.rgb;
#if defined(FX_BASE)
    textureColor = pow(textureColor, _Power1);
#endif
    half3 tint = _Color1.rgb;
#if defined(FX_LERP_COLOR)
    tint = lerp(tint, _Color2.rgb, abs(sin(_TimeParameters.x * _ColorLerp)));
#endif
#if defined(FX_SCROLL_EMISSION)
    half3 resultColor = textureColor * Vector1_AC3F40EB * tint;
#else
    half3 resultColor = textureColor * _Emission1 * tint;
#endif
#if defined(FX_FRESNEL)
    half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewDirectionWS)));
    half fresnel = pow(1.0h - facing, _FresnelPower1);
    resultColor += fresnel * _FresnelColor1.rgb * _FresnelEmission1;
#endif
    resultColor *= input.color.rgb;
    half opacity = mainSample.a * input.color.a;
#if defined(FX_MASK)
    opacity *= SAMPLE_TEXTURE2D(_Tex2, sampler_Tex2, input.uv2).r;
#endif
#if defined(FX_NOISE_MASK)
    opacity *= SAMPLE_TEXTURE2D(_Tex3, sampler_Tex3, input.uv3).r;
#endif
#if defined(FX_DISSOLVE)
    half dissolveSample = SAMPLE_TEXTURE2D(_Tex2, sampler_Tex2, input.uv2).r;
    half dissolve = FxCubicRamp(dissolveSample + 2.0h * _DissolveOffset1 - 1.0h, _DissolveEdge1);
    #if defined(FX_FRESNEL)
        opacity = dissolve * input.color.a;
    #else
        opacity *= dissolve;
    #endif
#endif
#if defined(FX_QUICK_MASK)
    half coordinate = _Vmask != 0.0 ? input.rawUV.y : input.rawUV.x;
    half maskValue = saturate(coordinate + _maskoffset)
        * saturate((coordinate - 0.5h) * (1.1920929e-7h - 1.0h) + _maskoffset + 0.5h);
    opacity *= _use_mask != 0.0 ? FxCubicRamp(maskValue, _maskedge) : 1.0h;
#endif
    resultColor *= _IF_ADD_MODE != 0.0 ? opacity : 1.0h;
#if defined(FX_MISSPELLED_ALPHA)
    half resultAlpha = opacity * _Aplha1;
#else
    half resultAlpha = opacity * _Alpha1;
#endif
#if defined(FX_BASE_HALF)
    resultAlpha = _DstBlendFactor * _SrcBlendFactor == 1.0h ? 0.001h : resultAlpha;
#endif
    return half4(resultColor, resultAlpha);
}

#endif
