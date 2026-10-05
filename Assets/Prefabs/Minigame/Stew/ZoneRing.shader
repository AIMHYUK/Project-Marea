// 스튜 2단계 세이프존 원 (+10/2, 이슈 106)
// 국물 위 건더기에 가려지지 않게 깊이를 무시하고 그린다(ZTest Always). 색은 _BaseColor(코드가 안/밖으로 바꾼다).
Shader "Marea/ZoneRing"
{
    Properties
    {
        _BaseMap ("링 텍스처", 2D) = "white" {}
        _BaseColor ("색", Color) = (0.35, 1, 0.4, 0.9)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ZoneRing"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(_BaseColor.rgb * t.rgb, _BaseColor.a * t.a);
            }
            ENDHLSL
        }
    }
}
