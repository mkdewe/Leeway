// The halo round a star: a soft additive disc that faces the camera.
Shader "Leeway/SpaceGlow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.85, 0.55, 1)
        _Strength ("Strength", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv);
                float falloff = saturate(1.0 - r);
                half glow = falloff * falloff * falloff + pow(falloff, 9.0) * 1.5;
                return half4(_Color.rgb * glow * _Strength, 1);
            }
            ENDHLSL
        }
    }
}
