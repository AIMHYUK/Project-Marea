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

        private float _minX;
        private float _maxX;
        private int _direction = 1;
        private bool _isRunning;

        public void Initialize()
        {
            if (gaugeBackground != null)
            {
                float halfWidth = gaugeBackground.rect.width * 0.5f;
                _minX = -halfWidth;
                _maxX = halfWidth;
            }

            if (movingBar != null)
            {
                movingBar.anchoredPosition = new Vector2(_minX, movingBar.anchoredPosition.y);
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
            float halfWidth = gaugeBackground.rect.width * 0.5f;
            movingBar.anchoredPosition = new Vector2(
                Mathf.Lerp(-halfWidth, halfWidth, Mathf.Clamp01(normalizedPosition)), movingBar.anchoredPosition.y);
        }

        private void Update()
        {
            if (!_isRunning || movingBar == null) return;

            Vector2 pos = movingBar.anchoredPosition;
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

            movingBar.anchoredPosition = pos;
        }

        public HitGrade EvaluateHit()
        {
            if (movingBar == null) return HitGrade.Miss;

            float barWorldX = movingBar.position.x;

            if (IsInsideWorldZone(barWorldX, perfectZone)) return HitGrade.Perfect;
            if (IsInsideWorldZone(barWorldX, goodZone)) return HitGrade.Good;

            return HitGrade.Miss;
        }

        private bool IsInsideWorldZone(float barWorldX, RectTransform zone)
        {
            if (zone == null) return false;

            Vector3[] corners = new Vector3[4];
            zone.GetWorldCorners(corners);
            float leftX = corners[0].x;
            float rightX = corners[2].x;

            return barWorldX >= leftX && barWorldX <= rightX;
        }
    }
}
