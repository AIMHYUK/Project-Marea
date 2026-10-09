using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
    ///
    /// (+10/9, 이슈 128) 휠로 거리, 우클릭 끌기로 Y축(좌우) 회전. 내려다보는 각도(X)는 고정.
    /// 좌클릭은 클릭 이동 · 상호작용이라 쓰지 않는다. UI 위에서는 안 받고(창 스크롤과 안 겹치게),
    /// 연출(FocusOn) 중에도 안 받는다. 미니게임 · 고래는 이 컴포넌트를 끄니 그동안은 저절로 안 받는다.
    /// WASD는 PlayerController가 매 프레임 카메라 방향을 기준으로 잡아서 돌려도 화면 기준이 유지된다.
    /// 연출(FocusOn) 중엔 거리를 focusDistance로 맞춘다 — 휠로 당겨 둔 정도와 상관없이 연출이 같게 보인다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("비워두면 Awake에서 Player 태그를 찾는다.")]
        [SerializeField] private Transform target;

        [Header("쿼터뷰 각도 · 거리")]
        [Tooltip("X가 내려다보는 각도, Y가 비틀어 보는 각도. (+10/9) Y는 시작 값 — 플레이 중엔 우클릭 끌기로 돈다.")]
        [SerializeField] private Vector3 eulerAngles = new(55f, 45f, 0f);
        [Tooltip("시작 거리. (+10/9) 플레이 중엔 휠로 min~maxDistance 안에서 바뀐다.")]
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

        [Header("휠 줌 · 우클릭 회전 (+10/9)")]
        [SerializeField, Min(1f)] private float minDistance = 5f;
        [SerializeField, Min(1f)] private float maxDistance = 20f;
        [Tooltip("휠 한 칸에 바뀌는 거리(m).")]
        [SerializeField, Min(0.1f)] private float zoomStep = 1.5f;
        [Tooltip("거리가 목표까지 따라가는 부드러움. 0이면 바로.")]
        [SerializeField, Min(0f)] private float zoomSmoothTime = 0.12f;
        [Tooltip("우클릭으로 끌 때 마우스 1픽셀당 Y축 회전(도).")]
        [SerializeField, Min(0f)] private float rotateDegreesPerPixel = 0.3f;
        [Tooltip("(+10/9) 연출(FocusOn — 해금 · 새로 등장) 동안 쓰는 거리. 휠로 얼마나 당겼든 연출은 같은 거리로 보이고, 끝나면 휠 거리로 돌아간다.")]
        [SerializeField, Min(1f)] private float focusDistance = 12f;

        private float _yaw;
        private float _targetDistance;
        private float _currentDistance;
        private float _zoomVelocity;
        private float _focusDistanceOverride;
        private bool _dragging;

        private Vector3 _velocity;
        private bool _snapped;
        private Vector3? _focus;
        private bool _returning;   // 포커스를 풀고 플레이어로 돌아오는 중 — 그동안은 느린 값으로 간다

        /// <summary>플레이어 대신 이 점을 본다. ClearFocus까지 유지된다.</summary>
        public void FocusOn(Vector3 point) => FocusOn(point, 0f);

        /// <summary>(+10/9) 연출 거리를 이번만 정해서 본다. 0 이하면 focusDistance. 넓은 곳(농장 해금)을 한눈에 볼 때.</summary>
        public void FocusOn(Vector3 point, float distance)
        {
            _focus = point;
            _focusDistanceOverride = distance;
        }

        /// <summary>다시 플레이어를 따라간다. 돌아오는 길도 부드럽게 간다.</summary>
        public void ClearFocus()
        {
            if (_focus == null) return;
            _focus = null;
            _focusDistanceOverride = 0f;
            _returning = true;
        }

        /// <summary>포커스 지점에 도착했는가. 이 컴포넌트가 꺼져 있으면 영영 false다 — 부르는 쪽이 시간 제한을 둘 것.</summary>
        public bool IsAtFocus => _focus.HasValue
            && (transform.position - PositionFor(_focus.Value)).sqrMagnitude <= arriveDistance * arriveDistance;

        private void Awake()
        {
            _yaw = eulerAngles.y;
            _targetDistance = _currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);

            if (target != null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        private void OnDisable() => _dragging = false;   // 미니게임이 끄는 동안 버튼을 놓쳐도 끌기가 남지 않게

        private void Update() => ReadZoomAndRotate();

        private void ReadZoomAndRotate()
        {
            Mouse mouse = Mouse.current;
            bool locked = _focus.HasValue || _returning;
            if (mouse == null || locked)
            {
                _dragging = false;
                return;
            }

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            float scroll = mouse.scroll.ReadValue().y;
            if (!overUi && Mathf.Abs(scroll) > 0.01f)
                _targetDistance = Mathf.Clamp(_targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);

            // UI 위에서 누른 건 회전으로 안 친다. 누른 뒤 UI 위로 지나가는 건 계속 돈다.
            if (mouse.rightButton.wasPressedThisFrame && !overUi) _dragging = true;
            if (!mouse.rightButton.isPressed) _dragging = false;
            if (_dragging) _yaw += mouse.delta.ReadValue().x * rotateDegreesPerPixel;
        }

        private void LateUpdate()
        {
            if (target == null && _focus == null) return;

            // (+10/9) 연출 중엔 고정 거리(이번 연출이 따로 정했으면 그 거리)
            float wantDistance = !_focus.HasValue ? _targetDistance
                               : _focusDistanceOverride > 0f ? _focusDistanceOverride : focusDistance;
            _currentDistance = zoomSmoothTime > 0f
                ? Mathf.SmoothDamp(_currentDistance, wantDistance, ref _zoomVelocity, zoomSmoothTime)
                : wantDistance;

            Vector3 desired = PositionFor(_focus ?? target.position + lookOffset);

            if (_returning && (transform.position - desired).sqrMagnitude <= arriveDistance * arriveDistance)
                _returning = false;
            float smooth = _focus.HasValue || _returning ? focusSmoothTime : smoothTime;

            transform.rotation = ViewRotation;
            transform.position = _snapped
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smooth)
                : desired;   // 첫 프레임은 원점에서 날아오지 않게 곧바로 붙인다
            _snapped = true;
        }

        /// <summary>내려다보는 각도(X) · 비틀기(Z)는 인스펙터 값 그대로, Y만 우클릭으로 돌린 값.</summary>
        private Quaternion ViewRotation => Quaternion.Euler(eulerAngles.x, _yaw, eulerAngles.z);

        private Vector3 PositionFor(Vector3 lookAt)
            => lookAt - ViewRotation * Vector3.forward * _currentDistance;
    }
}
