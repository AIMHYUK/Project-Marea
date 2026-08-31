using Marea.Core;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 서빙 직원. 유휴일 때 ServeBoard에서 작업을 하나 꺼내 픽업→배달을 왕복한다.
    ///
    /// 걷는 일은 AgentMover가 한다. 여기는 "무엇을 할지"만 정한다.
    /// IInteractor를 구현하지 않는다 — 클릭 경로를 안 타기 때문이다(설계 결정 10).
    /// </summary>
    [RequireComponent(typeof(AgentMover))]
    public class ServingStaff : MonoBehaviour
    {
        private enum State { Idle, Delivering }

        [SerializeField] private ServeBoard board;

        [Tooltip("들고 있는 음식 그림. 비워둬도 동작한다.")]
        [SerializeField] private SpriteRenderer carriedIcon;

        private AgentMover _mover;
        private State _state = State.Idle;
        private ServeTask _task;

        public bool IsIdle => _state == State.Idle;

        private void Awake()
        {
            _mover = GetComponent<AgentMover>();
            ShowIcon(null);
        }

        private void OnEnable()
        {
            if (board != null) board.OnPosted += TryStartNext;

            // 켜지기 전에 이미 쌓여 있을 수 있다.
            TryStartNext();
        }

        private void OnDisable()
        {
            if (board != null) board.OnPosted -= TryStartNext;
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
            if (board == null) return;
            if (!board.TryTake(out _task)) return;

            _state = State.Delivering;
            GoPickup();
        }

        private void GoPickup()
        {
            _mover.GoTo(_task.PickupPoint,
                onArrived: () =>
                {
                    ShowIcon(_task.FoodIcon);

                    // 콜백 안에서 다시 GoTo를 건다.
                    // AgentMover.Fire()가 상태를 먼저 비우고 콜백을 부르기 때문에 가능하다.
                    GoDeliver();
                },
                onFailed: OnPathFailed);
        }

        private void GoDeliver()
        {
            _mover.GoTo(_task.DeliverPoint,
                onArrived: () =>
                {
                    ShowIcon(null);
                    board.Complete(_task);

                    _state = State.Idle;
                    TryStartNext();   // ② 큐에 남은 게 있으면 이어서
                },
                onFailed: OnPathFailed);
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
                $"픽업 {_task.PickupPoint} / 배달 {_task.DeliverPoint}. " +
                $"목적지가 NavMesh 위인지, 가구 안쪽이 아닌지 확인할 것.", this);

            ShowIcon(null);
            _state = State.Idle;
            TryStartNext();
        }

        private void ShowIcon(Sprite sprite)
        {
            if (carriedIcon == null) return;

            carriedIcon.sprite = sprite;
            carriedIcon.enabled = sprite != null;
        }
    }
}
