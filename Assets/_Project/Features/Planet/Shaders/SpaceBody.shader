// A planet, a moon or a star seen from outside the air: flat colour in three steps of light, optional bands
// across a gas giant, and a star that simply glows. The light comes from wherever the star is for that body.
Shader "Leeway/SpaceBody"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.7, 0.6, 0.5, 1)
        _Accent ("Accent colour", Color) = (0.8, 0.7, 0.6, 1)
        _Bands ("Bands across the body (0 = none)", Float) = 0
        _LightDir ("Direction towards the star", Vector) = (0, 1, 0, 0)
        _Glow ("Glow (1 = a star)", Range(0, 1)) = 0
        _Night ("Light on the dark side", Range(0, 0.4)) = 0.07
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "SpaceBody"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _Accent;
                float _Bands;
                float4 _LightDir;
                half _Glow;
                half _Night;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 localDir : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.localDir = normalize(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 colour = _BaseColor.rgb;

                // Bands run round the body, wobbling a little so they do not look ruled.
                if (_Bands > 0.5)
                {
                    float wobble = sin(input.localDir.x * 5.0 + input.localDir.z * 3.0) * 0.06;
                    float band = sin((input.localDir.y + wobble) * _Bands * 6.2831853);
                    colour = lerp(colour, _Accent.rgb, smoothstep(-0.2, 0.5, band) * 0.8);
                }
                else
                {
                    // Rock: a few blotches of the accent colour.
                    float blot = sin(input.localDir.x * 7.0) * sin(input.localDir.y * 6.0 + 1.3) * sin(input.localDir.z * 8.0);
                    colour = lerp(colour, _Accent.rgb, smoothstep(0.0, 0.35, blot) * 0.65);
                }

                float3 normal = normalize(input.normalWS);
                float light = dot(normal, normalize(_LightDir.xyz));
                // Three steps with soft edges: the same flat shading as the rest of the game.
                half lit = lerp(0.0, 0.42, smoothstep(-0.05, 0.08, light));
                lit += lerp(0.0, 0.33, smoothstep(0.18, 0.34, light));
                lit += lerp(0.0, 0.25, smoothstep(0.55, 0.72, light));
                lit = max(lit, _Night);

                half3 shaded = colour * lit;
                half3 star = _BaseColor.rgb * 2.2;
                return half4(lerp(shaded, star, _Glow), 1);
            }
            ENDHLSL
        }
    }
}
