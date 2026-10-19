#ifndef LEEWAY_PLANET_FRAME
#define LEEWAY_PLANET_FRAME
// xyz = world centre, w = radius; zero selects the editor's flat mode.
float4 _LeewayPlanet;
float3 LeewayUpAt(float3 position)
{
    float3 offset=position-_LeewayPlanet.xyz;
    return _LeewayPlanet.w>0.0 && dot(offset,offset)>1e-8 ? normalize(offset) : float3(0,1,0);
}
float3 LeewayForwardAt(float3 up)
{
    float3 forward=float3(0,0,1)-up*up.z;
    if(dot(forward,forward)<1e-6)forward=float3(1,0,0)-up*up.x;
    return normalize(forward);
}
float3 LeewayRightAt(float3 up){return normalize(cross(up,LeewayForwardAt(up)));}
float3 LeewayTangentDirection(float3 position,float2 heading)
{
    float3 up=LeewayUpAt(position);
    return LeewayRightAt(up)*heading.x+LeewayForwardAt(up)*heading.y;
}
#endif
