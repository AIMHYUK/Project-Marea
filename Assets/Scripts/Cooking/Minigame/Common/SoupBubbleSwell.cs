using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 국물에서 거품이 부풀어 오르는 연출. 거품 프리팹 루트에 붙인다. (+10/2, 이슈 84)
    ///
    /// PopupTouchGame은 루트 크기를 0.15초 만에 키우고 그대로 둔다. 그 위에서 이 부품이
    /// 돔(반구) 자식의 높이만 따로 움직인다 — 국물 면에 납작하게 깔렸다가 천천히 솟고,
    /// 솟는 동안 숨 쉬듯 출렁인다. 돔의 중심은 국물 면에 두어 아래 반쪽은 국물에 잠긴다.
    /// </summary>
    public class SoupBubbleSwell : MonoBehaviour
    {
        [Tooltip("높이가 움직일 돔. 중심이 국물 면 높이에 있어야 한다.")]
        [SerializeField] private Transform dome;
        [Tooltip("국물 면에 닿는 테두리(선택). 돔이 솟을수록 조금 넓어진다.")]
        [SerializeField] private Transform rim;

        [Header("부풀기")]
        [Tooltip("다 솟을 때까지 걸리는 시간(초). 거품 수명(1.2~2초)보다 조금 짧게.")]
        [SerializeField, Min(0.05f)] private float swellTime = 1.1f;
        [Tooltip("처음 높이 / 다 솟은 높이 (돔 지름 대비).")]
        [SerializeField] private Vector2 heightRange = new Vector2(0.08f, 0.85f);
        [Tooltip("처음 폭 / 다 솟은 폭.")]
        [SerializeField] private Vector2 widthRange = new Vector2(0.55f, 1f);

        [Header("출렁임")]
        [SerializeField, Range(0f, 0.2f)] private float wobble = 0.06f;
        [SerializeField, Min(0f)] private float wobbleSpeed = 9f;

        private float _age;
        private float _phase;
        private Vector3 _rimScale;

        private void Awake()
        {
            if (dome == null) Debug.LogError($"{name}: SoupBubbleSwell.dome이 비어 있다. 부풀지 않는다.", this);
            if (rim != null) _rimScale = rim.localScale;
            _phase = Random.value * Mathf.PI * 2f;   // 여러 개가 같은 박자로 출렁이지 않게
            Apply(0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            Apply(_age);
        }

        private void Apply(float age)
        {
            if (dome == null) return;

            // 처음엔 빨리, 끝에선 천천히 솟는다 (ease-out).
            float t = Mathf.Clamp01(age / swellTime);
            float e = 1f - (1f - t) * (1f - t);

            // 솟는 동안 출렁이고, 다 솟으면 팽팽하게 잦아든다.
            float w = Mathf.Sin(age * wobbleSpeed + _phase) * wobble * (1f - 0.6f * t);

            float h = Mathf.Lerp(heightRange.x, heightRange.y, e) * (1f + w);
            float r = Mathf.Lerp(widthRange.x, widthRange.y, e) * (1f - w * 0.5f);
            dome.localScale = new Vector3(r, h, r);

            if (rim != null) rim.localScale = _rimScale * Mathf.Lerp(0.7f, 1f, e) * (r / Mathf.Max(0.01f, widthRange.y));
        }
    }
}
