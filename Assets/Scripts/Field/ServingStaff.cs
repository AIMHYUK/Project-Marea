using System.Collections.Generic;
using Marea.Core;
using Marea.Economy;
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
        /// <summary>
        /// 직원이 지금 무엇을 하는 중인가.
        ///
        /// 값이 AC_ServingStaff 의 State 파라미터로 그대로 넘어간다 (+9/22).
        /// 그래서 순서를 바꾸거나 중간에 끼워 넣으면 애니메이션이 어긋난다 —
        /// 늘릴 때는 뒤에 붙이고 컨트롤러에도 상태를 같이 추가할 것.
        ///
        /// Returning 은 배달을 끝내고 픽업대로 빈손으로 걸어 돌아가는 중이다. (+9/23)
        /// 일이 없는 상태이므로 여기서도 새 작업을 받는다 — Idle 과 같이 취급하는 자리가 있다.
        ///
        /// Tripped 는 배달 중에 넘어져 음식을 잃고 일어나는 중이다. (+9/28, 이슈 76)
        /// DeliverTarget 이 null이라 B는 그 손님에게 다시 조리한 음식을 배정할 수 있다.
        /// </summary>
        public enum State { Idle = 0, ToPickup = 1, ToTarget = 2, Returning = 3, Tripped = 4 }

        [Tooltip("작업을 받아올 게시판. 자동으로 못 찾으니 반드시 넣어야 한다 — "
               + "비면 OnEnable에서 에러를 내고 이 직원은 서빙을 하지 않는다. (+9/8)")]
        [SerializeField] private ServeBoard board;

        [Tooltip("들고 있는 음식 그림. 비워둬도 동작한다.")]
        [SerializeField] private SpriteRenderer carriedIcon;

        [Tooltip("(+10/7) 조리대에서 집은 음식 모델을 붙일 손 위치. 비우면 모델 없이 아이콘만.")]
        [SerializeField] private Transform holdPoint;
        [Tooltip("(+10/7) 음식을 집어 올 조리대. 비우면 씬에서 찾는다.")]
        [SerializeField] private Marea.Restaurant.CookingCounter counter;

        private GameObject _carried;   // (+10/7) 조리대에서 옮겨 온 음식 모델

        [Header("배달 판정 (+9/3)")]
        [Tooltip("대상과 이만큼 가까워지면 건넨 것으로 본다.")]
        [SerializeField, Min(0.1f)] private float handoffRadius = 1.8f;

        [Tooltip("대상 발밑이 NavMesh 밖일 때, 이 반경 안에서 가장 가까운 NavMesh 점을 목적지로 삼는다.")]
        [SerializeField, Min(0.1f)] private float navSampleRadius = 4f;

        // (+9/30) 넘어질 확률은 파손된 장판(TripHazard)이 든다. 예전의 "배달마다 15%"는 기획이
        // "바닥 돌출부에 걸리면 25%"로 바뀌어 지웠다.
        [Header("넘어짐 (+9/28, 이슈 76)")]
        [Tooltip("넘어지면 지갑에서 빠지는 골드. 잔액이 모자라면 있는 만큼만 빠진다.")]
        [SerializeField, Min(0)] private int tripPenalty = 50;

        [Tooltip("넘어져서 다시 움직일 때까지 초. AC_ServingStaff 의 Tripping(2.8초) + "
               + "Standing Up(11.4초, 3배속 ≈ 3.8초)에 맞췄다.")]
        [SerializeField, Min(0.1f)] private float tripSeconds = 6.6f;

        [Header("연출 (+10/6, 이슈 117) — 기획 「파손 바닥에서 음식 떨어뜨림」 VFX_10 / VFX_13")]
        [SerializeField] private Marea.Data.VfxId tripDustVfx = Marea.Data.VfxId.Dust;
        [SerializeField] private Marea.Data.VfxId tripFailVfx = Marea.Data.VfxId.Fail;

        [Header("소리 (+10/6, 이슈 117) — 기획 SRV-02 sfx_srv_pickup · 발소리(SRV-01)는 Footsteps")]
        [Tooltip("조리대에서 접시를 집을 때 \"달그락\". 그 자리에서 난다(3D).")]
        [SerializeField] private AudioClip pickupClip;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.8f;

        private AgentMover _mover;
        private State _state = State.Idle;
        private ServeTask _task;

        /// <summary>
        /// 이 작업이 대상 배달인가. _task.DeliverTarget 은 대상이 Destroy되면 null이 되므로
        /// "원래 대상이 있었나"를 이것으로 따로 기억한다. 안 그러면 손님이 사라진 것과
        /// 처음부터 좌표 배달이었던 것을 구분할 수 없다.
        /// </summary>
        private bool _expectTarget;

        // 넘어짐 (+9/28). (+9/30) 음식을 든 채 밟은 장판마다 한 번 판정한다 — 같은 장판에서 매 프레임 굴리면
        // 25%가 사실상 100%가 된다. 배달을 새로 떠날 때 비운다.
        private readonly HashSet<TripHazard> _rolledHazards = new();
        private float _tripEndsAt;
        private float _penaltyLabelUntil;
        private string _penaltyText;
        private WorldLabelUI _labels;
        private NavMeshAgent _agent;
        private float _baseSpeed;   // (+10/2) 업그레이드 전 속도. AgentMover가 Awake에서 넣은 값

        /// <summary>
        /// 지금 상태. ServingStaffAnimator 가 이것만 읽어서 Animator 에 넘긴다 (+9/22).
        /// </summary>
        public State Current => _state;

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
        public Transform DeliverTarget =>
            _state == State.ToPickup || _state == State.ToTarget ? _task.DeliverTarget : null;

        private void Awake()
        {
            _mover = GetComponent<AgentMover>();
            _agent = GetComponent<NavMeshAgent>();
            _labels = FindAnyObjectByType<WorldLabelUI>(FindObjectsInactive.Include);
            if (counter == null) counter = FindAnyObjectByType<Marea.Restaurant.CookingCounter>(FindObjectsInactive.Include);
            ShowIcon(null);
        }

        // (+10/2) 직원 운영 업그레이드 — 이동 속도 증가(STAFF_SPEED_ADD, 누적 총합).
        // AgentMover.Awake가 agent.speed를 넣은 뒤라 Start에서 바닥값을 잡는다.
        private void Start()
        {
            if (_agent != null) _baseSpeed = _agent.speed;
            if (FacilityLevels.Instance != null) FacilityLevels.Instance.OnLevelChanged += HandleLevelChanged;
            ApplySpeed();
        }

        private void OnDestroy()
        {
            if (FacilityLevels.Instance != null) FacilityLevels.Instance.OnLevelChanged -= HandleLevelChanged;
        }

        private void HandleLevelChanged(FacilityKind kind, int level)
        {
            if (kind == FacilityKind.Staff) ApplySpeed();
        }

        private void ApplySpeed()
        {
            if (_agent == null || _baseSpeed <= 0f) return;
            float add = FacilityLevels.Instance != null
                ? FacilityLevels.Instance.EffectValue(FacilityKind.Staff, Marea.Data.FacilityEffectType.StaffSpeedAdd)
                : 0f;
            _agent.speed = _baseSpeed * (1f + add);
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
            // 차감 표시는 상태와 상관없이 잠깐 띄운다. 씬에 WorldLabelUI가 없으면 표시만 빠진다.
            if (_labels != null && Time.time < _penaltyLabelUntil)
                _labels.Set(this, transform.position + Vector3.up * 2.2f, _penaltyText);

            if (_state == State.Tripped)
            {
                if (Time.time >= _tripEndsAt) EndTrip();
                return;
            }


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
            // 복귀 중에도 받는다. 빈손으로 걸어가는 중일 뿐이라 일을 못 할 이유가 없고,
            // 다 돌아갈 때까지 기다리면 손님이 그만큼 더 기다린다. (+9/23)
            // GoTo가 내부에서 Stop()을 부르므로 복귀 콜백은 여기서 알아서 버려진다.
            if (_state != State.Idle && _state != State.Returning) return;
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
                    TakeFoodFromCounter();   // (+10/7) 조리대 위 음식을 손으로
                    SoundManager.PlayAt(pickupClip, transform.position, pickupVolume);   // (+10/6)
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

            Vector3 deliverPoint = ResolveDeliverPoint();
            _rolledHazards.Clear();

            _mover.GoTo(deliverPoint,
                onArrived: OnDeliverArrived,
                onFailed: OnPathFailed);
        }

        /// <summary>
        /// 파손된 장판을 밟았다 — TripHazard의 트리거가 부른다. (+9/30)
        /// 음식을 들고 갈 때만, 장판 하나당 이번 배달에서 한 번만 굴린다.
        /// </summary>
        public void OnSteppedHazard(TripHazard hazard)
        {
            if (_state != State.ToTarget || hazard == null) return;
            if (!_rolledHazards.Add(hazard)) return;
            if (Random.value < hazard.TripChance) Trip();
        }

        /// <summary>
        /// 넘어졌다. 음식을 잃는다 — 작업을 버리면 DeliverTarget이 null이 되어, B가 다음 조리분을
        /// 그 손님에게 다시 배정한다. 손님은 OnDelivered가 올 때까지 그냥 기다린다. (+9/28, 이슈 76)
        /// 골드는 넘어지는 순간 뺀다. Wallet은 0 아래로 안 가서 모자라면 있는 만큼만.
        /// </summary>
        private void Trip()
        {
            _mover.Stop();
            ShowIcon(null);
            _task = default;
            _expectTarget = false;
            _state = State.Tripped;
            _tripEndsAt = Time.time + tripSeconds;

            // (+10/6) 음식이 떨어진 자리 — 직원 앞 바닥.
            Vector3 drop = transform.position + transform.forward * 0.6f;
            Vfx.Play(tripDustVfx, drop, 0.6f);
            Vfx.Play(tripFailVfx, drop + Vector3.up * 0.3f, 0.6f);

            Wallet wallet = Wallet.Instance;
            if (wallet == null)
            {
                Debug.LogError("[ServingStaff] 씬에 Wallet이 없다. 넘어졌는데 골드를 못 뺐다.", this);
                return;
            }

            int paid = Mathf.Min(wallet.Gold, tripPenalty);
            if (paid > 0 && wallet.TrySpend(paid))
            {
                _penaltyText = $"-{paid}G";
                _penaltyLabelUntil = Time.time + 2f;
            }
        }

        /// <summary>일어났다. 큐에 다음 일이 있으면 받고, 없으면 픽업대로 돌아간다 (CompleteDelivery와 같은 끝).</summary>
        private void EndTrip()
        {
            _state = State.Idle;
            TryStartNext();
            if (_state == State.Idle) GoReturn();
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

            // 이어받은 게 없으면 손님 옆에 그대로 서 있게 된다. 픽업대로 걸어 돌아간다. (+9/23)
            // TryStartNext가 이미 일을 집었으면 여기는 Idle이 아니라서 건너뛴다.
            if (_state == State.Idle) GoReturn();
        }

        /// <summary>
        /// 빈손으로 픽업대까지 걸어 돌아간다. 배달을 성공으로 끝냈을 때만 탄다. (+9/23)
        /// (+9/28) 넘어졌다 일어난 뒤에도 탄다(EndTrip) — 경로 실패가 아니라 돌아갈 길은 있다.
        ///
        /// 실패(DropTask)에서는 부르지 않는다. 경로를 못 만들어서 버린 상황이라면
        /// 돌아가는 경로도 대개 실패하고, 그때마다 로그가 두 번씩 쌓인다.
        ///
        /// allowPartialPath를 켠다 — 여기는 "그 자리에 정확히 서 있어야" 하는 일이 아니다.
        /// 픽업대 앞이 막혀 있으면 갈 수 있는 데까지만 가고 유휴로 돌아가면 그만이다.
        /// </summary>
        private void GoReturn()
        {
            if (board == null) return;

            // GoTo가 SetDestination에 실패하면 onFailed를 그 자리에서 부른다.
            // 그래서 상태를 먼저 Returning으로 올려둬야 콜백의 검사가 맞아떨어진다.
            _state = State.Returning;

            _mover.GoTo(board.PickupPosition,
                onArrived: EndReturn,
                onFailed: EndReturn,
                allowPartialPath: true);
        }

        /// <summary>
        /// 복귀가 끝났다. 도착이든 실패든 할 일은 같다 — 유휴로 돌아간다.
        ///
        /// 돌아가는 사이에 새 작업을 집었을 수 있다. 그때는 이미 Returning이 아니므로
        /// 건드리지 않는다. AgentMover가 콜백을 버리긴 하지만, 상태를 되돌리는 쪽에서
        /// 한 번 더 보는 편이 나중에 경로가 늘어나도 안전하다.
        /// </summary>
        private void EndReturn()
        {
            if (_state != State.Returning) return;
            _state = State.Idle;
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

        // (+10/7) 아이콘을 끄는 곳(배달 완료 · 넘어짐 · 포기 · 경로 실패)은 곧 손이 비는 곳이라 들고 있던 모델도 같이 치운다.
        private void ShowIcon(Sprite sprite)
        {
            if (sprite == null) ReleaseCarried();
            if (carriedIcon == null) return;

            carriedIcon.sprite = sprite;
            carriedIcon.enabled = sprite != null;
        }

        /// <summary>
        /// (+10/7) 조리대에 놓인 이 작업의 음식(같은 메뉴 아이콘)을 집어 손에 든다 — 조리대에선 없어진다.
        /// 플레이어가 먼저 가져갔으면 조리대가 비어 있다 — 그땐 빈손(아이콘만)으로 간다.
        /// </summary>
        private void TakeFoodFromCounter()
        {
            ReleaseCarried();
            if (counter == null || holdPoint == null) return;
            if (!counter.TryTakeFood(_task.FoodIcon, out _, out GameObject visual) || visual == null) return;

            _carried = visual;
            _carried.transform.SetParent(holdPoint, false);
            _carried.transform.localPosition = Vector3.zero;
            _carried.transform.localRotation = Quaternion.identity;
        }

        private void ReleaseCarried()
        {
            if (_carried == null) return;
            Destroy(_carried);
            _carried = null;
        }
    }
}
