// 끓기 전 맑은 물 (+10/7) — 스튜 1단계(재료 받기)에서 국물 대신 보이는 물 면.
// SoupSimmer와 같은 조명 계산(UniversalFragmentPBR)에 반투명으로 그린다 — 냄비 안쪽이 비쳐 맑아 보인다.
// 물 면은 평평한 원판이라 정점을 밀 수 없어서, 들썩임은 픽셀마다 노멀만 흔든다(반사가 일렁인다).
// 비스듬히 볼수록 덜 비치고 더 반사한다(프레넬).
Shader "Marea/WaterSimmer"
{
    Properties
    {
        _BaseColor ("물 색 (A = 정면에서 볼 때 불투명도)", Color) = (0.75, 0.9, 1, 0.25)
        _EdgeAlpha ("비스듬히 볼 때 불투명도", Range(0, 1)) = 0.7
        _FresnelPower ("프레넬 날카로움", Range(0.5, 8)) = 3
        _Smoothness ("매끈함", Range(0, 1)) = 0.95
        _AmbientScale ("주변광 세기", Range(0, 1.5)) = 1

        [Header(Simmer)]
        _RippleStrength ("물결 노멀 세기", Range(0, 1)) = 0.25
        _WaveFreq ("물결 촘촘함 (1/m)", Range(1, 80)) = 18
        _WaveSpeed ("물결 빠르기", Range(0, 10)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _EdgeAlpha, _FresnelPower, _Smoothness, _AmbientScale, _RippleStrength;
                float _WaveFreq, _WaveSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            // SoupSimmer와 같은 겹친 사인. 여기선 높이가 아니라 기울기만 쓴다.
            float Simmer(float2 p, float t)
            {
                float f = _WaveFreq;
                float h = sin(p.x * f + t) * sin(p.y * f * 1.13 - t * 0.83);
                h += 0.55 * sin((p.x + p.y) * f * 1.9 + t * 1.71);
                h += 0.35 * sin((p.x - p.y * 0.7) * f * 2.7 - t * 2.3);
                return h / 1.9;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pi = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pi.positionCS;
                o.positionWS = pi.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(pi.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // 월드 xz로 물결 기울기 — 냄비가 움직여도 물결 무늬가 크기 그대로.
                float t = _Time.y * _WaveSpeed;
                float2 p = i.positionWS.xz;
                const float e = 0.004;
                float h = Simmer(p, t);
                float dx = (Simmer(p + float2(e, 0), t) - h) / e;
                float dz = (Simmer(p + float2(0, e), t) - h) / e;
                float3 nWS = normalize(normalize(i.normalWS) + float3(-dx, 0, -dz) * (_RippleStrength / _WaveFreq));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = nWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fog;
                input.bakedGI = SampleSH(nWS) * _AmbientScale;
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surf = (SurfaceData)0;
                surf.albedo = _BaseColor.rgb;
                surf.alpha = 1;
                surf.smoothness = _Smoothness;
                surf.metallic = 0;
                surf.occlusion = 1;
                surf.normalTS = half3(0, 0, 1);

                half4 c = UniversalFragmentPBR(input, surf);
                c.rgb = MixFog(c.rgb, i.fog);

                half fresnel = pow(1 - saturate(dot(nWS, input.viewDirectionWS)), _FresnelPower);
                c.a = lerp(_BaseColor.a, _EdgeAlpha, fresnel);
                return c;
            }
            ENDHLSL
        }
    }
}
