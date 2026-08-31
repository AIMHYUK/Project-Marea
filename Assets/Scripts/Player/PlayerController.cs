using Marea.Core;
using UnityEngine;

namespace Marea.Player
{
    /// <summary>
    /// 플레이어 조작 + 클릭 이동 + 도착 후 상호작용.
    ///
    /// 실제로 걷는 일은 AgentMover가 한다. 여기는 입력을 읽고,
    /// 카메라 기준으로 방향을 만들고, 도착했을 때 무엇을 할지만 정한다.
    ///
    /// 상태는 private이다. 밖에서는 IsBusy만 본다 — 나중에 상태를 늘려도
    /// B 코드가 안 깨지게 하려는 것이다.
    /// </summary>
    [RequireComponent(typeof(AgentMover))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour, IInteractor
    {
        private enum State { Idle, Moving, Interacting }

        [Header("이동")]
        [Tooltip("WASD 방향의 기준. 비워두면 Camera.main을 쓴다.")]
        [SerializeField] private Transform cameraBasis;

        [Header("참조")]
        [SerializeField] private Warehouse warehouse;

        private AgentMover _mover;
        private PlayerInputReader _input;
        private State _state = State.Idle;
        private bool _busyHeld;

        // IInteractor
        public Transform Transform => transform;
        public Warehouse Warehouse => warehouse;

        public void BeginBusy() => _busyHeld = true;
        public void EndBusy() => _busyHeld = false;

        /// <summary>지금 다른 걸 하고 있는가. B는 이것만 보면 된다.</summary>
        public bool IsBusy => _busyHeld || _state != State.Idle;

        private void Awake()
        {
            _mover = GetComponent<AgentMover>();
            _input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            // TODO(B가 GameManager 커밋한 뒤): 정산 중이면 입력을 막는다.
            //   if (GameManager.Instance.Phase == DayPhase.Closing) return;
            // TODO(2차): UI 스택이 생기면.
            //   if (UIStack.BlocksGameplay) return;

            if (_busyHeld) return;   // 미니게임 등이 진행 중

            Vector2 axis = _input.MoveAxis;
            if (axis.sqrMagnitude < 0.01f) return;

            _mover.MoveBy(ToWorldDirection(axis), Time.deltaTime);

            // MoveBy가 진행 중이던 목적지 이동을 취소하므로 도착 콜백은 안 온다.
            // 여기서 Idle로 안 돌리면 IsBusy가 영영 true로 남는다.
            _state = State.Idle;
        }

        /// <summary>ClickSelector가 부른다. 대상 앞으로 걸어간 뒤 상호작용한다.</summary>
        public void GoInteract(IInteractable target)
        {
            if (target == null || _busyHeld) return;
            if (!target.CanInteract(this)) return;

            _state = State.Moving;
            _mover.GoTo(target.InteractPoint,
                onArrived: () =>
                {
                    _state = State.Interacting;

                    // 여기서 B의 코드가 돈다.
                    // 여러 프레임 걸리는 일이면 BeginBusy를 부를 것이다.
                    target.Interact(this);

                    _state = State.Idle;
                },
                onFailed: () =>
                {
                    // 갈 수 없는 자리다. 그냥 풀어준다.
                    _state = State.Idle;
                });
        }

        private Vector3 ToWorldDirection(Vector2 axis)
        {
            Transform basis = cameraBasis != null ? cameraBasis
                            : Camera.main != null ? Camera.main.transform
                            : null;

            if (basis == null) return new Vector3(axis.x, 0f, axis.y).normalized;

            Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;
            return (forward * axis.y + right * axis.x).normalized;
        }
    }
}
