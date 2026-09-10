using Marea.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Marea.Field
{
    /// <summary>
    /// 서빙 직원. <see cref="ServeBoard"/> 가 배정해 주면 픽업→배달을 왕복한다.
    ///
    /// 걷는 일은 <see cref="AgentMover"/> 가 한다. 여기는 "어디로 가서 누구에게 닿나" 만 안다.
    /// <c>IInteractor</c> 를 구현하지 않는다 — 클릭 경로를 안 타기 때문이다(설계 결정 10).
    ///
    /// <b>배정을 직원이 하지 않는다 (+9/10).</b> 전에는 B의 미니게임 컨트롤러가
    /// 대상을 골라 보드에 넣었고, 직원은 꺼내 갔다. 그러면 "이 손님 이미 누가 맡았나" 를
    /// 알아야 해서 고르는 쪽이 직원 목록을 뒤져야 했다 — 직원 혼자서는 못 하는 판단이다.
    /// 이제 보드가 정해서 <see cref="Assign"/> 으로 밀어준다. 직원이 둘이 되어도
    /// 같은 손님이 두 번 배정되지 않는다.
    ///
    /// 음식과 손님은 보드가 다룬다. 여기서는 <c>CookingResult</c> 도 <c>CustomerController</c> 도
    /// 모른다 — 꺼내기는 <see cref="ServeBoard.TryPickup"/>, 건네기는
    /// <see cref="ServeBoard.Deliver"/> 가 한다.
    ///
    /// 배달 완료는 "목적지 도착"이 아니라 "대상에게 닿았다"로 본다. (+9/3)
    /// 손님은 의자 위에 앉아 있고 의자는 NavMesh 구멍이라, 손님 발밑까지는 애초에 못 간다.
    /// </summary>
    [RequireComponent(typeof(AgentMover))]
    public class ServingStaff : MonoBehaviour
    {
        private enum State { Idle, ToPickup, ToTarget }

        [Tooltip("배정을 받아올 게시판. 자동으로 못 찾으니 반드시 넣어야 한다 — "
               + "비면 OnEnable에서 에러를 내고 이 직원은 서빙을 하지 않는다. (+9/8)")]
        [SerializeField] private ServeBoard board;

        [Tooltip("음식을 달아줄 손 위치. 조리대에 놓여 있던 실물이 여기로 옮겨온다 (+9/10). "
               + "비면 그림(carriedIcon)으로 대신한다.")]
        [SerializeField] private Transform holdPoint;

        [Tooltip("들고 있는 음식 그림. 실물을 달 수 없을 때만 쓴다 — holdPoint가 비었거나 "
               + "그 메뉴에 ServingPrefab이 없을 때. 비워둬도 동작한다.")]
        [SerializeField] private SpriteRenderer carriedIcon;

        [Header("배달 판정 (+9/3)")]
        [Tooltip("대상과 이만큼 가까워지면 건넨 것으로 본다.")]
        [SerializeField, Min(0.1f)] private float handoffRadius = 1.8f;

        [Tooltip("대상 발밑이 NavMesh 밖일 때, 이 반경 안에서 가장 가까운 NavMesh 점을 목적지로 삼는다.")]
        [SerializeField, Min(0.1f)] private float navSampleRadius = 4f;

        private AgentMover _mover;
        private State _state = State.Idle;
        private Transform _target;
        private Sprite _icon;

        /// <summary>
        /// 조리대에서 받아 온 실물. 파괴 책임이 여기로 넘어온다 (+9/10) —
        /// 건네든 버리든 <see cref="ClearState"/> 에서 지운다.
        /// </summary>
        private GameObject _carried;

        public bool IsIdle => _state == State.Idle;

        /// <summary>음식을 들고 배달 중인가.</summary>
        public bool IsCarryingFood => _state == State.ToTarget;

        /// <summary>지금 맡고 있는 배달 대상. 유휴면 null. 확인용.</summary>
        public Transform DeliverTarget => _state == State.Idle ? null : _target;

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

            board.Register(this);
        }

        private void OnDisable()
        {
            if (board != null) board.Unregister(this);

            // 들고 있던 실물도 여기서 지운다 — 안 지우면 직원이 꺼진 뒤에도 손 위치에 남는다.
            ClearState();
        }

        /// <summary>
        /// 보드가 부른다. "이 사람에게 갖다줘라."
        ///
        /// 음식은 아직 안 들었다 — 픽업 지점에 도착해서 보드에 꺼내 달라고 한다.
        /// 아이콘을 미리 받아두는 것은 꺼내고 나서 바로 켜기 위해서다.
        /// </summary>
        public void Assign(Transform target, Sprite icon)
        {
            if (_state != State.Idle)
            {
                Debug.LogError($"[ServingStaff] 이미 일하는 중인데 배정이 들어왔다 ({name}). "
                             + "보드가 IsIdle을 안 보고 있다.", this);
                return;
            }

            if (target == null)
            {
                Debug.LogError($"[ServingStaff] 배달 대상이 null이다 ({name}).", this);
                return;
            }

            _target = target;
            _icon = icon;
            _state = State.ToPickup;

            GoPickup();
        }

        private void Update()
        {
            if (_state != State.ToTarget) return;

            // 손님이 식사를 끝내거나 거절하고 Destroy됐다.
            if (_target == null)
            {
                Release("배달 대상이 사라졌다");
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
            if (receiver == null || _target == null) return false;

            // 콜라이더가 자식에 달려 있을 수 있다.
            Transform t = receiver.transform;
            if (t != _target && !t.IsChildOf(_target)) return false;

            CompleteDelivery();
            return true;
        }

        private void GoPickup()
        {
            // 보드가 "배정된 접시가 놓인 슬롯" 을 준다 (+9/10). 슬롯은 조리대 위라
            // NavMesh 밖이므로 아래에서 바닥으로 당긴다.
            _mover.GoTo(ResolveNavPoint(board.GetPickupTarget(this)),
                onArrived: OnPickupArrived,
                onFailed: () => Release("픽업 지점까지 경로를 못 만들었다"));
        }

        /// <summary>
        /// 픽업 지점에 닿았다. 여기서 조리대의 음식이 아직 있는지 보드에 물어본다.
        ///
        /// 걸어오는 동안 플레이어가 같은 접시를 집어갔을 수 있다. 그러면 꺼내기가
        /// 실패하고, 예약을 풀고 유휴로 돌아간다 — 음식이 조리대에 남아 있으면
        /// 보드가 다음 프레임에 다시 배정한다.
        /// </summary>
        private void OnPickupArrived()
        {
            if (_state != State.ToPickup) return;

            if (!board.TryPickup(this, out GameObject carried))
            {
                Release("픽업 실패 — 음식이 없다(플레이어가 먼저 집어갔거나 손님이 떠났다)");
                return;
            }

            Carry(carried);
            _state = State.ToTarget;

            // 콜백 안에서 다시 GoTo를 건다.
            // AgentMover.Fire()가 상태를 먼저 비우고 콜백을 부르기 때문에 가능하다.
            GoDeliver();
        }

        private void GoDeliver()
        {
            // 픽업하는 사이 손님이 나갔을 수 있다. 여기서 안 보면 사라진 손님의
            // 옛 좌표까지 헛걸음을 한다.
            if (_target == null)
            {
                Release("픽업하는 사이 대상이 사라졌다");
                return;
            }

            _mover.GoTo(ResolveNavPoint(_target.position),
                onArrived: OnDeliverArrived,
                onFailed: () => Release("배달 지점까지 경로를 못 만들었다"));
        }

        /// <summary>
        /// 실제로 걸어갈 지점. 픽업·배달 둘 다 이걸 쓴다 (+9/10).
        ///
        /// 목표 지점을 그대로 목적지로 주면 안 된다. 손님은 의자 위에 앉아 있고 음식은
        /// 조리대 위에 놓여 있는데, 가구를 Bake에 넣으면 그 자리는 NavMesh 구멍이라
        /// 경로가 안 생기거나 PathPartial이 되고, AgentMover는 그걸 실패로 처리한다.
        /// 가장 가까운 NavMesh 점으로 당겨 오면 "갈 수 있는 데까지"가 된다.
        /// </summary>
        private Vector3 ResolveNavPoint(Vector3 raw)
        {
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

            // Update와 이 콜백의 실행 순서는 정해져 있지 않다. 여기서도 거리를 본다.
            if (WithinHandoff())
            {
                CompleteDelivery();
                return;
            }

            Release($"갈 수 있는 데까지 갔는데 대상이 아직 멀다 — handoffRadius({handoffRadius})를 "
                  + "늘리거나 좌석 주변까지 Bake할 것");
        }

        private bool WithinHandoff()
        {
            if (_target == null) return false;

            return (_target.position - transform.position).sqrMagnitude <= handoffRadius * handoffRadius;
        }

        /// <summary>
        /// 건넸다. 상태를 먼저 비우고 <see cref="ServeBoard.Deliver"/> 를 부른다.
        ///
        /// 순서가 중요하다 — Deliver 안에서 손님 코드가 돌고, 그게 다음 배정을 부를 수도
        /// 있다. 그때 이미 유휴여야 이어서 받는다.
        /// </summary>
        private void CompleteDelivery()
        {
            ClearState();
            board.Deliver(this);
        }

        /// <summary>
        /// 배달을 포기한다. 예약을 보드에 되돌려 다시 배정되게 한다. (+9/10)
        ///
        /// 1차에서는 꺼내온 작업이 여기서 증발했다(이슈 #5 미해결). 이제 보드가
        /// 예약을 들고 있으므로 되돌릴 자리가 있다.
        /// </summary>
        private void Release(string reason)
        {
            ClearState();
            board.Abandon(this, reason);
        }

        /// <summary>
        /// 조리대에서 받은 실물을 손에 단다. (+9/10)
        ///
        /// <c>worldPositionStays: true</c> 로 붙인 뒤 로컬 좌표를 0으로 민다.
        /// 그냥 false 로 붙이면 손 오브젝트의 스케일이 음식에 곱해진다 — 조리대가
        /// 거치할 때 부모를 안 달았던 이유와 같다(비균등 스케일 상속).
        ///
        /// 실물이 없으면(그 메뉴에 ServingPrefab이 없거나 holdPoint가 비었으면)
        /// 그림으로 대신한다. 그림도 비어 있으면 빈손으로 걸어간다.
        /// </summary>
        private void Carry(GameObject visual)
        {
            _carried = visual;

            if (visual == null)
            {
                ShowIcon(_icon);
                return;
            }

            if (holdPoint == null)
            {
                // 달 곳이 없으면 실물을 들고 다닐 수 없다. 조리대에서 이미 꺼냈으므로
                // 되돌릴 수도 없다 — 지우고 그림으로 대신한다.
                Debug.LogWarning($"[ServingStaff] holdPoint가 비어 있어 음식 실물을 달 수 없다 ({name}). "
                               + "인스펙터에 손 위치를 넣을 것. 지금은 그림으로 대신한다.", this);
                Destroy(visual);
                _carried = null;
                ShowIcon(_icon);
                return;
            }

            visual.transform.SetParent(holdPoint, true);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
        }

        private void ClearState()
        {
            _mover.Stop();
            ShowIcon(null);

            // 손님이 받았으면 먹는 연출은 손님 쪽 몫이다. 직원 손의 실물은 여기서 사라진다.
            if (_carried != null)
            {
                Destroy(_carried);
                _carried = null;
            }

            _target = null;
            _icon = null;
            _state = State.Idle;
        }

        private void ShowIcon(Sprite sprite)
        {
            if (carriedIcon == null) return;

            carriedIcon.sprite = sprite;
            carriedIcon.enabled = sprite != null;
        }
    }
}
