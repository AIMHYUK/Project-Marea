using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Marea.Core
{
    /// <summary>
    /// 미니게임 가장자리 블러 패스. (+10/7) URP 기본 풀스크린 패스에 "세기가 0이면 건너뛰기"만 더했다.
    ///
    /// PC_Renderer에 붙어 늘 있지만, 전역값 _MareaEdgeBlur가 0인 동안(평소)엔 패스를 안 넣어 비용이 없다.
    /// 켜고 끄는 건 MinigameCameraController가 시점(ViewPointFocus.EdgeBlur)마다 한다 — 기능(Feature)의
    /// SetActive로 끄지 않는 이유: 에디터에선 렌더러 에셋에 저장돼 플레이를 꺼도 남는다.
    /// </summary>
    public class EdgeBlurRendererFeature : FullScreenPassRendererFeature
    {
        public static readonly int StrengthId = Shader.PropertyToID("_MareaEdgeBlur");
        public static readonly int InnerId = Shader.PropertyToID("_MareaEdgeInner");
        public static readonly int OuterId = Shader.PropertyToID("_MareaEdgeOuter");
        public static readonly int RadiusId = Shader.PropertyToID("_MareaEdgeRadius");
        public static readonly int DarkenId = Shader.PropertyToID("_MareaEdgeDarken");

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Shader.GetGlobalFloat(StrengthId) <= 0f) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;   // 씬 뷰 · 미리보기는 흐리지 않는다
            base.AddRenderPasses(renderer, ref renderingData);
        }

        /// <summary>가장자리 블러를 켠다(strength 0이면 끈다).</summary>
        public static void Set(float strength, float inner, float outer, float radiusPx, float darken)
        {
            Shader.SetGlobalFloat(StrengthId, Mathf.Clamp01(strength));
            Shader.SetGlobalFloat(InnerId, inner);
            Shader.SetGlobalFloat(OuterId, Mathf.Max(inner + 0.01f, outer));
            Shader.SetGlobalFloat(RadiusId, radiusPx);
            Shader.SetGlobalFloat(DarkenId, Mathf.Clamp01(darken));
        }

        public static void Clear() => Shader.SetGlobalFloat(StrengthId, 0f);
    }
}
