Shader "Leeway/CloudSky" {
 SubShader {
 Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
 Cull Off ZWrite Off
 Pass {
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "CloudSky.hlsl"
 struct A { float4 positionOS:POSITION; };
 struct V { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
 V Vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.direction=v.positionOS.xyz; return o; }
 half4 Frag(V i):SV_Target { return half4(LeewayCloudSky(i.direction,_Time.y),1); }
 ENDHLSL
 }
 }
}