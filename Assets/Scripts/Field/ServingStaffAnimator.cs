using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// ServingStaff의 상태를 Animator로 넘긴다. 그것만 한다. (+9/22)
    ///
    /// AgentMover에 넣지 않은 이유 — PF_Player도 같은 AgentMover를 쓴다.
    /// 거기에 애니메이션 구동을 넣으면 플레이어까지 같이 끌려온다.
    /// 플레이어는 캐릭터도 클립도 아직 다르므로 지금 엮으면 나중에 둘 다 뜯어야 한다.
    ///
    /// Animator에 넘기는 값은 ServingStaff.State의 정수값 그대로다.
    /// 컨트롤러(AC_ServingStaff)의 상태 이름도 enum과 같게 맞춰뒀다 —
    /// 둘이 어긋나면 눈으로 바로 잡을 수 있게 하려는 것이다.
    /// </summary>
    [RequireComponent(typeof(ServingStaff))]
    public class ServingStaffAnimator : MonoBehaviour
    {
        private const string StateParam = "State";

        [Tooltip("비워두면 자식에서 찾는다. 캐릭터 모델이 자식으로 들어가는 구조라 대개 자동으로 잡힌다.")]
        [SerializeField] private Animator animator;

        private ServingStaff _staff;
        private int _stateHash;
        private int _lastSent = -1;

        private void Awake()
        {
            _staff = GetComponent<ServingStaff>();
            _stateHash = Animator.StringToHash(StateParam);

            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            // 모델을 안 넣었거나 Animator를 빠뜨렸다. 이대로면 직원이 T-pose로 미끄러져 다니는데,
            // 화면만 봐서는 "애니메이션이 아직 안 붙었나" 싶을 뿐 원인을 못 찾는다.
            if (animator == null)
            {
                Debug.LogError(
                    "[ServingStaffAnimator] Animator가 없습니다. 캐릭터 모델을 자식으로 넣거나 " +
                    "인스펙터에 직접 지정하세요. 이대로면 애니메이션이 재생되지 않습니다.", this);
                enabled = false;
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError(
                    "[ServingStaffAnimator] Animator에 컨트롤러가 없습니다. " +
                    "AC_ServingStaff를 넣으세요.", this);
                enabled = false;
                return;
            }

            // 켜질 때 현재 상태를 한 번 밀어 넣는다. 안 그러면 첫 전환이 일어날 때까지
            // 기본 상태로 서 있는데, 유휴가 아닌 채로 활성화되면 그게 틀린 그림이다.
            _lastSent = -1;
            Push();
        }

        private void Update() => Push();

        /// <summary>
        /// 바뀐 순간에만 넘긴다. Animator.SetInteger는 매 프레임 불러도 동작은 같지만,
        /// 값이 그대로일 때 굳이 넘길 이유가 없다.
        /// </summary>
        private void Push()
        {
            int now = (int)_staff.Current;
            if (now == _lastSent) return;

            animator.SetInteger(_stateHash, now);
            _lastSent = now;
        }
    }
}
