using UnityEngine;

namespace Marea.Cooking
{
    public class TimingBarController : MonoBehaviour
    {
        [Header("게이지 바 트랜스폼")]
        [SerializeField] private RectTransform movingBar;
        [SerializeField] private RectTransform gaugeBackground;

        [Header("판정 영역")]
        [SerializeField] private RectTransform perfectZone;
        [SerializeField] private RectTransform goodZone;

        [Header("이동 설정")]
        [SerializeField] private float moveSpeed = 600f;
        [SerializeField] private bool vertical;

        private float HalfLength => gaugeBackground == null ? 0f
            : (vertical ? gaugeBackground.rect.height : gaugeBackground.rect.width) * 0.5f;

        private void SetBarPosition(float value)
        {
            if (movingBar == null) return;
            Vector2 position = movingBar.anchoredPosition;
            if (vertical) position.y = value;
            else position.x = value;
            movingBar.anchoredPosition = position;
        }

        private float _minX;
        private float _maxX;
        private int _direction = 1;
        private bool _isRunning;

        public void Initialize()
        {
            if (gaugeBackground != null)
            {
                float halfWidth = HalfLength;
                _minX = -halfWidth;
                _maxX = halfWidth;
            }

            if (movingBar != null)
            {
                SetBarPosition(_minX);
            }

            _direction = 1;
            _isRunning = true;
        }

        public void StopGauge()
        {
            _isRunning = false;
        }

        public void SetTravelPosition(float normalizedPosition)
        {
            _isRunning = false;
            if (movingBar == null || gaugeBackground == null) return;
            float halfWidth = HalfLength;
            SetBarPosition(Mathf.Lerp(-halfWidth, halfWidth, Mathf.Clamp01(normalizedPosition)));
        }

        private void Update()
        {
            if (!_isRunning || movingBar == null) return;

            float position = vertical ? movingBar.anchoredPosition.y : movingBar.anchoredPosition.x;
            position += _direction * moveSpeed * Time.deltaTime;

            if (position >= _maxX)
            {
                position = _maxX;
                _direction = -1;
            }
            else if (position <= _minX)
            {
                position = _minX;
                _direction = 1;
            }

            SetBarPosition(position);
        }

        public HitGrade EvaluateHit()
        {
            if (movingBar == null) return HitGrade.Miss;

            if (IsInsideWorldZone(perfectZone)) return HitGrade.Perfect;
            if (IsInsideWorldZone(goodZone)) return HitGrade.Good;

            return HitGrade.Miss;
        }

        private bool IsInsideWorldZone(RectTransform zone)
        {
            if (zone == null) return false;

            Vector3 localPosition = zone.InverseTransformPoint(movingBar.position);
            return vertical
                ? localPosition.y >= zone.rect.yMin && localPosition.y <= zone.rect.yMax
                : localPosition.x >= zone.rect.xMin && localPosition.x <= zone.rect.xMax;
        }
    }
}
