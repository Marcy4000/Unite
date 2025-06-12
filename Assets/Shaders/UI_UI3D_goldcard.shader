Shader "UI/UI3D_goldcard"
{
    Properties
    {
        [Header(Tex)] 
        [MainTexture] _MainTexEX ("Diffuse", 2D) = "white" {}
        [NoScaleOffset] _MixMap ("Layermap(RGB)Depth(A)", 2D) = "black" {}

        [Header(Layer1)]
        _Layer1Tex ("Distortion Map", 2D) = "black" {}
        _Layer1Vector ("Panner(XY)Distortion(ZW)", Vector) = (0,0,0,0)
        _Layer1Rotate ("Rotate", Range(0, 6.28)) = 0
        _Layer1PulseClip ("Pulse Min", Range(0, 10)) = 0
        _Layer1PulseIntensity ("Pulse Max", Range(0, 10)) = 1
        _Layer1PulseRate ("Pulse Rate", Range(0, 30)) = 1

        [Header(Layer2)]
        _Layer2Tex ("Effect Map", 2D) = "black" {}
        _Layer2Vector ("Panner(XY)", Vector) = (0,0,0,0)
        _Layer2Rotate ("Rotate", Range(0, 6.28)) = 0
        _Layer2PulseClip ("Pulse Min", Range(0, 10)) = 0
        _Layer2PulseIntensity ("Pulse Max", Range(0, 10)) = 1
        _Layer2PulseRate ("Pulse Rate", Range(0, 30)) = 1
        [KeywordEnum(ADD, BLEND)] _MODEL2 ("Blend Mode", Float) = 0
        [HDR] _Layer2Color ("Color", Color) = (1,1,1,1)

        [Header(Layer3)]
        _Layer3FTex ("Flowmap", 2D) = "black" {}
        _Layer3Tex ("Distortion Map", 2D) = "black" {}
        _Layer3Vector ("Panner(XY)", Vector) = (0,0,0,0)
        _Layer3Rotate ("Rotate", Range(0, 6.28)) = 0
        _Layer3PulseClip ("Pulse Min", Range(0, 10)) = 0
        _Layer3PulseIntensity ("Pulse Max", Range(0, 10)) = 1
        _Layer3PulseRate ("Pulse Rate", Range(0, 30)) = 1
        [HDR] _Layer3Color ("Color", Color) = (1,1,1,1)
        [KeywordEnum(ADD, BLEND)] _MODEL3 ("Blend Mode", Float) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Header(Extra Hegiht)]
        _ExtraHeight ("Extra Height", Range(-1, 1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "QUEUE" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "true"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "CanUseSpriteAtlas" = "true"
                "IGNOREPROJECTOR" = "true"
                "PreviewType" = "Plane"
                "QUEUE" = "Transparent"
                "RenderType" = "Transparent"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask [_ColorMask]
            ZTest Always
            ZWrite Off

            Stencil
            {
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
                Comp [_StencilComp]
                Pass [_StencilOp]
                Fail Keep
                ZFail Keep
            }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _MODEL2_ADD _MODEL2_BLEND
            #pragma multi_compile _MODEL3_ADD _MODEL3_BLEND
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"

            // Texture STs
            float4 _MainTexEX_ST;
            float4 _Layer1Tex_ST;
            float4 _Layer2Tex_ST;
            float4 _Layer3FTex_ST;
            float4 _Layer3Tex_ST;

            // Layer 1
            float4 _Layer1Vector;
            float _Layer1Rotate;
            float _Layer1PulseClip;
            float _Layer1PulseIntensity;
            float _Layer1PulseRate;

            // Layer 2
            float4 _Layer2Vector;
            float _Layer2Rotate;
            float _Layer2PulseClip;
            float _Layer2PulseIntensity;
            float _Layer2PulseRate;
            fixed4 _Layer2Color;

            // Layer 3
            float4 _Layer3Vector;
            float _Layer3Rotate;
            float _Layer3PulseClip;
            float _Layer3PulseIntensity;
            float _Layer3PulseRate;
            fixed4 _Layer3Color;

            // Alpha clip
            float _Cutoff;

            // Adjustments in height
            float _ExtraHeight;

            // Textures and samplers
            sampler2D _MainTexEX;
            sampler2D _MixMap;
            sampler2D _Layer1Tex;
            sampler2D _Layer2Tex;
            sampler2D _Layer3FTex;
            sampler2D _Layer3Tex;

            // Vertex input
            struct appdata {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            // Varyings
            struct v2f {
                float4 vertex     : SV_POSITION;
                float4 uvMain     : TEXCOORD0; 
                float4 uvLayer1   : TEXCOORD1;
                float4 uvLayer2   : TEXCOORD2; 
                float3 uvLayer3   : TEXCOORD3; 
            };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);

                float3 positionWS = v.vertex.xyz;
                o.vertex = UnityObjectToClipPos(positionWS);

                o.uvMain.xy = v.uv.xy * _MainTexEX_ST.xy + _MainTexEX_ST.zw;

                float2 centered_uv = v.uv.xy - 0.5;

                // Layer 1 UVs (Panner + Rotate)
                float2 panned_uv1 = frac(_Time.xx * _Layer1Vector.xy);
                float s1 = sin(_Layer1Rotate);
                float c1 = cos(_Layer1Rotate);
                float2x2 rotMatrix1 = float2x2(c1, s1, -s1, c1);
                float2 rotated_uv1 = mul(rotMatrix1, centered_uv) + 0.5;
                o.uvMain.zw = rotated_uv1 * _Layer1Tex_ST.xy + _Layer1Tex_ST.zw + panned_uv1;

                // Layer 2 UVs (Panner + Rotate)
                float2 panned_uv2 = frac(_Time.xx * _Layer2Vector.xy);
                float s2 = sin(_Layer2Rotate);
                float c2 = cos(_Layer2Rotate);
                float2x2 rotMatrix2 = float2x2(c2, s2, -s2, c2);
                float2 rotated_uv2 = mul(rotMatrix2, centered_uv) + 0.5;
                o.uvLayer1.xy = rotated_uv2 * _Layer2Tex_ST.xy + _Layer2Tex_ST.zw + panned_uv2;

                // Layer 3 Flowmap UVs (Rotate)
                float s3 = sin(_Layer3Rotate);
                float c3 = cos(_Layer3Rotate);
                float2x2 rotMatrix3 = float2x2(c3, s3, -s3, c3);
                float2 rotated_uv3_flow = mul(rotMatrix3, centered_uv) + 0.5;
                o.uvLayer1.zw = rotated_uv3_flow * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw;

                // Pulse calculations
                float3 pulseTimes = _Time.xxx * float3(_Layer1PulseRate, _Layer2PulseRate, _Layer3PulseRate);
                float3 pulseCos = cos(frac(pulseTimes) * 6.2831853); // TWO_PI
                float3 pulse01 = pulseCos * 0.5 + 0.5;
                o.uvLayer3.x = lerp(_Layer1PulseClip, _Layer1PulseIntensity, pulse01.x);
                o.uvLayer3.y = lerp(_Layer2PulseClip, _Layer2PulseIntensity, pulse01.y);
                o.uvLayer3.z = lerp(_Layer3PulseClip, _Layer3PulseIntensity, pulse01.z);

                return o;
            }


            fixed4 frag(v2f i) : SV_Target
            {
                // Layer: mixMap
                fixed4 mixMap = tex2D(_MixMap, i.uvMain);

                // Layer 1 (Distortion)
                fixed layer1Sampler = tex2D(_Layer1Tex, i.uvMain).r;
                fixed layer1DistortFactor = layer1Sampler * i.uvLayer3.x;
                float2 distortedMainUV = i.uvMain + mixMap.r * layer1DistortFactor * _Layer1Vector.zw;
                fixed4 mainColor = tex2D(_MainTexEX, distortedMainUV);

                // Layer 2
                fixed4 layer2TexColor = tex2D(_Layer2Tex, i.uvLayer1.xy);
                fixed3 effect2Color = layer2TexColor.rgb * _Layer2Color.rgb;
                fixed layer2BlendFactor = mixMap.g * i.uvLayer3.y * layer2TexColor.a;

                #if _MODEL2_ADD
                    mainColor.rgb += effect2Color * layer2BlendFactor;
                #else
                    mainColor.rgb = lerp(mainColor.rgb, effect2Color, layer2BlendFactor);
                #endif

                // Layer 3 (Flowmap + Distortion)
                float2 flowSample = tex2D(_Layer3FTex, i.uvLayer1.zw).rg * 2.0 - 1.0;
                float2 baseUV = frac(i.uvLayer1.zw + _Layer3Vector.xy * _Time.x); 
                float2 layer3DistortUV = baseUV + flowSample * 0.1;
              
                layer3DistortUV = layer3DistortUV * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;

                // it's higher than expected?? let's add some extra height
                layer3DistortUV.y += _ExtraHeight;

                fixed4 layer3TexColor = tex2D(_Layer3Tex, layer3DistortUV);

                fixed3 effect3Color = layer3TexColor.rgb * _Layer3Color.rgb;
                fixed layer3BlendFactor = mixMap.b * i.uvLayer3.z * layer3TexColor.a;

                #if _MODEL3_ADD
                    mainColor.rgb += effect3Color * layer3BlendFactor;
                #else
                    mainColor.rgb = lerp(mainColor.rgb, effect3Color, layer3BlendFactor);
                #endif

                #if UNITY_UI_ALPHACLIP
                    clip(mainColor.a - _Cutoff);
                #endif

                return mainColor;
            }

            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}
