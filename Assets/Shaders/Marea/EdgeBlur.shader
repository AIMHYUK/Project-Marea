// (+10/7, A) 미니게임 가장자리 블러 — 화면 가운데는 선명하게, 가장자리로 갈수록 흐리고 살짝 어둡게.
// EdgeBlurRendererFeature가 PC_Renderer에서 이 셰이더로 전체 화면을 한 번 그린다.
// 세기는 전역값 _MareaEdgeBlur(0이면 패스 자체를 건너뜀)로, MinigameCameraController가 시점(ViewPointFocus)마다 넣는다.
Shader "Hidden/Marea/EdgeBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "EdgeBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _MareaEdgeBlur;    // 0~1 전체 세기
            float _MareaEdgeInner;   // 이 반경(가운데 0, 화면 모서리 1)까지는 선명
            float _MareaEdgeOuter;   // 여기서 최대
            float _MareaEdgeRadius;  // 최대 흐림 반경(1080p 픽셀)
            float _MareaEdgeDarken;  // 가장자리 어둡게 0~1

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d = (uv - 0.5) * float2(aspect, 1.0);
                float r = length(d) / (0.5 * sqrt(aspect * aspect + 1.0));   // 모서리 = 1
                float m = smoothstep(_MareaEdgeInner, _MareaEdgeOuter, r) * _MareaEdgeBlur;
                if (m <= 0.001) return center;

                float2 step = _MareaEdgeRadius * m * (_ScreenParams.y / 1080.0) / _ScreenParams.xy;
                half3 acc = center.rgb;
                float w = 1.0;
                UNITY_UNROLL
                for (int k = 0; k < 24; k++)
                {
                    // 황금각 나선 — 링 무늬 없이 원판을 고르게 채운다.
                    float t = (k + 0.5) / 24.0;
                    float a = k * 2.39996;
                    float2 o = float2(cos(a), sin(a)) * sqrt(t);
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + o * step).rgb;
                    w += 1.0;
                }
                half3 blurred = acc / w;
                blurred *= 1.0 - _MareaEdgeDarken * m;
                return half4(blurred, center.a);
            }
            ENDHLSL
        }
    }
}
