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
        [Header("테스트용 — B의 요리가 붙으면 지운다")]
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

        /// <summary>B가 부른다. 음식이 완성되면 픽업·배달 좌표와 아이콘을 넘긴다.</summary>
        public void Post(Vector3 pickup, Vector3 deliver, Sprite icon)
        {
            _pending.Enqueue(new ServeTask(pickup, deliver, icon));
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

        /// <summary>배달이 끝났다.</summary>
        internal void Complete(ServeTask task)
        {
            // TODO(B가 SalesManager 커밋한 뒤): 판정 등급을 곱해 매출을 올린다.
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
