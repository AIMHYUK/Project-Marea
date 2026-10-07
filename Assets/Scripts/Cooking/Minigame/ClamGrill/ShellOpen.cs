using System;
using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 두 조각 조개의 뚜껑(윗껍데기)을 경첩 축으로 돌려 여닫는다. 조개 프리팹 루트에 붙인다. (+10/6)
    ///
    /// 켜질 때는 닫힌 각도로 시작하고, Open()을 부르면 openTime 동안 열린 각도까지 돌아간다(끝에 살짝 튄다).
    /// 뚜껑 메시의 원점이 경첩이어야 한다 — 가리비 openMouse 두 조각은 원점이 경첩(뒤쪽 가운데)에 맞춰 나와 있다.
    /// 열린 뒤에도 이 모습 그대로 둔다(ReplaceWhenDone = false) — 전복(FlipOpen)과 다른 점.
    /// </summary>
    public class ShellOpen : ClamOpener
    {
        [Tooltip("돌릴 뚜껑. 원점이 경첩이어야 한다.")]
        [SerializeField] private Transform lid;
        [Tooltip("뚜껑 로컬 기준 경첩 축. 가리비 조각은 X(좌우)다.")]
        [SerializeField] private Vector3 hingeAxis = Vector3.right;
        [Tooltip("닫혔을 때 각도(도). 모델이 원래 열린 모습이라 그만큼 덮는 쪽으로 돌린다.")]
        [SerializeField] private float closedAngle = 30f;
        [Tooltip("열렸을 때 각도(도). 0이면 모델 원래 모습.")]
        [SerializeField] private float openAngle;
        [SerializeField, Min(0.01f)] private float openTime = 0.35f;
        [Tooltip("열린 끝에서 넘어갔다 돌아오는 정도(도). 0이면 안 튄다.")]
        [SerializeField, Min(0f)] private float overshoot = 6f;

        private Quaternion _rest;
        private float _t = -1f;   // 0~1 진행, 음수면 정지
        private Action _onFinished;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (lid == null)
            {
                Debug.LogError($"{name}: ShellOpen.lid가 비어 있다. 열리지 않는다.", this);
                enabled = false;
                return;
            }
            _rest = lid.localRotation;
            Close();
        }

        /// <summary>바로 닫는다(애니메이션 없음).</summary>
        public void Close()
        {
            if (lid == null) return;
            _t = -1f;
            IsOpen = false;
            SetAngle(closedAngle);
        }

        public override void Open(Action onFinished)
        {
            if (lid == null || IsOpen) return;
            IsOpen = true;
            _onFinished = onFinished;
            _t = 0f;
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / openTime);
            SetAngle(Mathf.LerpUnclamped(closedAngle, openAngle, EaseOutBack(_t)));
            if (_t < 1f) return;
            _t = -1f;
            Action done = _onFinished;
            _onFinished = null;
            done?.Invoke();
        }

        // 끝에서 overshoot만큼 넘어갔다 돌아온다. 각도 차이 기준으로 비율을 잡는다.
        private float EaseOutBack(float t)
        {
            float range = Mathf.Abs(openAngle - closedAngle);
            float s = range > 0.01f ? overshoot / range * 4f : 0f;
            float u = t - 1f;
            return 1f + (s + 1f) * u * u * u + s * u * u;
        }

        private void SetAngle(float degrees) => lid.localRotation = _rest * Quaternion.AngleAxis(degrees, hingeAxis);
    }
}
