// 구름 그림자 전용 — 화면에는 아무것도 안 그리고 섀도맵에만 그린다. (+10/5)
// 가장자리 띠(_Softness)에서는 디더링으로 일부 텍셀만 그림자를 드리우고,
// URP 소프트 섀도 필터가 그 점들을 뭉개 흐린 외곽을 만든다.
// 같은 디더링으로 _Strength < 1 이면 구름 안쪽도 일부만 가려 그림자가 옅어진다 (땅의 다른 그림자는 그대로).
//
// 생김새 값(크기·양·흐림·세기)은 이 머티리얼에서 조절한다 — 바로 반영되고 에셋에 남는다.
// CloudShadowScroller 는 _CloudOffset(바람)만 MaterialPropertyBlock 으로 넣는다.
// UV는 월드 XZ 기준 — 판이 카메라를 따라 움직여도 구름은 제자리.
Shader "Marea/CloudShadowCaster"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Cloud Texture (A)", 2D) = "white" {}
        _CloudSize ("Cloud Size (m)", Float) = 35
        _Threshold ("Threshold (높을수록 듬성듬성)", Range(0, 1)) = 0.72
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.15
        _Strength ("Shadow Strength", Range(0, 1)) = 1
        [HideInInspector] _CloudOffset ("Cloud Offset", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float _CloudSize;
                half _Threshold;
                half _Softness;
                half _Strength;
                float4 _CloudOffset;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                o.uv = positionWS.xz / max(_CloudSize, 0.01) + _CloudOffset.xy;
                return o;
            }

            half4 Frag(Varyings input) : SV_TARGET
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                // 0 = 그림자 없음, 1 = 꽉 찬 그림자. 임계값 주변 _Softness 폭이 그라데이션 띠.
                half density = saturate((a - _Threshold) / _Softness + 0.5) * _Strength;
                // 섀도맵 텍셀 단위 노이즈 (Interleaved Gradient Noise)
                half noise = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
                clip(density - noise - 0.0001);
                return 0;
            }
            ENDHLSL
        }
    }
}
