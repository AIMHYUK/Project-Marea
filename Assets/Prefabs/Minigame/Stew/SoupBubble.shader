// 국물 거품 (+10/2, 이슈 84)
// 가운데는 국물 색 · 텍스처, 가장자리로 갈수록 투명해져 국물 면에 녹아든다(프레넬).
// 빛 반사점 하나만 또렷하게 — 구슬처럼 보이는 공 음영은 일부러 약하게 둔다.
Shader "Marea/SoupBubble"
{
    Properties
    {
        _BaseMap ("국물 텍스처", 2D) = "white" {}
        _BaseColor ("국물 색", Color) = (0.86, 0.78, 0.64, 1)
        _CenterAlpha ("가운데 불투명도", Range(0, 1)) = 0.9
        _RimAlpha ("가장자리 불투명도", Range(0, 1)) = 0.0
        _FresnelPow ("가장자리 폭 (클수록 좁다)", Range(0.5, 6)) = 2.0
        _Shade ("공 음영 세기", Range(0, 1)) = 0.3
        _SpecPow ("반사점 크기 (클수록 작다)", Range(4, 256)) = 80
        _SpecStrength ("반사점 밝기", Range(0, 2)) = 0.9
        _RimTint ("가장자리 막 색", Color) = (1, 0.95, 0.85, 0.25)
        _HighlightDir ("반사점 빛 방향 (월드)", Vector) = (-0.4, 1, -0.6, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "SoupBubble"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _CenterAlpha, _RimAlpha, _FresnelPow, _Shade, _SpecPow, _SpecStrength;
                half4 _RimTint;
                float4 _HighlightDir;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                // 주방엔 주광(방향광)이 없고 등불 같은 보조광뿐이라 조명에 기대지 않는다.
                // 국물 밝기에 맞춘 자체 발색 + 고정 방향 반사점. 시점이 고정(냄비 내려다보기)이라 이게 안정적이다.
                float3 L = normalize(_HighlightDir.xyz);

                half ndv = saturate(dot(N, V));
                half fres = pow(1.0h - ndv, _FresnelPow);

                half3 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half ndl = saturate(dot(N, L));
                half3 col = baseCol * lerp(1.0h, 0.7h + 0.3h * ndl, _Shade);

                half spec = pow(saturate(dot(N, normalize(L + V))), _SpecPow) * _SpecStrength;
                col += spec;
                col = lerp(col, _RimTint.rgb, fres * _RimTint.a);

                half a = lerp(_CenterAlpha, _RimAlpha, fres);
                a = saturate(a + spec);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
