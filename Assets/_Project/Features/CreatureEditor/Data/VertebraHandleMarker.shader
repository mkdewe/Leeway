// Marker kregu w edytorze stworkow.
//
// Rysuje sie ZAWSZE na wierzchu (ZTest Always, kolejka Overlay), bo kosc siedzi
// w srodku tkanki o promieniu ~0.3, a marker ma ~0.09 — przy normalnym tescie
// glebokosci bylby w calosci zasloniety przez wlasne cialo stworka.
//
// Kolor idzie z _BaseColor, ktory VertebraHandle nadpisuje przez
// MaterialPropertyBlock przy podswietleniu hoverem.
Shader "Leeway/VertebraHandleMarker"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.9, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            ZTest Always
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_BaseColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
