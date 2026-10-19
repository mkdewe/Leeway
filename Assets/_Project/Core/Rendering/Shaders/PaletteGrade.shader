// Post-process stylizacji Leeway (GDD 4.2) - parametry w PaletteGrade (Volume).
//
// Kolejnosc: mgla (w jasnosci) -> posteryzacja -> mapowanie na palete
// -> powrot akcentow -> kontury. Mgla dziala na jasnosci przed mapowaniem,
// wiec dalekie plany same wpadaja w jasne turkusy palety, jak na concepcie.
Shader "Hidden/Leeway/PaletteGrade"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "PaletteGrade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _LeewayDayNightEnabled;
            float _LeewayDaylight;
            float _LeewayPlanetView; // 0 on the ground, 1 when the camera has left for the planet: only atmospheric haze fades out
            float4 _LeewayPlanet; // xyz centre, w radius; zero for the flat editor
            float4 _Palette[6];
            float4 _Grade;        // x intensity, y blackPoint, z whitePoint, w bands
            float4 _Grade2;       // x bandSoftness, y chromaKeep, z accentThreshold, w accentKeep
            float4 _Fog;          // x start, y end, z density, w luminance
            float4 _FogHeight;    // x height, y falloff, z toneGamma (>1 = ciemniejsze polcienie)
            float4 _Outline;      // x strength, y thickness (px), z depthThreshold, w normalThreshold
            float4 _OutlineColor; // rgb kolor, a fadeDistance

            float3 SamplePalette(float t)
            {
                t = saturate(t) * 5.0;
                int i = min((int)floor(t), 4);
                return lerp(_Palette[i].rgb, _Palette[i + 1].rgb, t - i);
            }

            float Posterize(float l, float bands, float softness)
            {
                float x = l * bands;
                float w = max(softness * 0.5, 1e-3);
                return (floor(x) + smoothstep(0.5 - w, 0.5 + w, frac(x))) / bands;
            }

            bool IsSky(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth <= 1e-5;
                #else
                    return rawDepth >= 1.0 - 1e-5;
                #endif
            }

            float OutlineMask(float2 uv, float eye)
            {
                float2 o = _ScreenSize.zw * _Outline.y;
                float2 uv0 = uv + float2(-o.x, -o.y);
                float2 uv1 = uv + float2( o.x,  o.y);
                float2 uv2 = uv + float2( o.x, -o.y);
                float2 uv3 = uv + float2(-o.x,  o.y);

                float d0 = LinearEyeDepth(SampleSceneDepth(uv0), _ZBufferParams);
                float d1 = LinearEyeDepth(SampleSceneDepth(uv1), _ZBufferParams);
                float d2 = LinearEyeDepth(SampleSceneDepth(uv2), _ZBufferParams);
                float d3 = LinearEyeDepth(SampleSceneDepth(uv3), _ZBufferParams);
                // Prog wzgledny do glebokosci - inaczej daleki teren pod katem dawalby linie.
                float depthEdge = max(abs(d0 - d1), abs(d2 - d3)) / max(eye, 1e-3);

                float3 n0 = SampleSceneNormals(uv0);
                float3 n1 = SampleSceneNormals(uv1);
                float3 n2 = SampleSceneNormals(uv2);
                float3 n3 = SampleSceneNormals(uv3);
                float normalEdge = max(length(n0 - n1), length(n2 - n3));

                float dt = _Outline.z;
                float nt = _Outline.w;
                return max(smoothstep(dt, dt * 1.5, depthEdge), smoothstep(nt, nt + 0.2, normalEdge));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;

                float rawDepth = SampleSceneDepth(uv);
                bool sky = IsSky(rawDepth);
                float eye = LinearEyeDepth(rawDepth, _ZBufferParams);

                float fog = 0.0;
                if (!sky)
                {
                    float3 wpos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                    float dist = saturate((eye - _Fog.x) / (_Fog.y - _Fog.x));
                    float altitude = _LeewayPlanet.w > 0 ? length(wpos - _LeewayPlanet.xyz) - _LeewayPlanet.w : wpos.y;
                    float low = exp(-max(altitude - _FogHeight.x, 0.0) * _FogHeight.y);
                    fog = saturate(dist * lerp(0.4, 1.0, low)) * _Fog.z;
                    fog *= 1.0 - _LeewayPlanetView;
                }

                float srcLum = Luminance(src);
                float lum = PositivePow(srcLum, 1.0 / 2.2);
                lum = saturate((lum - _Grade.y) / (_Grade.z - _Grade.y));
                lum = PositivePow(lum, _FogHeight.z);
                lum = lerp(lum, _Fog.w, fog);

                float3 graded = SamplePalette(Posterize(lum, _Grade.w, _Grade2.x));
                graded = max(graded + (src - srcLum) * _Grade2.y * (1.0 - fog), 0.0);

                // Akcenty: mocno nasycone i jasne piksele zostaja w oryginalnym kolorze.
                float maxC = Max3(src.r, src.g, src.b);
                float sat = (maxC - Min3(src.r, src.g, src.b)) / max(maxC, 1e-4);
                float val = PositivePow(maxC, 1.0 / 2.2);
                float accent = smoothstep(_Grade2.z, _Grade2.z + 0.15, sat)
                             * smoothstep(0.5, 0.75, val) * (1.0 - fog) * _Grade2.w;
                // Niebo zawsze idzie w palete - zachod slonca nie jest akcentem.
                if (sky) accent = 0.0;
                // Akcent zachowuje barwe, ale dostaje te same pasy jasnosci co reszta obrazu.
                float srcGamma = max(PositivePow(srcLum, 1.0 / 2.2), 1e-4);
                float banded = Posterize(srcGamma, _Grade.w, _Grade2.x);
                float3 accentColor = src * PositivePow(banded / srcGamma, 2.2);
                graded = lerp(graded, accentColor, accent);

                if (!sky && _Outline.x > 0.0)
                {
                    float fade = saturate(1.0 - eye / _OutlineColor.a) * (1.0 - fog);
                    graded = lerp(graded, _OutlineColor.rgb, OutlineMask(uv, eye) * _Outline.x * fade);
                }

                
                // Preserve the celestial sky; the daytime palette would erase stars and moon.
                float day=lerp(1.0,_LeewayDaylight,_LeewayDayNightEnabled);
                graded*=lerp(float3(0.22,0.30,0.46),float3(1,1,1),day);
                if(sky && _LeewayDayNightEnabled>0.5) return half4(src,1);
                return half4(lerp(src, graded, _Grade.x * (sky ? 1.0 - _LeewayPlanetView : 1.0)), 1.0);
            }
            ENDHLSL
        }
    }
}
