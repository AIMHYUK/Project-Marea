using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 물에 뜬 것처럼 보이게 하는 가짜 부력. (+10/7)
    ///
    /// 실제 파도를 따라가지 않는다 — 우리 바다(UberStylizedWater)는 파도가 꺼져 있어 수면이 평평하다.
    /// 그래서 사인파 셋으로 출렁임(위아래) · 앞뒤 기울임 · 좌우 기울임을 섞고,
    /// 부모가 움직일 때 가속도를 읽어 감속하면 앞이 숙고 가속하면 앞이 든다.
    ///
    /// 모델(자식)에 붙인다. 위치 · 방향은 부모가 정하고 이건 쉬는 자세 위에 흔들림만 얹는다 —
    /// 부모를 직접 흔들면 GuestBoatArrival처럼 부모를 옮기는 코드와 서로 덮어쓴다.
    /// </summary>
    public class FloatBob : MonoBehaviour
    {
        [Header("출렁임")]
        [SerializeField, Min(0f)] private float heaveHeight = 0.08f;
        [SerializeField, Min(0f)] private float heaveSpeed = 1.1f;

        [Header("기울임 (도)")]
        [SerializeField, Min(0f)] private float pitchDegrees = 1.5f;
        [SerializeField, Min(0f)] private float pitchSpeed = 0.8f;
        [SerializeField, Min(0f)] private float rollDegrees = 2.5f;
        [SerializeField, Min(0f)] private float rollSpeed = 0.65f;

        [Header("움직일 때 앞뒤로 숙임")]
        [Tooltip("진행 방향 가속도 1 m/s²당 기울기(도). 감속하면 앞이 숙고 가속하면 앞이 든다.")]
        [SerializeField, Min(0f)] private float leanPerAccel = 3f;
        [SerializeField, Min(0f)] private float maxLean = 6f;
        [Tooltip("숙임이 따라오는 빠르기. 작을수록 천천히 숙고 천천히 돌아온다.")]
        [SerializeField, Min(0.1f)] private float leanResponse = 3f;
        [Tooltip("가속도를 재기 전에 빠르기를 거르는 정도. 작을수록 매끈하다 — 프레임 간격 흔들림이 숙임으로 튀지 않게. (+10/9)")]
        [SerializeField, Min(0.1f)] private float speedSmoothing = 4f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private float _phase;

        private Vector3 _lastParentPos;
        private float _speed;          // 걸러낸 수평 빠르기
        private Vector3 _moveDir = Vector3.forward;
        private float _lean;
        private bool _hasLast;
        private bool _hasSpeed;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
            _phase = Random.Range(0f, 100f);   // 여러 개가 같이 흔들리지 않게
        }

        // 껐다 켜면 순간이동한 걸 가속으로 읽지 않는다. 켜질 때 이미 달리고 있으면(입항) 그 빠르기에서 시작한다.
        private void OnEnable() => _hasLast = _hasSpeed = false;

        private void LateUpdate()
        {
            Transform parent = transform.parent;
            UpdateLean(parent);

            float t = Time.time + _phase;
            float heave = Mathf.Sin(t * heaveSpeed) * heaveHeight;
            float pitch = Mathf.Sin(t * pitchSpeed) * pitchDegrees;
            float roll = Mathf.Sin(t * rollSpeed + 1.3f) * rollDegrees;

            if (parent == null)
            {
                transform.localPosition = _restPosition + Vector3.up * heave;
                transform.localRotation = _restRotation * Quaternion.Euler(pitch, 0f, roll);
                return;
            }

            // 부모 크기가 축마다 달라도(GuestBoat는 7 · 8 · 9) 월드 위쪽으로 heave만큼 오르게 부모 공간으로 바꾼다.
            transform.localPosition = _restPosition + parent.InverseTransformVector(Vector3.up * heave);

            Quaternion rest = parent.rotation * _restRotation;
            Quaternion lean = Quaternion.AngleAxis(_lean, Vector3.Cross(Vector3.up, _moveDir));
            transform.rotation = lean * rest * Quaternion.Euler(pitch, 0f, roll);
        }

        private void UpdateLean(Transform parent)
        {
            float dt = Time.deltaTime;
            Vector3 pos = parent != null ? parent.position : transform.position;
            if (!_hasLast || dt <= 0f)
            {
                _lastParentPos = pos;
                _hasLast = true;
                return;
            }

            Vector3 velocity = (pos - _lastParentPos) / dt;
            velocity.y = 0f;
            _lastParentPos = pos;
            if (velocity.sqrMagnitude > 0.01f) _moveDir = velocity.normalized;

            // (+10/9) 빠르기(스칼라)만 걸러서 미분한다. 예전엔 속도 벡터를 프레임마다 그대로 미분해서
            // 프레임 간격 흔들림 · 방향 꺾임이 가속도로 읽혀 숙임이 들쭉날쭉 튀었다.
            float rawSpeed = velocity.magnitude;
            if (!_hasSpeed)
            {
                _speed = rawSpeed;
                _hasSpeed = true;
                return;
            }
            float prevSpeed = _speed;
            _speed = Mathf.Lerp(_speed, rawSpeed, 1f - Mathf.Exp(-speedSmoothing * dt));
            float accel = (_speed - prevSpeed) / dt;

            // 감속(음수)이면 양의 각 = 앞이 숙는다.
            float target = Mathf.Clamp(-accel * leanPerAccel, -maxLean, maxLean);
            _lean = Mathf.Lerp(_lean, target, 1f - Mathf.Exp(-leanResponse * dt));
        }
    }
}
