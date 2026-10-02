// 끓는 국물 (+10/2, 이슈 84)
// 조명은 URP Lit과 같은 계산(UniversalFragmentPBR) — 주방의 등불(보조광)에도 원래 국물처럼 밝다.
// 그 위에 정점을 노이즈로 들썩이고(건더기도 같은 메시라 같이 들썩인다) 텍스처를 살짝 일렁인다.
Shader "Marea/SoupSimmer"
{
    Properties
    {
        _BaseMap ("국물 텍스처", 2D) = "white" {}
        _BaseColor ("색", Color) = (1, 0.882, 0.714, 1)
        [Normal] _BumpMap ("노멀맵", 2D) = "bump" {}
        _BumpScale ("노멀 세기", Float) = 1
        _Smoothness ("매끈함", Range(0, 1)) = 0
        _AmbientScale ("주변광 세기 (원본 국물 밝기 맞춤)", Range(0, 1.5)) = 1

        [Header(Simmer)]
        _WaveAmp ("들썩임 높이 (오브젝트 단위)", Range(0, 0.02)) = 0.004
        _WaveFreq ("들썩임 촘촘함", Range(1, 80)) = 28
        _WaveSpeed ("들썩임 빠르기", Range(0, 10)) = 2.2
        _UVWarp ("텍스처 일렁임", Range(0, 0.02)) = 0.004
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale, _Smoothness;
                float _WaveAmp, _WaveFreq, _WaveSpeed, _UVWarp, _AmbientScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            // 겹친 사인 — 여기저기서 솟았다 꺼지는 들썩임.
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
                float t = _Time.y * _WaveSpeed;
                float2 p = v.positionOS.xz;
                float h = Simmer(p, t);
                const float e = 0.002;
                float dx = (Simmer(p + float2(e, 0), t) - h) / e;
                float dz = (Simmer(p + float2(0, e), t) - h) / e;

                // 메시 전체를 같이 민다. 윗면만 밀면 건더기 윗면과 옆면 사이가 벌어진다.
                // 물결 파장이 건더기보다 훨씬 커서 건더기는 통째로 떠오르듯 움직인다.
                float3 pos = v.positionOS.xyz + float3(0, h * _WaveAmp, 0);
                float3 n = normalize(v.normalOS + float3(-dx, 0, -dz) * _WaveAmp * saturate(v.normalOS.y));

                VertexPositionInputs pi = GetVertexPositionInputs(pos);
                VertexNormalInputs ni = GetVertexNormalInputs(n, v.tangentOS);
                o.positionCS = pi.positionCS;
                o.positionWS = pi.positionWS;
                o.normalWS = ni.normalWS;
                o.tangentWS = float4(ni.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                float2 warp = float2(sin(p.y * _WaveFreq * 0.7 + t * 1.3), cos(p.x * _WaveFreq * 0.6 - t)) * _UVWarp;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap) + warp;
                o.fog = ComputeFogFactor(pi.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                float3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                float3 nWS = normalize(TransformTangentToWorld(nTS, half3x3(i.tangentWS.xyz, bitangent, i.normalWS)));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = nWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fog;
                // 원본 Lit은 씬 프로브를 읽고 이건 기본 주변광(SH)이라 더 밝다 — 세기로 맞춘다.
                input.bakedGI = SampleSH(nWS) * _AmbientScale;
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surf = (SurfaceData)0;
                surf.albedo = albedo.rgb;
                surf.alpha = 1;
                surf.smoothness = _Smoothness;
                surf.metallic = 0;
                surf.occlusion = 1;
                surf.normalTS = nTS;

                half4 c = UniversalFragmentPBR(input, surf);
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }

        // 깊이 · 그림자는 Lit 것을 쓴다 (들썩임 몇 mm는 무시). 초점 흐림이 국물 깊이를 읽어야 해서 꼭 있어야 한다.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
