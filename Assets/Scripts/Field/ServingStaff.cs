using Marea.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Marea.Field
{
    /// <summary>
    /// 서빙 직원. 유휴일 때 ServeBoard에서 작업을 하나 꺼내 픽업→배달을 왕복한다.
    ///
    /// 걷는 일은 AgentMover가 한다. 여기는 "무엇을 할지"만 정한다.
    /// IInteractor를 구현하지 않는다 — 클릭 경로를 안 타기 때문이다(설계 결정 10).
    ///
    /// 배달 완료는 "목적지 도착"이 아니라 "대상에게 닿았다"로 본다. (+9/3)
    /// 손님은 의자 위에 앉아 있고 의자는 NavMesh 구멍이라, 손님 발밑까지는 애초에 못 간다.
    /// </summary>
    [RequireComponent(typeof(AgentMover))]
    public class ServingStaff : MonoBehaviour
    {
        private enum State { Idle, ToPickup, ToTarget }

        [SerializeField] private ServeBoard board;

        [Tooltip("들고 있는 음식 그림. 비워둬도 동작한다.")]
        [SerializeField] private SpriteRenderer carriedIcon;

        [Header("배달 판정 (+9/3)")]
        [Tooltip("대상과 이만큼 가까워지면 건넨 것으로 본다.")]
        [SerializeField, Min(0.1f)] private float handoffRadius = 1.8f;

        [Tooltip("대상 발밑이 NavMesh 밖일 때, 이 반경 안에서 가장 가까운 NavMesh 점을 목적지로 삼는다.")]
        [SerializeField, Min(0.1f)] private float navSampleRadius = 4f;

        private AgentMover _mover;
        private State _state = State.Idle;
        private ServeTask _task;

        /// <summary>
        /// 이 작업이 대상 배달인가. _task.DeliverTarget 은 대상이 Destroy되면 null이 되므로
        /// "원래 대상이 있었나"를 이것으로 따로 기억한다. 안 그러면 손님이 사라진 것과
        /// 처음부터 좌표 배달이었던 것을 구분할 수 없다.
        /// </summary>
        private bool _expectTarget;

        public bool IsIdle => _state == State.Idle;

        /// <summary>음식을 들고 배달 중인가.</summary>
        public bool IsCarryingFood => _state == State.ToTarget;

        /// <summary>
        /// 지금 맡고 있는 배달 대상. 아무것도 안 들고 있으면 null.
        ///
        /// ⚠️ 픽업하러 가는 중(ToPickup)에도 반환해야 한다. B는 이것으로 "이 손님에게
        /// 이미 음식이 가고 있나"를 판단하는데, 픽업 구간에 null을 주면 그 사이에
        /// 같은 손님이 한 번 더 배정된다. 실제로 그렇게 짰다가 배달 하나를 날렸다.
        /// 건네는 것 자체는 TryHandOff가 ToTarget으로 따로 막는다.
        /// </summary>
        public Transform DeliverTarget => _state == State.Idle ? null : _task.DeliverTarget;

        private void Awake()
        {
            _mover = GetComponent<AgentMover>();
            ShowIcon(null);
        }

        private void OnEnable()
        {
            // board가 없으면 이 직원은 영원히 아무것도 안 한다. 그런데 화면상으로는
            // 그냥 서 있는 것과 구분이 안 된다 — 조용히 넘어가면 원인을 못 찾는다. (+9/3)
            if (board == null)
            {
                Debug.LogError(
                    "[ServingStaff] board가 비어 있습니다. 인스펙터에 ServeBoard를 넣으세요. " +
                    "이대로면 이 직원은 서빙을 하지 않습니다.", this);
                return;
            }

            board.OnPosted += TryStartNext;

            // 켜지기 전에 이미 쌓여 있을 수 있다.
            TryStartNext();
        }

        private void OnDisable()
        {
            if (board != null) board.OnPosted -= TryStartNext;
        }

        private void Update()
        {
            if (_state != State.ToTarget || !_expectTarget) return;

            // 손님이 식사를 끝내고 Destroy됐다. 들고 있던 음식은 버린다.
            if (_task.DeliverTarget == null)
            {
                Debug.LogWarning("[ServingStaff] 배달 대상이 사라졌다 — 음식을 버리고 유휴로 돌아간다.", this);
                DropTask();
                return;
            }

            // 정상 경로는 손님 쪽 트리거가 TryHandOff를 부르는 것이다.
            // 여기 거리 검사는 그게 안 걸릴 때의 보험이다 — 트리거 이벤트는 두 콜라이더 중
            // 한쪽에 Rigidbody가 있어야 발생하는데, NavMeshAgent는 그걸 만들어주지 않는다.
            // 같은 CompleteDelivery를 타므로 두 경로가 겹쳐도 완료는 한 번뿐이다.
            if (WithinHandoff())
            {
                CompleteDelivery();
            }
        }

        /// <summary>
        /// 손님이 부른다. "네가 들고 있는 게 내 음식이면 지금 받겠다."
        ///
        /// 대상 확인을 여기서 하는 이유는, 배달 중인 직원 옆을 남의 손님이
        /// 스쳐 지나가도 그 손님이 음식을 가로채면 안 되기 때문이다.
        /// </summary>
        /// <returns>실제로 건넸으면 true.</returns>
        public bool TryHandOff(GameObject receiver)
        {
            if (_state != State.ToTarget) return false;
            if (receiver == null) return false;

            Transform target = _task.DeliverTarget;
            if (target == null) return false;

            // 콜라이더가 자식에 달려 있을 수 있다.
            Transform t = receiver.transform;
            if (t != target && !t.IsChildOf(target)) return false;

            CompleteDelivery();
            return true;
        }

        /// <summary>
        /// 진입점은 둘이다.
        ///   ① ServeBoard.OnPosted — 밖에서 깨워줄 때
        ///   ② 배달을 끝낸 직후 — 스스로 확인할 때
        ///
        /// ②가 없으면 배달 중에 들어온 작업이 영영 안 나간다.
        /// 알림은 그 순간 바쁜 구독자를 그냥 지나치기 때문이다.
        /// </summary>
        private void TryStartNext()
        {
            if (_state != State.Idle) return;
            if (board == null) return;   // OnEnable에서 이미 에러를 냈다. 여기선 조용히 빠진다
            if (!board.TryTake(out _task)) return;

            _expectTarget = _task.DeliverTarget != null;
            _state = State.ToPickup;
            GoPickup();
        }

        private void GoPickup()
        {
            _mover.GoTo(_task.PickupPoint,
                onArrived: () =>
                {
                    ShowIcon(_task.FoodIcon);
                    _state = State.ToTarget;

                    // 콜백 안에서 다시 GoTo를 건다.
                    // AgentMover.Fire()가 상태를 먼저 비우고 콜백을 부르기 때문에 가능하다.
                    GoDeliver();
                },
                onFailed: OnPathFailed);
        }

        private void GoDeliver()
        {
            // 픽업하러 가는 동안 손님이 식사를 끝내고 나갔을 수 있다.
            // 여기서 안 보면 사라진 손님의 옛 좌표까지 헛걸음을 한다.
            if (_expectTarget && _task.DeliverTarget == null)
            {
                Debug.LogWarning("[ServingStaff] 픽업하는 사이 대상이 사라졌다 — 음식을 버린다.", this);
                DropTask();
                return;
            }

            _mover.GoTo(ResolveDeliverPoint(),
                onArrived: OnDeliverArrived,
                onFailed: OnPathFailed);
        }

        /// <summary>
        /// 실제로 걸어갈 지점.
        ///
        /// 손님 발밑을 그대로 목적지로 주면 안 된다. 의자·책상을 Bake에 넣으면 그 자리는
        /// NavMesh 구멍이라 경로가 안 생기거나 PathPartial이 되고, AgentMover는 그걸
        /// 실패로 처리한다. 가장 가까운 NavMesh 점으로 당겨 오면 "갈 수 있는 데까지"가 된다.
        /// </summary>
        private Vector3 ResolveDeliverPoint()
        {
            Vector3 raw = _task.CurrentDeliverPoint;

            if (NavMesh.SamplePosition(raw, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            Debug.LogWarning(
                $"[ServingStaff] 대상 {raw} 근처 {navSampleRadius}m 안에 NavMesh가 없다. " +
                $"그대로 시도한다 — 실패하면 Bake 범위를 확인할 것.", this);
            return raw;
        }

        private void OnDeliverArrived()
        {
            if (_state != State.ToTarget) return;

            // 좌표 배달(ContextMenu 테스트)은 도착이 곧 완료다.
            if (!_expectTarget)
            {
                CompleteDelivery();
                return;
            }

            // Update와 이 콜백의 실행 순서는 정해져 있지 않다. 여기서도 거리를 본다.
            if (WithinHandoff())
            {
                CompleteDelivery();
                return;
            }

            Debug.LogWarning(
                "[ServingStaff] 갈 수 있는 데까지 갔는데 대상이 아직 멀다 — 음식을 버린다. " +
                $"handoffRadius({handoffRadius})를 늘리거나 좌석 주변까지 Bake할 것.", this);
            DropTask();
        }

        private bool WithinHandoff()
        {
            Transform target = _task.DeliverTarget;
            if (target == null) return false;

            return (target.position - transform.position).sqrMagnitude <= handoffRadius * handoffRadius;
        }

        /// <summary>
        /// 건넸다. 상태를 먼저 비우고 board.Complete를 부른다.
        ///
        /// 순서가 중요하다 — Complete 안에서 B 콜백이 돌고, 그게 다시 Post를 부를 수도 있다.
        /// 그때 이미 유휴여야 이어서 집어간다. Complete를 먼저 부르면 그 Post를 놓친다.
        /// </summary>
        private void CompleteDelivery()
        {
            ServeTask done = _task;

            _mover.Stop();
            ShowIcon(null);
            _task = default;
            _expectTarget = false;
            _state = State.Idle;

            board.Complete(done);

            TryStartNext();   // 큐에 남은 게 있으면 이어서
        }

        /// <summary>
        /// 배달을 포기한다. 꺼내온 작업은 증발한다 — board에 되돌리는 함수가 없다.
        /// 이슈 #5 미해결 그대로다.
        /// </summary>
        private void DropTask()
        {
            _mover.Stop();
            ShowIcon(null);
            _task = default;
            _expectTarget = false;
            _state = State.Idle;

            TryStartNext();
        }

        /// <summary>
        /// 경로를 못 만들었다. 대개 목적지가 NavMesh 밖이거나 가구 안쪽이다.
        ///
        /// ⚠️ 꺼내온 작업이 여기서 증발한다. ServeBoard에 되돌리는 함수가 없다.
        /// 1차에서는 씬을 제대로 만들면 안 나는 상황이라 로그만 남기고 넘어간다.
        /// 실제로 자주 나면 Return(task)를 논의한다 — 이슈 #5 미해결.
        /// </summary>
        private void OnPathFailed()
        {
            Debug.LogWarning(
                $"[ServingStaff] 경로 실패 — 작업을 버린다. " +
                $"픽업 {_task.PickupPoint} / 배달 {_task.CurrentDeliverPoint}. " +
                $"목적지가 NavMesh 위인지, 가구 안쪽이 아닌지 확인할 것.", this);

            DropTask();
        }

        private void ShowIcon(Sprite sprite)
        {
            if (carriedIcon == null) return;

            carriedIcon.sprite = sprite;
            carriedIcon.enabled = sprite != null;
        }
    }
}
