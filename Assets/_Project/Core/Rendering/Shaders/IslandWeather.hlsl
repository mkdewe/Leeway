#ifndef LEEWAY_ISLAND_WEATHER
#define LEEWAY_ISLAND_WEATHER
// xy = world XZ direction, z = strength, w = cloud drift speed.
float4 _LeewayWind;
float _LeewayWindTime;
float4 _LeewayCloudOffset;
float _LeewayWindEnabled;
float LeewayWeatherTime() { return _LeewayWindEnabled > 0.5 ? _LeewayWindTime : _Time.y; }
float2 LeewayWindDirection() { return _LeewayWindEnabled > 0.5 ? _LeewayWind.xy : float2(0.91,0.41); }
#endif
