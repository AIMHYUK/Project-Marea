using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 캐릭터 머리 위에 잠깐 튀어나오는 말풍선 이모트(❗ 등). (+10/8) Kenney Emotes Pack 그림을 쓴다.
    ///
    /// 머리 위 자식 SpriteRenderer 하나를 카메라 쪽으로 돌려 세운다. Show를 부르면 톡 커졌다가
    /// 정해진 시간 뒤 줄어들며 사라진다. 다시 부르면 처음부터 다시.
    /// </summary>
    public class EmotePopup : MonoBehaviour
    {
        [Tooltip("캐릭터 발밑(이 오브젝트 원점)에서 이모트 아래 끝까지 높이(m).")]
        [SerializeField] private float height = 2.3f;
        [Tooltip("이모트 높이(m).")]
        [SerializeField, Min(0.1f)] private float size = 0.7f;
        [SerializeField, Min(0.1f)] private float defaultSeconds = 1.2f;
        [SerializeField, Min(0.01f)] private float popSeconds = 0.18f;

        private SpriteRenderer _sr;
        private float _shownAt;
        private float _hideAt;
        private bool _showing;

        private void Awake()
        {
            var go = new GameObject("Emote");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 100;
            go.SetActive(false);
        }

        /// <summary>sprite를 머리 위에 띄운다. seconds가 0 이하면 기본 시간.</summary>
        public void Show(Sprite sprite, float seconds = 0f)
        {
            if (sprite == null || _sr == null) return;
            _sr.sprite = sprite;
            _shownAt = Time.time;
            _hideAt = Time.time + (seconds > 0f ? seconds : defaultSeconds);
            _showing = true;
            _sr.gameObject.SetActive(true);
            Place(0f);
        }

        public void Hide()
        {
            _showing = false;
            if (_sr != null) _sr.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_showing) return;
            float t = Time.time;
            if (t >= _hideAt + popSeconds) { Hide(); return; }

            // 나올 때 0 → 1.15 → 1, 들어갈 때 1 → 0.
            float k = t < _hideAt
                ? Pop(Mathf.Clamp01((t - _shownAt) / popSeconds))
                : 1f - Mathf.Clamp01((t - _hideAt) / popSeconds);
            Place(k);
        }

        private void Place(float k)
        {
            Transform e = _sr.transform;
            Camera cam = Camera.main;
            if (cam != null) e.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

            float spriteH = _sr.sprite != null ? _sr.sprite.bounds.size.y : 1f;
            float s = spriteH > 0f ? size / spriteH : 1f;
            Vector3 parentScale = transform.lossyScale;
            float inv = parentScale.y != 0f ? 1f / parentScale.y : 1f;
            e.localScale = Vector3.one * (s * k * inv);
            // 아래 끝(말풍선 꼬리)이 height에 오게 — 피벗이 가운데라 반 높이만큼 올린다.
            e.position = transform.position + Vector3.up * (height + size * 0.5f * k);
        }

        private static float Pop(float x)
            => x < 0.7f ? Mathf.Lerp(0f, 1.15f, x / 0.7f) : Mathf.Lerp(1.15f, 1f, (x - 0.7f) / 0.3f);
    }
}
