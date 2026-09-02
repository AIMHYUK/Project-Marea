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

        private float _minY;
        private float _maxY;
        private int _direction = 1;
        private bool _isRunning;

        public void Initialize()
        {
            if (gaugeBackground != null)
            {
                float halfHeight = gaugeBackground.rect.height * 0.5f;
                _minY = -halfHeight;
                _maxY = halfHeight;
            }

            if (movingBar != null)
            {
                movingBar.anchoredPosition = new Vector2(movingBar.anchoredPosition.x, _minY);
            }

            _direction = 1;
            _isRunning = true;
        }

        public void StopGauge()
        {
            _isRunning = false;
        }

        private void Update()
        {
            if (!_isRunning || movingBar == null) return;

            Vector2 pos = movingBar.anchoredPosition;
            pos.y += _direction * moveSpeed * Time.deltaTime;

            if (pos.y >= _maxY)
            {
                pos.y = _maxY;
                _direction = -1;
            }
            else if (pos.y <= _minY)
            {
                pos.y = _minY;
                _direction = 1;
            }

            movingBar.anchoredPosition = pos;
        }

        public HitGrade EvaluateHit()
        {
            if (movingBar == null) return HitGrade.Miss;

            float barWorldY = movingBar.position.y;

            if (IsInsideWorldZone(barWorldY, perfectZone)) return HitGrade.Perfect;
            if (IsInsideWorldZone(barWorldY, goodZone)) return HitGrade.Good;

            return HitGrade.Miss;
        }

        private bool IsInsideWorldZone(float barWorldY, RectTransform zone)
        {
            if (zone == null) return false;

            Vector3[] corners = new Vector3[4];
            zone.GetWorldCorners(corners);
            float bottomY = corners[0].y;
            float topY = corners[1].y;

            return barWorldY >= bottomY && barWorldY <= topY;
        }
    }
}