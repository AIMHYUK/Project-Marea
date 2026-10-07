using System;
using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 아트가 만든 애니메이션 클립으로 조개 입을 연다 — 홍합 HP.fbx. 조개 프리팹 루트에 붙인다. (+10/6)
    ///
    /// Animator 없이 클립을 직접 샘플링한다(AnimationClip.SampleAnimation). 켜질 때 closedTime 자세로 두고,
    /// Open()이면 openTime 동안 openedTime 자세까지 간다. 클립들은 같은 정규화 시각(0~1)으로 같이 샘플링한다.
    /// 홍합 클립은 "열림 → 닫힘"으로 만들어져 있어서 closedTime 1 · openedTime 0(거꾸로)이 기본이다.
    /// 클립 길이(약 3초)와 상관없이 openTime으로 빠르기를 정한다.
    /// 열린 뒤에도 이 모습 그대로 둔다(가리비 ShellOpen과 같다).
    /// </summary>
    public class ClipShellOpen : ClamOpener
    {
        [Tooltip("같이 샘플링할 클립들. 홍합은 HP_L|…001Action(왼쪽) · HP_R|…002Action(오른쪽).")]
        [SerializeField] private AnimationClip[] clips;
        [Tooltip("클립 경로의 기준 — 모델(FBX) 루트. 비우면 첫 자식.")]
        [SerializeField] private Transform animRoot;
        [Tooltip("닫힌 자세 — 클립 정규화 시각(0~1).")]
        [SerializeField, Range(0f, 1f)] private float closedTime = 1f;
        [Tooltip("열린 자세 — 클립 정규화 시각(0~1).")]
        [SerializeField, Range(0f, 1f)] private float openedTime;
        [SerializeField, Min(0.01f)] private float openTime = 0.5f;

        private float _t = -1f;
        private Action _onFinished;
        private bool _open;

        private void Awake()
        {
            if (clips == null || clips.Length == 0)
            {
                Debug.LogError($"{name}: ClipShellOpen.clips가 비어 있다. 열리지 않는다.", this);
                enabled = false;
                return;
            }
            if (animRoot == null && transform.childCount > 0) animRoot = transform.GetChild(0);
            if (animRoot == null) { Debug.LogError($"{name}: ClipShellOpen.animRoot가 없다.", this); enabled = false; return; }
            Sample(closedTime);
        }

        public override void Open(Action onFinished)
        {
            if (_open || clips == null || clips.Length == 0) return;
            _open = true;
            _onFinished = onFinished;
            _t = 0f;
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / openTime);
            Sample(Mathf.Lerp(closedTime, openedTime, Mathf.SmoothStep(0f, 1f, _t)));
            if (_t < 1f) return;
            _t = -1f;
            Action done = _onFinished;
            _onFinished = null;
            done?.Invoke();
        }

        private void Sample(float normalized)
        {
            foreach (AnimationClip clip in clips)
                if (clip != null) clip.SampleAnimation(animRoot.gameObject, normalized * clip.length);
        }
    }
}
