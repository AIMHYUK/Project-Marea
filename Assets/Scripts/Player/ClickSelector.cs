using Marea.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Marea.Player
{
    /// <summary>
    /// 마우스 아래의 상호작용 오브젝트를 찾아 하이라이트하고, 클릭하면 플레이어에게 넘긴다.
    /// 여기는 무엇을 골랐는지까지만 안다. 어떻게 가는지는 PlayerController 몫이다.
    /// </summary>
    public class ClickSelector : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private PlayerController player;

        [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private PlayerInputReader input;

        [Tooltip("상호작용 오브젝트가 있는 레이어만 켜두면 레이캐스트가 싸진다.")]
        [SerializeField] private LayerMask interactableMask = ~0;

        [SerializeField] private float maxDistance = 100f;

        private InteractableBase _hovered;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (input == null) input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (input == null || cam == null || player == null) return;

            // UI 위에 있으면 씬을 건드리지 않는다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                SetHovered(null);
                return;
            }

            InteractableBase found = RaycastInteractable(input.PointerPosition);
            SetHovered(found);

            if (found != null && input.ClickPressed)
                player.GoInteract(found);
        }

        private InteractableBase RaycastInteractable(Vector2 screenPos)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableMask))
                return null;

            // 인터페이스(IInteractable)가 아니라 base 타입으로 잡는다.
            // hover 처리(OnHoverEnter/Exit)가 InteractableBase에만 있기 때문이다.
            // 그래서 IInteractable만 직접 구현한 오브젝트는 여기서 안 걸린다 —
            // 상호작용 오브젝트는 반드시 InteractableBase를 상속해야 한다.
            var target = hit.collider.GetComponentInParent<InteractableBase>();
            if (target == null) return null;

            return target.CanInteract(player) ? target : null;
        }

        private void SetHovered(InteractableBase next)
        {
            if (_hovered == next) return;

            if (_hovered != null) _hovered.OnHoverExit();
            _hovered = next;
            if (_hovered != null) _hovered.OnHoverEnter();
        }
    }
}
