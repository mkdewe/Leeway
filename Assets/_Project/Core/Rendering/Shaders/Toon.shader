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

        // Rozklad ciala zmarlego stworzenia: skora rozsypuje sie w kostki i brunatnieje (CreatureRemains).
        _Decay ("Decay", Range(0, 1)) = 0
        _DecayColor ("Decay Tint", Color) = (0.3, 0.33, 0.18, 1)

        // 0 = bez mgly: planeta ogladana z kilku kilometrow inaczej cala tonie w kolorze mgly wyspy.
        _FogAmount ("Fog Amount", Range(0, 1)) = 1

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
            half _Decay;
            half4 _DecayColor;
            half _FogAmount;
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

        // GPU Resident Drawer i GPU occlusion culling (Unity 6): obiekty tego shadera rysuje wtedy GPU z wlasnym cullingiem.
        // Wlasciwosci materialu musza byc zadeklarowane takze dla DOTS instancing.
        #ifdef UNITY_DOTS_INSTANCING_ENABLED
        UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseMap_ST)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DOTS_INSTANCED_PROP(float, _BumpScale)
            UNITY_DOTS_INSTANCED_PROP(float, _Decay)
            UNITY_DOTS_INSTANCED_PROP(float4, _DecayColor)
            UNITY_DOTS_INSTANCED_PROP(float, _FogAmount)
            UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
            UNITY_DOTS_INSTANCED_PROP(float, _Bands)
            UNITY_DOTS_INSTANCED_PROP(float, _BandSoftness)
            UNITY_DOTS_INSTANCED_PROP(float4, _ShadowTint)
            UNITY_DOTS_INSTANCED_PROP(float, _ShadowStrength)
            UNITY_DOTS_INSTANCED_PROP(float, _AmbientStrength)
            UNITY_DOTS_INSTANCED_PROP(float4, _RimColor)
            UNITY_DOTS_INSTANCED_PROP(float, _RimStrength)
            UNITY_DOTS_INSTANCED_PROP(float, _RimPower)
        UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
        #define _BaseMap_ST UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseMap_ST)
        #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
        #define _BumpScale UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _BumpScale)
        #define _Decay UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Decay)
        #define _DecayColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _DecayColor)
        #define _FogAmount UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FogAmount)
        #define _EmissionColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor)
        #define _Bands UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Bands)
        #define _BandSoftness UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _BandSoftness)
        #define _ShadowTint UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ShadowTint)
        #define _ShadowStrength UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShadowStrength)
        #define _AmbientStrength UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _AmbientStrength)
        #define _RimColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _RimColor)
        #define _RimStrength UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RimStrength)
        #define _RimPower UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RimPower)
        #endif

        // Wyrastanie propow wyspy (IslandGenerator): uv2 = (pivot.xyz, opoznienie + 2).
        // w < 2 to zwykly obiekt - dziala jak dotad (brakujacy kanal UV2 bywa czytany jako (0,0,0,1)). Globalne, bo propy sa
        // scalone w meshe chunkow i nie maja wlasnych transformow do skalowania.
        float _LeewayDayNightEnabled;
        float _LeewayDaylight;
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
        #include "IslandWeather.hlsl"
        // UV3.x is authored only for living vegetation. Rock, terrain and creatures stay still.
        float3 ApplyVegetationWind(float3 positionOS, float4 grow, float2 wind)
        {
            if (wind.x <= 0.0 || abs(wind.y - 17.0) > 0.01 || _LeewayWindEnabled < 0.5) return positionOS;
            float3 world = TransformObjectToWorld(positionOS);
            float3 pivot = TransformObjectToWorld(grow.xyz);
            float3 up=LeewayUpAt(pivot);
            float3 direction=LeewayTangentDirection(pivot,LeewayWindDirection());
            float3 crossDirection=cross(up,direction);
            float height = max(0, dot(world-pivot,up));
            float time = LeewayWeatherTime();
            float phase = dot(pivot.xz,float2(0.17,0.23));
            float gust = 0.75 + 0.25*sin(time*0.65+dot(pivot.xz,LeewayWindDirection())*0.025);
            float sway = 0.55+0.35*sin(time*1.45+phase)+0.1*sin(time*3.1+phase*2.3);
            float bend = (height*0.055+height*height*0.002)*wind.x*_LeewayWind.z*gust;
            world += direction*bend*sway;
            world += crossDirection*sin(time*2.4+phase)*bend*0.12;
            return TransformWorldToObject(world);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
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
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 grow : TEXCOORD2;
                float2 wind : TEXCOORD3;
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

                VertexPositionInputs pos = GetVertexPositionInputs(ApplyVegetationWind(ApplyGrow(input.positionOS.xyz, input.grow), input.grow, input.wind));
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
                half lit = Band(ndotl) * saturate(light.shadowAttenuation);
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
                // A dead body crumbles away piece by piece, in noise cells that stay put in the world.
                if (_Decay > 0.001)
                {
                    float3 cell = floor(input.positionWS * 14.0);
                    float n = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                    clip(n - _Decay * 1.05 + 0.02);
                    albedo = lerp(albedo, _DecayColor.rgb, saturate(_Decay * 1.7));
                }
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

                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                half3 color = ShadeLight(mainLight, normalWS, albedo) * ao.directAmbientOcclusion;
                color += albedo * SampleSH(normalWS) * _AmbientStrength * ao.indirectAmbientOcclusion;

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
                color += _RimColor.rgb * rim * _RimStrength * (0.4 + 0.6 * mainLight.shadowAttenuation) * lerp(1.0, lerp(0.12, 1.0, _LeewayDaylight), _LeewayDayNightEnabled);

                color += _EmissionColor.rgb;
                color = lerp(color, MixFog(color, input.fogFactor), _FogAmount);
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
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 grow : TEXCOORD2;
                float2 wind : TEXCOORD3;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(ApplyVegetationWind(ApplyGrow(input.positionOS.xyz, input.grow), input.grow, input.wind));
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
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 grow : TEXCOORD2;
                float2 wind : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(ApplyVegetationWind(ApplyGrow(input.positionOS.xyz, input.grow), input.grow, input.wind));
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
            #pragma target 4.5
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 grow : TEXCOORD2;
                float2 wind : TEXCOORD3;
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
                output.positionCS = TransformObjectToHClip(ApplyVegetationWind(ApplyGrow(input.positionOS.xyz, input.grow), input.grow, input.wind));
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
