// Céu em degradê vertical (em espaço de tela), desenhado num quad preso à câmera, atrás de tudo.
Shader "Oiram/Sky"
{
    Properties
    {
        _TopColor("Topo", Color) = (0.36, 0.62, 0.95, 1)
        _HorizonColor("Horizonte", Color) = (0.78, 0.9, 1, 1)
        _BottomColor("Base", Color) = (0.62, 0.8, 0.98, 1)
        _HorizonHeight("Altura do horizonte", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _HorizonHeight;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screen : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float y = i.screen.y / i.screen.w;
                half3 below = lerp(_BottomColor.rgb, _HorizonColor.rgb, smoothstep(0.0, _HorizonHeight, y));
                half3 above = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(_HorizonHeight, 1.0, y));
                return half4(y < _HorizonHeight ? below : above, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
