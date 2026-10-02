using UnityEngine;

namespace Marea.Cooking
{
    public class SeafoodSkewersTimingUI : MonoBehaviour
    {
        [Header("UI 구성 요소")]
        [SerializeField] private RectTransform gaugeBackground;
        [SerializeField] private RectTransform targetZone;
        [SerializeField] private RectTransform indicator;

        [Header("타이밍 속도 및 판정 범위")]
        [SerializeField] private float moveSpeed = 400f;
        [SerializeField] private float perfectRange = 40f;
        [SerializeField] private float goodRange = 80f;

        private float _minX;
        private float _maxX;
        private int _direction = 1;
        private bool _isActive;

        private void OnEnable()
        {
            ShowGauge();
        }

        public void ShowGauge()
        {
            gameObject.SetActive(true);

            if (gaugeBackground != null)
            {
                float halfWidth = gaugeBackground.rect.width * 0.5f;
                _minX = -halfWidth;
                _maxX = halfWidth;
            }

            if (indicator != null)
            {
                indicator.anchoredPosition = new Vector2(_minX, 0f);
            }

            _isActive = true;
        }

        public void HideGauge()
        {
            _isActive = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isActive || indicator == null) return;

            Vector2 pos = indicator.anchoredPosition;
            pos.x += _direction * moveSpeed * Time.deltaTime;

            if (pos.x >= _maxX)
            {
                pos.x = _maxX;
                _direction = -1;
            }
            else if (pos.x <= _minX)
            {
                pos.x = _minX;
                _direction = 1;
            }

            indicator.anchoredPosition = pos;
        }

        public HitGrade EvaluateSliceTiming()
        {
            if (indicator == null || targetZone == null) return HitGrade.Miss;

            float distance = Mathf.Abs(indicator.anchoredPosition.x - targetZone.anchoredPosition.x);

            if (distance <= perfectRange) return HitGrade.Perfect;
            if (distance <= goodRange) return HitGrade.Good;
            return HitGrade.Miss;
        }
    }
}
