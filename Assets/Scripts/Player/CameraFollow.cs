using UnityEngine;

namespace Marea.Player
{
    /// <summary>
    /// 쿼터뷰 카메라. 각도는 고정이고 위치만 플레이어를 따라간다.
    /// 각도를 정하면 위치는 계산으로 나온다 — 회전하지 않는 게 쿼터뷰의 전부다.
    /// NavMeshAgent가 Update에서 캐릭터를 옮기므로 LateUpdate여야 화면이 안 떨린다.
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

        private Vector3 _velocity;
        private bool _snapped;

        private void Awake()
        {
            if (target != null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + lookOffset
                            - Quaternion.Euler(eulerAngles) * Vector3.forward * distance;

            transform.rotation = Quaternion.Euler(eulerAngles);
            transform.position = _snapped
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime)
                : desired;   // 첫 프레임은 원점에서 날아오지 않게 곧바로 붙인다
            _snapped = true;
        }
    }
}
