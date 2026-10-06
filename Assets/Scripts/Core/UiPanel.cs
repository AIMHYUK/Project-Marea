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

        [Header("소리 (+10/6, 이슈 117) — 기획 MNU-01 ui_menu_open · MNU-04 sfx_inv_open")]
        [Tooltip("열 때. 비우면 조용하다. 연출 때문에 잠깐 가렸다 되돌릴 땐 안 난다.")]
        [SerializeField] private AudioClip openClip;
        [Tooltip("닫을 때. 비우면 조용하다.")]
        [SerializeField] private AudioClip closeClip;
        [SerializeField, Range(0f, 1f)] private float openCloseVolume = 0.8f;

        // (+10/6, 이슈 117) 해금 연출 동안 잠깐 가린다 — Suspend / Resume.
        // Close로 닫으면 OnClosed가 대상(어느 밭, 어느 탐사정)을 놓아서 다시 열 수가 없다.
        // 가린 동안 IsOpen은 그대로 true다 — 논리상 열린 창이고, 화면에만 안 보인다.
        private bool _suspended;

        public bool IsOpen => panel != null && (panel.activeSelf || _suspended);

        /// <summary>지금 화면에 보이는가. 가려진 창은 열려 있어도 false. (+10/6)</summary>
        public bool IsVisible => panel != null && panel.activeSelf;

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

        /// <summary>보이는 창을 잠깐 가린다. 대상은 그대로 든다. (+10/6)</summary>
        public void Suspend()
        {
            if (!IsVisible) return;
            _suspended = true;
            panel.SetActive(false);
        }

        /// <summary>가렸던 창을 다시 보인다. 가린 동안 바뀐 값은 OnOpened로 맞춘다. (+10/6)</summary>
        public void Resume()
        {
            if (!_suspended) return;
            _suspended = false;
            panel.SetActive(true);
            OnOpened();
        }

        private void SetOpen(bool open)
        {
            if (panel == null) return;

            // (+10/6) 가린 동안 열기는 무시한다(연출 중 U · Tab). 닫기는 진짜로 닫는다 —
            // 해금 확인 창은 연출이 시작된 뒤에 [실행] 성공으로 Close가 온다. 이걸 무시하면 연출 끝에 다시 뜬다.
            if (_suspended)
            {
                if (open) return;
                _suspended = false;
                SoundManager.Play(closeClip, openCloseVolume);   // (+10/6)
                OnClosed();
                OnOpenChanged?.Invoke(false);
                return;
            }

            if (panel.activeSelf == open) return;

            panel.SetActive(open);
            SoundManager.Play(open ? openClip : closeClip, openCloseVolume);   // (+10/6)
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
