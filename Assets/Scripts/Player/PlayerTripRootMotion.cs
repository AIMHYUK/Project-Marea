using UnityEngine;
using UnityEngine.AI;

namespace Marea.Player
{
    /// <summary>
    /// 넘어진 동안에만 Tripping 클립의 앞으로 쏠리는 이동을 에이전트에 옮긴다. (+10/9) ServingStaffRootMotion과 같은 규칙.
    ///
    /// 예전엔 플레이어 Animator의 루트 모션이 꺼져 있어 제자리에서 넘어졌다. 직원은 클립대로 앞으로 3m쯤 쏠린다.
    /// agent.Move로 넘겨서 NavMesh 밖(난간 · 물)으로는 안 나간다. 걷기 중의 루트 모션은 예전처럼 버린다.
    ///
    /// 플레이어는 프리팹이 아니라 씬 오브젝트라(Main 포함) 씬에 붙이지 않고 PlayerTrip이 Awake에서 모델에 붙인다.
    /// Animator와 같은 오브젝트에 있어야 OnAnimatorMove가 불린다.
    ///
    /// (+10/13) 넘어지는 동작(Tripped 상태 · 그리로 들어가는 전환)일 때만 옮긴다. 예전엔 넘어진 동안 내내 옮겨서,
    /// 일어나기 → 걷기 전환 구간에 섞인 걷기 클립 이동(프레임마다 0.2~0.4m)까지 실려 끝날 때 1m쯤 순간이동했다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerTripRootMotion : MonoBehaviour
    {
        private Animator _animator;
        private PlayerTrip _trip;
        private NavMeshAgent _agent;
        private static readonly int TrippedState = Animator.StringToHash("Tripped");

        public void Init(PlayerTrip trip, NavMeshAgent agent)
        {
            _animator = GetComponent<Animator>();
            _trip = trip;
            _agent = agent;
            if (_agent == null)
                Debug.LogError($"{name}: 플레이어에 NavMeshAgent가 없다. 넘어질 때 앞으로 안 쏠린다.", this);

            // 꺼져 있으면 OnAnimatorMove가 안 불리고 deltaPosition도 0이다.
            _animator.applyRootMotion = true;
        }

        private void OnAnimatorMove()
        {
            if (_trip == null || _agent == null || !_trip.IsTripped || !_agent.isOnNavMesh) return;
            if (!IsFalling(_animator)) return;

            Vector3 delta = _animator.deltaPosition;
            delta.y = 0f;   // 높이는 포즈에 구웠다. 에이전트는 바닥에 붙어 있어야 한다.
            _agent.Move(delta);
        }

        /// <summary>(+10/13) 지금 넘어지는 동작인가 — Tripped 상태거나 그리로 넘어가는 중. 일어나기 · 걷기로 가는 구간은 아니다.</summary>
        internal static bool IsFalling(Animator animator)
        {
            if (animator.IsInTransition(0)) return animator.GetNextAnimatorStateInfo(0).shortNameHash == TrippedState;
            return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == TrippedState;
        }
    }
}
