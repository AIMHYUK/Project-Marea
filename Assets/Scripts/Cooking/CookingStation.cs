using Marea.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class CookingStation : InteractableBase
    {
        [Header("UI 연결")]
        [Tooltip("상호작용 시 띄울 메뉴 선택 UI")]
        [SerializeField] private CookingMenuUI menuUI;

        [Header("호버 연출 (선택 사항)")]
        [SerializeField] private GameObject highlightEffect;

        [Header("키보드 상호작용")]
        [Tooltip("플레이어가 이 범위 안에 있을 때 E키로 상호작용할 수 있습니다.")]
        [SerializeField] private Collider interactionRange;

        private IInteractor _nearbyInteractor;
        private bool _isPlayerInRange;

        private void Awake()
        {
            if (interactionRange != null)
            {
                interactionRange.isTrigger = true;
            }
        }

        private void Update()
        {
            if (!_isPlayerInRange || _nearbyInteractor == null) return;

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                Interact(_nearbyInteractor);
            }
        }

        public override void Interact(IInteractor actor)
        {
            if (menuUI == null)
            {
                Debug.LogWarning("[CookingStation] MenuUI가 연결되지 않았습니다.");
                return;
            }

            // 플레이어 조작 잠금
            actor.BeginBusy();

            // 메뉴 선택 팝업 열기
            menuUI.Open(actor);
        }

        private void OnTriggerEnter(Collider other)
        {
            IInteractor interactor = FindInteractor(other);

            if (interactor == null) return;

            _nearbyInteractor = interactor;
            _isPlayerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            IInteractor interactor = FindInteractor(other);

            if (interactor == null || interactor != _nearbyInteractor) return;

            _nearbyInteractor = null;
            _isPlayerInRange = false;
        }

        private IInteractor FindInteractor(Collider other)
        {
            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(true);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInteractor interactor)
                {
                    return interactor;
                }
            }

            return null;
        }

        public override void OnHoverEnter()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(true);
            }
        }

        public override void OnHoverExit()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }
        }
    }
}
