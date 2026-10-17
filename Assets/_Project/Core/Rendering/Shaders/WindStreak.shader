Shader "Leeway/WindStreak" {
 SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off Cull Off
 Pass {
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float4 positionOS:POSITION; half4 color:COLOR; };
 struct V { float4 positionCS:SV_POSITION; half4 color:COLOR; };
 V Vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color;return o; }
 half4 Frag(V i):SV_Target { return i.color; }
 ENDHLSL
 }
 }
}