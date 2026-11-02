Shader "UI/TrainerHead"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _TrainerColor ("Trainer Color", Color) = (1,1,1,1)
        [Toggle] _Eye ("Eye Tone Curve", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "TrainerColorTone.hlsl"

            struct Attributes
            {
                float4 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 mask : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            float4 _MainTex_ST, _TextureSampleAdd, _TrainerColor, _ClipRect;
            float _Eye, _UIMaskSoftnessX, _UIMaskSoftnessY;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                float2 pixelSize = output.position.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.position.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            float3 UntoneHair(float3 color)
            {
                float3 quadratic = 0.508275 - 0.492075 * color;
                float3 coefficient = 0.2655 * color - 0.0135;
                return (coefficient + sqrt(max(coefficient * coefficient + 0.32 * quadratic * color, 0.0))) /
                    max(2.0 * quadratic, 1e-6);
            }

            float3 UntoneEye(float3 color)
            {
                float3 mapped = pow(max(color, 0.0), 1.0 / 2.2) / 1.019;
                return 0.155 * mapped / max(0.45 * (1.0 - mapped), 1e-6);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 sprite = tex2D(_MainTex, input.uv) + _TextureSampleAdd;
                float3 neutral = sprite.rgb;
                #if defined(UNITY_COLORSPACE_GAMMA)
                    neutral = GammaToLinearSpace(neutral);
                #endif
                float3 color = _Eye > 0.5
                    ? TrainerEyeTone(UntoneEye(neutral) * _TrainerColor.rgb)
                    : TrainerHairTone(UntoneHair(neutral) * _TrainerColor.rgb);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    color = LinearToGammaSpace(color);
                #endif
                float4 output = float4(color, sprite.a * _TrainerColor.a) * input.color;
                #if defined(UNITY_UI_CLIP_RECT)
                    float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                    output.a *= mask.x * mask.y;
                #endif
                #if defined(UNITY_UI_ALPHACLIP)
                    clip(output.a - 0.001);
                #endif
                return output;
            }
            ENDCG
        }
    }
}
