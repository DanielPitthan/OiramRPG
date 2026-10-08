// Água toon: ondinhas no vértice, faixas de brilho animadas e sombra dos objetos.
Shader "Oiram/Water"
{
    Properties
    {
        [MainColor] _BaseColor("Cor", Color) = (0.27, 0.6, 0.9, 1)
        _DeepColor("Cor funda", Color) = (0.16, 0.42, 0.78, 1)
        _FoamColor("Brilho", Color) = (0.92, 0.97, 1, 1)
        _WaveHeight("Altura da onda", Float) = 0.05
        _Speed("Velocidade", Float) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _DeepColor;
            half4 _FoamColor;
            float _WaveHeight;
            float _Speed;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            float Waves(float2 p, float t)
            {
                return sin(p.x * 0.9 + t * 0.8) + sin(p.y * 1.1 - t * 0.6) + sin((p.x + p.y) * 0.55 + t * 1.1);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y * _Speed;
                positionWS.y += Waves(positionWS.xz, t) * _WaveHeight * 0.33;
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.positionWS.xz;
                float w = Waves(p, t);
                // Manchas claras/escuras suaves + linhas de brilho finas onde as ondas se somam.
                half mix1 = smoothstep(-2.6, 2.6, w);
                half3 color = lerp(_DeepColor.rgb, _BaseColor.rgb, mix1);
                // Brilhos finos e ondulados (produto de senos "entortados"), concentrados nas cristas.
                float2 q = p * 1.7;
                float ripple = sin(q.x + sin(q.y * 1.3 + t) * 1.4 + t * 0.9) * sin(q.y * 1.15 + sin(q.x * 0.8 - t * 0.7) * 1.2 + t * 0.6);
                half glint = smoothstep(0.93, 0.985, ripple) * smoothstep(0.0, 2.0, w);
                color = lerp(color, _FoamColor.rgb, glint * 0.45);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half shadow = smoothstep(0.3, 0.7, mainLight.shadowAttenuation);
                color *= lerp(0.72, 1.0, shadow) * lerp(half3(1, 1, 1), mainLight.color, 0.5);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
