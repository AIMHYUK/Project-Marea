using System.Collections.Generic;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 물살 — 움직이면 뱃머리에서 V로 갈라져 선체 옆을 따라 부드럽게 꺾인 뒤 배 뒤로 길게 이어지는 항적 선 둘 · 뱃머리 물보라,
    /// 멈춰 있으면 뱃머리 앞 물결 고리. (+10/12)
    /// 손님 배 · 탐사정 · 떠다니는 물건 · 고래가 같이 쓴다. PF_WaterWake를 그 물체 자식으로 넣는다.
    ///
    /// 선 하나 = 뱃머리 꼭짓점(bow) → 어깨점(shoulder, 선체 가장 넓은 곳 바로 바깥) → 지나간 자리에 남는 점들.
    /// 앞 두 점은 배에 붙어 있고, 남는 점은 어깨점에서 찍혀 천천히 바깥으로 벌어진다(lineSpread) — 거의 일자지만 살짝 퍼진다.
    /// 꺾이는 곳은 LineRenderer 모서리 보간으로 둥글다. 어깨점이 선체 폭보다 바깥이라 선이 배 안에 안 들어간다.
    /// (+10/12) 예전 넓은 거품 띠 · 거품 파티클 · 뱃머리 파도 파티클 · 고정 각도 V · 가운데 일자 자국은 뺐다.
    ///
    /// 선 · 파티클은 월드 공간이라 매 프레임 수면 높이(waterLevel)에 깐다 — 물체가 출렁이거나 기울어도 물 위에 붙는다.
    /// 고래처럼 잠수 · 점프가 있으면 Allowed로 끈다(WhaleFX). 크기는 이 오브젝트의 localScale로 맞춘다.
    /// </summary>
    public class WaterWake : MonoBehaviour
    {
        [SerializeField] private ParticleSystem bowSpray;
        [SerializeField] private ParticleSystem ripples;
        [Tooltip("왼쪽 · 오른쪽 항적 선.")]
        [SerializeField] private LineRenderer bowLineLeft;
        [SerializeField] private LineRenderer bowLineRight;
        [Tooltip("남는 점 하나가 사는 시간(초) — 배 뒤 항적 길이 = 속도 × 이 값.")]
        [SerializeField, Min(0.1f)] private float lineLife = 3f;
        [Tooltip("남는 점이 바깥으로 벌어지는 속도(m/s). 작을수록 일자에 가깝다.")]
        [SerializeField, Min(0f)] private float lineSpread = 0.35f;

        [Header("자리 — 이 오브젝트 기준(로컬)")]
        [Tooltip("V의 꼭짓점 · 물보라 · 물결 고리 자리. 보이는 뱃머리 끝보다 조금 앞. 진행 방향 · 속도도 이 점의 움직임으로 잰다.")]
        [SerializeField] private Vector3 bowOffset = new(0f, 0f, 1f);
        [Tooltip("오른쪽 어깨점(왼쪽은 x를 뒤집는다). 선체 가장 넓은 곳 바로 바깥 — 꼭짓점에서 여기까지가 V, 여기서부터 배 뒤로 남는다.")]
        [SerializeField] private Vector3 shoulderOffset = new(1f, 0f, -1f);
        [Tooltip("멈췄을 때 물결 고리를 뱃머리에서 이만큼 앞에(로컬). 고리가 선체 앞쪽을 덮지 않게 고리 반지름쯤.")]
        [SerializeField, Min(0f)] private float rippleAhead = 2.5f;
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
        [Tooltip("이 속도(m/s)면 물보라가 가장 진하다.")]
        [SerializeField, Min(0.1f)] private float fullSpeed = 5f;
        [Tooltip("이보다 느리면 멈춘 것 — 항적 대신 물결 고리.")]
        [SerializeField, Min(0f)] private float movingSpeed = 0.4f;

        /// <summary>끄면 아무것도 안 낸다. 고래가 잠수 · 공중일 때 WhaleFX가 끈다.</summary>
        public bool Allowed { get; set; } = true;

        // 배 뒤로 남는 점들 — 위치(월드) · 벌어지는 방향 · 태어난 시각.
        private class Wake
        {
            public readonly List<Vector3> Points = new();
            public readonly List<Vector3> Out = new();
            public readonly List<float> Born = new();
        }

        private readonly Wake _left = new(), _right = new();
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
            foreach (LineRenderer l in new[] { bowLineLeft, bowLineRight })
                if (l != null) { l.useWorldSpace = true; l.widthMultiplier *= transform.lossyScale.x; l.positionCount = 0; }
        }

        private void LateUpdate()
        {
            // 방향 · 속도는 뱃머리 점으로 — 배는 곡선을 뱃머리 기준으로 따라가서, 가운데 점은 선회 때 옆으로 쓸려
            // 진행 방향이 20° 가까이 어긋났다.
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
            bool moving = on && _speed > movingSpeed;
            float t = Mathf.Clamp01(_speed / fullSpeed);
            Quaternion yaw = Quaternion.LookRotation(_dir, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, _dir);
            Vector3 bow = OnWater(bowNow, surfaceLift);

            Place(ripples, bow + _dir * (rippleAhead * transform.lossyScale.x), Quaternion.identity);
            Place(bowSpray, bow + Vector3.up * 0.05f, yaw);

            Vector3 sh = shoulderOffset;
            Vector3 shoulderR = OnWater(transform.TransformPoint(sh), surfaceLift);
            Vector3 shoulderL = OnWater(transform.TransformPoint(new Vector3(-sh.x, sh.y, sh.z)), surfaceLift);
            UpdateWake(bowLineLeft, _left, bow, shoulderL, -right, moving);
            UpdateWake(bowLineRight, _right, bow, shoulderR, right, moving);

            Rate(bowSpray, _bowRate * t);
            SetEmitting(bowSpray, moving);
            SetEmitting(ripples, on && !moving);
        }

        // 움직이는 동안: [꼭짓점, 어깨점, 남은 점들(새 것 → 옛 것)]. 멈추면 남은 점만 사라질 때까지.
        private void UpdateWake(LineRenderer line, Wake w, Vector3 apex, Vector3 shoulder, Vector3 outward, bool moving)
        {
            if (line == null) return;
            float now = Time.time, dt = Time.deltaTime;
            for (int i = w.Points.Count - 1; i >= 0; i--)
            {
                if (now - w.Born[i] > lineLife) { w.Points.RemoveAt(i); w.Out.RemoveAt(i); w.Born.RemoveAt(i); continue; }
                w.Points[i] += w.Out[i] * (lineSpread * dt);
            }
            float spacing = 0.4f * transform.lossyScale.x;
            if (moving && (w.Points.Count == 0 || (w.Points[^1] - shoulder).sqrMagnitude > spacing * spacing))
            {
                w.Points.Add(shoulder);
                w.Out.Add(outward);
                w.Born.Add(now);
            }

            int n = w.Points.Count, head = moving ? 2 : 0;
            line.positionCount = n + head;
            if (moving) { line.SetPosition(0, apex); line.SetPosition(1, shoulder); }
            for (int i = 0; i < n; i++) line.SetPosition(head + i, w.Points[n - 1 - i]);
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
            Vector3 b = transform.TransformPoint(bowOffset);
            Vector3 r = transform.TransformPoint(shoulderOffset);
            Vector3 l = transform.TransformPoint(new Vector3(-shoulderOffset.x, shoulderOffset.y, shoulderOffset.z));
            Gizmos.DrawLine(b, r);
            Gizmos.DrawLine(b, l);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.TransformPoint(centerOffset), 0.3f);
        }
    }
}
