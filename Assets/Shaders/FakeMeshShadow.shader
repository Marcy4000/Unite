Shader "Unite/Fake Mesh Shadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.3)
        _GroundPlane ("Ground Plane", Vector) = (0, 1, 0, 0)
        _ProjectionDirection ("Projection Direction", Vector) = (0, -1, 0, 0)
        _SurfaceOffset ("Surface Offset", Float) = 0.015
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Name "FakeShadow"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off
            // Reserve bit 0 so body parts (and adjacent shadows) blend only once.
            Stencil { Ref 1 ReadMask 1 WriteMask 1 Comp NotEqual Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                float4 _GroundPlane;
                float4 _ProjectionDirection;
                float _SurfaceOffset;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float height = dot(_GroundPlane.xyz, world) + _GroundPlane.w;
                float denominator = min(dot(_GroundPlane.xyz, _ProjectionDirection.xyz), -0.05);
                world -= _ProjectionDirection.xyz * (height / denominator);
                world += _GroundPlane.xyz * _SurfaceOffset;
                output.positionCS = TransformWorldToHClip(world);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _ShadowColor; }
            ENDHLSL
        }
    }
}
