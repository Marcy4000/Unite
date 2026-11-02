#ifndef CHINESE_LOBBY_FX_HOLOGRAPHIC_INCLUDED
#define CHINESE_LOBBY_FX_HOLOGRAPHIC_INCLUDED

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MixMap);
SAMPLER(sampler_MixMap);
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

struct FxHolographicAttributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FxHolographicVaryings
{
    float4 positionCS : SV_POSITION;
    half4 color : TEXCOORD0;
    float2 detailUV : TEXCOORD1;
    float2 baseUV : TEXCOORD2;
    half flicker : TEXCOORD3;
    half fog : TEXCOORD4;
    UNITY_VERTEX_OUTPUT_STEREO
};

float FxHolographicHash(float value)
{
    return frac(sin(dot(float2(value, value), float2(12.9898005, 78.2330017))) * 43758.5469);
}

float3 FxHolographicPosition(float3 positionOS, out half flicker)
{
    float phase = frac(_Time.x);
    float minimum = abs(sin(phase * 15.0)) <= 0.300000012 ? _minlight : 1.0;
    flicker = lerp(minimum, 1.0, FxHolographicHash(phase));
    float block = floor((phase + positionOS.y) * _BlockSize);
    return positionOS + flicker * 0.1h * FxHolographicHash(block);
}

FxHolographicVaryings FxHolographicVertex(FxHolographicAttributes input)
{
    FxHolographicVaryings output = (FxHolographicVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionOS = FxHolographicPosition(input.positionOS, output.flicker);
    float3 positionWS = TransformObjectToWorld(positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(positionWS));
    half rim = 1.0h - saturate(dot(viewDirectionWS, normalWS));
    output.color = lerp(_BaseColor, _RimColor, rim * rim);
    float2 detailUV = input.uv;
#if !defined(_WSUV_ON)
    float4 screenPosition = ComputeScreenPos(output.positionCS);
    detailUV = screenPosition.xy / screenPosition.w;
#endif
    float phase = frac(_Time.x);
    output.detailUV = detailUV * _MainTexST.xy + phase * _MainTexST.zw;
    output.baseUV = input.uv * _MixMapST.xy + phase * _MixMapST.zw;
    output.fog = ComputeFogFactor(output.positionCS.z);
    return output;
}

half4 FxHolographicFragment(FxHolographicVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 resultColor = input.color;
    resultColor.a *= input.flicker;
    resultColor *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.detailUV);
#if defined(_TEX_ON)
    resultColor *= SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, input.baseUV);
#endif
    resultColor.rgb = MixFog(resultColor.rgb, input.fog);
    return resultColor;
}

float4 FxHolographicStencilVertex(FxHolographicAttributes input) : SV_POSITION
{
    UNITY_SETUP_INSTANCE_ID(input);
    half flicker;
    return TransformObjectToHClip(FxHolographicPosition(input.positionOS, flicker));
}

float4 FxHolographicDepthVertex(FxHolographicAttributes input) : SV_POSITION
{
    UNITY_SETUP_INSTANCE_ID(input);
    return TransformObjectToHClip(input.positionOS);
}

half4 FxHolographicStencilFragment() : SV_Target
{
    return 1.0h;
}

half4 FxHolographicDepthFragment() : SV_Target
{
    return 0.0h;
}

FxHolographicVaryings FxHolographic2DVertex(FxHolographicAttributes input)
{
    FxHolographicVaryings output = (FxHolographicVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.baseUV = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return output;
}

half4 FxHolographic2DFragment(FxHolographicVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV) * _BaseColor;
}

#endif
