#ifndef LEEWAY_CLOUD_SKY_INCLUDED
#define LEEWAY_CLOUD_SKY_INCLUDED
#include "IslandWeather.hlsl"
float LeewayCloudHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float LeewayCloudNoise(float2 p) {
 float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
 return lerp(lerp(LeewayCloudHash(i),LeewayCloudHash(i+float2(1,0)),f.x),
 lerp(LeewayCloudHash(i+float2(0,1)),LeewayCloudHash(i+1),f.x),f.y);
}
float _LeewayDayNightEnabled;
float _LeewayDaylight;
float3 _LeewaySunDirection;
float3 _LeewayMoonDirection;
half3 LeewayCloudSky(float3 ray, float time) {
 ray=normalize(ray);
 half3 sky=lerp(half3(0.64,0.76,0.68),half3(0.24,0.48,0.62),pow(saturate(ray.y),0.6));
 float2 drift=_LeewayWindEnabled>0.5?_LeewayCloudOffset.xy:LeewayWindDirection()*LeewayWeatherTime()*0.025;
 float2 p=ray.xz/max(ray.y+0.12,0.04)*2.1-drift;
 float n=LeewayCloudNoise(p)*0.62+LeewayCloudNoise(p*2.03+7.1)*0.26+LeewayCloudNoise(p*4.07)*0.12;
 float cloud=smoothstep(0.53,0.58,n)*smoothstep(0.015,0.10,ray.y);
 float shade=smoothstep(0.56,0.70,n);
 shade=floor(shade*3.0+0.5)/3.0;
 half3 cloudColor=lerp(half3(0.57,0.66,0.66),half3(0.95,0.95,0.84),shade);

 float day=lerp(1.0,_LeewayDaylight,_LeewayDayNightEnabled);
 float twilight=1-abs(day*2-1);
 half3 nightSky=lerp(half3(0.025,0.045,0.085),half3(0.008,0.016,0.044),pow(saturate(ray.y),0.6));
 sky=lerp(nightSky,sky,day);
 float sunset=pow(saturate(dot(ray,normalize(_LeewaySunDirection+float3(0,0.12,0)))),5)*twilight;
 sky=lerp(sky,half3(0.92,0.38,0.20),sunset*0.7);
 // Stable world-space stars, revealed by night and occluded by moving clouds.
 float2 starUV=float2(atan2(ray.z,ray.x)*90,asin(clamp(ray.y,-1,1))*90);
 float2 cell=floor(starUV);float h=LeewayCloudHash(cell);
 float2 local=frac(starUV)-float2(LeewayCloudHash(cell+19),LeewayCloudHash(cell+37));
 float stars=smoothstep(0.09,0.015,length(local))*step(0.985,h)*smoothstep(0.05,0.25,ray.y);
 sky+=stars*(1-day)*half3(0.65,0.75,1)*0.75;
 float sunDot=dot(ray,_LeewaySunDirection),moonDot=dot(ray,_LeewayMoonDirection);
 float sunDisc=smoothstep(cos(0.018),cos(0.014),sunDot)*smoothstep(-0.02,0.03,ray.y);
 float moonDisc=smoothstep(cos(0.022),cos(0.018),moonDot)*smoothstep(-0.02,0.03,ray.y);
 sky+=_LeewayDayNightEnabled*(sunDisc*half3(2.5,1.65,0.75)+pow(saturate(sunDot),180)*half3(0.18,0.08,0.025));
 // A softly mottled moon remains readable without a photographic texture.
 float crater=0.85+0.15*LeewayCloudNoise(ray.xz*420);
 sky=lerp(sky,half3(0.69,0.79,0.95)*crater,moonDisc*_LeewayDayNightEnabled);
 cloudColor=lerp(half3(0.055,0.075,0.125),cloudColor,day);
 cloudColor=lerp(cloudColor,half3(0.68,0.40,0.32),sunset*0.4);
 return lerp(sky,cloudColor,cloud*0.94);

}
#endif
