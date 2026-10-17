// Toon dla obiektow Leeway: oswietlenie w pasach, cien zabarwiony na kolor
// palety (zamiast szarosci), rim light dla czytelnosci sylwetek na ciemnym tle
// i emisja dla akcentow (PaletteGrade przepuszcza je bez mapowania).
//
// Nazwy _BaseMap/_BaseColor/_EmissionColor jak w URP/Lit, wiec MaterialPropertyBlocki
// z CreatureVisuals/CreatureBodyBuilder dzialaja bez zmian.
Shader "Leeway/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [Toggle(_VERTEX_COLOR)] _UseVertexColor ("Multiply By Vertex Color", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Bands)]
        [IntRange] _Bands ("Light Bands", Range(1, 6)) = 3
        _BandSoftness ("Band Softness", Range(0.001, 0.5)) = 0.05
        _ShadowTint ("Shadow Tint", Color) = (0.21, 0.29, 0.35, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.75
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.6

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (0.79, 0.89, 0.79, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.35
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half4 _EmissionColor;
            half _Bands;
            half _BandSoftness;
            half4 _ShadowTint;
            half _ShadowStrength;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
        CBUFFER_END

        // Wyrastanie propow wyspy (IslandGenerator): uv2 = (pivot.xyz, opoznienie + 2).
        // w < 2 to zwykly obiekt - dziala jak dotad (brakujacy kanal UV2 bywa czytany jako (0,0,0,1)). Globalne, bo propy sa
        // scalone w meshe chunkow i nie maja wlasnych transformow do skalowania.
        float _LeewayGrowActive;
        float _LeewayGrowElapsed;
        float _LeewayGrowDuration;

        float3 ApplyGrow(float3 positionOS, float4 grow)
        {
            if (_LeewayGrowActive < 0.5 || grow.w < 2.0) return positionOS;
            float t = saturate((_LeewayGrowElapsed - (grow.w - 2.0)) / max(_LeewayGrowDuration, 1e-3));
            float u = t - 1.0;
            float s = 1.0 + 2.70158 * u * u * u + 1.70158 * u * u; // easeOutBack - lekkie przestrzelenie
            return grow.xyz + (positionOS - grow.xyz) * s;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _VERTEX_COLOR
            // Wzory skory z palety stwora (CreatureSkinPainter) niosa normal mape - relief zostaje w pasach swiatla.
            #pragma shader_feature_local _NORMALMAP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 grow : TEXCOORD2;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half4 color : TEXCOORD4;
                float4 tangentWS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs pos = GetVertexPositionInputs(ApplyGrow(input.positionOS.xyz, input.grow));
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                output.color = input.color;
                return output;
            }

            half Band(half x)
            {
                half scaled = saturate(x) * _Bands;
                half stepped = floor(scaled) + smoothstep(0.5 - _BandSoftness, 0.5 + _BandSoftness, frac(scaled));
                return saturate(stepped / _Bands);
            }

            half3 ShadeLight(Light light, half3 normalWS, half3 albedo)
            {
                half ndotl = dot(normalWS, light.direction) * 0.5 + 0.5; // half-lambert: miekszy przejscie na pasy
                // Cien osobno od pasow: miekki shadowmap mnozony przez ndotl dawal prazki na plaskim terenie.
                half lit = Band(ndotl) * smoothstep(0.3, 0.7, light.shadowAttenuation);
                // Strona cienia nie schodzi do czerni, tylko do przyciemnionego koloru palety.
                half3 shadowCol = lerp(half3(1, 1, 1), _ShadowTint.rgb, _ShadowStrength) * 0.35;
                return albedo * light.color * light.distanceAttenuation * lerp(shadowCol, half3(1, 1, 1), lit);
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 albedo = baseSample.rgb;
                #if defined(_VERTEX_COLOR)
                    albedo *= input.color.rgb;
                #endif
                // Cull Off (trawa, liscie): tyl trojkata oswietlany jak przod.
                half3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                #if defined(_NORMALMAP)
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS))) * IS_FRONT_VFACE(face, 1.0, -1.0);
                #endif
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 color = ShadeLight(mainLight, normalWS, albedo);
                color += albedo * SampleSH(normalWS) * _AmbientStrength;

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    color += ShadeLight(light, normalWS, albedo) * 0.5;
                LIGHT_LOOP_END
                #endif

                half rim = PositivePow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength * (0.4 + 0.6 * mainLight.shadowAttenuation);

                color += _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 grow : TEXCOORD2;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(ApplyGrow(input.positionOS.xyz, input.grow));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 grow : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(ApplyGrow(input.positionOS.xyz, input.grow));
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        // Potrzebny konturom w PaletteGrade (_CameraNormalsTexture).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 grow : TEXCOORD2;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(ApplyGrow(input.positionOS.xyz, input.grow));
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
