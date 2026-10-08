// Brilho sem luz (partículas, pilares de loot, chamas, sombras-redondas suaves).
// _Shape: 0 = círculo suave pelo UV (quads/partículas), 1 = borda pela visão (cilindros/esferas), 2 = cor chapada.
Shader "Oiram/Glow"
{
    Properties
    {
        [HDR][MainColor] _BaseColor("Cor", Color) = (1, 1, 1, 1)
        _Shape("Forma (0 círculo, 1 fresnel, 2 chapado)", Float) = 0
        _Softness("Suavidade", Range(0.01, 1)) = 0.6
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Shape;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half mask = 1;
                if (_Shape < 0.5)
                {
                    half d = length(i.uv - 0.5) * 2.0;
                    mask = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                }
                else if (_Shape < 1.5)
                {
                    float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                    half facing = abs(dot(normalize(i.normalWS), viewDir));
                    mask = smoothstep(0.0, _Softness, facing);
                }
                half4 c = _BaseColor * i.color;
                c.a *= mask;
                // Blend SrcAlpha One (aditivo) ou SrcAlpha OneMinusSrcAlpha (sombra suave): o alfa faz o resto.
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
