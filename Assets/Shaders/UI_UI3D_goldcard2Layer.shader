//////////////////////////////////////////
//
// NOTE: This is *not* a valid shader file
//
///////////////////////////////////////////
Shader "UI/UI3D_goldcard2Layer" {
Properties {
[Header(Tex)] [MainTexture] _MainTexEX ("BackgroundTex", 2D) = "white" { }
[NoScaleOffset] _MixMap ("Layermap(RGB)Depth(A)", 2D) = "black" { }

[Header(Actor)] _actorTex ("actorTex", 2D) = "black" { }
_ActorRotate ("Rotate", Range(0, 6.28)) = 0
_ActorRotateAxis ("RotateAxis(XY)", Vector) = (0.5,0.5,0,0)
_ActorRotateSpeed ("RotateSpeed", Range(-10, 10)) = 0
[Toggle(_MODEL1_LOOP)] _Loop1 ("Loop (Actor)", Float) = 0
_ActorVector ("PannerBase(XY)PannerRange(ZW)", Vector) = (0,0,0,0) // Changed interpretation for clarity: XY=Base, ZW=Range
_ActorUSpeed ("PannerUSpeed", Range(0, 4)) = 0
_ActorVSpeed ("PannerVSpeed", Range(0, 10)) = 0

[Header(Layer2)] _Layer2Tex ("Effect Texture", 2D) = "black" { }
_Layer2Vector ("PannerSpeed(XY)", Vector) = (0,0,0,0)
_Layer2Rotate ("Rotate", Range(0, 6.28)) = 0
_Layer2RotateAxis ("RotateAxis(XY)", Vector) = (0.5,0.5,0,0)
_Layer2RotateSpeed ("RotateSpeed", Range(-10, 10)) = 0
[Toggle(_MODEL2_LOOP)] _Loop2 ("Loop (Layer2)", Float) = 0
_Layer2PulseClip ("Pulse Min Value", Range(0, 10)) = 0
_Layer2PulseIntensity ("Pulse Max Value", Range(0, 10)) = 1
_Layer2PulseRate ("Pulse Frequency", Range(0, 30)) = 1
[KeywordEnum(ADD, BLEND)] _MODEL2 ("Blend Mode (Layer2)", Float) = 0
[HDR] _Layer2Color ("Color (Layer2)", Color) = (1,1,1,1)

[Header(Layer3)] _Layer3FTex ("Flowmap", 2D) = "black" { }
_Layer3Tex ("Distortion Texture", 2D) = "black" { }
_Layer3Vector ("FlowmapPannerSpeed(XY)", Vector) = (0,0,0,0)
_Layer3Rotate ("Rotate", Range(0, 6.28)) = 0
_Layer3RotateAxis ("RotateAxis(XY)", Vector) = (0.5,0.5,0,0)
_Layer3PulseClip ("Pulse Min Value", Range(0, 10)) = 0
_Layer3PulseIntensity ("Pulse Max Value", Range(0, 10)) = 1
_Layer3PulseRate ("Pulse Frequency", Range(0, 30)) = 1
[HDR] _Layer3Color ("Color (Layer3)", Color) = (1,1,1,1)
[KeywordEnum(ADD, BLEND)] _MODEL3 ("Blend Mode (Layer3)", Float) = 0

// Dummy Layer1 properties from original for completeness, though not fully used in fragment
[HideInInspector] _Layer1PulseRate("L1 Pulse Rate", Range(0,30)) = 1
[HideInInspector] _Layer1PulseClip("L1 Pulse Clip", Range(0,10)) = 0
[HideInInspector] _Layer1PulseIntensity("L1 Pulse Intensity", Range(0,10)) = 1


[HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
[HideInInspector] _Stencil ("Stencil ID", Float) = 0
[HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
[HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
[HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
[HideInInspector] _ColorMask ("Color Mask", Float) = 15
[Toggle(UNITY_UI_ALPHACLIP)] [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
}
SubShader {
    Tags { 
        "QUEUE" = "Transparent" 
        "RenderPipeline" = "UniversalPipeline" 
        "RenderType" = "Transparent" 
        "IGNOREPROJECTOR" = "true" 
        "PreviewType" = "Plane"
        "CanUseSpriteAtlas" = "true"
    }
    LOD 300

    Pass {
        Name "ForwardLit"
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always // Or LEqual for standard UI, but original was Always
        ColorMask [_ColorMask]

        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag

        #pragma multi_compile_local _ _MODEL1_LOOP
        #pragma multi_compile_local _ _MODEL2_LOOP
        #pragma shader_feature_local _MODEL2_ADD _MODEL2_BLEND
        #pragma shader_feature_local _MODEL3_ADD _MODEL3_BLEND
        #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes {
            float4 positionOS   : POSITION;
            float2 uv           : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings {
            float4 positionCS   : SV_POSITION;
            float2 uv_Main      : TEXCOORD0;
            float4 uv_Layer2_Layer3F : TEXCOORD1; // .xy for Layer2, .zw for Layer3F
            float2 uv_Actor     : TEXCOORD2;
            // TEXCOORD3 is unused from original (was viewDir/worldPos)
            float3 pulseFactors : TEXCOORD4; // .x L1, .y L2, .z L3
            float4 clipPos      : TEXCOORD5; // For UI Alpha Clip
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TEXTURE2D(_MainTexEX);       SAMPLER(sampler_MainTexEX);
        TEXTURE2D(_MixMap);          SAMPLER(sampler_MixMap);
        TEXTURE2D(_actorTex);        SAMPLER(sampler_actorTex);
        TEXTURE2D(_Layer2Tex);       SAMPLER(sampler_Layer2Tex);
        TEXTURE2D(_Layer3FTex);      SAMPLER(sampler_Layer3FTex);
        TEXTURE2D(_Layer3Tex);       SAMPLER(sampler_Layer3Tex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTexEX_ST;
            float4 _actorTex_ST;
            float4 _Layer2Tex_ST;
            float4 _Layer3FTex_ST;
            float4 _Layer3Tex_ST;

            float _ActorRotate;
            float4 _ActorRotateAxis;
            float _ActorRotateSpeed;
            float4 _ActorVector; // XY=Base, ZW=Range for panner
            float _ActorUSpeed;
            float _ActorVSpeed;

            float4 _Layer2Vector; // XY = PannerSpeed
            float _Layer2Rotate;
            float4 _Layer2RotateAxis;
            float _Layer2RotateSpeed;
            float _Layer2PulseClip;
            float _Layer2PulseIntensity;
            float _Layer2PulseRate;
            half4 _Layer2Color;

            float4 _Layer3Vector; // XY = Flowmap PannerSpeed
            float _Layer3Rotate;
            float4 _Layer3RotateAxis;
            float _Layer3PulseClip;
            float _Layer3PulseIntensity;
            float _Layer3PulseRate;
            half4 _Layer3Color;
            
            float _Layer1PulseRate;
            float _Layer1PulseClip;
            float _Layer1PulseIntensity;

            float _Cutoff;
        CBUFFER_END

        // Helper for 2D rotation
        float2 RotateUV(float2 uv, float2 pivot, float angle) {
            float s = sin(angle);
            float c = cos(angle);
            uv -= pivot;
            uv = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
            uv += pivot;
            return uv;
        }
        
        Varyings vert(Attributes input) {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.clipPos = output.positionCS; // For UI Alpha Clip

            output.uv_Main = input.uv * _MainTexEX_ST.xy + _MainTexEX_ST.zw;

            float2 baseUV = input.uv; // Or output.uv_Main, depending on desired base for effects

            // Layer 2 UVs
            float layer2_angle;
            #if defined(_MODEL2_LOOP)
                layer2_angle = _Time.y * _Layer2RotateSpeed; // Continuous rotation
            #else
                float layer2_rot_anim = cos(_Time.y * _Layer2RotateSpeed * TWO_PI) * 0.5 + 0.5; // 0-1 oscillation
                layer2_angle = lerp(-_Layer2Rotate, _Layer2Rotate, layer2_rot_anim);
            #endif
            float2 layer2_uv = RotateUV(baseUV, _Layer2RotateAxis.xy, layer2_angle);
            float2 layer2_pan_offset = frac(_Time.xx * _Layer2Vector.xy);
            output.uv_Layer2_Layer3F.xy = layer2_uv * _Layer2Tex_ST.xy + _Layer2Tex_ST.zw + layer2_pan_offset;

            // Layer 3 UVs (Flowmap and Disturbmap)
            float layer3_angle = _Layer3Rotate; // Static rotation from property for L3 base UV
            float2 layer3_base_uv = RotateUV(baseUV, _Layer3RotateAxis.xy, layer3_angle);
            output.uv_Layer2_Layer3F.zw = layer3_base_uv * _Layer3FTex_ST.xy + _Layer3FTex_ST.zw; // For _Layer3FTex

            // Actor UVs
            float actor_final_rotate;
            float2 actor_final_pan;

            #if defined(_MODEL1_LOOP)
                actor_final_rotate = _ActorRotate + _Time.y * _ActorRotateSpeed; // Continuous rotation
                actor_final_pan = _ActorVector.xy + _Time.xx * float2(_ActorUSpeed, _ActorVSpeed); // Continuous panning
            #else
                float actor_rot_anim = cos(_Time.y * _ActorRotateSpeed * TWO_PI) * 0.5 + 0.5;
                actor_final_rotate = lerp(-_ActorRotate, _ActorRotate, actor_rot_anim);
                
                float2 actor_pan_anim = cos(_Time.xx * float2(_ActorUSpeed, _ActorVSpeed) * TWO_PI) * 0.5 + 0.5;
                // _ActorVector: XY=Base, ZW=Range. Original was YW base, XZ range. Let's use XY as start, ZW as end.
                // Original: u_xlat16_1.yz = _ActorVector.yw; u_xlat16_4.yz = (-u_xlat16_1.yz) + _ActorVector.xz;
                // This means start is _ActorVector.yw, end is _ActorVector.xz
                float2 pan_start = _ActorVector.yw; // Using original interpretation of _ActorVector
                float2 pan_end = _ActorVector.xz;
                actor_final_pan = lerp(pan_start, pan_end, actor_pan_anim);
            #endif
            float2 actor_uv = RotateUV(baseUV, _ActorRotateAxis.xy, actor_final_rotate);
            output.uv_Actor = actor_uv * _actorTex_ST.xy + _actorTex_ST.zw + actor_final_pan;
            
            // Pulse factors
            float3 pulse_cos = cos(_Time.yyy * float3(_Layer1PulseRate, _Layer2PulseRate, _Layer3PulseRate) * TWO_PI) * 0.5 + 0.5;
            float3 pulse_clips = float3(_Layer1PulseClip, _Layer2PulseClip, _Layer3PulseClip);
            float3 pulse_intensities = float3(_Layer1PulseIntensity, _Layer2PulseIntensity, _Layer3PulseIntensity);
            output.pulseFactors = lerp(pulse_clips, pulse_intensities, pulse_cos);

            return output;
        }

        half4 frag(Varyings i) : SV_Target {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            half4 mainTexColor = SAMPLE_TEXTURE2D(_MainTexEX, sampler_MainTexEX, i.uv_Main);
            half3 accumulatedColor = mainTexColor.rgb;
            half finalAlpha = mainTexColor.a;

            // Layer 3 (Distortion via Flowmap)
            half2 flowmap_val = SAMPLE_TEXTURE2D(_Layer3FTex, sampler_Layer3FTex, i.uv_Layer2_Layer3F.zw).rg; // Assuming RG for flow
            flowmap_val = (flowmap_val * 2.0h - 1.0h); // Unpack from 0-1 to -1-1 range if needed
            
            half2 flow_panning = frac(_Time.xx * _Layer3Vector.xy); // _Layer3Vector is panner speed for flow
            half2 layer3_disturb_uv_offset = flowmap_val + flow_panning;
            
            // Apply Layer3 base UV transform (rotation already in i.uv_Layer2_Layer3F.zw, add ST for _Layer3Tex)
            // The original vertex shader calculated TEXCOORD1.zw for _Layer3FTex.
            // For _Layer3Tex, it used this TEXCOORD1.zw + flow.
            // So, the base UV for _Layer3Tex is the same as _Layer3FTex.
            half2 layer3_disturb_uv = i.uv_Layer2_Layer3F.zw + layer3_disturb_uv_offset; // This might need scaling/adjustment
            layer3_disturb_uv = layer3_disturb_uv * _Layer3Tex_ST.xy + _Layer3Tex_ST.zw;


            half4 layer3TexColor = SAMPLE_TEXTURE2D(_Layer3Tex, sampler_Layer3Tex, layer3_disturb_uv);
            half layer3Mix = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, i.uv_Main).z; // Mix map B channel for L3
            half layer3PulseFactor = i.pulseFactors.z;
            half finalLayer3Mix = layer3Mix * layer3PulseFactor * layer3TexColor.a; // Include texture alpha

            #if defined(_MODEL3_ADD)
                accumulatedColor += layer3TexColor.rgb * _Layer3Color.rgb * finalLayer3Mix;
            #elif defined(_MODEL3_BLEND)
                accumulatedColor = lerp(accumulatedColor, layer3TexColor.rgb * _Layer3Color.rgb, finalLayer3Mix);
            #endif

            // Actor Layer
            half4 actorTexColor = SAMPLE_TEXTURE2D(_actorTex, sampler_actorTex, i.uv_Actor);
            // Standard alpha blend for actor
            accumulatedColor = lerp(accumulatedColor, actorTexColor.rgb, actorTexColor.a);
            finalAlpha = lerp(finalAlpha, actorTexColor.a, actorTexColor.a); // Or max(finalAlpha, actorTexColor.a) or other logic

            // Layer 2
            half4 layer2TexColor = SAMPLE_TEXTURE2D(_Layer2Tex, sampler_Layer2Tex, i.uv_Layer2_Layer3F.xy);
            half layer2Mix = SAMPLE_TEXTURE2D(_MixMap, sampler_MixMap, i.uv_Actor).y; // Mix map G channel for L2, using actor UVs as per original
            half layer2PulseFactor = i.pulseFactors.y;
            half finalLayer2Mix = layer2Mix * layer2PulseFactor; // Assuming L2 tex alpha is pre-multiplied or handled by _Layer2Color.a

            #if defined(_MODEL2_ADD)
                accumulatedColor += layer2TexColor.rgb * _Layer2Color.rgb * finalLayer2Mix;
            #elif defined(_MODEL2_BLEND)
                accumulatedColor = lerp(accumulatedColor, layer2TexColor.rgb * _Layer2Color.rgb, finalLayer2Mix);
            #endif
            // finalAlpha could be affected by Layer 2 as well, e.g. finalAlpha = max(finalAlpha, layer2TexColor.a * finalLayer2Mix);

            half4 finalColor = half4(accumulatedColor, finalAlpha);

            #if defined(UNITY_UI_ALPHACLIP)
                clip(finalColor.a - _Cutoff);
            #endif

            return finalColor;
        }
        ENDHLSL
    }
}
Fallback "Hidden/InternalErrorShader"
}