Shader "Unite/Goal Zone Shield"
{
    Properties
    {
        _EmissionMap ("Shield Mask", 2D) = "white" {}
        [HDR] _EmissionColor ("Shield Color", Color) = (0.17, 0, 1, 1)
        _ScrollSpeed ("Scroll Speed", Float) = 0.5
        _PulseSpeed ("Pulse Speed", Float) = 5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.1
        [HideInInspector] _ShieldFade ("Visibility Transition", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Shield"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionMap_ST;
                half4 _EmissionColor;
                float4 _ShieldFade;
                float _ScrollSpeed;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 emission : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _EmissionMap);
                output.uv.x += frac(_Time.y * _ScrollSpeed);
                float fade = lerp(_ShieldFade.x, _ShieldFade.y, saturate((_Time.y - _ShieldFade.z) * _ShieldFade.w));
                float pulse = 1 - sin(_Time.y * _PulseSpeed) * _PulseAmount;
                output.emission = _EmissionColor.rgb * fade * pulse;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 mask = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
                return half4(mask * input.emission, 0);
            }
            ENDHLSL
        }
    }
}
