#ifndef CHINESE_LOBBY_ENVIRONMENT_SKY
#define CHINESE_LOBBY_ENVIRONMENT_SKY

#include "EnvironmentCommon.hlsl"

half4 EnvironmentSkyFragment(EnvironmentVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float3 direction = normalize(input.positionWS - GetCameraPositionWS());
    float elevation = saturate(direction.y + _heightAdjust);
    float horizon = exp2(-abs(direction.y) * max(_horizonWidth, 0.001));
    float sunAzimuth = radians(_rimAdjust.x);
    float3 sunDirection = normalize(float3(sin(sunAzimuth), _rimRange, cos(sunAzimuth)));
    float towardSun = saturate(dot(direction, sunDirection) * 0.5 + 0.5);
    float frontBack = lerp(towardSun, 1.0 - towardSun, saturate(_FrontBack));
    half3 low = lerp(_Color7.rgb, _Color6.rgb, frontBack);
    half3 middle = lerp(_Color2.rgb, _Color3.rgb, frontBack);
    half3 high = lerp(_Color0.rgb, _Color4.rgb, frontBack);
    half3 sky = lerp(low, middle, smoothstep(0.0, 0.5, elevation));
    sky = lerp(sky, high, smoothstep(0.5, 1.0, elevation));
    sky = lerp(sky, lerp(_Color8.rgb, _Color9.rgb, frontBack), horizon * saturate(_horizonAdjust.y));
    float2 uv = input.uv * _CloudTex_ST.xy + _CloudTex_ST.zw;
    float2 noiseUV = input.uv * _NoiseTilling + _Time.y * float2(_NoiseMoveX, _NoiseMoveY);
    half2 distortion = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rg * 2.0h - 1.0h;
    half4 cloudMap = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv + distortion * _NoiseIntensity);
    half cloud = saturate(cloudMap.r + _CloudRange);
    half layer = smoothstep(_LayerCloudMask, 1.0h, cloudMap.g) * saturate(_LayerCloud);
    half3 cloudColor = lerp(_Color10.rgb, _Color1.rgb, frontBack);
    cloudColor = lerp(cloudColor, _Color11.rgb, layer);
    sky = lerp(sky, cloudColor, saturate(cloud + layer));
    half rim = smoothstep(1.0h - _rimRange - max(_rimSoft, 0.0001h), 1.0h - _rimRange, dot(direction, sunDirection));
    sky += lerp(_Color5.rgb, _Color12.rgb, frontBack) * rim * horizon;
    return half4(sky * _MainColor.rgb * _Intensity, 1);
}

#endif
