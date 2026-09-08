using System;
using UnityEngine;
using UnityEngine.AI;

namespace Marea.Core
{
    /// <summary>
    /// NavMeshAgent를 감싼 이동 컴포넌트. 플레이어와 NPC(서빙 직원)가 같이 쓴다.
    ///
    /// 무엇을 향해 가는지는 모른다 — 좌표와 콜백만 안다.
    /// IInteractable도 ServeTask도 여기서는 보이지 않는다.
    /// 그래서 이건 계약이 아니라 A 내부 물건이다. 마음대로 고쳐도 B는 모른다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AgentMover : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;

        [Tooltip("목적지에 이만큼 가까워지면 도착으로 본다.")]
        [SerializeField, Min(0f)] private float arriveThreshold = 0.15f;

        private NavMeshAgent _agent;
        private Action _onArrived;
        private Action _onFailed;
        private bool _allowPartialPath;

        /// <summary>목적지를 향해 걷는 중인가. MoveBy로 미는 건 여기 안 잡힌다.</summary>
        public bool IsMoving { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = moveSpeed;
        }

        private void Update()
        {
            if (IsMoving) TickArrival();
        }

        /// <summary>
        /// 목적지로 간다. 도착하면 onArrived, 경로를 못 만들면 onFailed가 불린다.
        /// 콜백 안에서 다시 GoTo를 불러도 된다 — 직원의 픽업→배달이 그 형태다.
        ///
        /// allowPartialPath는 "목적지까지 못 가면 갈 수 있는 데까지 가고 도착으로 친다"이다.
        /// 기본은 false — 상호작용과 배달은 그 자리에 실제로 서 있어야 뜻이 있어서,
        /// 중간에 멈춘 걸 도착으로 치면 멀리서 Interact()가 불리거나 배달이 안 됐는데
        /// 됐다고 처리된다. 바닥 클릭 이동만 true로 켠다 — 거기선 목적지가 사용자가 찍은
        /// 한 점일 뿐이라 못 가면 최대한 가까이 가는 게 맞는 손맛이다. (+9/8)
        /// </summary>
        public void GoTo(Vector3 destination, Action onArrived, Action onFailed = null,
                         bool allowPartialPath = false)
        {
            Stop();
            _agent.isStopped = false;
            _allowPartialPath = allowPartialPath;

            // 반환값을 반드시 본다. 목적지가 NavMesh 밖이면 경로가 아예 안 생기는데,
            // 그대로 두면 remainingDistance가 Infinity라 영원히 IsMoving에 머문다.
            // 플레이어는 WASD로 빠져나오지만 키보드가 없는 NPC는 영구 정지한다.
            if (!_agent.SetDestination(destination))
            {
                onFailed?.Invoke();
                return;
            }

            _onArrived = onArrived;
            _onFailed = onFailed;
            IsMoving = true;
        }

        /// <summary>진행 중인 목적지 이동을 취소한다. 걸어둔 콜백은 버린다.</summary>
        public void Stop()
        {
            _onArrived = null;
            _onFailed = null;
            _allowPartialPath = false;
            IsMoving = false;
            if (_agent.hasPath) _agent.ResetPath();
        }

        /// <summary>
        /// WASD 같은 직접 이동. 진행 중인 목적지 이동을 알아서 취소한다.
        /// 취소를 부르는 쪽이 기억하게 두면 언젠가 빠뜨린다.
        /// </summary>
        public void MoveBy(Vector3 worldDir, float deltaTime)
        {
            if (worldDir.sqrMagnitude < 0.01f) return;
            if (IsMoving || _agent.hasPath) Stop();

            // transform.position 직접 대입이 아니라 agent.Move를 쓴다.
            // 안 그러면 NavMesh 밖으로 나가서 다음 목적지 이동이 실패한다.
            _agent.Move(worldDir * (_agent.speed * deltaTime));
            transform.rotation = Quaternion.LookRotation(worldDir);
        }

        private void TickArrival()
        {
            if (_agent.pathPending) return;

            // 경로가 아예 안 나온 경우다. 도착을 기다리지 않고 실패로 끝낸다.
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                FailAndStop();
                return;
            }

            // PathPartial = 목적지까지는 못 가고 도중까지만 경로가 있다.
            // 기본은 실패다 — 상호작용 지점까지 못 가면 도착한 게 아니다. (+9/8)
            if (_agent.pathStatus == NavMeshPathStatus.PathPartial && !_allowPartialPath)
            {
                FailAndStop();
                return;
            }

            // 부분 경로에서도 remainingDistance를 그대로 쓴다. "목적지까지"라 안 줄어들까
            // 싶었는데, 재 보니 갈 수 있는 마지막 점에서 0까지 떨어진다 — Unity가 목적지를
            // 도달 가능한 지점으로 잡아준다. path.corners의 끝점까지 직선거리로 재는 쪽도
            // 만들어 봤지만 더 나빴다: 경로 끝이 ㄱ자로 꺾이면 직선으로는 코앞인데 실제로는
            // 모서리를 돌아야 해서, 도착하기 전에 도착으로 처리된다. (+9/8)
            float stop = Mathf.Max(_agent.stoppingDistance, arriveThreshold);
            if (_agent.remainingDistance > stop) return;
            if (_agent.hasPath && _agent.velocity.sqrMagnitude > 0.01f) return;

            Fire(ref _onArrived);
        }

        /// <summary>
        /// 못 간다고 판정났을 때. 콜백만 부르고 끝내면 에이전트는 경로를 그대로 들고
        /// 계속 걸어간다 — "실패했다"고 알린 뒤에도 캐릭터가 벽 쪽으로 걸어가는 게
        /// 보인다. 경로를 먼저 버리고 알린다. (+9/8)
        /// </summary>
        private void FailAndStop()
        {
            if (_agent.hasPath) _agent.ResetPath();

            Fire(ref _onFailed);
        }

        /// <summary>
        /// 콜백을 부르기 전에 내부 상태를 먼저 비운다.
        /// 콜백 안에서 GoTo가 다시 걸리면 IsMoving이 true로 돌아오는데,
        /// 부른 뒤에 상태를 만지면 그걸 덮어써서 새 이동의 도착이 영영 안 불린다.
        /// </summary>
        private void Fire(ref Action slot)
        {
            Action callback = slot;
            _onArrived = null;
            _onFailed = null;
            _allowPartialPath = false;
            IsMoving = false;

            callback?.Invoke();
        }
    }
}
