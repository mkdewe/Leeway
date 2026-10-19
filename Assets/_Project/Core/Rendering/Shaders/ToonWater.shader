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
        _FallFoam ("Waterfall Impact Foam", Range(0, 1)) = 0.35
        _RiverFlowSpeed ("River Flow Speed (metres/sec)", Float) = 1.3
        _FlowContrast ("Flow Brightness Variation", Range(0, 0.08)) = 0.03
        _FlowNormalStrength ("Flow Normal Strength", Range(0,0.1)) = 0.03
        _FlowDistortion ("Flow Reflection Distortion", Range(0, 0.01)) = 0.003
        _OceanWaveHeight ("Maximum Ocean Wave Height (metres)", Range(0, 0.15)) = 0.08
        _OceanWaveLength ("Ocean Wave Length", Float) = 18
        _OceanWaveSpeed ("Ocean Wave Speed", Float) = 1.8
        _LightInfluence ("Light Influence", Range(0, 1)) = 0.35
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.65
        _ReflectionDistortion ("Reflection Distortion", Range(0,0.05)) = 0.008
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
            float _RiverFlowSpeed;
            half _FlowContrast;
            float _FlowDistortion;
            float _FlowNormalStrength;
            float _OceanWaveHeight;
            float _OceanWaveLength;
            float _OceanWaveSpeed;
            half _LightInfluence;
            half _ReflectionStrength;
            float _ReflectionDistortion;
        CBUFFER_END
#include "CloudSky.hlsl"
        float _LeewayPlanetView;
        void ClipOcean(half4 colour)
        {
            // Blue marks streamed ocean vertices; alpha carries a signed shoreline mask.
            // Inland water keeps its own geometry and is not clipped by this ocean mask.
            clip(lerp(1.0, colour.a - 0.5, step(0.5, colour.b)));
        }
        float _LeewayReflectionCount;
TEXTURE2D(_LeewayReflection0); SAMPLER(sampler_LeewayReflection0); float4x4 _LeewayReflectionVP0; float _LeewayReflectionHeight0; float4 _LeewayReflectionPlane0;
TEXTURE2D(_LeewayReflection1); SAMPLER(sampler_LeewayReflection1); float4x4 _LeewayReflectionVP1; float _LeewayReflectionHeight1; float4 _LeewayReflectionPlane1;
TEXTURE2D(_LeewayReflection2); SAMPLER(sampler_LeewayReflection2); float4x4 _LeewayReflectionVP2; float _LeewayReflectionHeight2; float4 _LeewayReflectionPlane2;
TEXTURE2D(_LeewayReflection3); SAMPLER(sampler_LeewayReflection3); float4x4 _LeewayReflectionVP3; float _LeewayReflectionHeight3; float4 _LeewayReflectionPlane3;
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
TEXTURECUBE(_LeewayWaterEnvironment); SAMPLER(sampler_LeewayWaterEnvironment);
float4 _LeewayWaterEnvironmentHDR,_LeewayWaterEnvironmentPosition,_LeewayWaterEnvironmentExtents;
float _LeewayWaterEnvironmentValid;
half3 EnvironmentReflection(float3 pos,float3 ray)
{
    half3 sky=LeewayCloudSky(ray,LeewayWeatherTime());
    if(_LeewayWaterEnvironmentValid<0.5)return sky;
    float3 centre=_LeewayWaterEnvironmentPosition.xyz,extent=_LeewayWaterEnvironmentExtents.xyz;
    float outside=max(abs(pos.x-centre.x)/extent.x,max(abs(pos.y-centre.y)/extent.y,abs(pos.z-centre.z)/extent.z));
    float weight=1-smoothstep(0.75,1.25,outside);
    float3 lookup=ray;
    if(outside<1.0)
    {
        float3 safeRay=lerp(-1.0,1.0,step(0.0,ray))*max(abs(ray),0.0001);
        float3 bound=centre+lerp(-extent,extent,step(0.0,ray));
        float3 hit=(bound-pos)/safeRay;
        float distance=max(0,min(hit.x,min(hit.y,hit.z)));
        lookup=pos+ray*distance-centre;
    }
    half4 sample=SAMPLE_TEXTURECUBE_LOD(_LeewayWaterEnvironment,sampler_LeewayWaterEnvironment,lookup,0.6);
    return lerp(sky,DecodeHDREnvironment(sample,_LeewayWaterEnvironmentHDR),weight);
}
half3 WaterReflection(float3 pos,float3 ray,float2 distortion,float3 normal)
{
    half3 reflected=EnvironmentReflection(pos,ray);
    half3 planar=reflected;float bestWeight=0;

    if(_LeewayReflectionCount>0.5)
    {
        float4 projected=mul(_LeewayReflectionVP0,float4(pos,1));
        float2 uv=projected.xy/max(projected.w,0.0001)*0.5+0.5;
        #if UNITY_UV_STARTS_AT_TOP
        uv.y=1-uv.y;
        #endif
        uv+=distortion;
        float edge=min(min(uv.x,uv.y),min(1-uv.x,1-uv.y));
        float weight=saturate(dot(normal,_LeewayReflectionPlane0.xyz))*saturate(dot(normal,_LeewayReflectionPlane0.xyz))*exp2(-abs(dot(float4(pos,1),_LeewayReflectionPlane0))*0.9)*smoothstep(0,0.04,edge);
        if(projected.w>0&&weight>bestWeight)
        {planar=SAMPLE_TEXTURE2D(_LeewayReflection0,sampler_LeewayReflection0,saturate(uv)).rgb;bestWeight=weight;}
    }

    if(_LeewayReflectionCount>1.5)
    {
        float4 projected=mul(_LeewayReflectionVP1,float4(pos,1));
        float2 uv=projected.xy/max(projected.w,0.0001)*0.5+0.5;
        #if UNITY_UV_STARTS_AT_TOP
        uv.y=1-uv.y;
        #endif
        uv+=distortion;
        float edge=min(min(uv.x,uv.y),min(1-uv.x,1-uv.y));
        float weight=saturate(dot(normal,_LeewayReflectionPlane1.xyz))*saturate(dot(normal,_LeewayReflectionPlane1.xyz))*exp2(-abs(dot(float4(pos,1),_LeewayReflectionPlane1))*0.9)*smoothstep(0,0.04,edge);
        if(projected.w>0&&weight>bestWeight)
        {planar=SAMPLE_TEXTURE2D(_LeewayReflection1,sampler_LeewayReflection1,saturate(uv)).rgb;bestWeight=weight;}
    }

    if(_LeewayReflectionCount>2.5)
    {
        float4 projected=mul(_LeewayReflectionVP2,float4(pos,1));
        float2 uv=projected.xy/max(projected.w,0.0001)*0.5+0.5;
        #if UNITY_UV_STARTS_AT_TOP
        uv.y=1-uv.y;
        #endif
        uv+=distortion;
        float edge=min(min(uv.x,uv.y),min(1-uv.x,1-uv.y));
        float weight=saturate(dot(normal,_LeewayReflectionPlane2.xyz))*saturate(dot(normal,_LeewayReflectionPlane2.xyz))*exp2(-abs(dot(float4(pos,1),_LeewayReflectionPlane2))*0.9)*smoothstep(0,0.04,edge);
        if(projected.w>0&&weight>bestWeight)
        {planar=SAMPLE_TEXTURE2D(_LeewayReflection2,sampler_LeewayReflection2,saturate(uv)).rgb;bestWeight=weight;}
    }

    if(_LeewayReflectionCount>3.5)
    {
        float4 projected=mul(_LeewayReflectionVP3,float4(pos,1));
        float2 uv=projected.xy/max(projected.w,0.0001)*0.5+0.5;
        #if UNITY_UV_STARTS_AT_TOP
        uv.y=1-uv.y;
        #endif
        uv+=distortion;
        float edge=min(min(uv.x,uv.y),min(1-uv.x,1-uv.y));
        float weight=saturate(dot(normal,_LeewayReflectionPlane3.xyz))*saturate(dot(normal,_LeewayReflectionPlane3.xyz))*exp2(-abs(dot(float4(pos,1),_LeewayReflectionPlane3))*0.9)*smoothstep(0,0.04,edge);
        if(projected.w>0&&weight>bestWeight)
        {planar=SAMPLE_TEXTURE2D(_LeewayReflection3,sampler_LeewayReflection3,saturate(uv)).rgb;bestWeight=weight;}
    }
    return lerp(reflected,planar,saturate(bestWeight));
}

        float _LeewayWaterImpactCount;
        float4 _LeewayWaterImpactPositions[24];
        float4 _LeewayWaterImpactDirections[24];
        half ReceiverFoam(float3 rest,float3 normal,float time)
        {
            if(dot(normal,LeewayUpAt(rest))<0.65)return 0;
            half foam=0;
            [loop] for(int i=0;i<24;i++)
            {
                if(i>=(int)_LeewayWaterImpactCount)break;
                float4 hit=_LeewayWaterImpactPositions[i];
                float3 impactUp=LeewayUpAt(hit.xyz);
                float3 worldDelta=rest-hit.xyz;
                if(abs(dot(worldDelta,impactUp))>max(0.4,hit.w*0.15))continue;
                float3 impactRight=LeewayRightAt(impactUp),impactForward=LeewayForwardAt(impactUp);
                float2 delta=float2(dot(worldDelta,impactRight),dot(worldDelta,impactForward));
                float3 worldDirection=_LeewayPlanet.w>0?_LeewayWaterImpactDirections[i].xyz:
                    float3(_LeewayWaterImpactDirections[i].x,0,_LeewayWaterImpactDirections[i].y);
                float2 dir=float2(dot(worldDirection,impactRight),dot(worldDirection,impactForward));
                if(dot(delta,delta)>hit.w*hit.w*2.5)continue;
                float2 local=float2(dot(delta,dir)-hit.w*0.2,dot(delta,float2(-dir.y,dir.x)));
                local/=float2(hit.w*1.25,hit.w*0.8);
                float radius=length(local);
                if(radius>1)continue;
                float noise=LeewayCloudNoise(delta*0.85-dir*time*0.65);
                float ring=sin(radius*22-time*5+noise*3);
                float core=1-smoothstep(0.1,0.45,radius);
                float broken=smoothstep(0.22,0.68,noise+ring*0.18);
                float edge=1-smoothstep(0.62,1.0,radius);
                foam=max(foam,max(core*0.7,broken*0.5)*edge);
            }
            return foam;
        }

        // One world-space displacement function in colour, depth and normals passes.
        float3 OceanWave(float3 world, half4 data)
        {
            float mask=(1.0-saturate(data.g*4.0))*smoothstep(0.08,0.8,data.r);
            float lod=1.0-smoothstep(180.0,550.0,distance(world,_WorldSpaceCameraPos));
            float windStrength=_LeewayWindEnabled>0.5?_LeewayWind.z:0.8;
            // Height is a bound in metres, not the amplitude of each harmonic. Wind
            // and overlapping waves may not amplify it; shallow water has a tighter bound.
            float maximumHeight=min(clamp(_OceanWaveHeight,0.0,0.15),saturate(data.r)*0.3);
            float amplitude=maximumHeight*mask*lod*lerp(0.55,1.0,saturate(windStrength/1.2))/1.32;
            float2 d=LeewayWindDirection();
            float2 e=normalize(float2(d.x*0.7-d.y*0.7,d.x*0.7+d.y*0.7));
            float k=6.283185/max(_OceanWaveLength,2.0);
            float time=LeewayWeatherTime()*_OceanWaveSpeed;
            float3 axisA=_LeewayPlanet.w>0?normalize(float3(d.x,0.37,d.y)):float3(d.x,0,d.y);
            float3 axisB=_LeewayPlanet.w>0?normalize(float3(e.x,-0.61,e.y)):float3(e.x,0,e.y);
            float3 samplePosition=_LeewayPlanet.w>0?world-_LeewayPlanet.xyz:world;
            float p=dot(samplePosition,axisA)*k-time;
            float q=dot(samplePosition,axisB)*k*1.9-time*1.35;
            float height=amplitude*(sin(p)+0.32*sin(q));
            float3 gradient=amplitude*k*(axisA*cos(p)+axisB*0.608*cos(q));
            float3 up=LeewayUpAt(world);
            return float3(height,dot(gradient,LeewayRightAt(up)),dot(gradient,LeewayForwardAt(up)));
        }
        float3 WaterPosition(float3 world,half4 data)
        {
            world+=LeewayUpAt(world)*OceanWave(world,data).x;
            return world;
        }
        float3 WaterNormal(float3 world,float3 normal,half4 data)
        {
            float3 wave=OceanWave(world,data);
            float3 up=LeewayUpAt(world);
            return normalize(normal-LeewayRightAt(up)*wave.y-LeewayForwardAt(up)*wave.z);
        }
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
                float2 uv : TEXCOORD0;
                float2 flowData : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float2 flowUV : TEXCOORD4;
                float3 restPositionWS : TEXCOORD5;
                half flowAmount : TEXCOORD6;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 rest=TransformObjectToWorld(input.positionOS.xyz);
                output.restPositionWS=rest;
                output.flowUV=input.uv;
                output.flowAmount=input.flowData.x;
                output.positionWS=WaterPosition(rest,input.color);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=WaterNormal(rest,TransformObjectToWorldNormal(input.normalOS),input.color);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ClipOcean(input.color);
                float3 wpos = input.positionWS;
                half depth = input.color.r;
                half fresh = input.color.g;
                float t = LeewayWeatherTime();
                float3 up=LeewayUpAt(wpos);
                float3 right=LeewayRightAt(up),forward=LeewayForwardAt(up);
                float2 p = (_LeewayPlanet.w>0?float2(dot(wpos,float3(0.7,0.2,0.1)),dot(wpos,float3(0.1,-0.3,0.8))):wpos.xz) * 0.07;

                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.0, 1.0, depth));
                color = lerp(color, lerp(_FreshTint.rgb, _DeepColor.rgb, depth * 0.6), fresh);

                // Flow changes only shading. Its coordinates, colour and reflection blend
                // continuously with still water on exactly the same geometry.
                half moving=saturate(input.flowAmount)*fresh;
                float along=input.flowUV.y-t*_RiverFlowSpeed;
                float across=input.flowUV.x;
                float motion=sin(along*2.1+sin(across*3.0)*0.6);
                float filament=sin(across*17.0+sin(along*0.6)*0.15);
                float variation=motion*0.6+filament*0.4;
                color*=1.0+variation*_FlowContrast*moving;

                // Reflections retain the palette treatment of the main camera.
                float2 distortion=float2(sin(p.y*5.0+t*0.45),cos(p.x*4.0-t*0.32))*_ReflectionDistortion + float2(dot(input.normalWS,right),dot(input.normalWS,forward))*0.035*(1.0-fresh);
                distortion+=float2(motion,filament)*_FlowDistortion*moving;
                // Subpixel ripples must not alias into broad stripes in the orbital view.
                distortion*=1.0-smoothstep(120.0,550.0,distance(wpos,_WorldSpaceCameraPos));
                float3 surfaceNormal=normalize(lerp(input.normalWS,up,step(0.5,input.color.b)));
                float3 n=normalize(surfaceNormal+(right*distortion.x+forward*distortion.y)*3.0);
                if(moving>0.001)
                {
                    float2 ux=ddx(input.flowUV),uy=ddy(input.flowUV);
                    float det=ux.x*uy.y-ux.y*uy.x;
                    if(abs(det)>0.0000001)
                    {
                        float3 tangent=normalize((ddy(wpos)*ux.x-ddx(wpos)*uy.x)/det);
                        float3 crossTangent=normalize(cross(n,tangent));
                        n=normalize(n+(tangent*motion+crossTangent*filament*0.35)*_FlowNormalStrength*moving);
                    }
                }
                float3 view=GetWorldSpaceNormalizeViewDir(wpos);
                float fresnel=0.22+0.65*pow(1.0-saturate(dot(n,view)),3.0);
                half3 reflection=WaterReflection(input.restPositionWS,reflect(-view,n),distortion,n);
                color=lerp(color,reflection,_ReflectionStrength*fresnel*saturate(depth*5.0));
                
                // Static narrow shore foam; moving streaks belong only to flowing falls.
                half edge = _FoamWidth;
                half foam = 1.0 - step(edge, depth);
                foam=max(foam,ReceiverFoam(input.restPositionWS,normalize(input.normalWS),t));
                color = lerp(color, _FoamColor.rgb, saturate(foam));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(wpos));
                half lit = lerp(1.0 - _LightInfluence, 1.0, mainLight.shadowAttenuation);
                color *= lit * lerp(half3(1, 1, 1), mainLight.color, _LightInfluence);

                color *= lerp(1.0,lerp(0.38,1.0,_LeewayDaylight),_LeewayDayNightEnabled);
                color = lerp(color, MixFog(color, input.fogFactor), 1.0 - saturate(_LeewayPlanetView));
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

            struct DepthAttributes { float4 positionOS:POSITION; half4 color:COLOR; };
            struct DepthVaryings { float4 positionCS:SV_POSITION; half4 color:TEXCOORD0; };
            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS=TransformWorldToHClip(WaterPosition(TransformObjectToWorld(input.positionOS.xyz),input.color));
                output.color=input.color;
                return output;
            }
            half DepthFrag(DepthVaryings input) : SV_Target { ClipOcean(input.color); return 0; }
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
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : TEXCOORD1;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                float3 rest=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(WaterPosition(rest,input.color));
                output.normalWS=WaterNormal(rest,TransformObjectToWorldNormal(input.normalOS),input.color);
                output.color=input.color;
                return output;
            }

            half4 NormalsFrag(Varyings input) : SV_Target
            {
                ClipOcean(input.color);
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
