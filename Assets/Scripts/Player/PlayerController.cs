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
        [Tooltip("WASD 방향의 기준. 비워두면 Awake에서 Camera.main을 쓴다. 프리팹은 이 칸이 비어 있는 게 정상이다.")]
        [SerializeField] private Transform cameraBasis;

        private AgentMover _mover;
        private PlayerInputReader _input;
        private State _state = State.Idle;
        private bool _busyHeld;

        // IInteractor
        public Transform Transform => transform;

        public void BeginBusy() => _busyHeld = true;
        public void EndBusy() => _busyHeld = false;

        /// <summary>지금 다른 걸 하고 있는가. B는 이것만 보면 된다.</summary>
        public bool IsBusy => _busyHeld || _state != State.Idle;

        private void Awake()
        {
            _mover = GetComponent<AgentMover>();
            _input = GetComponent<PlayerInputReader>();

            // 프리팹이 되면 cameraBasis는 반드시 빈다 — 프리팹은 씬 오브젝트를 못 들고
            // 간다. 그래서 여기서 한 번 잡는다. ClickSelector.cam이 이미 쓰는 방식과 같다. (#49)
            if (cameraBasis == null && Camera.main != null) cameraBasis = Camera.main.transform;

            // 둘 다 없으면 WASD가 월드 축 기준으로 간다. 증상이 "카메라를 돌려도 앞이
            // 안 바뀐다"라서 원인이 카메라인지 입력인지 안 보인다.
            if (cameraBasis == null)
                Debug.LogError($"{name}: PlayerController.cameraBasis가 비어 있고 Camera.main도 없다. "
                             + "MainCamera 태그가 붙은 카메라가 씬에 있는지 확인할 것. "
                             + "WASD 방향이 카메라를 안 따라간다.", this);
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

        /// <summary>
        /// ClickSelector가 부른다. 좌표까지 걸어간다. 도착해서 할 일은 없다. (+9/8)
        ///
        /// 목적지가 NavMesh 위인지는 부르는 쪽이 이미 확인했다고 본다.
        /// 여기서 또 SamplePosition을 하면 보정 반경이 두 군데로 갈린다.
        ///
        /// 부분 경로를 허용한다 — 목적지까지 못 가면 갈 수 있는 데까지 간다.
        /// 여기 목적지는 사용자가 찍은 한 점일 뿐이라 "못 가면 제자리"보다
        /// "최대한 가까이"가 맞다. 상호작용(GoInteract)은 반대로 두는 게 맞아서
        /// 여기만 켠다. (+9/8)
        /// </summary>
        public void GoTo(Vector3 destination)
        {
            if (_busyHeld) return;

            // 이동 중에 다시 불러도 된다. AgentMover.GoTo가 먼저 Stop()을 부르므로
            // 앞 목적지의 콜백은 버려지고 새 목적지로 갈아탄다.
            _state = State.Moving;
            _mover.GoTo(destination,
                onArrived: () => _state = State.Idle,

                // 여기서 Idle로 안 돌리면 IsBusy가 영영 true로 남아 B 쪽이 잠긴다.
                onFailed: () => _state = State.Idle,

                allowPartialPath: true);
        }

        private Vector3 ToWorldDirection(Vector2 axis)
        {
            // Awake에서 이미 Camera.main까지 봤다. 여기까지 null이면 씬에 카메라가 없는
            // 것이고, 그건 Awake가 에러로 알렸다. 매 프레임 Camera.main을 다시 뒤지지 않는다.
            if (cameraBasis == null) return new Vector3(axis.x, 0f, axis.y).normalized;

            Vector3 forward = Vector3.ProjectOnPlane(cameraBasis.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraBasis.right, Vector3.up).normalized;
            return (forward * axis.y + right * axis.x).normalized;
        }
    }
}
