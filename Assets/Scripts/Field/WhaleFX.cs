using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 고래 이펙트 — 물살 자국 · 등 물보라 · 숨 뿜기 · 점프 도약/공중/입수 물보라. (+10/10)
    /// WhaleSwimmer와 같은 오브젝트에 붙인다. 파티클은 PF_WhaleFX 안에 있고, 이 스크립트는 언제 · 어디서 틀지만 정한다.
    ///
    /// 물살 · 물보라는 몸(body 뼈)이 해수면 근처일 때만 나온다 — 떠오르기 전 · 잠수 뒤 · 점프로 공중에 있을 땐 안 나온다.
    /// 점프 이펙트는 점프 클립 시간에 맞춘다(도약 1.7초, 입수 높은 점프 4.4초 · 낮은 점프 3.61초 — 027 기록의 궤적 수치).
    /// </summary>
    [RequireComponent(typeof(WhaleSwimmer))]
    public class WhaleFX : MonoBehaviour
    {
        [Header("고래")]
        [SerializeField] private Animator animator;
        [Tooltip("숨구멍 기준 뼈(Head).")]
        [SerializeField] private Transform head;
        [Tooltip("head 뼈 기준 숨구멍 위치.")]
        [SerializeField] private Vector3 blowholeOffset;
        [Tooltip("수면 판정 · 물살 위치 기준 뼈(몸 가운데).")]
        [SerializeField] private Transform body;

        [Header("파티클")]
        [SerializeField] private ParticleSystem wake;
        [SerializeField] private ParticleSystem bowSpray;
        [SerializeField] private ParticleSystem spout;
        [SerializeField] private ParticleSystem launchSplash;
        [SerializeField] private ParticleSystem airDrops;
        [SerializeField] private ParticleSystem landingSplash;

        [Header("타이밍")]
        [Tooltip("몸 가운데 뼈가 해수면 아래 이 깊이 ~ 위 surfaceAbove 사이면 '수면에 있다'.")]
        [SerializeField] private float surfaceBelow = 5f;
        [SerializeField] private float surfaceAbove = 1.5f;
        [Tooltip("숨 뿜기 간격(초). 처음은 수면에 닿고 1초 뒤.")]
        [SerializeField] private Vector2 spoutInterval = new Vector2(8f, 15f);
        [SerializeField] private float launchTime = 1.7f;
        [SerializeField] private float landTimeHigh = 4.4f;
        [SerializeField] private float landTimeLow = 3.61f;

        private WhaleSwimmer _whale;
        private float _spoutTimer = -1f;
        private float _lastJumpTime = -1f;
        private int _lastJumpState;

        private static readonly int JumpHash = Animator.StringToHash("Whale_Jump");
        private static readonly int JumpLowHash = Animator.StringToHash("Whale_Jump_Low");

        private void Awake()
        {
            _whale = GetComponent<WhaleSwimmer>();
            if (animator == null || head == null || body == null)
                Debug.LogError($"{name}: WhaleFX의 animator · head · body가 비어 있다. 이펙트가 안 나온다.", this);
            SetEmitting(wake, false);
            SetEmitting(bowSpray, false);
            SetEmitting(airDrops, false);
        }

        private void LateUpdate()
        {
            if (animator == null || head == null || body == null) return;
            if (!_whale.IsSwimming || !animator.isActiveAndEnabled)
            {
                SetEmitting(wake, false);
                SetEmitting(bowSpray, false);
                SetEmitting(airDrops, false);
                _spoutTimer = -1f;
                _lastJumpTime = -1f;
                return;
            }

            float sea = _whale.SeaLevel;
            Vector3 b = body.position;
            bool atSurface = b.y > sea - surfaceBelow && b.y < sea + surfaceAbove;

            // 물살 자국 · 등 물보라: 수면 위에 깔고, 고래 진행 방향을 따른다.
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Quaternion yaw = fwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;
            Place(wake, new Vector3(b.x, sea + 0.05f, b.z), yaw);
            Vector3 h = head.position;
            Place(bowSpray, new Vector3(h.x, sea + 0.1f, h.z), yaw);
            SetEmitting(wake, atSurface);
            SetEmitting(bowSpray, atSurface);

            UpdateJump(sea, b);
            UpdateSpout(atSurface);
        }

        private void UpdateJump(float sea, Vector3 b)
        {
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            bool high = st.shortNameHash == JumpHash, low = st.shortNameHash == JumpLowHash;
            if (!high && !low)
            {
                SetEmitting(airDrops, false);
                _lastJumpTime = -1f;
                return;
            }

            if (st.shortNameHash != _lastJumpState) _lastJumpTime = -1f;   // 새 점프
            _lastJumpState = st.shortNameHash;
            float t = st.normalizedTime * st.length;
            float land = high ? landTimeHigh : landTimeLow;

            if (_lastJumpTime < launchTime && t >= launchTime) Burst(launchSplash, new Vector3(b.x, sea, b.z));
            if (_lastJumpTime < land && t >= land) Burst(landingSplash, new Vector3(b.x, sea, b.z));
            _lastJumpTime = t;

            bool airborne = t > launchTime + 0.1f && t < land - 0.05f;
            Place(airDrops, b, Quaternion.identity);
            SetEmitting(airDrops, airborne);
        }

        private void UpdateSpout(bool atSurface)
        {
            if (!atSurface || spout == null) return;
            if (_spoutTimer < 0f) _spoutTimer = 1f;          // 수면에 처음 닿음
            _spoutTimer -= Time.deltaTime;
            if (_spoutTimer > 0f) return;
            _spoutTimer = Random.Range(spoutInterval.x, Mathf.Max(spoutInterval.x, spoutInterval.y));
            // 헤엄 깊이에선 숨구멍이 수면 살짝 아래다 — 물기둥은 수면 위에서 솟게.
            Vector3 p = head.TransformPoint(blowholeOffset);
            p.y = Mathf.Max(p.y, _whale.SeaLevel + 0.2f);
            Burst(spout, p);
        }

        private static void Place(ParticleSystem ps, Vector3 pos, Quaternion rot)
        {
            if (ps != null) ps.transform.SetPositionAndRotation(pos, rot);
        }

        private static void Burst(ParticleSystem ps, Vector3 pos)
        {
            if (ps == null) return;
            // 원뿔(+Z)이 하늘을 보게 — 부모(고래)가 기울어 있어도 물기둥은 똑바로 솟는다.
            ps.transform.SetPositionAndRotation(pos, Quaternion.Euler(-90f, 0f, 0f));
            ps.Play(true);
        }

        private static void SetEmitting(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule em = ps.emission;
            if (em.enabled == on) return;
            em.enabled = on;
            if (on && !ps.isPlaying) ps.Play();
        }
    }
}
