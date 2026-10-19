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
// 0 on the ground, 1 once the camera has left the air: the sky goes black and the stars come out.
float _LeewaySpace;
// Angular radius of the moon in radians; zero until something sets it.
float _LeewayMoonSize;
// The turn of the planet: takes a direction in the sky to its place among the fixed stars.
float4x4 _LeewaySkyRotation;
float _LeewayDaylight;
float3 _LeewaySunDirection;
float3 _LeewayMoonDirection;
// Stars scattered evenly over the sphere: a coarse 3D grid, one candidate per cell, so no pole pinches them.
float3 LeewayPcg3(int3 c) {
 uint3 v=asuint(c)*1664525u+1013904223u;
 v.x+=v.y*v.z; v.y+=v.z*v.x; v.z+=v.x*v.y;
 v^=v>>16u;
 v.x+=v.y*v.z; v.y+=v.z*v.x; v.z+=v.x*v.y;
 return float3(v>>8u)*(1.0/16777216.0);
}
float LeewayStars(float3 ray, float density, float size) {
 int3 cell=(int3)floor(ray*70.0);
 float3 rnd=LeewayPcg3(cell);
 float3 jitter=LeewayPcg3(cell+int3(911,277,433));
 float3 dir=normalize((float3)cell+0.3+jitter*0.4);
 float3 offset=cross(ray,dir);
 float angle2=dot(offset,offset);
 float radius=size*(0.55+0.9*rnd.y);
 return step(1.0-density,rnd.x)*smoothstep(radius*radius,radius*radius*0.25,angle2)*(0.45+0.55*rnd.z);
}

half3 LeewayCloudSky(float3 ray, float time) {
 ray=normalize(ray);
 float3 localUp=LeewayUpAt(_WorldSpaceCameraPos);
 float3 horizonRay=float3(dot(ray,LeewayRightAt(localUp)),dot(ray,localUp),dot(ray,LeewayForwardAt(localUp)));
 float3 fixedRay=mul((float3x3)_LeewaySkyRotation,ray);
 fixedRay=dot(fixedRay,fixedRay)>0.5?normalize(fixedRay):ray;
 float space=saturate(_LeewaySpace);
 half3 sky=lerp(half3(0.64,0.76,0.68),half3(0.24,0.48,0.62),pow(saturate(horizonRay.y),0.6));
 float2 drift=_LeewayWindEnabled>0.5?_LeewayCloudOffset.xy:LeewayWindDirection()*LeewayWeatherTime()*0.025;
 float2 p=horizonRay.xz/max(horizonRay.y+0.12,0.04)*2.1-drift;
 float n=LeewayCloudNoise(p)*0.62+LeewayCloudNoise(p*2.03+7.1)*0.26+LeewayCloudNoise(p*4.07)*0.12;
 float cloud=smoothstep(0.53,0.58,n)*smoothstep(0.015,0.10,horizonRay.y);
 float shade=smoothstep(0.56,0.70,n);
 shade=floor(shade*3.0+0.5)/3.0;
 half3 cloudColor=lerp(half3(0.57,0.66,0.66),half3(0.95,0.95,0.84),shade);

 float day=lerp(1.0,_LeewayDaylight,_LeewayDayNightEnabled);
 float twilight=1-abs(day*2-1);
 half3 nightSky=lerp(half3(0.025,0.045,0.085),half3(0.008,0.016,0.044),pow(saturate(horizonRay.y),0.6));
 sky=lerp(nightSky,sky,day);
 float sunset=pow(saturate(dot(ray,normalize(_LeewaySunDirection+localUp*0.12))),5)*twilight;
 sky=lerp(sky,half3(0.92,0.38,0.20),sunset*0.7);
 // By night the stars come out, turning with the planet and hidden by clouds; outside the air they fill the whole sky.
 float nightStars=LeewayStars(fixedRay,0.05,0.0018)*smoothstep(0.05,0.25,horizonRay.y)*(1-day);
 float deepStars=LeewayStars(fixedRay,lerp(0.05,0.16,space),0.0016)+LeewayStars(fixedRay*1.9+3.1,0.2,0.0011)*0.8;
 float starLight=lerp(nightStars,deepStars,space);
 sky+=starLight*half3(0.65,0.75,1)*0.9;

 // The galaxy: a soft band of haze across the sky, seen only from outside the air.
 float band=exp(-pow(dot(fixedRay,normalize(float3(0.35,0.82,0.45)))*4.5,2));
 float haze=LeewayCloudNoise(fixedRay.xz*7.0+fixedRay.y*5.0)*0.7+LeewayCloudNoise(fixedRay.zy*17.0)*0.3;
 half3 spaceSky=half3(0.004,0.006,0.016)+band*haze*half3(0.12,0.10,0.2)*(0.5+0.5*haze);
 sky=lerp(sky,spaceSky+starLight*half3(0.65,0.75,1)*0.9,space);

 // The discs of the sun and the moon belong to the sky only while there is air to see them through;
 // beyond it the bodies themselves are drawn.
 float discs=1.0-smoothstep(0.1,0.7,space);
 float sunDot=dot(ray,_LeewaySunDirection),moonDot=dot(ray,_LeewayMoonDirection);
 float sunDisc=smoothstep(cos(0.018),cos(0.014),sunDot)*smoothstep(-0.02,0.03,horizonRay.y);
 float moonRadius=_LeewayMoonSize>0.0?_LeewayMoonSize:0.02;
 float moonDisc=smoothstep(cos(moonRadius*1.1),cos(moonRadius*0.9),moonDot)*smoothstep(-0.02,0.03,horizonRay.y);
 sky+=_LeewayDayNightEnabled*discs*(sunDisc*half3(2.5,1.65,0.75)+pow(saturate(sunDot),180)*half3(0.18,0.08,0.025));
 // A softly mottled moon remains readable without a photographic texture.
 float crater=0.85+0.15*LeewayCloudNoise(horizonRay.xz*420);
 sky=lerp(sky,half3(0.69,0.79,0.95)*crater,moonDisc*_LeewayDayNightEnabled*discs);
 cloudColor=lerp(half3(0.055,0.075,0.125),cloudColor,day);
 cloudColor=lerp(cloudColor,half3(0.68,0.40,0.32),sunset*0.4);
 return lerp(sky,cloudColor,cloud*0.94*(1.0-smoothstep(0.0,0.4,space)));

}
#endif
