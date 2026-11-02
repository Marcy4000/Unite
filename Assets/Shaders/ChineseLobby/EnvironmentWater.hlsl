#ifndef CHINESE_LOBBY_ENVIRONMENT_WATER
#define CHINESE_LOBBY_ENVIRONMENT_WATER

#include "EnvironmentCommon.hlsl"

half4 EnvironmentWaterFragment(EnvironmentVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    Light mainLight = GetMainLight();
    float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(ENV_INTERWATER)
    float4 bumpUV = input.positionWS.xzxz * _BumpTiling + frac(_Time.x * _BumpDirection);
    half2 normal0 = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV.xy).rg * 2.0h - 1.0h;
    half2 normal1 = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV.zw).rg * 2.0h - 1.0h;
    half2 perturbation = (normal0 + normal1 * 0.5h) * _NormalScale;
    float3 normalWS = normalize(float3(perturbation.x, 1, perturbation.y));
    float2 causticUV = input.positionWS.xz / max(abs(_causticscaleoffset.xy), 0.0001.xx)
                     * sign(_causticscaleoffset.xy) + frac(_Time.x * _causticscaleoffset.zw);
    half3 caustic = SAMPLE_TEXTURE2D(_Caustic, sampler_Caustic, causticUV + normalWS.xz * _CausticScale).rgb;
    caustic *= _CausticColor.rgb;
    half3 ambient = max(SampleSH(mainLight.color), _EnvironmentAmbientFloor.xxx) * (_GIDiffuseColor.rgb + 1.0h);
    half3 color = caustic * (ambient + mainLight.color);
    half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), 4.0h);
    float3 reflected = normalize(reflect(viewWS, normalWS));
    float2 paraboloidUV = float2(reflected.x, -reflected.z) / max(reflected.y + 1.02, 0.0001) * 0.5 + 0.5;
    color += SAMPLE_TEXTURE2D(_FrontDualParaboloidMap, sampler_FrontDualParaboloidMap, paraboloidUV).rgb
           * _SpecularColor.rgb * fresnel;
    half2 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, EnvironmentMainUV(input)).rg;
    half foam = saturate((perturbation.x - mask.r + 1.0h - _foam1) / max(_foam2 - _foam1, 0.0001h));
    foam = foam * foam * (3.0h - 2.0h * foam);
    color += foam * _FoamColor.rgb;
    half alpha = saturate(_CausticColor.a * mask.g + foam * _FoamColor.a);
#else
    float2 normalUV = input.positionWS.xz * _NormalMap_ST.xy + _NormalMap_ST.zw;
    float2 rippleSpeed = _WaterRippleData.zw * _Time.x;
    half2 normal0 = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV + rippleSpeed).rg * 2.0h - 1.0h;
    half2 normal1 = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV * _WaterRippleData.xy - rippleSpeed).rg * 2.0h - 1.0h;
    half2 perturbation = (normal0 + normal1 * 0.5h) * _NormalScale;
    float3 normalWS = normalize(float3(perturbation.x, 1, perturbation.y));
    if (_WaterIgnoreMeshTangent < 0.5)
        normalWS = EnvironmentNormal(input, half4(perturbation * 0.5h + 0.5h, 1, 1), 1);
    float2 coastUV = EnvironmentMainUV(input) + perturbation * _WaterCoastDistortion;
    half4 coastMap = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, coastUV);
    half coast = saturate(dot(coastMap, _WaterCoastChannel));
    coast = lerp(1.0h, coast, saturate(_UseCoastMap));
    half depth = saturate(coast / max(_WaterDepthFadeScale, 0.0001h));
    half3 ambient = max(SampleSH(normalWS), _EnvironmentAmbientFloor.xxx);
    half3 color = _WaterDepthColor.rgb * (ambient + mainLight.color * saturate(dot(normalWS, mainLight.direction)));
    float2 causticUV = input.positionWS.xz * _CausticMap_ST.xy + _CausticMap_ST.zw;
    half4 causticMap = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap,
        causticUV + perturbation * _WaterCausticDistortion + _Time.x * 0.02);
    half caustic = dot(causticMap, _WaterCausticChannel);
    color += caustic * _WaterCausticColor.rgb * _UseCausticMap * exp2(-depth * _WaterCausticDepthScale);
    half fresnel = saturate(pow(1.0h - saturate(dot(normalWS, viewWS)), 4.0h) + _FresnelEffectBias);
    half3 reflection = EnvironmentReflection(reflect(-viewWS, normalWS), input.positionWS, saturate(_ReflectionRoughness));
    color += reflection * _ReflectionColor.rgb * fresnel * _ReflectionOcclusion * (1.0h - _ReflectionFade);
    float2 foamUV = coastUV + perturbation * _WaterFoamDistortion;
    half foamMask = dot(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, foamUV), _WaterFoamChannel);
    half foam = smoothstep(_WaterFoamCoverage, 1.0h, foamMask) * (1.0h - coast);
    color += foam * _WaterFoamColor.rgb * (1.0h + _WaterFoamHighlightScale * mainLight.color);
    half alpha = saturate(depth * _WaterDepthColor.a + foam * _WaterFoamColor.a + _AlphaOffset);
    if (_WaterVertexColorDebugMode > 0.5) color = input.color.rgb;
    if (_WaterVertexNormalDebugMode > 0.5) color = input.normalWS * 0.5 + 0.5;
    if (_WaterVertexUVDebugMode > 0.5) color = half3(frac(input.uv), 0);
#endif
    return half4(EnvironmentFinish(color, input), alpha);
}

#endif
