using UnityEngine;
using UnityEngine.AI;

namespace Marea.Field
{
    /// <summary>
    /// 넘어진 동안에만 클립의 앞으로 쏠리는 이동을 에이전트에 옮긴다. (+9/28, 이슈 76)
    ///
    /// Tripping 클립은 몸을 앞으로 3m쯤 보낸다. 이걸 포즈에 구우면 몸만 앞으로 가고
    /// 에이전트(와 머리 위 "-50G")는 제자리에 남아서, 일어나는 순간 몸이 3m 뒤로 순간이동했다.
    /// 그래서 XZ는 루트 모션으로 두고 여기서 agent.Move로 넘긴다 — NavMesh 밖(벽·물)으로는 안 나간다.
    ///
    /// 걷기 중의 루트 모션은 버린다. 이동은 AgentMover가 맡는다. OnAnimatorMove가 있으면
    /// Animator가 루트 모션을 스스로 적용하지 않으므로, 여기서 안 옮긴 프레임은 그냥 사라진다.
    ///
    /// Animator와 같은 오브젝트(캐릭터 모델)에 붙어야 OnAnimatorMove가 불린다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ServingStaffRootMotion : MonoBehaviour
    {
        private Animator _animator;
        private ServingStaff _staff;
        private NavMeshAgent _agent;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _staff = GetComponentInParent<ServingStaff>();
            _agent = GetComponentInParent<NavMeshAgent>();

            if (_staff == null || _agent == null)
                Debug.LogError($"{name}: 부모에 ServingStaff·NavMeshAgent가 없다. 넘어질 때 몸이 제자리에서 앞으로 쏠렸다가 순간이동한다.", this);

            // 꺼져 있으면 OnAnimatorMove가 안 불리고 deltaPosition도 0이다.
            _animator.applyRootMotion = true;
        }

        private void OnAnimatorMove()
        {
            if (_staff == null || _agent == null || !_agent.isOnNavMesh) return;
            if (_staff.Current != ServingStaff.State.Tripped) return;

            Vector3 delta = _animator.deltaPosition;
            delta.y = 0f;   // 높이는 포즈에 구웠다. 에이전트는 바닥에 붙어 있어야 한다.
            _agent.Move(delta);
        }
    }
}
