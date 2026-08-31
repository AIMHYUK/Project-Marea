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
        /// </summary>
        public void GoTo(Vector3 destination, Action onArrived, Action onFailed = null)
        {
            Stop();
            _agent.isStopped = false;

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

            // 경로가 불완전하면 목적지에 못 닿는다. 도착을 기다리지 않고 실패로 끝낸다.
            // PathPartial도 실패로 본다 — 상호작용 지점까지 못 가면 도착한 게 아니다.
            if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Fire(ref _onFailed);
                return;
            }

            float stop = Mathf.Max(_agent.stoppingDistance, arriveThreshold);
            if (_agent.remainingDistance > stop) return;
            if (_agent.hasPath && _agent.velocity.sqrMagnitude > 0.01f) return;

            Fire(ref _onArrived);
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
            IsMoving = false;

            callback?.Invoke();
        }
    }
}
