using System;
using System.Collections;
using Marea.Core;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 가끔 섬 둘레 바다에 나타나는 거대 고래. (+10/2)
    ///
    /// 모델은 뼈만 있고 애니메이션 클립이 없다. 그래서 헤엄은 코드로 만든다:
    /// 몸통 뼈(머리→꼬리)를 시간차를 두고 위아래로 굽혀 꼬리를 치고, 지느러미를 살짝 젓는다.
    ///
    /// 한 번 나타나는 흐름: 기다림 → 카메라 가까운 경로 고르기 → 깊은 곳에서 떠오름 → 수면을 따라 헤엄
    /// → 고개 숙여 잠수 → 숨김. 카메라 근처 경로가 없으면 그번은 건너뛴다(아무도 못 보는 등장은 의미가 없다).
    ///
    /// (+10/8) lap이 켜져 있으면 직선 경로 대신 데크를 감싸는 곡선(lapPoints)으로 한 바퀴 돌고 잠수해 떠난다.
    ///
    /// (+10/10) 새 고래(FT_Whale_Rigged)는 animator를 연결하면 헤엄 · 점프를 클립(Whale_Swim · Whale_Jump · Whale_Jump_Low)으로 한다.
    /// 이때 뼈 굽히기 · 출렁임 코드는 쉬고, 이 스크립트는 경로 이동 · 떠오름 · 잠수만 맡는다. 한 바퀴에 jumpChance 확률로 한 번 뛴다.
    /// </summary>
    public class WhaleSwimmer : MonoBehaviour
    {
        [Serializable]
        private struct SwimPath
        {
            public Transform from;
            public Transform to;
        }

        [Header("모델")]
        [Tooltip("고래 모델 루트. 숨을 땐 이걸 끈다.")]
        [SerializeField] private Transform model;
        [Tooltip("몸통 뼈, 머리 → 꼬리 순서.")]
        [SerializeField] private Transform[] spine;
        [Tooltip("지느러미 뿌리 뼈들. 살짝 젓는다.")]
        [SerializeField] private Transform[] fins;
        [Tooltip("(+10/10) 새 고래(FT_Whale_Rigged)의 Animator. 있으면 헤엄 · 점프를 클립으로 하고 spine · fins는 안 쓴다(비워 둔다).")]
        [SerializeField] private Animator animator;

        // (+10/10) 점프 클립은 앞뒤 이동 없이 높이 · 기울기만 있다 — 앞으로는 이 스크립트가 경로를 따라 옮긴다.
        // 점프 첫 · 끝 프레임이 헤엄 첫 프레임과 같아서, 헤엄이 한 바퀴 도는 순간에 틀면 이음새가 안 보인다.
        [Header("점프 (+10/10)")]
        [Tooltip("한 번 나타날 때 점프할 확률.")]
        [SerializeField, Range(0f, 1f)] private float jumpChance = 0.3f;
        [Tooltip("점프할 때 낮은 점프를 고를 확률.")]
        [SerializeField, Range(0f, 1f)] private float lowJumpChance = 0.5f;
        [Tooltip("한 바퀴 경로 중 점프를 시작할 수 있는 구간(0 시작 ~ 1 끝). 잠수 전에 끝나게 앞쪽으로.")]
        [SerializeField] private Vector2 jumpWindow = new Vector2(0.2f, 0.5f);
        [Tooltip("점프하는 동안 앞으로 가는 속도 배율 — 뛰어오르는 힘이 보이게.")]
        [SerializeField, Min(1f)] private float jumpSpeedBoost = 1.5f;

        [Header("경로")]
        [SerializeField] private SwimPath[] paths;
        [Tooltip("경로 위 점이 하나라도 지금 화면 안 · 카메라에서 이 거리 안이어야 고른다.")]
        [SerializeField, Min(5f)] private float cameraRange = 70f;
        [Tooltip("해수면 높이(월드 y).")]
        [SerializeField] private float seaLevel = 6.9f;

        [Header("등장")]
        [SerializeField] private Vector2 intervalRange = new Vector2(60f, 150f);
        [Tooltip("처음 등장까지 기다리는 시간.")]
        [SerializeField, Min(0f)] private float firstDelay = 20f;
        [SerializeField, Min(0.1f)] private float swimSpeed = 2.5f;
        [Tooltip("헤엄칠 때 모델 기준점이 해수면 아래 이만큼. 등 · 나무가 물 위로 나온다.")]
        [SerializeField] private float surfaceDepth = 2.2f;
        [SerializeField] private float hiddenDepth = 14f;
        [SerializeField, Min(0.1f)] private float riseTime = 5f;
        [SerializeField, Min(0.1f)] private float diveTime = 6f;
        [SerializeField, Range(0f, 45f)] private float divePitch = 25f;

        [Header("헤엄 동작")]
        [Tooltip("꼬리 끝에서의 굽힘 각도. 머리 쪽으로 갈수록 작아진다.")]
        [SerializeField, Range(0f, 45f)] private float tailAmplitude = 30f;
        [SerializeField, Min(0f)] private float strokeFrequency = 0.5f;
        [Tooltip("머리→꼬리로 물결이 넘어가는 시간차(한 마디당 라디안).")]
        [SerializeField] private float phaseStep = 0.6f;
        [Tooltip("꼬리를 칠 때 몸 전체가 반대로 끄덕이는 각도.")]
        [SerializeField, Range(0f, 10f)] private float bodyRock = 3f;
        [SerializeField, Range(0f, 40f)] private float finAmplitude = 20f;
        [SerializeField, Range(0f, 2f)] private float bobHeight = 0.35f;

        // (+10/8) 데크를 감싸듯 한 바퀴 — 남쪽에서 떠올라 서쪽을 따라 올라가 북쪽을 돌고 잠수해 떠난다.
        // 동쪽은 바로 땅이라 섬 전체를 도는 원은 못 만든다. 끄면 예전 paths(직선)를 쓴다.
        [Header("데크 한 바퀴 (+10/8)")]
        [SerializeField] private bool lap = true;
        [Tooltip("한 바퀴 경로의 점들(월드 x, z — y는 무시). 깊은 바다로만. 곡선으로 부드럽게 잇는다.")]
        [SerializeField] private Vector3[] lapPoints =
        {
            new(62f, 0f, -2f), new(30f, 0f, 4f), new(14f, 0f, 30f), new(16f, 0f, 60f),
            new(30f, 0f, 80f), new(55f, 0f, 82f), new(80f, 0f, 79f), new(102f, 0f, 76f),
        };
        [Tooltip("한 바퀴 돌 때 속도(m/s). 길이가 200m쯤이라 swimSpeed보다 빠르게.")]
        [SerializeField, Min(0.1f)] private float lapSpeed = 4f;
        [Tooltip("도는 쪽으로 기우는 최대 각도.")]
        [SerializeField, Range(0f, 20f)] private float bankAngle = 8f;
        [Tooltip("굽힘을 몸 가운데(허리)에 싣는 비율. 0이면 꼬리 끝, 1이면 허리.")]
        [SerializeField, Range(0f, 1f)] private float waistWeight = 0.65f;

        [Header("소리 (+10/6, 이슈 117)")]
        [Tooltip("수면까지 다 떠오른 순간 한 번 우는 소리. 고래가 멀리(최대 cameraRange) 나와서 화면 소리(2D)로 낸다.")]
        [SerializeField] private AudioClip callClip;
        [SerializeField, Range(0f, 1f)] private float callVolume = 1f;

        private const string SwimState = "Whale_Swim";
        private const string JumpState = "Whale_Jump";
        private const string JumpLowState = "Whale_Jump_Low";

        private Quaternion[] _spineBase;
        private Quaternion[] _finBase;
        private float _swimTime;
        private bool _swimming;
        private Renderer _renderer;
        private string _queuedJump;      // (+10/10) 헤엄 클립이 한 바퀴 돌면 틀 점프 상태 이름
        private float _lastSwimPhase;

        // (+10/10) 클립으로 헤엄치면 코드가 뼈 · 출렁임을 만들지 않는다(클립에 들어 있다).
        private bool Procedural => animator == null;

        private void Awake()
        {
            if (model == null) Debug.LogError($"{name}: WhaleSwimmer.model이 비어 있다.", this);
            if (Procedural && (spine == null || spine.Length == 0)) Debug.LogError($"{name}: WhaleSwimmer.spine이 비어 있다. 꼬리를 못 친다.", this);
            if (model != null) _renderer = model.GetComponentInChildren<Renderer>(true);
            if (paths == null || paths.Length == 0) Debug.LogError($"{name}: WhaleSwimmer.paths가 비어 있다. 나타날 곳이 없다.", this);

            _spineBase = CaptureBase(spine);
            _finBase = CaptureBase(fins);
            if (model != null) model.gameObject.SetActive(false);
        }

        private void OnEnable() => StartCoroutine(Loop());

        /// <summary>
        /// (+10/8) 데크 한 바퀴: 경로 첫 점 아래 깊은 곳에서 떠올라 곡선을 따라 헤엄치고, 끝에서 잠수해 사라진다.
        /// 방향은 그때그때 뒤집는다. 진행 방향을 보고, 꺾이는 만큼 안쪽으로 기운다.
        /// </summary>
        private IEnumerator Lap()
        {
            if (model == null || lapPoints == null || lapPoints.Length < 2) yield break;

            Vector3[] pts = (Vector3[])lapPoints.Clone();
            if (UnityEngine.Random.value < 0.5f) Array.Reverse(pts);
            var line = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < pts.Length - 1; i++)
                for (int k = 0; k < 16; k++)
                    line.Add(CatmullRom(pts[Mathf.Max(0, i - 1)], pts[i], pts[i + 1], pts[Mathf.Min(pts.Length - 1, i + 2)], k / 16f));
            line.Add(pts[pts.Length - 1]);
            var dist = new float[line.Count];
            for (int i = 1; i < line.Count; i++) dist[i] = dist[i - 1] + Vector3.Distance(Flat(line[i - 1]), Flat(line[i]));
            float total = dist[dist.Length - 1];

            float surfaceY = seaLevel - surfaceDepth, hiddenY = seaLevel - hiddenDepth;
            // (+10/10) 점프 중엔 빨라지므로 시간 대신 지나온 거리로 진행한다. 잠수는 남은 거리로 판단.
            float diveDistance = diveTime * lapSpeed;
            _queuedJump = null;
            model.gameObject.SetActive(true);
            _swimming = true;
            bool called = false;
            bool jumpPlanned = !Procedural && UnityEngine.Random.value < jumpChance;
            float jumpAt = total * UnityEngine.Random.Range(jumpWindow.x, Mathf.Max(jumpWindow.x, jumpWindow.y));
            int seg = 0;
            float roll = 0f;
            Vector3 lastDir = Flat(line[1] - line[0]).normalized;

            float t = 0f;
            for (float d = 0f; d < total; t += Time.deltaTime)
            {
                if (jumpPlanned && d >= jumpAt) { jumpPlanned = false; QueueJump(UnityEngine.Random.value < lowJumpChance); }
                d = Mathf.Min(total, d + lapSpeed * (IsJumping ? jumpSpeedBoost : 1f) * Time.deltaTime);
                while (seg < dist.Length - 2 && dist[seg + 1] < d) seg++;
                float u = Mathf.InverseLerp(dist[seg], dist[seg + 1], d);
                Vector3 pos = Vector3.Lerp(line[seg], line[seg + 1], u);
                Vector3 dir = Flat(line[seg + 1] - line[seg]).normalized;
                if (dir.sqrMagnitude < 0.001f) dir = lastDir;

                // 꺾이는 정도로 기울기 — 왼쪽으로 꺾으면 왼쪽으로
                float turn = Vector3.SignedAngle(lastDir, dir, Vector3.up) / Mathf.Max(0.0001f, Time.deltaTime);
                roll = Mathf.Lerp(roll, Mathf.Clamp(-turn * 0.6f, -bankAngle, bankAngle), 1f - Mathf.Exp(-2f * Time.deltaTime));
                lastDir = dir;

                float rise = Mathf.Clamp01(t / riseTime);
                float dive = Mathf.Clamp01((d - (total - diveDistance)) / diveDistance);
                if (!called && rise >= 1f) { called = true; SoundManager.Play(callClip, callVolume); }
                float stroke = Procedural ? Mathf.Sin(_swimTime * strokeFrequency * Mathf.PI * 2f) : 0f;   // (+10/10) 클립이면 출렁임은 클립에
                float y = Mathf.Lerp(hiddenY, surfaceY, Smooth(rise));
                y = Mathf.Lerp(y, hiddenY, Smooth(dive)) + stroke * bobHeight * (1f - dive);

                float pitch = -divePitch * 0.5f * (1f - rise) + divePitch * Smooth(dive) - stroke * bodyRock;
                transform.SetPositionAndRotation(new Vector3(pos.x, y, pos.z),
                    Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitch, 0f, roll));
                yield return null;
            }

            _swimming = false;
            model.gameObject.SetActive(false);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Quaternion[] CaptureBase(Transform[] bones)
        {
            if (bones == null) return Array.Empty<Quaternion>();
            var r = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) r[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
            return r;
        }

        private IEnumerator Loop()
        {
            yield return new WaitForSeconds(firstDelay);
            while (true)
            {
                if (lap) yield return Lap();   // (+10/8)
                else if (TryPickPath(out SwimPath p)) yield return Appear(p);
                yield return new WaitForSeconds(UnityEngine.Random.Range(intervalRange.x, Mathf.Max(intervalRange.x, intervalRange.y)));
            }
        }

        private bool TryPickPath(out SwimPath picked)
        {
            picked = default;
            Camera cam = Camera.main;
            if (cam == null || paths == null) return false;

            int count = 0;
            foreach (SwimPath p in paths)
            {
                if (p.from == null || p.to == null) continue;
                if (!IsOnScreen(cam, p)) continue;
                // 저수지 샘플링 — 조건 맞는 것 중 하나를 고르게 뽑는다.
                count++;
                if (UnityEngine.Random.Range(0, count) == 0) picked = p;
            }
            return count > 0;
        }

        // 카메라가 플레이어 앞쪽만 내려다봐서 "가까운 경로"로는 부족하다 — 경로 위 해수면 점이 실제로 화면에 들어오는지 본다.
        private bool IsOnScreen(Camera cam, SwimPath p)
        {
            const int samples = 12;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 pt = Vector3.Lerp(p.from.position, p.to.position, i / (float)samples);
                pt.y = seaLevel;
                if (Vector3.Distance(pt, cam.transform.position) > cameraRange) continue;
                Vector3 v = cam.WorldToViewportPoint(pt);
                if (v.z > 0f && v.x > 0.05f && v.x < 0.95f && v.y > 0.05f && v.y < 0.95f) return true;
            }
            return false;
        }

        /// <summary>바로 한 번 지나가게 한다 (테스트 · 연출용).</summary>
        [ContextMenu("Appear Now")]
        public void AppearNow()
        {
            if (!Application.isPlaying) return;
            StopAllCoroutines();
            if (lap) { _swimming = false; ResetBones(); StartCoroutine(LapThenLoop()); return; }   // (+10/8)
            if (TryPickPath(out SwimPath p)) StartCoroutine(AppearThenLoop(p));
            else
            {
                Debug.LogWarning($"{name}: 카메라 {cameraRange}m 안에 경로가 없다. 첫 경로로 나타난다.", this);
                StartCoroutine(AppearThenLoop(paths[0]));
            }
        }

        /// <summary>지금 헤엄치는 중인가(떠오름 ~ 잠수 끝). 숨어 있으면 false. (+10/5)</summary>
        public bool IsSwimming => _swimming;

        public float SeaLevel => seaLevel;

        /// <summary>
        /// (+10/5) 화면 조건 없이 카메라에서 가장 가까운 경로로 바로 한 번 지나가게 한다 — 개발용 단축키(WhaleDebugKey)가 부른다.
        /// 지나간 뒤엔 평소 루프(intervalRange 간격)로 돌아간다.
        /// </summary>
        public void AppearNearest()
        {
            if (!Application.isPlaying) return;
            if (lap) { AppearNow(); return; }   // (+10/8) 한 바퀴 경로는 하나뿐
            if (paths == null || paths.Length == 0) return;
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : transform.position;

            SwimPath best = paths[0];
            float bestDist = float.MaxValue;
            foreach (SwimPath p in paths)
            {
                if (p.from == null || p.to == null) continue;
                // 선분 위 가장 가까운 점까지의 수평 거리
                Vector3 a = Flat(p.from.position), ab = Flat(p.to.position) - a, e = Flat(eye);
                float s = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(e - a, ab) / ab.sqrMagnitude) : 0f;
                float d = Vector3.Distance(e, a + ab * s);
                if (d < bestDist) { bestDist = d; best = p; }
            }

            StopAllCoroutines();
            _swimming = false;
            ResetBones();
            StartCoroutine(AppearThenLoop(best));
        }

        // 중간에 끊고 다시 나타날 때 뼈가 굽은 채로 시작하지 않게.
        private void ResetBones()
        {
            for (int i = 0; i < spine.Length; i++) if (spine[i] != null) spine[i].localRotation = _spineBase[i];
            for (int i = 0; i < fins.Length; i++) if (fins[i] != null) fins[i].localRotation = _finBase[i];
        }

        private IEnumerator LapThenLoop()
        {
            yield return Lap();
            yield return Loop();
        }

        private IEnumerator AppearThenLoop(SwimPath p)
        {
            yield return Appear(p);
            yield return Loop();
        }

        private IEnumerator Appear(SwimPath p)
        {
            if (model == null) yield break;

            Vector3 a = Flat(p.from.position), b = Flat(p.to.position);
            if (UnityEngine.Random.value < 0.5f) (a, b) = (b, a);   // 같은 경로라도 방향은 그때그때
            Vector3 dir = (b - a).normalized;
            Quaternion heading = Quaternion.LookRotation(dir, Vector3.up);
            float total = Vector3.Distance(a, b);
            float surfaceY = seaLevel - surfaceDepth, hiddenY = seaLevel - hiddenDepth;

            model.gameObject.SetActive(true);
            _swimming = true;

            // 떠오르기 + 헤엄 + 잠수를 한 줄기 이동으로 — 이동 내내 앞으로 간다.
            float swimDuration = total / swimSpeed;
            float t = 0f;
            bool called = false;
            while (t < swimDuration)
            {
                t += Time.deltaTime;
                if (!called && t >= riseTime) { called = true; SoundManager.Play(callClip, callVolume); }   // (+10/6)
                float along = Mathf.Clamp01(t / swimDuration);
                Vector3 pos = Vector3.Lerp(a, b, along);

                float rise = Mathf.Clamp01(t / riseTime);
                float dive = Mathf.Clamp01((t - (swimDuration - diveTime)) / diveTime);
                float y = Mathf.Lerp(hiddenY, surfaceY, Smooth(rise));
                y = Mathf.Lerp(y, hiddenY, Smooth(dive));
                float procedural = Procedural ? 1f : 0f;   // (+10/10) 클립이면 출렁임 · 끄덕임은 클립에
                y += Mathf.Sin(_swimTime * strokeFrequency * Mathf.PI * 2f) * bobHeight * (1f - dive) * procedural;

                // 떠오를 땐 고개를 들고, 잠수할 땐 숙인다.
                float pitch = -divePitch * 0.5f * (1f - rise) + divePitch * Smooth(dive);
                pitch += Mathf.Sin(_swimTime * strokeFrequency * Mathf.PI * 2f + Mathf.PI) * bodyRock * procedural;   // 꼬리와 반대로 끄덕
                transform.SetPositionAndRotation(new Vector3(pos.x, y, pos.z), heading * Quaternion.Euler(pitch, 0f, 0f));
                yield return null;
            }

            _swimming = false;
            model.gameObject.SetActive(false);
        }

        /// <summary>(+10/10) 지금 점프 클립을 트는 중인가.</summary>
        public bool IsJumping
        {
            get
            {
                if (animator == null || !animator.isActiveAndEnabled) return false;
                AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                return st.IsName(JumpState) || st.IsName(JumpLowState);
            }
        }

        /// <summary>(+10/10) 고래 몸의 화면상 중심 — 점프하면 오브젝트 위치보다 한참 위로 간다. 카메라가 이걸 본다.</summary>
        public Vector3 VisualCenter => _renderer != null && _renderer.gameObject.activeInHierarchy ? _renderer.bounds.center : transform.position;

        /// <summary>(+10/10) 헤엄치는 중이면 다음 헤엄 한 바퀴가 끝날 때 점프한다(개발 단축키 · 연출용).</summary>
        public void JumpNow(bool low)
        {
            if (Procedural) { Debug.LogWarning($"{name}: animator가 없어 점프 클립을 못 튼다.", this); return; }
            if (!_swimming) { Debug.LogWarning($"{name}: 헤엄치는 중에만 점프한다 — 먼저 나타나게 해라.", this); return; }
            if (IsJumping) return;
            QueueJump(low);
        }

        private void QueueJump(bool low) => _queuedJump = low ? JumpLowState : JumpState;

        // 점프 첫 프레임 = 헤엄 첫 프레임이라, 헤엄 클립이 한 바퀴 돌아 0으로 넘어가는 프레임에 튼다.
        // 점프가 끝나면 Animator 전이(종료 시점, 0초)로 헤엄 첫 프레임에 돌아온다.
        private void UpdateJump()
        {
            if (!animator.isActiveAndEnabled) return;
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            if (!st.IsName(SwimState) || animator.IsInTransition(0)) { _lastSwimPhase = 0f; return; }
            float phase = st.normalizedTime % 1f;
            if (_queuedJump != null && phase < _lastSwimPhase)
            {
                animator.Play(_queuedJump, 0, 0f);
                _queuedJump = null;
                _lastSwimPhase = 0f;
                return;
            }
            _lastSwimPhase = phase;
        }

        private void LateUpdate()
        {
            if (!_swimming) return;
            if (!Procedural) { UpdateJump(); return; }   // (+10/10) 헤엄 · 점프는 클립이 한다
            _swimTime += Time.deltaTime;
            float w = _swimTime * strokeFrequency * Mathf.PI * 2f;

            // 꼬리를 위아래로 친다 — 고래 몸의 오른쪽 축을 각 뼈의 부모 기준으로 바꿔 돌린다.
            Vector3 right = transform.right;
            for (int i = 0; i < spine.Length; i++)
            {
                Transform bone = spine[i];
                if (bone == null) continue;
                float k = spine.Length > 1 ? (float)i / (spine.Length - 1) : 1f;   // 머리 0 → 꼬리 1
                // (+10/2) k*k는 꼬리 끝만 움직여 12m 고래에서 끝이 0.6m밖에 안 흔들렸다 — 몸통 뒤 절반도 같이 휜다.
                // (+10/8) 꼬리만 파닥이지 않게 — 굽힘을 몸 가운데(허리)에 싣고 꼬리 끝은 덜 움직인다. 허릿심으로 미는 느낌.
                float tailShape = Mathf.Pow(k, 1.3f);
                float waistShape = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 0.9f + 0.05f));
                float angle = Mathf.Sin(w - i * phaseStep) * tailAmplitude * Mathf.Lerp(tailShape, waistShape, waistWeight);
                Vector3 axis = bone.parent != null ? bone.parent.InverseTransformDirection(right) : right;
                bone.localRotation = Quaternion.AngleAxis(angle, axis) * _spineBase[i];
            }

            for (int i = 0; i < fins.Length; i++)
            {
                Transform fin = fins[i];
                if (fin == null) continue;
                float angle = Mathf.Sin(w + i) * finAmplitude;
                Vector3 axis = fin.parent != null ? fin.parent.InverseTransformDirection(right) : right;
                fin.localRotation = Quaternion.AngleAxis(angle, axis) * _finBase[i];
            }
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
