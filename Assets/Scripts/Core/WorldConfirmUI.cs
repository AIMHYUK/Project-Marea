using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Core
{
    /// <summary>창 한 번에 그릴 내용. 여는 쪽이 지금 상태로 만들어 준다.</summary>
    public struct ConfirmView
    {
        public string Title;
        public string Body;
        /// <summary>필요 조건 · 보유 현황 줄. 부족하면 이유도 여기 쓴다.</summary>
        public string Status;
        public string ConfirmLabel;
        public bool CanConfirm;
    }

    /// <summary>
    /// 월드 오브젝트 위에 붙어 다니는 작은 확인창 — [취소] / [실행]. (+10/2, 이슈 92 — 와이어프레임 「해금 / 바닥 수리」)
    ///
    /// 잠긴 시설 해금과 파손된 바닥 수리가 같이 쓴다. 창은 무엇을 하는지 모른다 —
    /// 여는 쪽이 내용(<see cref="ConfirmView"/>)을 만드는 함수와 [실행] 때 할 일을 넘긴다.
    /// 내용은 매 프레임 다시 만든다: 열어둔 채 골드가 바뀌면 버튼이 켜지고 꺼져야 하는데,
    /// 여기서 Wallet 이벤트를 구독하면 Core가 Economy를 알게 된다. 문자열 몇 개라 싸다.
    ///
    /// 부족하면 같은 창에서 이유를 보이고 [실행]을 끈다. [실행]이 성공하면 닫는다.
    /// </summary>
    public class WorldConfirmUI : UiPanel
    {
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI bodyLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI confirmLabel;

        [Tooltip("월드 위치에서 화면 위로 띄울 거리(px).")]
        [SerializeField] private Vector2 screenOffset = new(0f, 40f);

        private Transform _anchor;
        private Vector3 _anchorOffset;
        private Func<ConfirmView> _build;
        private Func<bool> _onConfirm;
        private RectTransform _panelRect;
        private Canvas _canvas;

        protected override void Awake()
        {
            base.Awake();
            if (confirmButton == null)
                Debug.LogError($"{name}: WorldConfirmUI.confirmButton이 비어 있다. 실행할 수 없다.", this);
            else
                confirmButton.onClick.AddListener(HandleConfirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);

            if (panel != null) _panelRect = panel.transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
        }

        /// <param name="anchor">따라다닐 월드 오브젝트.</param>
        /// <param name="build">지금 상태로 창 내용을 만든다. 열려 있는 동안 매 프레임 부른다.</param>
        /// <param name="onConfirm">[실행]. 성공하면 true — 창이 닫힌다. 실패면 창을 두고 다시 그린다.</param>
        public void Open(Transform anchor, Vector3 anchorOffset, Func<ConfirmView> build, Func<bool> onConfirm)
        {
            _anchor = anchor;
            _anchorOffset = anchorOffset;
            _build = build;
            _onConfirm = onConfirm;
            Open();
            Refresh();
            FollowAnchor();
        }

        protected override void OnClosed()
        {
            _anchor = null;
            _build = null;
            _onConfirm = null;
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;

            // 대상이 사라지면(해금되어 부서진 모습이 꺼지는 등) 같이 닫는다.
            if (_anchor == null || !_anchor.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            Refresh();
            FollowAnchor();
        }

        private void HandleConfirm()
        {
            if (_onConfirm == null) return;
            if (_onConfirm()) Close();
            else Refresh();
        }

        private void Refresh()
        {
            if (_build == null) return;
            ConfirmView view = _build();

            if (titleLabel != null) titleLabel.text = view.Title;
            if (bodyLabel != null) bodyLabel.text = view.Body;
            if (statusLabel != null) statusLabel.text = view.Status;
            if (confirmLabel != null) confirmLabel.text = view.ConfirmLabel;
            if (confirmButton != null) confirmButton.interactable = view.CanConfirm;
        }

        /// <summary>월드 위치를 화면 좌표로 옮겨 창을 붙인다. 카메라 뒤면 숨기지 않고 마지막 자리에 둔다.</summary>
        private void FollowAnchor()
        {
            Camera cam = Camera.main;
            if (_anchor == null || _panelRect == null || cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(_anchor.position + _anchorOffset);
            if (screen.z < 0f) return;

            var parent = (RectTransform)_panelRect.parent;
            Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, (Vector2)screen + screenOffset, uiCam, out Vector2 local))
                _panelRect.localPosition = ClampInside(parent, local);
        }

        /// <summary>창이 화면 밖으로 잘리지 않게 부모 사각형 안으로 민다.</summary>
        private Vector2 ClampInside(RectTransform parent, Vector2 local)
        {
            Rect area = parent.rect;
            Vector2 size = _panelRect.rect.size;
            Vector2 pivot = _panelRect.pivot;
            float minX = area.xMin + size.x * pivot.x, maxX = area.xMax - size.x * (1f - pivot.x);
            float minY = area.yMin + size.y * pivot.y, maxY = area.yMax - size.y * (1f - pivot.y);
            return new Vector2(Mathf.Clamp(local.x, minX, maxX), Mathf.Clamp(local.y, minY, maxY));
        }
    }
}
