using Marea.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Marea.Player
{
    /// <summary>
    /// 플레이어 조작 + 클릭 이동 + 도착 후 상호작용.
    ///
    /// 상태는 private이다. 밖에서는 IsBusy만 본다 — 나중에 상태를 늘려도
    /// B 코드가 안 깨지게 하려는 것이다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerController : MonoBehaviour, IInteractor
    {
        private enum State { Idle, Moving, Interacting }

        [Header("이동")]
        [SerializeField] private float moveSpeed = 4f;

        [Tooltip("WASD 방향의 기준. 비워두면 Camera.main을 쓴다.")]
        [SerializeField] private Transform cameraBasis;

        [Tooltip("목적지에 이만큼 가까워지면 도착으로 본다.")]
        [SerializeField] private float arriveThreshold = 0.15f;

        [Header("참조")]
        [SerializeField] private Warehouse warehouse;

        private NavMeshAgent _agent;
        private State _state = State.Idle;
        private IInteractable _pending;
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
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = moveSpeed;
        }

        private void Update()
        {
            // TODO(B가 GameManager 커밋한 뒤): 정산 중이면 입력을 막는다.
            //   if (GameManager.Instance.Phase == DayPhase.Closing) return;
            // TODO(2차): UI 스택이 생기면.
            //   if (UIStack.BlocksGameplay) return;

            if (_busyHeld) return;   // 미니게임 등이 진행 중

            Vector2 axis = ReadMoveAxis();
            if (axis.sqrMagnitude > 0.01f)
            {
                CancelPendingMove();
                MoveManually(axis);
                return;
            }

            if (_state == State.Moving) TickArrival();
        }

        /// <summary>ClickSelector가 부른다. 대상 앞으로 걸어간 뒤 상호작용한다.</summary>
        public void GoInteract(IInteractable target)
        {
            if (target == null || _busyHeld) return;
            if (!target.CanInteract(this)) return;

            _pending = target;
            _state = State.Moving;
            _agent.isStopped = false;
            _agent.SetDestination(target.InteractPoint);
        }

        private void TickArrival()
        {
            if (_agent.pathPending) return;

            float stop = Mathf.Max(_agent.stoppingDistance, arriveThreshold);
            if (_agent.remainingDistance > stop) return;
            if (_agent.hasPath && _agent.velocity.sqrMagnitude > 0.01f) return;

            var target = _pending;
            _pending = null;
            _state = State.Interacting;

            // 여기서 B의 코드가 돈다. 여러 프레임 걸리는 일이면 BeginBusy를 부를 것이다.
            target?.Interact(this);

            _state = State.Idle;
        }

        private void MoveManually(Vector2 axis)
        {
            Vector3 dir = ToWorldDirection(axis);
            if (dir.sqrMagnitude < 0.01f) return;

            // transform.position 직접 대입이 아니라 agent.Move를 쓴다.
            // 안 그러면 NavMesh 밖으로 나가서 다음 클릭 이동이 실패한다.
            _agent.Move(dir * (moveSpeed * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(dir);
            _state = State.Idle;
        }

        private void CancelPendingMove()
        {
            _pending = null;
            if (_agent.hasPath) _agent.ResetPath();
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

        /// <summary>
        /// 이 프로젝트는 Input System Package 전용이라 레거시 Input을 못 쓴다.
        /// 나중에 InputSystem_Actions 에셋으로 옮기려면 이 메서드만 갈아끼우면 된다.
        /// </summary>
        private static Vector2 ReadMoveAxis()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
    }
}
