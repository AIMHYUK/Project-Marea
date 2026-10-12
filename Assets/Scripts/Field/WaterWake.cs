using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 물살 — 움직이면 뱃머리 한 점에서 뒤로 벌어지는 V자 항적(bowLine) · 뱃머리 물보라(bowSpray),
    /// 멈춰 있으면 뱃머리 앞 물결 고리(ripples). (+10/12)
    /// 손님 배 · 탐사정 · 떠다니는 물건 · 고래가 같이 쓴다. PF_WaterWake를 그 물체 자식으로 넣는다.
    ///
    /// V는 뱃머리 꼭짓점에 붙은 선 두 개를 진행 방향 뒤로 좌우 lineAngle만큼 벌려 그린다 — 배를 따라 움직여 선회해도 늘 V다.
    /// (점을 찍어 바깥으로 밀던 방식은 선회 · 감속 때 선이 휘고 꺾였다.) 각도를 선체보다 넓게 잡아 배 안에 안 들어간다.
    /// 길이는 속도에 따라 늘고 줄며, 끝으로 갈수록 넓어지고 흐려진다(LineRenderer 폭 · 색 곡선).
    /// (+10/12) 배 뒤로는 고물 바로 뒤에서 시작하는 일자 자국(trail)이 지나간 자리에 남는다 — 물살을 헤치고 나간 흔적.
    /// 예전 넓은 거품 띠 · 거품 파티클 · 뱃머리 파도 파티클은 뺐다(파도 파티클은 감속 때 선체 안에 남았다).
    ///
    /// 파티클 · 선은 월드 공간이라 매 프레임 수면 높이(waterLevel)에 깐다 — 물체가 출렁이거나 기울어도 물 위에 붙는다.
    /// 고래처럼 잠수 · 점프가 있으면 Allowed로 끈다(WhaleFX). 크기는 이 오브젝트의 localScale로 맞춘다.
    /// </summary>
    public class WaterWake : MonoBehaviour
    {
        [SerializeField] private ParticleSystem bowSpray;
        [SerializeField] private ParticleSystem ripples;
        [Tooltip("뱃머리에서 뒤로 벌어지는 V자 항적 선(왼쪽 · 오른쪽).")]
        [SerializeField] private LineRenderer bowLineLeft;
        [SerializeField] private LineRenderer bowLineRight;
        [Tooltip("V 한쪽 선과 진행 반대 방향 사이 각도(도). 선체가 뱃머리에서 넓어지는 각도보다 커야 배 안에 안 들어간다.")]
        [SerializeField, Range(5f, 80f)] private float lineAngle = 40f;
        [Tooltip("fullSpeed일 때 V 선 길이(로컬). 느리면 짧아진다.")]
        [SerializeField, Min(0.1f)] private float lineLength = 12f;

        [Tooltip("배 뒤 일자 자국. 고물(sternOffset)에서 시작해 지나간 자리에 남는다. 폭은 이 오브젝트 배율을 따른다.")]
        [SerializeField] private TrailRenderer trail;

        [Header("자리 — 이 오브젝트 기준(로컬)")]
        [Tooltip("V의 꼭짓점 · 물보라 · 물결 고리 자리. 보이는 뱃머리 끝보다 조금 앞. 진행 방향 · 속도도 이 점의 움직임으로 잰다.")]
        [SerializeField] private Vector3 bowOffset = new(0f, 0f, 1f);
        [Tooltip("멈췄을 때 물결 고리를 뱃머리에서 이만큼 앞에(로컬). 고리가 선체 앞쪽을 덮지 않게 고리 반지름쯤.")]
        [SerializeField, Min(0f)] private float rippleAhead = 2.5f;
        [Tooltip("고물(뒤끝) 바로 뒤. 일자 자국이 여기서 시작한다 — 배 안에 안 들어가게 선체 끝보다 조금 뒤.")]
        [SerializeField] private Vector3 sternOffset = new(0f, 0f, -1f);
        [Tooltip("몸 가운데. 수면에 있는지 판정 기준.")]
        [SerializeField] private Vector3 centerOffset = Vector3.zero;

        [Header("물")]
        [Tooltip("해수면 높이(월드 y). WhaleSwimmer.seaLevel과 같다.")]
        [SerializeField] private float waterLevel = 6.9f;
        [Tooltip("몸 가운데가 수면에서 이만큼 안에 있어야 물살이 난다.")]
        [SerializeField, Min(0f)] private float surfaceBand = 3f;
        [Tooltip("수면 위로 띄우는 높이. 너무 낮으면 물 셰이더의 물결에 덮인다.")]
        [SerializeField, Min(0f)] private float surfaceLift = 0.2f;

        [Header("속도")]
        [Tooltip("이 속도(m/s)면 거품 · 물보라가 가장 진하다.")]
        [SerializeField, Min(0.1f)] private float fullSpeed = 5f;
        [Tooltip("이보다 느리면 멈춘 것 — 항적 대신 물결 고리.")]
        [SerializeField, Min(0f)] private float movingSpeed = 0.4f;

        /// <summary>끄면 아무것도 안 낸다. 고래가 잠수 · 공중일 때 WhaleFX가 끈다.</summary>
        public bool Allowed { get; set; } = true;

        private const int LinePoints = 12;
        private float _lineAmount;   // 0~1, 선 길이 비율 — 속도를 부드럽게 따라간다
        private float _bowRate;
        private Vector3 _lastBow;
        private float _speed;
        private Vector3 _dir = Vector3.forward;

        private void Awake()
        {
            if (bowLineLeft == null && ripples == null)
                Debug.LogError($"{name}: WaterWake의 선 · 파티클이 다 비어 있다. 물살이 안 난다.", this);
            if (bowSpray != null) _bowRate = bowSpray.emission.rateOverTimeMultiplier;
            _lastBow = transform.TransformPoint(bowOffset);
            SetEmitting(bowSpray, false);
            SetEmitting(ripples, false);
            if (trail != null) { trail.widthMultiplier *= transform.lossyScale.x; trail.emitting = false; trail.Clear(); }
            foreach (LineRenderer l in new[] { bowLineLeft, bowLineRight })
                if (l != null) { l.useWorldSpace = true; l.widthMultiplier *= transform.lossyScale.x; l.positionCount = 0; }
        }

        private void LateUpdate()
        {
            // (+10/12) 방향 · 속도는 뱃머리 점으로 — 배는 곡선을 뱃머리 기준으로 따라가서, 가운데 점은 선회 때 옆으로 쓸려
            // 진행 방향이 20° 가까이 어긋났다(V 한쪽이 선체 밑으로 들어갔다).
            Vector3 center = transform.TransformPoint(centerOffset);
            Vector3 bowNow = transform.TransformPoint(bowOffset);
            Vector3 step = bowNow - _lastBow;
            step.y = 0f;
            _lastBow = bowNow;
            if (Time.deltaTime > 0f) _speed = Mathf.Lerp(_speed, step.magnitude / Time.deltaTime, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (step.sqrMagnitude > 1e-6f) _dir = step.normalized;
            else
            {
                Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (f.sqrMagnitude > 1e-4f) _dir = f.normalized;
            }

            bool on = Allowed && Mathf.Abs(center.y - waterLevel) < surfaceBand;
            bool moving = _speed > movingSpeed;
            float t = Mathf.Clamp01(_speed / fullSpeed);
            Quaternion yaw = Quaternion.LookRotation(_dir, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, _dir);
            Vector3 bow = OnWater(bowNow, surfaceLift);

            Place(ripples, bow + _dir * (rippleAhead * transform.lossyScale.x), Quaternion.identity);
            Place(bowSpray, bow + Vector3.up * 0.05f, yaw);

            _lineAmount = Mathf.MoveTowards(_lineAmount, on && moving ? Mathf.Max(0.35f, t) : 0f, Time.deltaTime * 1.5f);
            Vector3 apex = bow;
            float len = lineLength * transform.lossyScale.x * _lineAmount;
            float a = lineAngle * Mathf.Deg2Rad;
            DrawLine(bowLineLeft, apex, (-_dir * Mathf.Cos(a) - right * Mathf.Sin(a)) * len);
            DrawLine(bowLineRight, apex, (-_dir * Mathf.Cos(a) + right * Mathf.Sin(a)) * len);

            if (trail != null)
            {
                trail.transform.SetPositionAndRotation(OnWater(transform.TransformPoint(sternOffset), surfaceLift), Quaternion.Euler(-90f, 0f, 0f));
                trail.emitting = on && moving;
            }

            Rate(bowSpray, _bowRate * t);
            SetEmitting(bowSpray, on && moving);
            SetEmitting(ripples, on && !moving);
        }

        // 꼭짓점에서 span만큼 곧게 — 길이가 0이면 숨긴다.
        private static void DrawLine(LineRenderer line, Vector3 apex, Vector3 span)
        {
            if (line == null) return;
            if (span.sqrMagnitude < 0.01f) { line.positionCount = 0; return; }
            line.positionCount = LinePoints;
            for (int i = 0; i < LinePoints; i++) line.SetPosition(i, apex + span * (i / (LinePoints - 1f)));
            line.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // TransformZ 정렬 — 수면에 눕힌다
        }

        private Vector3 OnWater(Vector3 p, float lift) => new(p.x, waterLevel + lift, p.z);

        private static void Place(ParticleSystem ps, Vector3 pos, Quaternion rot)
        {
            if (ps != null) ps.transform.SetPositionAndRotation(pos, rot);
        }

        private static void Rate(ParticleSystem ps, float perSecond)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTimeMultiplier = perSecond;
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
        }
    }
}
