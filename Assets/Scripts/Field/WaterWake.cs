using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 물살 — 움직이면 뱃머리 양옆에서 바깥으로 퍼지는 파도(bowWave) · 고물 뒤 거품 자국(wake · trail) · 뱃머리 물보라(bowSpray),
    /// 멈춰 있으면 뱃머리 앞 물결 고리(ripples). (+10/12)
    /// (+10/12) 배 안(선체 위)에는 아무것도 안 나게: 뱃머리 파도는 선체 반폭(halfBeam) 바깥에서 시작해 선체보다 빨리 벌어지고,
    /// 띠 · 거품은 고물(sternOffset) 뒤에서 시작한다. 띠는 생긴 자리에 남아서 뱃머리에서 그리면 배가 그 위를 지나간다.
    /// 손님 배 · 탐사정 · 떠다니는 물건 · 고래가 같이 쓴다. PF_WaterWake를 그 물체 자식으로 넣는다.
    ///
    /// 파티클은 월드 공간이라 매 프레임 수면 높이(waterLevel)에 깔고 진행 방향으로 돌린다 — 물체가 출렁이거나 기울어도 물 위에 붙는다.
    /// 세기는 속도에 비례(fullSpeed에서 최대). 고래처럼 잠수 · 점프가 있으면 Allowed로 끈다(WhaleFX).
    /// 크기는 이 오브젝트의 localScale로 맞춘다(파티클이 Hierarchy 배율).
    /// </summary>
    public class WaterWake : MonoBehaviour
    {
        [SerializeField] private ParticleSystem wake;
        [SerializeField] private ParticleSystem bowSpray;
        [SerializeField] private ParticleSystem ripples;
        [Tooltip("(+10/12) 뱃머리 왼쪽 · 오른쪽 파도. 바깥 · 뒤로 퍼진다(파티클 +z가 바깥).")]
        [SerializeField] private ParticleSystem bowWaveLeft;
        [SerializeField] private ParticleSystem bowWaveRight;
        [Tooltip("(+10/12) 뒤로 이어지는 거품 띠. 물 셰이더의 흰 반사에 파티클만으론 묻혀서 더했다. 폭은 이 오브젝트 배율을 따른다.")]
        [SerializeField] private TrailRenderer trail;

        [Header("자리 — 이 오브젝트 기준(로컬)")]
        [Tooltip("뱃머리. 물보라가 여기서 튄다.")]
        [SerializeField] private Vector3 bowOffset = new(0f, 0f, 1f);
        [Tooltip("몸 가운데. 속도 기준.")]
        [SerializeField] private Vector3 centerOffset = Vector3.zero;
        [Tooltip("(+10/12) 고물(뒤끝) 바로 뒤. 거품 자국 · 띠가 여기서 시작한다.")]
        [SerializeField] private Vector3 sternOffset = new(0f, 0f, -1f);
        [Tooltip("(+10/12) 수면 높이의 선체 반폭(로컬). 뱃머리 파도가 이 바깥에서 시작한다.")]
        [SerializeField, Min(0f)] private float halfBeam = 1f;

        [Header("물")]
        [Tooltip("해수면 높이(월드 y). WhaleSwimmer.seaLevel과 같다.")]
        [SerializeField] private float waterLevel = 6.9f;
        [Tooltip("몸 가운데가 수면에서 이만큼 안에 있어야 물살이 난다.")]
        [SerializeField, Min(0f)] private float surfaceBand = 3f;
        [Tooltip("수면 위로 띄우는 높이. 너무 낮으면 물 셰이더의 물결에 덮인다.")]
        [SerializeField, Min(0f)] private float surfaceLift = 0.2f;

        [Header("속도")]
        [Tooltip("이 속도(m/s)면 자국 · 물보라가 가장 진하다.")]
        [SerializeField, Min(0.1f)] private float fullSpeed = 5f;
        [Tooltip("이보다 느리면 멈춘 것 — 자국 · 물보라 대신 물결 고리.")]
        [SerializeField, Min(0f)] private float movingSpeed = 0.4f;

        /// <summary>끄면 아무것도 안 낸다. 고래가 잠수 · 공중일 때 WhaleFX가 끈다.</summary>
        public bool Allowed { get; set; } = true;

        private float _wakeRate, _wakeDistRate, _bowRate, _waveRate;
        private Vector3 _lastCenter;
        private float _speed;
        private Vector3 _dir = Vector3.forward;

        private void Awake()
        {
            if (wake == null && bowSpray == null && ripples == null)
                Debug.LogError($"{name}: WaterWake의 파티클이 다 비어 있다. 물살이 안 난다.", this);
            if (wake != null) { _wakeRate = wake.emission.rateOverTimeMultiplier; _wakeDistRate = wake.emission.rateOverDistanceMultiplier; }
            if (bowSpray != null) _bowRate = bowSpray.emission.rateOverTimeMultiplier;
            if (bowWaveLeft != null) _waveRate = bowWaveLeft.emission.rateOverTimeMultiplier;
            if (trail != null) { trail.widthMultiplier *= transform.lossyScale.x; trail.emitting = false; trail.Clear(); }
            _lastCenter = transform.TransformPoint(centerOffset);
            SetEmitting(wake, false);
            SetEmitting(bowSpray, false);
            SetEmitting(ripples, false);
            SetEmitting(bowWaveLeft, false);
            SetEmitting(bowWaveRight, false);
        }

        private void LateUpdate()
        {
            Vector3 center = transform.TransformPoint(centerOffset);
            Vector3 step = center - _lastCenter;
            step.y = 0f;
            _lastCenter = center;
            if (Time.deltaTime > 0f) _speed = Mathf.Lerp(_speed, step.magnitude / Time.deltaTime, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (step.sqrMagnitude > 1e-6f) _dir = step.normalized;
            else
            {
                Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (f.sqrMagnitude > 1e-4f) _dir = f.normalized;
            }

            bool near = Allowed && Mathf.Abs(center.y - waterLevel) < surfaceBand;
            bool moving = _speed > movingSpeed;
            float t = Mathf.Clamp01(_speed / fullSpeed);
            Quaternion yaw = Quaternion.LookRotation(_dir, Vector3.up);

            Vector3 stern = transform.TransformPoint(sternOffset);
            Vector3 bow = transform.TransformPoint(bowOffset);
            Vector3 side = Vector3.Cross(Vector3.up, _dir) * (halfBeam * transform.lossyScale.x);   // 오른쪽
            Place(wake, OnWater(stern, surfaceLift), yaw);
            Place(ripples, OnWater(bow, surfaceLift), Quaternion.identity);
            Place(bowSpray, OnWater(bow, surfaceLift + 0.05f), yaw);
            Place(bowWaveLeft, OnWater(bow - side, surfaceLift), yaw * Quaternion.Euler(0f, -110f, 0f));
            Place(bowWaveRight, OnWater(bow + side, surfaceLift), yaw * Quaternion.Euler(0f, 110f, 0f));
            if (trail != null)
            {
                // 띠는 수면에 눕힌다(TransformZ 정렬 — z가 하늘을 보게).
                trail.transform.SetPositionAndRotation(OnWater(stern, surfaceLift * 0.5f), Quaternion.Euler(-90f, 0f, 0f));
                trail.emitting = near && moving;
            }

            Rate(wake, _wakeRate * t, _wakeDistRate);
            Rate(bowSpray, _bowRate * t, 0f);
            Rate(bowWaveLeft, _waveRate * t, 0f);
            Rate(bowWaveRight, _waveRate * t, 0f);
            SetEmitting(bowWaveLeft, near && moving);
            SetEmitting(bowWaveRight, near && moving);
            SetEmitting(wake, near && moving);
            SetEmitting(bowSpray, near && moving);
            SetEmitting(ripples, near && !moving);
        }

        private Vector3 OnWater(Vector3 p, float lift) => new(p.x, waterLevel + lift, p.z);

        private static void Place(ParticleSystem ps, Vector3 pos, Quaternion rot)
        {
            if (ps != null) ps.transform.SetPositionAndRotation(pos, rot);
        }

        private static void Rate(ParticleSystem ps, float perSecond, float perMeter)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTimeMultiplier = perSecond;
            em.rateOverDistanceMultiplier = perMeter;
        }

        private static void SetEmitting(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule em = ps.emission;
            if (em.enabled == on) return;
            em.enabled = on;
            if (on && !ps.isPlaying) ps.Play();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.TransformPoint(bowOffset), 0.3f);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.TransformPoint(centerOffset), 0.3f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(sternOffset), 0.3f);
            Vector3 b = transform.TransformPoint(bowOffset), r = transform.right * (halfBeam * transform.lossyScale.x);
            Gizmos.DrawLine(b - r, b + r);
        }
    }
}
