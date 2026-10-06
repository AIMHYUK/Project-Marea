using System;
using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 그릴 위에서 튀어 오르며 뒤집힌다 — 전복이 익어 "입 벌리는" 연출. 조개 프리팹 루트에 붙인다. (+10/6)
    ///
    /// Open()이면 flipTime 동안 hopHeight만큼 포물선으로 떴다가 내려오면서 flipAxis로 flipDegrees 돈다.
    /// 끝나면 PopupTouchGame이 slotOpenPrefabs(Clam_Junbok_OpenMouse — 같은 모델을 뒤집어 둔 프리팹)로 갈아 끼운다.
    /// 돌리는 중심은 모델 상자의 가운데 — 루트 원점(바닥)으로 돌면 석쇠 밑으로 파고든다.
    /// </summary>
    public class FlipOpen : ClamOpener
    {
        [Tooltip("루트 로컬 기준 뒤집는 축. 전복 OpenMouse 프리팹은 X축으로 180° 뒤집은 모습이다.")]
        [SerializeField] private Vector3 flipAxis = Vector3.right;
        [SerializeField] private float flipDegrees = 180f;
        [Tooltip("튀어 오르는 높이(m, 월드).")]
        [SerializeField, Min(0f)] private float hopHeight = 0.25f;
        [SerializeField, Min(0.05f)] private float flipTime = 0.5f;

        private float _t = -1f;
        private Vector3 _startPos, _pivot, _axisWorld;
        private Quaternion _startRot;
        private Action _onFinished;

        public override bool ReplaceWhenDone => true;

        public override void Open(Action onFinished)
        {
            if (_t >= 0f) return;
            _onFinished = onFinished;
            _startPos = transform.position;
            _startRot = transform.rotation;
            _axisWorld = transform.TransformDirection(flipAxis).normalized;
            _pivot = CenterOfRenderers();
            _t = 0f;
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / flipTime);

            // 도는 건 처음 느리고 가운데 빠르게(SmoothStep), 높이는 포물선.
            Quaternion spin = Quaternion.AngleAxis(flipDegrees * Mathf.SmoothStep(0f, 1f, _t), _axisWorld);
            Vector3 lift = Vector3.up * (hopHeight * 4f * _t * (1f - _t));
            transform.SetPositionAndRotation(_pivot + spin * (_startPos - _pivot) + lift, spin * _startRot);

            if (_t < 1f) return;
            _t = -1f;
            Action done = _onFinished;
            _onFinished = null;
            done?.Invoke();
        }

        private Vector3 CenterOfRenderers()
        {
            Bounds b = new Bounds(transform.position, Vector3.zero);
            bool has = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b.center;
        }
    }
}
