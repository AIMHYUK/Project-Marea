using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 완성된 음식이 쌓이는 곳. B가 Post하고 직원이 꺼내간다.
    ///
    /// 인터페이스가 아니라 구체 클래스다 — Warehouse와 같은 패턴이다.
    /// 구현이 하나뿐이고 B가 구현할 물건도 아니라 인터페이스가 값을 못 한다.
    /// 자세한 근거는 Docs/설계_결정.md 결정 9.
    /// </summary>
    public class ServeBoard : MonoBehaviour
    {
        [Header("픽업 지점 (+9/3)")]
        [Tooltip("직원이 음식을 받아가는 자리. 비워두면 이 오브젝트 자신의 위치를 쓴다. " +
                 "요리대 옆 NavMesh 위에 둘 것.")]
        [SerializeField] private Transform pickupPoint;

        [Header("테스트용 — 좌표 배달 확인에만 쓴다")]
        [Tooltip("ContextMenu로 작업을 넣을 때 쓸 픽업 지점.")]
        [SerializeField] private Transform testPickup;

        [Tooltip("ContextMenu로 작업을 넣을 때 쓸 배달 지점.")]
        [SerializeField] private Transform testDeliver;

        [SerializeField] private Sprite testIcon;

        private readonly Queue<ServeTask> _pending = new Queue<ServeTask>();

        /// <summary>
        /// 작업이 들어왔다는 신호.
        ///
        /// 이건 상태의 원본이 아니라 "지금 봐라"는 알림일 뿐이다.
        /// 구독자가 바빠서 무시해도 작업은 큐에 그대로 남아 있으므로,
        /// 유휴가 되면 스스로 TryTake를 한 번 더 부르면 된다.
        /// 놓친 알림을 따로 저장하면 큐 위에 큐를 하나 더 만드는 셈이 된다.
        /// </summary>
        public event Action OnPosted;

        public int PendingCount => _pending.Count;

        /// <summary>직원이 음식을 받아가는 자리. (+9/3)</summary>
        public Vector3 PickupPosition => pickupPoint != null ? pickupPoint.position : transform.position;

        /// <summary>B가 부른다. 음식이 완성되면 픽업·배달 좌표와 아이콘을 넘긴다.</summary>
        public void Post(Vector3 pickup, Vector3 deliver, Sprite icon)
        {
            Enqueue(new ServeTask(pickup, deliver, icon));
        }

        /// <summary>
        /// B가 부른다. 대상에게 배달한다. (+9/3)
        ///
        /// 픽업 지점은 이 보드의 <see cref="PickupPosition"/> 이다 — 음식은 여기서 나오니까
        /// 호출자가 요리대 좌표를 따로 알 필요가 없다.
        /// onDelivered는 직원이 대상에게 닿았을 때 딱 한 번 불린다.
        /// </summary>
        public void Post(Transform target, Sprite icon, Action onDelivered)
        {
            Post(PickupPosition, target, icon, onDelivered);
        }

        /// <summary>픽업 지점을 직접 지정하는 대상 배달. (+9/3)</summary>
        public void Post(Vector3 pickup, Transform target, Sprite icon, Action onDelivered)
        {
            // 부르는 쪽 버그다. 음식이 조용히 사라지는 것보다 시끄럽게 터지는 게 낫다. (+9/3)
            if (target == null)
            {
                Debug.LogError("[ServeBoard] 배달 대상이 null이다 — 작업을 넣지 않는다. " +
                               "Post를 부르기 전에 대상이 살아 있는지 확인할 것.", this);
                return;
            }

            Enqueue(new ServeTask(pickup, target, icon, onDelivered));
        }

        /// <summary>
        /// 이 대상에게 갈 음식이 큐에 이미 있나. (+9/3)
        ///
        /// 같은 손님에게 음식을 두 번 배정하지 않으려고 B가 부른다.
        /// 손님 쪽에 "예약됨" 플래그를 다는 것보다 이게 낫다 — 플래그는 배달이
        /// 실패했을 때 안 풀려서 손님이 좌석을 물고 안 나간다.
        /// 직원이 이미 들고 나간 것은 여기 없다. ServingStaff.DeliverTarget 을 같이 봐야 한다.
        /// </summary>
        public bool IsTargeted(Transform target)
        {
            if (target == null) return false;

            foreach (ServeTask task in _pending)
            {
                if (task.DeliverTarget == target) return true;
            }

            return false;
        }

        private void Enqueue(ServeTask task)
        {
            _pending.Enqueue(task);
            OnPosted?.Invoke();
        }

        /// <summary>
        /// 직원이 하나 꺼내간다. 꺼낸 순간 큐에서 빠지므로
        /// 직원이 여러 명이 돼도 같은 음식을 둘이 집지 않는다.
        /// </summary>
        internal bool TryTake(out ServeTask task)
        {
            if (_pending.Count == 0)
            {
                task = default;
                return false;
            }

            task = _pending.Dequeue();
            return true;
        }

        /// <summary>
        /// 배달이 끝났다. 대상에게 알리는 것도 여기서 한다.
        ///
        /// try/catch가 있는 이유: OnDelivered 안은 B 코드다. 거기서 예외가 나도
        /// 직원이 그걸 뒤집어쓰면 안 된다 — 한 번 터지면 유휴로 못 돌아와서
        /// 그 뒤 모든 서빙이 멈춘다.
        /// </summary>
        internal void Complete(ServeTask task)
        {
            // TODO(B가 SalesManager 커밋한 뒤): 판정 등급을 곱해 매출을 올린다.
            try
            {
                task.OnDelivered?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServeBoard] 배달 완료 콜백에서 예외 — {e}", this);
            }

            Debug.Log($"[ServeBoard] 배달 완료 — 남은 작업 {_pending.Count}", this);
        }

        [ContextMenu("테스트 작업 하나 넣기")]
        private void PostTestTask()
        {
            if (testPickup == null || testDeliver == null)
            {
                Debug.LogWarning("[ServeBoard] testPickup / testDeliver 를 인스펙터에 넣어라.", this);
                return;
            }

            Post(testPickup.position, testDeliver.position, testIcon);
            Debug.Log($"[ServeBoard] 테스트 작업 추가 — 대기 {_pending.Count}", this);
        }
    }
}
