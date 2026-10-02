using System;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Core
{
    /// <summary>
    /// 여닫는 창의 공통 바닥. (+10/2, 이슈 92)
    ///
    /// 지금까지 창마다 panel.SetActive 를 각자 했다. 메인 HUD의 [메뉴]가 창을 열어야
    /// 해서 "열기/닫기/토글"을 밖에서 부를 이름이 필요해졌고, × 버튼도 창마다 붙는다.
    ///
    /// 막지 않는 창(논블로킹)이다 — 설계 결정 7의 UIStack은 B 몫이고 아직 없다.
    /// UIStack이 생기면 Open/Close 안에서 거기에 등록만 하면 된다.
    ///
    /// panel 을 이 컴포넌트가 붙은 오브젝트와 따로 두는 이유는 기존 창들과 같다 —
    /// 자기 자신을 끄면 Update 가 멈춰 단축키로 다시 열 수 없다.
    /// </summary>
    public abstract class UiPanel : MonoBehaviour
    {
        [Header("창")]
        [Tooltip("여닫을 대상. 이 컴포넌트가 붙은 오브젝트를 끄면 Update가 안 돌아 "
               + "다시 열 수 없으니, 패널은 따로 지정한다.")]
        [SerializeField] protected GameObject panel;

        [Tooltip("× 버튼. 비워두면 단축키로만 닫힌다.")]
        [SerializeField] private Button closeButton;

        [SerializeField] private bool openOnStart;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>열리거나 닫힐 때. HUD가 버튼 표시를 맞추는 데 쓴다.</summary>
        public event Action<bool> OnOpenChanged;

        protected virtual void Awake()
        {
            if (panel == null)
                Debug.LogError($"{name}: {GetType().Name}.panel이 비어 있다. 여닫을 대상이 없다.", this);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        protected virtual void Start()
        {
            if (panel != null) panel.SetActive(openOnStart);
        }

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);
        public void Toggle() => SetOpen(!IsOpen);

        private void SetOpen(bool open)
        {
            if (panel == null || panel.activeSelf == open) return;

            panel.SetActive(open);
            if (open) OnOpened();
            else OnClosed();
            OnOpenChanged?.Invoke(open);
        }

        /// <summary>닫혀 있는 동안 바뀐 값(골드 등)을 열 때 맞춘다.</summary>
        protected virtual void OnOpened() { }

        /// <summary>열 때 넘겨받은 대상(어느 밭, 어느 탐사정)을 놓는다. (+10/2)</summary>
        protected virtual void OnClosed() { }
    }
}
