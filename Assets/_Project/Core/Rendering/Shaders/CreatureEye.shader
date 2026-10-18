// Oko stwora: gałka z namalowaną w shaderze teksturą - biało, tęczówka, źrenica, odbicie światła i powieka.
// Wzór liczony z kierunku od środka gałki, więc źrenica porusza się po powierzchni (spojrzenie) bez żadnych
// dodatkowych obiektów. Oświetlenie jak w Leeway/Toon.
Shader "Leeway/CreatureEye"
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

        [Header(Eye)]
        _EyeCenter ("Eyeball Centre (object space)", Vector) = (0, 0, 0, 0)
        _EyeGaze ("Gaze (object space xyz, pupil dilation w)", Vector) = (0, 0, 1, 1)
        _EyeLid ("Lid Closed", Range(0, 1)) = 0
        _EyeScleraColor ("Sclera", Color) = (0.96, 0.94, 0.88, 1)
        _EyeIrisColor ("Iris", Color) = (0.25, 0.55, 0.35, 1)
        _EyeLidColor ("Lid", Color) = (0.8, 0.5, 0.45, 1)
        _EyeIrisSize ("Iris Radius (rad)", Range(0.2, 1.2)) = 0.7
        _EyePupilSize ("Pupil Radius (rad)", Range(0.05, 0.8)) = 0.3

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
            float4 _EyeCenter;
            float4 _EyeGaze;
            half _EyeLid;
            half4 _EyeScleraColor;
            half4 _EyeIrisColor;
            half4 _EyeLidColor;
            half _EyeIrisSize;
            half _EyePupilSize;
        CBUFFER_END

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
            float height = max(0, world.y - pivot.y);
            float time = LeewayWeatherTime();
            float phase = dot(pivot.xz,float2(0.17,0.23));
            float gust = 0.75 + 0.25*sin(time*0.65+dot(pivot.xz,LeewayWindDirection())*0.025);
            float sway = 0.55+0.35*sin(time*1.45+phase)+0.1*sin(time*3.1+phase*2.3);
            float bend = (height*0.055+height*height*0.002)*wind.x*_LeewayWind.z*gust;
            world.xz += LeewayWindDirection()*bend*sway;
            world.xz += float2(-_LeewayWind.y,_LeewayWind.x)*sin(time*2.4+phase)*bend*0.12;
            return TransformWorldToObject(world);
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
                float3 positionOS : TEXCOORD6;
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
                output.positionOS = input.positionOS.xyz;
                output.color = input.color;
                return output;
            }

            // The painted eye. d is the direction from the eyeball's centre; the gaze is where the pupil points.
            half3 EyeAlbedo(float3 positionOS, float3 normalWS)
            {
                float3 d = normalize(positionOS - _EyeCenter.xyz);
                float3 g = normalize(_EyeGaze.xyz);
                float angle = acos(clamp(dot(d, g), -1.0, 1.0));

                half3 albedo = _EyeScleraColor.rgb;

                // Iris with a darker rim and a lighter ring around the pupil.
                float irisR = _EyeIrisSize;
                float pupilR = _EyePupilSize * _EyeGaze.w;
                float irisMask = 1.0 - smoothstep(irisR - 0.05, irisR, angle);
                float rim = smoothstep(irisR - 0.18, irisR, angle);
                float ring = 1.0 - smoothstep(pupilR, pupilR + 0.16, angle);
                half3 iris = lerp(_EyeIrisColor.rgb, _EyeIrisColor.rgb * 0.45, rim);
                iris = lerp(iris, _EyeIrisColor.rgb * 1.5 + 0.08, ring * 0.6);
                albedo = lerp(albedo, iris, irisMask);

                // Pupil.
                float pupilMask = 1.0 - smoothstep(pupilR - 0.03, pupilR, angle);
                albedo = lerp(albedo, half3(0.02, 0.02, 0.03), pupilMask);

                // The lid comes down from above in world space, whichever way the eye is turned.
                float3 up = normalize(mul((float3x3)unity_WorldToObject, float3(0, 1, 0)));
                float height = dot(d, up);
                float lidLine = lerp(1.2, -1.2, saturate(_EyeLid));
                float lid = smoothstep(lidLine - 0.04, lidLine + 0.04, height);
                albedo = lerp(albedo, _EyeLidColor.rgb, lid);

                return albedo;
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

                half3 albedo = EyeAlbedo(input.positionOS, input.normalWS);
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

                // A crisp highlight makes the eye wet: bright where the surface faces halfway between light and viewer.
                half3 halfDir = normalize(mainLight.direction + viewWS);
                half spec = smoothstep(0.985, 0.992, dot(normalWS, halfDir));
                color += spec * mainLight.color * 0.9 * (1.0 - saturate(_EyeLid));

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
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

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
            #pragma target 3.0
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

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
