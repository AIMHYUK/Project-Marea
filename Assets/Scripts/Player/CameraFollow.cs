using UnityEngine;

namespace Marea.Player
{
    /// <summary>
    /// 쿼터뷰 카메라. 각도는 고정이고 위치만 플레이어를 따라간다.
    /// 각도를 정하면 위치는 계산으로 나온다 — 회전하지 않는 게 쿼터뷰의 전부다.
    /// NavMeshAgent가 Update에서 캐릭터를 옮기므로 LateUpdate여야 화면이 안 떨린다.
    ///
    /// (+10/6, 이슈 117) 잠깐 다른 지점을 볼 수 있다 — FocusOn / ClearFocus. 해금 연출이 쓴다.
    /// 미니게임·고래처럼 이 컴포넌트를 꺼서 카메라를 뺏지 않는다 — 셋이 끄고 켜면 서로 꼬인다.
    /// 각도는 그대로고 바라보는 점만 바뀐다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("비워두면 Awake에서 Player 태그를 찾는다.")]
        [SerializeField] private Transform target;

        [Header("쿼터뷰 각도 · 거리")]
        [Tooltip("X가 내려다보는 각도, Y가 비틀어 보는 각도.")]
        [SerializeField] private Vector3 eulerAngles = new(55f, 45f, 0f);
        [SerializeField, Min(1f)] private float distance = 14f;
        [Tooltip("발밑 대신 상체쯤을 보게 올린다.")]
        [SerializeField] private Vector3 lookOffset = new(0f, 1f, 0f);

        [Header("따라가기")]
        [Tooltip("0이면 딱 붙어서 따라간다.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.15f;

        [Header("잠깐 다른 곳 보기 (+10/6, 이슈 117)")]
        [Tooltip("포커스 지점까지 갈 때 · 플레이어로 돌아올 때의 부드러움. 따라가기보다 느려야 날아가는 게 보인다.")]
        [SerializeField, Min(0f)] private float focusSmoothTime = 0.35f;
        [Tooltip("포커스 지점과 이 거리 안이면 도착으로 본다.")]
        [SerializeField, Min(0.01f)] private float arriveDistance = 0.1f;

        private Vector3 _velocity;
        private bool _snapped;
        private Vector3? _focus;
        private bool _returning;   // 포커스를 풀고 플레이어로 돌아오는 중 — 그동안은 느린 값으로 간다

        /// <summary>플레이어 대신 이 점을 본다. ClearFocus까지 유지된다.</summary>
        public void FocusOn(Vector3 point) => _focus = point;

        /// <summary>다시 플레이어를 따라간다. 돌아오는 길도 부드럽게 간다.</summary>
        public void ClearFocus()
        {
            if (_focus == null) return;
            _focus = null;
            _returning = true;
        }

        /// <summary>포커스 지점에 도착했는가. 이 컴포넌트가 꺼져 있으면 영영 false다 — 부르는 쪽이 시간 제한을 둘 것.</summary>
        public bool IsAtFocus => _focus.HasValue
            && (transform.position - PositionFor(_focus.Value)).sqrMagnitude <= arriveDistance * arriveDistance;

        private void Awake()
        {
            if (target != null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        private void LateUpdate()
        {
            if (target == null && _focus == null) return;

            Vector3 desired = PositionFor(_focus ?? target.position + lookOffset);

            if (_returning && (transform.position - desired).sqrMagnitude <= arriveDistance * arriveDistance)
                _returning = false;
            float smooth = _focus.HasValue || _returning ? focusSmoothTime : smoothTime;

            transform.rotation = Quaternion.Euler(eulerAngles);
            transform.position = _snapped
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smooth)
                : desired;   // 첫 프레임은 원점에서 날아오지 않게 곧바로 붙인다
            _snapped = true;
        }

        private Vector3 PositionFor(Vector3 lookAt)
            => lookAt - Quaternion.Euler(eulerAngles) * Vector3.forward * distance;
    }
}
