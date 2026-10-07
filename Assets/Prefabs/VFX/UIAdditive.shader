// UI 위 파티클을 화면에 얹는다. (+10/6, 이슈 117)
// UI 카메라가 검은(투명) 바탕에 그린 파티클 텍스처를 더하기로 섞는다 — 검은 곳은 아무것도 안 바뀐다.
Shader "Marea/UI Additive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                return fixed4(c.rgb * i.color.rgb * i.color.a, 1);
            }
            ENDCG
        }
    }
}
