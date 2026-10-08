// Toon do OiramRPG: luz em duas faixas (com sombra azulada), brilho de borda, destaque especular opcional,
// luzes adicionais (tochas) em faixas, emissão para o bloom e contorno por "casco invertido".
// O contorno usa normais suavizadas guardadas no UV3 (veja MeshLibrary) para não abrir nas quinas.
Shader "Oiram/Toon"
{
    Properties
    {
        [MainColor] _BaseColor("Cor", Color) = (1, 1, 1, 1)
        _ShadowColor("Tom da sombra (multiplica)", Color) = (0.66, 0.63, 0.84, 1)
        _ShadowThreshold("Limiar da sombra", Range(-1, 1)) = 0.02
        _ShadowSoftness("Suavidade da sombra", Range(0.001, 0.5)) = 0.035
        _AmbientStrength("Luz ambiente", Range(0, 1)) = 0.32
        _RimStrength("Brilho de borda", Range(0, 1)) = 0.25
        _Gloss("Brilho especular", Range(0, 1)) = 0
        [HDR] _EmissionColor("Emissão", Color) = (0, 0, 0, 1)
        _UseVertexColor("Usar cor de vértice", Float) = 0
        _OutlineWidth("Largura do contorno (m)", Float) = 0.022
        _OutlineColor("Cor do contorno", Color) = (0.12, 0.09, 0.1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst", Float) = 0
        _ZWrite("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _AmbientStrength;
            half _RimStrength;
            half _Gloss;
            half4 _EmissionColor;
            half _UseVertexColor;
            float _OutlineWidth;
            half4 _OutlineColor;
        CBUFFER_END

        // Luz ambiente em hemisfério (céu/chão), definida por cena (SceneLighting).
        half4 _OiramAmbientSky;
        half4 _OiramAmbientGround;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = lerp(half4(1, 1, 1, 1), v.color, _UseVertexColor);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Band(half x)
            {
                return smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, x);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n = normalize(i.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 albedo = _BaseColor.rgb * i.color.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half shadow = smoothstep(0.3, 0.7, mainLight.shadowAttenuation);
                half lit = Band(dot(n, mainLight.direction)) * shadow * mainLight.distanceAttenuation;

                half3 shadeColor = albedo * _ShadowColor.rgb;
                half3 litColor = albedo * mainLight.color;
                half3 color = lerp(shadeColor, litColor, lit);

                half3 ambient = lerp(_OiramAmbientGround.rgb, _OiramAmbientSky.rgb, n.y * 0.5 + 0.5);
                color += albedo * ambient * _AmbientStrength;

                half3 halfDir = normalize(mainLight.direction + viewDir);
                color += smoothstep(0.93, 0.96, dot(n, halfDir)) * _Gloss * lit * mainLight.color;

                half fresnel = 1.0 - saturate(dot(n, viewDir));
                half rim = smoothstep(0.58, 0.72, fresnel) * _RimStrength * (0.35 + 0.65 * lit);
                color += rim * (albedo * 0.5 + 0.5) * 0.45;

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    half energy = light.distanceAttenuation * light.shadowAttenuation * saturate(dot(n, light.direction) * 0.6 + 0.4);
                    color += albedo * light.color * (smoothstep(0.03, 0.08, energy) * 0.45 + energy * 0.55);
                LIGHT_LOOP_END
                #endif

                color += _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);
                return half4(color, _BaseColor.a * i.color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormal : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings OutlineVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                if (_OutlineWidth <= 0.0)
                {
                    // Sem contorno: triângulos degenerados (nada é desenhado).
                    o.positionCS = float4(0, 0, -2, 1);
                    return o;
                }
                float3 normalOS = dot(v.smoothNormal, v.smoothNormal) > 0.01 ? v.smoothNormal : v.normalOS;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                positionWS += normalWS * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack Off
}
