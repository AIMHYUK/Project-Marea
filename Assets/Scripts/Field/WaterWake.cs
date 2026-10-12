using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 물살 — 선체 둘레 흰 거품 테두리(hullRing) · 움직이면 고물 뒤로 동글동글 말린 거품 자국(sternFoam) · 하늘색 반짝이(sternSparkle) · 뱃머리 물보라,
    /// 멈춰 있으면 뱃머리 앞 물결 고리. (+10/12)
    /// 손님 배 · 탐사정 · 떠다니는 물건 · 고래가 같이 쓴다. PF_WaterWake를 그 물체 자식으로 넣는다.
    ///
    /// 레퍼런스(사용자 boat.png): 수면선을 따라 선체를 감싸는 흰 띠 + 배 뒤로 "ooo"처럼 이어지다 흐려지는 고리 거품.
    /// 테두리는 가운데가 뚫린 고리 텍스처 판을 수면에 깔고 hullSize로 늘린다(안쪽이 투명해 배 안엔 아무것도 안 보인다).
    /// 자국은 고리 거품 파티클을 이동 거리마다 찍는다 — 조금씩 커지며 흐려지고 겹쳐서 말린 모양이 된다.
    /// (+10/12) 예전 V자 선 · 넓은 거품 띠 · 거품 파티클 · 뱃머리 파도는 뺐다.
    ///
    /// 판 · 파티클은 월드 공간이라 매 프레임 수면 높이(waterLevel)에 깐다 — 물체가 출렁이거나 기울어도 물 위에 붙는다.
    /// 고래처럼 잠수 · 점프가 있으면 Allowed로 끈다(WhaleFX). 크기는 이 오브젝트의 localScale을 따른다.
    /// </summary>
    public class WaterWake : MonoBehaviour
    {
        [SerializeField] private ParticleSystem bowSpray;
        [SerializeField] private ParticleSystem ripples;
        [Tooltip("선체 둘레 흰 거품 테두리(고리 텍스처를 입힌 판).")]
        [SerializeField] private Transform hullRing;
        [Tooltip("고물 뒤 고리 거품 자국(이동 거리마다 찍힌다).")]
        [SerializeField] private ParticleSystem sternFoam;
        [Tooltip("(+10/12) 고물 뒤로 떠올랐다 사라지는 하늘색 반짝이 조각 — 장식.")]
        [SerializeField] private ParticleSystem sternSparkle;

        [Header("자리 — 이 오브젝트 기준(로컬)")]
        [Tooltip("뱃머리. 물보라 · 물결 고리 자리. 진행 방향 · 속도도 이 점의 움직임으로 잰다.")]
        [SerializeField] private Vector3 bowOffset = new(0f, 0f, 1f);
        [Tooltip("선체 가운데(수면). 테두리 중심이자 수면에 있는지 판정 기준.")]
        [SerializeField] private Vector3 centerOffset = Vector3.zero;
        [Tooltip("테두리 크기 — x 폭, y 길이(로컬). 선체를 살짝 감쌀 만큼.")]
        [SerializeField] private Vector2 hullSize = new(2f, 4f);
        [Tooltip("고물(뒤끝) 바로 뒤. 고리 거품 자국이 여기서 나온다.")]
        [SerializeField] private Vector3 sternOffset = new(0f, 0f, -1f);
        [Tooltip("멈췄을 때 물결 고리를 뱃머리에서 이만큼 앞에(로컬). 고리가 선체 앞쪽을 덮지 않게 고리 반지름쯤.")]
        [SerializeField, Min(0f)] private float rippleAhead = 2.5f;

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
        [Tooltip("이보다 느리면 멈춘 것 — 자국 대신 물결 고리.")]
        [SerializeField, Min(0f)] private float movingSpeed = 0.4f;

        /// <summary>끄면 아무것도 안 낸다. 고래가 잠수 · 공중일 때 WhaleFX가 끈다.</summary>
        public bool Allowed { get; set; } = true;

        private float _bowRate;
        private Vector3 _lastBow;
        private float _speed;
        private Vector3 _dir = Vector3.forward;

        private void Awake()
        {
            if (hullRing == null && sternFoam == null && ripples == null)
                Debug.LogError($"{name}: WaterWake의 테두리 · 자국 · 물결이 다 비어 있다. 물살이 안 난다.", this);
            if (bowSpray != null) _bowRate = bowSpray.emission.rateOverTimeMultiplier;
            _lastBow = transform.TransformPoint(bowOffset);
            SetEmitting(bowSpray, false);
            SetEmitting(ripples, false);
            SetEmitting(sternFoam, false);
            SetEmitting(sternSparkle, false);
        }

        private void LateUpdate()
        {
            // 방향 · 속도는 뱃머리 점으로 — 배는 곡선을 뱃머리 기준으로 따라가서, 가운데 점은 선회 때 옆으로 쓸린다.
            Vector3 center = transform.TransformPoint(centerOffset);
            Vector3 bowNow = transform.TransformPoint(bowOffset);
            Vector3 step = bowNow - _lastBow;
            step.y = 0f;
            _lastBow = bowNow;
            if (Time.deltaTime > 0f) _speed = Mathf.Lerp(_speed, step.magnitude / Time.deltaTime, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude > 1e-4f) _dir = fwd.normalized;   // 테두리는 배가 바라보는 쪽 — 선체에 맞춘다

            bool on = Allowed && Mathf.Abs(center.y - waterLevel) < surfaceBand;
            bool moving = on && _speed > movingSpeed;
            float t = Mathf.Clamp01(_speed / fullSpeed);
            Quaternion yaw = Quaternion.LookRotation(_dir, Vector3.up);
            Vector3 bow = OnWater(bowNow, surfaceLift);

            if (hullRing != null)
            {
                hullRing.gameObject.SetActive(on);
                float s = transform.lossyScale.x;
                hullRing.SetPositionAndRotation(OnWater(center, surfaceLift * 0.5f), yaw * Quaternion.Euler(90f, 0f, 0f));
                hullRing.localScale = new Vector3(hullSize.x, hullSize.y, 1f) * s / Mathf.Max(0.0001f, hullRing.parent != null ? hullRing.parent.lossyScale.x : 1f);
            }
            Vector3 stern = OnWater(transform.TransformPoint(sternOffset), surfaceLift);
            Place(sternFoam, stern, yaw);
            Place(sternSparkle, stern + Vector3.up * 0.3f, yaw);
            Place(ripples, bow + _dir * (rippleAhead * transform.lossyScale.x), Quaternion.identity);
            Place(bowSpray, bow + Vector3.up * 0.05f, yaw);

            Rate(bowSpray, _bowRate * t);
            SetEmitting(bowSpray, moving);
            SetEmitting(sternFoam, moving);
            SetEmitting(sternSparkle, moving);
            SetEmitting(ripples, on && !moving);
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
            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(centerOffset), transform.rotation, transform.lossyScale);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(hullSize.x, 0.05f, hullSize.y));
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(sternOffset), 0.3f);
        }
    }
}
