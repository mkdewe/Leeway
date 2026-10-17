// Woda Leeway: nieprzezroczysta, glebokosc wypieczona w kolor wierzcholka przez
// IslandMeshBuilder.BuildWaterSurface (r = glebokosc 0..1, g = 1 dla wody slodkiej).
// Dzieki temu nie czyta _CameraDepthTexture i moze pisac glebie/normalne -
// kontury i mgla z PaletteGrade dzialaja na niej jak na terenie.
Shader "Leeway/ToonWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.62, 0.82, 0.76, 1)
        _DeepColor ("Deep", Color) = (0.24, 0.42, 0.46, 1)
        _FreshTint ("Fresh Water Tint", Color) = (0.72, 0.88, 0.80, 1)
        _FoamColor ("Foam", Color) = (0.86, 0.95, 0.88, 1)
        _FoamWidth ("Shore Foam Width (depth 0..1)", Range(0, 0.5)) = 0.12
        _FallFoam ("Waterfall Foam From Slope", Range(0, 1)) = 0.35
        _RippleScale ("Ripple Scale", Float) = 0.07
        _RippleSpeed ("Ripple Speed", Float) = 0.04
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.5
        _LightInfluence ("Light Influence", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor;
            half4 _DeepColor;
            half4 _FreshTint;
            half4 _FoamColor;
            half _FoamWidth;
            half _FallFoam;
            float _RippleScale;
            float _RippleSpeed;
            half _RippleStrength;
            half _LightInfluence;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 wpos = input.positionWS;
                half depth = input.color.r;
                half fresh = input.color.g;
                float t = _Time.y;

                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.0, 1.0, depth));
                color = lerp(color, lerp(_FreshTint.rgb, _DeepColor.rgb, depth * 0.6), fresh);

                // Fale jako waskie, przesuwajace sie pasy - posteryzacja i tak zrobi z nich linie.
                float2 p = wpos.xz * _RippleScale;
                float wave = sin(p.x * 6.0 + sin(p.y * 4.0 + t * 0.7) * 1.5 + t * _RippleSpeed * 40.0)
                           * sin(p.y * 5.0 + t * _RippleSpeed * 25.0);
                half ripple = step(0.82, wave) * _RippleStrength * saturate(depth * 3.0);
                color = lerp(color, _FoamColor.rgb, ripple * 0.5);

                // Piana przy brzegu oddycha w czasie; na stromych spadkach rzeki - wodospad.
                half edge = _FoamWidth * (0.75 + 0.25 * sin(t * 1.4 + wpos.x * 0.35 + wpos.z * 0.27));
                half foam = 1.0 - step(edge, depth);
                half slope = 1.0 - saturate(abs(input.normalWS.y));
                foam = max(foam, step(0.15, slope) * _FallFoam * 2.0);
                color = lerp(color, _FoamColor.rgb, saturate(foam));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(wpos));
                half lit = lerp(1.0 - _LightInfluence, 1.0, mainLight.shadowAttenuation);
                color *= lit * lerp(half3(1, 1, 1), mainLight.color, _LightInfluence);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Off

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(Varyings input) : SV_Target
            {
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
                    return half4(PackFloat2To888(oct), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
