using Marea.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

namespace Marea.Player
{
    /// <summary>
    /// 마우스 아래를 한 번 쏴 보고 무엇을 클릭했는지 가른다.
    /// 상호작용 오브젝트면 하이라이트하고 플레이어에게 넘기고, 빈 바닥이면 그 지점으로 보낸다. (+9/8)
    ///
    /// 여기는 무엇을 골랐는지까지만 안다. 어떻게 가는지는 PlayerController 몫이다.
    /// </summary>
    public class ClickSelector : MonoBehaviour
    {
        [Tooltip("레이를 쏘는 카메라. 비워두면 Awake에서 Camera.main을 쓴다. (+9/8)")]
        [SerializeField] private Camera cam;

        [Tooltip("클릭을 넘길 대상. 자동으로 못 찾으니 반드시 넣어야 한다 — "
               + "비면 Awake에서 에러를 낸다. (+9/8)")]
        [SerializeField] private PlayerController player;

        [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private PlayerInputReader input;

        [Tooltip("상호작용 오브젝트가 있는 레이어만 켜두면 레이캐스트가 싸진다.")]
        [SerializeField] private LayerMask interactableMask = ~0;

        [Tooltip("걸어갈 수 있는 바닥 레이어. Ground. (+9/8)")]
        [SerializeField] private LayerMask groundMask;

        [SerializeField] private float maxDistance = 100f;

        [Tooltip("클릭 지점에서 이만큼 안에 NavMesh가 있으면 거기로 붙여서 간다. "
               + "이 값이 클릭 이동의 손맛을 정한다 — 크면 벽 너머를 클릭해도 끌려오고, "
               + "작으면 갈 수 있는 자리인데 반응이 없다. (+9/8)")]
        [SerializeField, Min(0f)] private float navSampleRadius = 1.5f;

        private InteractableBase _hovered;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (input == null) input = GetComponent<PlayerInputReader>();

            // 아래 셋 중 하나라도 비면 Update가 첫 줄에서 그냥 빠진다.
            // 증상은 "클릭이 전부 무반응"인데 로그가 없어서 어디가 빈 건지 안 보인다. (+9/8)
            if (cam == null)
                Debug.LogError($"{name}: ClickSelector.cam이 비어 있고 Camera.main도 없다. "
                             + "MainCamera 태그가 붙은 카메라가 씬에 있는지 확인할 것. 클릭이 전부 안 먹는다.", this);
            if (input == null)
                Debug.LogError($"{name}: ClickSelector.input이 비어 있다. "
                             + "같은 오브젝트에 PlayerInputReader가 없다. 클릭이 전부 안 먹는다.", this);
            if (player == null)
                Debug.LogError($"{name}: ClickSelector.player가 비어 있다. "
                             + "인스펙터에 PlayerController를 넣을 것. 클릭이 전부 안 먹는다.", this);

            // 여긴 클릭 전체가 아니라 바닥 이동만 죽는다.
            // 눈에 보이는 증상이 "가끔 안 간다"라서 원인을 찾는 데 오래 걸린다.
            if (groundMask.value == 0)
                Debug.LogError($"{name}: ClickSelector.groundMask가 비어 있다. 바닥 클릭 이동이 동작하지 않는다.", this);
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

            // 한 프레임에 한 번만 읽는다. 아래 분기마다 다시 물으면 읽는 시점이 갈린다.
            bool clicked = input.ClickPressed;

            // 상호작용과 바닥을 한 레이로 같이 본다. 따로 두 번 쏘면 벽 뒤의 상호작용
            // 오브젝트와 앞의 바닥이 동시에 잡혀서 어느 쪽이 이기는지가 마스크 순서에 달린다.
            // 한 번 쏘면 Physics.Raycast가 주는 "가장 가까운 히트"가 곧 답이다.
            Ray ray = cam.ScreenPointToRay(input.PointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableMask | groundMask))
            {
                SetHovered(null);
                return;
            }

            // 인터페이스(IInteractable)가 아니라 base 타입으로 잡는다.
            // hover 처리(OnHoverEnter/Exit)가 InteractableBase에만 있기 때문이다.
            // 그래서 IInteractable만 직접 구현한 오브젝트는 여기서 안 걸린다 —
            // 상호작용 오브젝트는 반드시 InteractableBase를 상속해야 한다.
            var target = hit.collider.GetComponentInParent<InteractableBase>();
            if (target != null)
            {
                // 상호작용 오브젝트를 맞았으면 여기서 끝낸다.
                // CanInteract가 false여도 바닥 이동으로 흘려보내지 않는다 — 아직 안 자란 밭을
                // 클릭했는데 그 자리로 걸어가 버리면 한 번의 클릭이 두 가지 뜻이 된다. (+9/8)
                bool usable = target.CanInteract(player);
                SetHovered(usable ? target : null);

                if (clicked && usable) player.GoInteract(target);
                return;
            }

            SetHovered(null);
            if (!clicked) return;

            TryMoveToGround(hit);
        }

        /// <summary>바닥을 클릭했으면 그 지점으로 보낸다. 바닥이 아니면 아무 일도 안 한다. (+9/8)</summary>
        private void TryMoveToGround(RaycastHit hit)
        {
            if ((groundMask.value & (1 << hit.collider.gameObject.layer)) == 0) return;

            // hit.point는 콜라이더 표면 좌표라 NavMesh 위라는 보장이 없다 — 벽 윗면, 경사면, 메시 바깥.
            // 보정에 실패하면 이동하지 않는다. 그냥 SetDestination에 넘기면 부분 경로가 생기고
            // AgentMover가 그걸 실패로 끝내는데, 화면에서는 "클릭했는데 안 간다"로만 보인다.
            // SetDestination도 내부적으로 가까운 지점을 찾지만 그 반경을 코드에서 정할 수 없어서 따로 둔다.
            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, navSampleRadius, NavMesh.AllAreas))
                return;

            player.GoTo(navHit.position);
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
