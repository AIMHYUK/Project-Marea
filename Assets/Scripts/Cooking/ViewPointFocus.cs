using UnityEngine;

namespace Marea.Cooking
{
    public enum MinigameBlurMode { Profile, Gaussian, Bokeh, Off }
    /// <summary>
    /// 미니게임 시점이 초점을 맞출 대상. 시점(ViewPoint) 오브젝트에 붙인다. (+10/2)
    ///
    /// MinigameCameraController는 이게 붙은 시점에서만 초점 흐림을 켠다 — 없으면 흐리지 않는다.
    /// (+10/2) 예전엔 없을 때 정면 레이캐스트를 썼는데, 주방 가구에 콜라이더가 없어 레이가 요리를 지나
    /// 뒷벽에 닿았고 요리가 흐려졌다.
    /// </summary>
    public class ViewPointFocus : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("이 시점에서만 쓸 조리개(f값). 0이면 프로필 값. 가까이서 내려다보는 시점은 심도가 얕아 조여야 대상 전체가 선명하다.")]
        [SerializeField, Min(0f)] private float aperture;

        [Header("이 시점의 배경 흐림")]
        [SerializeField] private MinigameBlurMode blurMode = MinigameBlurMode.Profile;
        [Tooltip("초점 대상보다 이 거리만큼 뒤에서 배경 흐림을 시작한다. 단위: m")]
        [SerializeField, Min(0f)] private float backgroundStartOffset = 0.6f;
        [Tooltip("흐림 시작부터 최대 강도에 도달하기까지의 거리. 단위: m")]
        [SerializeField, Min(0.1f)] private float backgroundFadeDistance = 2.5f;
        [SerializeField, Range(0.5f, 1.5f)] private float blurRadius = 0.8f;
        [SerializeField] private bool highQualitySampling = true;
        [SerializeField, Range(1f, 300f)] private float focalLength = 50f;

        // (+10/7, A) 거리 흐림은 대상과 같은 거리(같은 조리대 위 옆자리)를 못 흐린다 — 화면 가장자리를 따로 흐린다.
        [Header("가장자리 블러 (+10/7) — 0이면 끔")]
        [Tooltip("가장자리 흐림 세기 0~1. 0이면 이 시점에선 안 흐린다.")]
        [SerializeField, Range(0f, 1f)] private float edgeBlur;
        [Tooltip("화면 가운데(0)부터 이 반경까지는 선명. 모서리가 1.")]
        [SerializeField, Range(0f, 1f)] private float edgeInner = 0.35f;
        [Tooltip("이 반경에서 최대로 흐리다.")]
        [SerializeField, Range(0f, 1.2f)] private float edgeOuter = 0.85f;
        [Tooltip("최대 흐림 반경(1080p 기준 픽셀).")]
        [SerializeField, Range(1f, 60f)] private float edgeRadius = 24f;
        [Tooltip("가장자리를 이만큼 어둡게(비네트).")]
        [SerializeField, Range(0f, 1f)] private float edgeDarken = 0.25f;

        public float EdgeBlur => edgeBlur;
        public float EdgeInner => edgeInner;
        public float EdgeOuter => edgeOuter;
        public float EdgeRadius => edgeRadius;
        public float EdgeDarken => edgeDarken;

        public Transform Target => target;
        public float Aperture => aperture;
        public MinigameBlurMode BlurMode => blurMode;
        public float BackgroundStartOffset => backgroundStartOffset;
        public float BackgroundFadeDistance => backgroundFadeDistance;
        public float BlurRadius => blurRadius;
        public bool HighQualitySampling => highQualitySampling;
        public float FocalLength => focalLength;
    }
}
