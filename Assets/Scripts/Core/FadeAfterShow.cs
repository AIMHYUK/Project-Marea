using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 켜질 때는 선명하게 보였다가, 몇 초 뒤 흐려진다. (+10/12)
    ///
    /// 처음 붙인 곳은 영업 타이머(B의 BusinessUIController.timerRoot)다. 영업이 시작될 때 켜지므로
    /// "켜진 뒤 5초" = "영업 시작 뒤 5초"가 된다. 계속 떠 있는 시간 표시가 게임을 가린다는 피드백이었다.
    /// B 코드는 건드리지 않으려고 따로 붙는 컴포넌트로 만들었다 — 끄고 켜는 건 B가 하고, 여기는 보이는 정도만 정한다.
    ///
    /// 시간은 unscaled 로 센다. 미니게임 · 연출이 timeScale 을 바꿔도 같은 박자로 흐려진다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FadeAfterShow : MonoBehaviour
    {
        [Tooltip("켜진 뒤 선명하게 두는 시간(초).")]
        [SerializeField, Min(0f)] private float holdSeconds = 5f;

        [Tooltip("흐려지는 데 걸리는 시간(초).")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;

        [Tooltip("흐려진 뒤의 불투명도. 0 = 안 보임, 1 = 그대로.")]
        [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.45f;

        private CanvasGroup _group;
        private float _shownAt;

        private void Awake() => _group = GetComponent<CanvasGroup>();

        private void OnEnable()
        {
            _shownAt = Time.unscaledTime;
            if (_group != null) _group.alpha = 1f;
        }

        private void Update()
        {
            if (_group == null) return;
            float t = Time.unscaledTime - _shownAt - holdSeconds;
            float k = fadeSeconds > 0f ? Mathf.Clamp01(t / fadeSeconds) : (t >= 0f ? 1f : 0f);
            _group.alpha = Mathf.Lerp(1f, fadedAlpha, k);
        }
    }
}
