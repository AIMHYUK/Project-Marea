using UnityEngine;

namespace Marea.Player
{
    /// <summary>
    /// PlayerController의 이동 속도를 Animator로 넘긴다. 그것만 한다. (+9/23)
    ///
    /// ServingStaffAnimator와 같은 패턴이다 — 걷는 일은 AgentMover가, 무엇을 재생할지는
    /// Animator가, 그 둘을 잇는 한 줄만 여기가 한다. 판단(gait 결정)은 여기서 안 한다.
    ///
    /// 넘기는 값은 PlayerController.Speed(m/s)를 Speed(float) 파라미터로 그대로 보낸 것이다.
    /// AC_Player의 1D 블렌드 트리가 이 값으로 Idle→Walk→Run을 섞는다. run이 들어와도 여기는
    /// 안 바뀐다 — 속도가 커지면 트리가 알아서 Run으로 넘어간다. 그게 float로 간 이유다.
    ///
    /// dampTime을 줘서 속도가 뚝 끊기지 않게 한다. 멈출 때 Idle로, 뛰기 시작할 때 Run으로
    /// 부드럽게 넘어가는 게 여기서 나온다. 그래서 매 프레임 불러야 한다(값이 같아도 수렴 중).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimator : MonoBehaviour
    {
        private const string SpeedParam = "Speed";

        [Tooltip("비워두면 자식에서 찾는다. 캐릭터 모델(Ray)이 자식으로 들어가는 구조라 대개 자동으로 잡힌다.")]
        [SerializeField] private Animator animator;

        [Tooltip("속도 파라미터가 목표값을 따라가는 데 걸리는 시간(초). 크면 반응이 느긋해진다.")]
        [SerializeField, Min(0f)] private float dampTime = 0.1f;

        private PlayerController _player;
        private int _speedHash;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _speedHash = Animator.StringToHash(SpeedParam);

            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            // 모델을 안 넣었거나 Animator를 빠뜨렸다. 이대로면 플레이어가 T-pose로 미끄러져 다니는데,
            // 화면만 봐서는 "애니메이션이 아직 안 붙었나" 싶을 뿐 원인을 못 찾는다.
            if (animator == null)
            {
                Debug.LogError(
                    "[PlayerAnimator] Animator가 없습니다. 캐릭터 모델(Ray)을 자식으로 넣거나 " +
                    "인스펙터에 직접 지정하세요. 이대로면 애니메이션이 재생되지 않습니다.", this);
                enabled = false;
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError(
                    "[PlayerAnimator] Animator에 컨트롤러가 없습니다. AC_Player를 넣으세요.", this);
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            // SetFloat의 dampTime 오버로드가 목표값을 향해 프레임독립적으로 따라간다.
            // 매 프레임 현재 속도를 목표로 주면 알아서 부드럽게 수렴한다.
            animator.SetFloat(_speedHash, _player.Speed, dampTime, Time.deltaTime);
        }
    }
}
