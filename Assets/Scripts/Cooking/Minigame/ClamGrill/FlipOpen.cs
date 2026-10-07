using System;
using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 그릴 위에서 튀어 오르며 뒤집힌다 — 전복이 익어 "입 벌리는" 연출. 굽기 첫 모습(껍데기 등이 위인 프리팹) 루트에 붙인다. (+10/6)
    ///
    /// swapBeforeFlip(기본): 뒤집기 전에 열린 모습(slotOpenPrefabs — 살 있는 전복)으로 먼저 갈아 끼우고,
    /// 그 새 모습을 뒤집힌 자세(껍데기가 위 — 바뀐 게 안 보인다)에서 시작해 제자리까지 튀어 오르며 돌린다.
    /// 공중에서 살이 드러나고, 끝에 프리팹을 바꾸지 않아서 내려앉는 순간 튀지 않는다.
    /// 새 모습에는 이 컴포넌트를 그 자리에서 붙여 돌린다 — 살 있는 프리팹은 닦기 단계에서도 써서 프리팹에 붙이면 안 된다.
    ///
    /// swapBeforeFlip을 끄면 예전처럼 이 모습을 뒤집은 뒤 열린 모습으로 갈아 끼운다(ReplaceWhenDone).
    /// 돌리는 중심은 모델 상자의 가운데 — 루트 원점(바닥)으로 돌면 석쇠 밑으로 파고든다.
    /// </summary>
    public class FlipOpen : ClamOpener
    {
        [Tooltip("루트 로컬 기준 뒤집는 축. 전복은 X축으로 180° 뒤집으면 껍데기 등 ↔ 살이 바뀐다.")]
        [SerializeField] private Vector3 flipAxis = Vector3.right;
        [SerializeField] private float flipDegrees = 180f;
        [Tooltip("튀어 오르는 높이(m, 월드).")]
        [SerializeField, Min(0f)] private float hopHeight = 0.25f;
        [SerializeField, Min(0.05f)] private float flipTime = 0.5f;
        [Tooltip("켜면 뒤집기 전에 열린 모습으로 먼저 갈아 끼운다(공중에서 살이 보인다). 끄면 뒤집은 뒤 갈아 끼운다.")]
        [SerializeField] private bool swapBeforeFlip = true;
        [Tooltip("갈아 끼운 열린 모습에서, 돌다가 옆으로 선(반 바퀴 지점) 뒤에야 보일 자식 이름 — 살. "
               + "뒤집힌 시작 자세에선 이게 껍데기 밖으로 비쳐서 갈아 끼운 게 티 난다. 비우면 다 보인다.")]
        [SerializeField] private string revealChildName = "Clam_Cooked_Junbook";

        // 돌리는 기준 자세(base) — 각도 a에서 pivot + R(a)·(basePos - pivot), R(a)·baseRot
        private float _t = -1f, _a0, _a1;
        private Vector3 _basePos, _pivot, _axisWorld;
        private Quaternion _baseRot;
        private Action _onFinished;
        private Renderer[] _hidden;   // 반 바퀴 지나면 켤 렌더러

        public override bool ReplaceWhenDone => !swapBeforeFlip;
        public override bool SwapBeforeOpen => swapBeforeFlip;

        public override void Open(Action onFinished)
        {
            if (_t >= 0f) return;
            Begin(0f, flipDegrees, onFinished);
        }

        public override void PlayOnSwapped(GameObject swapped, Action onFinished)
        {
            FlipOpen into = swapped.AddComponent<FlipOpen>();
            into.flipAxis = flipAxis;
            into.flipDegrees = flipDegrees;
            into.hopHeight = hopHeight;
            into.flipTime = flipTime;
            into.swapBeforeFlip = false;
            Transform reveal = string.IsNullOrEmpty(revealChildName) ? null : swapped.transform.Find(revealChildName);
            if (reveal != null)
            {
                into._hidden = reveal.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in into._hidden) r.enabled = false;
            }
            // 제자리 자세가 끝 — 뒤집힌 자세(-flipDegrees)에서 시작해 0까지 돈다.
            into.Begin(-flipDegrees, 0f, onFinished);
        }

        private void Begin(float fromDegrees, float toDegrees, Action onFinished)
        {
            _onFinished = onFinished;
            _basePos = transform.position;
            _baseRot = transform.rotation;
            _axisWorld = transform.TransformDirection(flipAxis).normalized;
            _pivot = CenterOfRenderers();
            _a0 = fromDegrees;
            _a1 = toDegrees;
            _t = 0f;
            Pose(_a0, 0f);   // 시작 자세를 바로 — 갈아 끼운 첫 프레임에 제자리 모습이 번쩍 보이지 않게
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / flipTime);

            // 도는 건 처음 느리고 가운데 빠르게(SmoothStep), 높이는 포물선.
            float s = Mathf.SmoothStep(0f, 1f, _t);
            Pose(Mathf.Lerp(_a0, _a1, s), hopHeight * 4f * _t * (1f - _t));
            if (_hidden != null && s >= 0.5f) ShowHidden();

            if (_t < 1f) return;
            ShowHidden();
            _t = -1f;
            Action done = _onFinished;
            _onFinished = null;
            done?.Invoke();
        }

        private void ShowHidden()
        {
            if (_hidden == null) return;
            foreach (Renderer r in _hidden) if (r != null) r.enabled = true;
            _hidden = null;
        }

        // 도중에 지워져도(눌러서 · 탐 · 단계 끝) 숨긴 채 남지 않게 — 모습째 지워지니 실제론 상관없지만 재사용 대비.
        private void OnDisable() => ShowHidden();

        private void Pose(float degrees, float lift)
        {
            Quaternion spin = Quaternion.AngleAxis(degrees, _axisWorld);
            transform.SetPositionAndRotation(_pivot + spin * (_basePos - _pivot) + Vector3.up * lift, spin * _baseRot);
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
