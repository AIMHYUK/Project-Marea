using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Player;
using Marea.Restaurant;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Marea.Field
{
    /// <summary>
    /// 돌발 상황 — 고래 충돌. (+10/12) 영업 중 하루 한 번, 고래가 데크 가장자리를 따라 지나가다 툭 치고 간다.
    ///
    /// 흐름: 영업을 열고 delayAfterOpen 뒤 → 부딪칠 자리 고르기 → 고래가 그 가장자리를 스치며 헤엄(WhaleSwimmer.Graze)
    /// → 가장 깊이 파고든 순간: 물보라 · 먼지, 카메라 흔들림, 판자 조각, 그 근처 데크에 파손 장판 hazardCount개, 알림
    /// → 그 데크 판이 뗏목처럼 기울었다 잦아든다(DeckBob.Kick).
    ///
    /// 부딪칠 자리: 흔들리는(지어진) 데크 판(DeckBob.ActiveFloors)마다 네 가장자리 중 고래가 지나갈 길이 바다로 트인 쪽만 후보로 둔다.
    /// 후보 중 플레이어에게 가장 가까운 곳 — 보이는 데서 친다. (+10/12) 판 목록을 DeckBob과 같이 쓴다 — 예전엔 여기 따로 들었다.
    ///
    /// 흔들림은 DeckBob이 맡는다 — 평소 출렁임과 같은 물체를 움직여서, 따로 돌리면 서로 덮어쓴다.
    ///
    /// 고친 장판은 다시 안 생긴다. 안 고친 장판 · 치른 날은 저장한다(CaptainEvent와 같은 형식).
    /// 고래 오브젝트(WhaleSwimmer 옆)에 붙인다.
    /// </summary>
    [RequireComponent(typeof(WhaleSwimmer))]
    public class WhaleCrashEvent : MonoBehaviour
    {
        [Header("언제")]
        [Tooltip("영업을 연 뒤 이만큼(초, 최소~최대) 지나 부딪친다. 그 전에 영업이 끝나면 그날은 없다.")]
        [SerializeField] private Vector2 delayAfterOpen = new(20f, 60f);
        [Tooltip("고래가 이미 헤엄치는 중이면 끝날 때까지 이만큼(초)까지 기다린다. 한 바퀴(lap)가 60초쯤이라 그보다 길게 — 짧으면 도는 도중에 끊겨 사라진다.")]
        [SerializeField, Min(0f)] private float waitForWhale = 90f;
        [Tooltip("플레이 중 이 키로 바로 부딪치게 한다(테스트 — 영업 · 날짜와 상관없이).")]
        [SerializeField] private Key testKey = Key.F12;

        [Header("고래 경로 — 바다에서 비스듬히 들어와 툭 치고 비스듬히 빠진다")]
        [Tooltip("닿는 순간 고래 몸 가운데와 데크 가장자리 사이(m). 몸 반 폭이 3.5m라 그보다 조금 작으면 살짝 파고든다.")]
        [SerializeField, Min(0f)] private float contact = 3.2f;
        [Tooltip("들어오는 · 빠지는 점이 가장자리에서 바다 쪽으로 떨어진 거리(m).")]
        [SerializeField, Min(0f)] private float approach = 14f;
        [Tooltip("들어오는 · 빠지는 점이 부딪칠 자리에서 가장자리를 따라 떨어진 거리(m). 경로가 길어야 다 떠오른 뒤(riseTime) 친다.")]
        [SerializeField, Min(5f)] private float runUp = 22f;
        [SerializeField, Min(0.1f)] private float speed = 3.5f;

        [Header("부서짐")]
        [SerializeField] private GameObject hazardPrefab;
        [SerializeField, Min(0)] private int hazardCount = 2;
        [Tooltip("부딪친 자리에서 이만큼(m) 안쪽 데크에 장판을 깐다.")]
        [SerializeField, Min(1f)] private float hazardRadius = 4f;
        [Tooltip("장판끼리 · 기존 장판 · 좌석에서 이만큼 떨어진 곳만.")]
        [SerializeField, Min(0.5f)] private float hazardSpacing = 2f;
        [SerializeField] private VfxId splashVfx = VfxId.Splash;
        [SerializeField] private VfxId breakVfx = VfxId.Dust;
        [SerializeField] private VfxId impactVfx = VfxId.Fail;
        [SerializeField, Min(0.1f)] private float vfxScale = 3f;
        [Tooltip("튀는 판자 조각 프리팹. 물리로 날아가고 pieceLife초 뒤 사라진다.")]
        [SerializeField] private GameObject[] burstPieces;
        [Tooltip("조각에 입힐 재질(데크 색). 비우면 프리팹 그대로.")]
        [SerializeField] private Material burstMaterial;
        [SerializeField, Min(0)] private int burstCount = 7;
        [SerializeField, Min(0f)] private float burstSpeed = 6f;
        [SerializeField, Min(0.5f)] private float pieceLife = 5f;
        [Tooltip("부딪치는 소리. 비우면 소리 없음(고래 울음은 떠오를 때 WhaleSwimmer가 낸다).")]
        [SerializeField] private AudioClip hitClip;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 1f;

        [Header("흔들림")]
        [Tooltip("카메라 흔들기 세기(m) · 시간(초).")]
        [SerializeField, Min(0f)] private float shakeStrength = 0.35f;
        [SerializeField, Min(0f)] private float shakeSeconds = 0.7f;
        [Tooltip("데크 판이 기우는 최대 각도(도) — DeckBob.Kick에 넘긴다. 판 폭이 13m면 1도에 바다 쪽 가장자리가 23cm 오르내린다.")]
        [SerializeField, Range(0f, 5f)] private float swayAngle = 0.8f;
        [Tooltip("흔들리는 빠르기(Hz).")]
        [SerializeField, Min(0.05f)] private float swayFrequency = 0.45f;
        [Tooltip("이만큼(초) 지나면 처음 세기의 1/e로 잦아든다.")]
        [SerializeField, Min(0.1f)] private float swayDecay = 2.5f;
        [SerializeField, Min(0.5f)] private float swaySeconds = 8f;

        [Header("알림")]
        [SerializeField] private string toast = "고래가 식당을 들이받았다! 부서진 바닥을 고쳐야 한다";

        private struct Spot
        {
            public Vector3 edge;      // 가장자리 위 부딪칠 점(바닥 윗면 높이)
            public Vector3 outward;   // 데크 → 바다
            public Vector3 tangent;   // 가장자리 방향
            public Bounds floor;      // 그 판 바닥 메시 상자
            public float approach;    // 들어오는 · 빠지는 점이 바다 쪽으로 떨어진 거리
            public float skew;        // 들어오는 · 빠지는 점을 가장자리 방향으로 같이 민 거리 — 한쪽에 부두가 있으면 비껴 간다
        }

        private WhaleSwimmer _whale;
        private CameraFollow _camera;
        private int _doneDay = -1;
        private bool _running;
        private readonly List<TripHazard> _hazards = new();   // 이 이벤트가 부순 자리 — 고치지 않은 것만 저장한다

        private static bool IsOpen => BusinessManager.Instance != null && BusinessManager.Instance.CurrentState == BusinessState.Open;

        private void Awake()
        {
            _whale = GetComponent<WhaleSwimmer>();
            if (hazardPrefab == null) Debug.LogError($"{name}: WhaleCrashEvent.hazardPrefab이 비어 있다. 고래가 부딪쳐도 바닥이 안 부서진다.", this);

            // 저장 — 치른 날, 아직 안 고친 파손 자리. 형식 "x,y,z,yaw;..."
            _doneDay = SaveFile.GetInt("whaleCrashDay", -1);
            foreach (string h in SaveFile.Get("whaleCrashHazards").Split(';'))
            {
                string[] v = h.Split(',');
                if (v.Length != 4) continue;
                SpawnHazard(new Vector3(F(v[0]), F(v[1]), F(v[2])), F(v[3]));
            }
        }

        private void Start()
        {
            Camera cam = Camera.main;
            if (cam != null) _camera = cam.GetComponent<CameraFollow>();
            if (DeckBob.Instance == null) Debug.LogError($"{name}: 씬에 DeckBob이 없다. 고래가 부딪칠 데크를 못 고른다(PF_DeckBob을 놓을 것).", this);
            BusinessManager bm = BusinessManager.Instance;
            if (bm == null) { Debug.LogError($"{name}: BusinessManager가 없다. 고래 충돌이 안 일어난다.", this); return; }
            bm.OnStateChanged += OnBusinessState;
            bm.OnDayChanged += Save;
            if (bm.CurrentState == BusinessState.Open) OnBusinessState(BusinessState.Open);
        }

        private void OnDestroy()
        {
            if (BusinessManager.Instance == null) return;
            BusinessManager.Instance.OnStateChanged -= OnBusinessState;
            BusinessManager.Instance.OnDayChanged -= Save;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame) RunNow();
        }

        /// <summary>테스트: 영업 · 날짜와 상관없이 지금 부딪치게 한다. 플레이 중 testKey 또는 인스펙터 메뉴.</summary>
        [ContextMenu("지금 충돌 (테스트)")]
        public void RunNow()
        {
            if (!Application.isPlaying || _running) return;
            StartCoroutine(Run(0f, true));
        }

        private void OnBusinessState(BusinessState state)
        {
            if (state != BusinessState.Open || _running || _doneDay == BusinessManager.Instance.Day) return;
            StartCoroutine(Run(Random.Range(delayAfterOpen.x, Mathf.Max(delayAfterOpen.x, delayAfterOpen.y)), false));
        }

        private IEnumerator Run(float delay, bool test)
        {
            _running = true;
            yield return new WaitForSeconds(delay);
            for (float w = 0f; !test && _whale.IsSwimming && w < waitForWhale; w += Time.deltaTime) yield return null;   // 테스트는 바로 끊고 부딪친다
            if (!test && !IsOpen) { _running = false; yield break; }   // 그 사이 영업이 끝났다 — 오늘은 없다

            if (!TryPickSpot(out Spot spot))
            {
                Debug.LogWarning($"{name}: 고래가 부딪칠 데크 가장자리를 못 찾았다(바다 쪽이 막혔거나 판이 다 꺼졌다). 오늘은 건너뛴다.", this);
                _running = false;
                yield break;
            }
            if (!test && BusinessManager.Instance != null) _doneDay = BusinessManager.Instance.Day;

            Path(spot, out Vector3 from, out Vector3 apex, out Vector3 to);
            _whale.Graze(from, apex, to, -spot.outward, speed, () => Hit(spot));
            _running = false;
        }

        // --- 부딪칠 자리 ---

        private bool TryPickSpot(out Spot best)
        {
            best = default;
            PlayerController player = FindAnyObjectByType<PlayerController>();
            Vector3 eye = player != null ? player.transform.position : Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            float bestDist = float.MaxValue;
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            List<Bounds> obstacles = Obstacles();
            if (DeckBob.Instance == null) return false;
            foreach (Bounds floor in DeckBob.Instance.ActiveFloors)
            {
                foreach (Vector3 dir in dirs)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, dir);
                    float half = Extent(floor, dir), along = Extent(floor, tangent);
                    for (float u = -0.6f; u <= 0.61f; u += 0.3f)
                    {
                        Vector3 edge = floor.center + dir * half + tangent * (along * u);
                        edge.y = floor.max.y;
                        float d = (Flat(eye) - Flat(edge)).sqrMagnitude;
                        if (d >= bestDist) continue;
                        // 곧은 V부터 — 막히면 더 멀리서 · 한쪽으로 비껴서
                        foreach (float far in new[] { approach, approach * 1.5f })
                            foreach (float skew in new[] { 0f, -runUp * 0.5f, runUp * 0.5f })
                            {
                                var spot = new Spot { edge = edge, outward = dir, tangent = tangent, floor = floor, approach = far, skew = skew };
                                if (d >= bestDist || !OpenSea(spot, obstacles)) continue;
                                bestDist = d;
                                best = spot;
                            }
                    }
                }
            }
            return bestDist < float.MaxValue;
        }

        private void Path(Spot s, out Vector3 from, out Vector3 apex, out Vector3 to)
        {
            apex = s.edge + s.outward * contact;
            from = s.edge + s.outward * s.approach + s.tangent * (s.skew - runUp);
            to = s.edge + s.outward * s.approach + s.tangent * (s.skew + runUp);
        }

        // 물속에서 솟은 것들(기둥 · 작은 부두 · 섬) — 콜라이더가 없는 장식도 고래가 뚫고 지나가면 안 된다. 렌더러 상자로 본다.
        // 시설 폐허(물에 뜬 잔해) · 떠다니는 상자는 빼다 — 밀고 지나가도 어색하지 않다.
        private List<Bounds> Obstacles()
        {
            float top = _whale.SeaLevel + 0.3f;
            var wreck = new List<Transform>();
            foreach (FacilitySite site in FindObjectsByType<FacilitySite>(FindObjectsSortMode.None))
                if (site.BrokenVisual != null) wreck.Add(site.BrokenVisual.transform);
            var list = new List<Bounds>();
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.transform.IsChildOf(transform) || r.GetComponentInParent<DriftingCrate>() != null) continue;
                if (wreck.Exists(w => r.transform.IsChildOf(w))) continue;
                Bounds b = r.bounds;
                if (b.max.y > top && b.min.y < top + 1.5f) list.Add(b);   // 물에 닿아 있고 수면 위로 나온 것
            }
            return list;
        }

        // 고래가 지나갈 곡선과 그 양옆(몸 폭)이 바다인가 — 물에서 솟은 물체가 없어야 한다. 닿는 언저리의 데크 쪽은 당연히 데크라 안 본다.
        // 콜라이더는 안 본다 — 걷기용 투명 판(확장 전 부지 · 부두 연결부)이 눈에 안 보이는데도 막았다.
        private bool OpenSea(Spot spot, List<Bounds> obstacles)
        {
            Vector3 edge = spot.edge, outward = spot.outward;
            Path(spot, out Vector3 a, out Vector3 apex, out Vector3 b);
            Vector3 c = 2f * apex - 0.5f * (a + b);
            Bounds deck = spot.floor;
            deck.Expand(new Vector3(3f, 4f, 3f));   // 그 판 가장자리 기둥 · 난간 — 닿는 언저리에선 장애물이 아니다
            for (float s = 0f; s <= 1.001f; s += 0.04f)
            {
                Vector3 p = (1f - s) * (1f - s) * a + 2f * (1f - s) * s * c + s * s * b;
                Vector3 across = Vector3.Cross(Vector3.up, Flat(2f * (1f - s) * (c - a) + 2f * s * (b - c)).normalized);
                bool nearContact = Mathf.Abs(s - 0.5f) < 0.15f;
                foreach (float k in new[] { -3.5f, 0f, 3.5f })
                {
                    Vector3 q = p + across * k;
                    if (nearContact && Vector3.Dot(q - edge, outward) < contact) continue;   // 데크 쪽 옆구리 — 닿을 자리
                    foreach (Bounds o in obstacles)
                        if (q.x > o.min.x && q.x < o.max.x && q.z > o.min.z && q.z < o.max.z && !(nearContact && o.Intersects(deck)))
                            return false;
                }
            }
            return true;
        }

        // --- 부딪친 순간 ---

        private void Hit(Spot s)
        {
            Vector3 p = s.edge;
            Vfx.Play(splashVfx, p + s.outward * 1.2f + Vector3.up * 0.3f, vfxScale);
            Vfx.Play(breakVfx, p + Vector3.up * 0.3f, vfxScale);
            Vfx.Play(impactVfx, p + Vector3.up * 0.5f, vfxScale * 0.7f);
            if (hitClip != null) SoundManager.Play(hitClip, hitVolume);
            if (_camera != null) _camera.Shake(shakeStrength, shakeSeconds);
            BreakFloor(s);
            Burst(p - s.outward * 0.8f + Vector3.up * 0.4f, s.outward);
            if (DeckBob.Instance != null) DeckBob.Instance.Kick(p, s.outward, swayAngle, swayFrequency, swayDecay, swaySeconds);
            Toast.Show(toast);
        }

        private void BreakFloor(Spot s)
        {
            if (hazardPrefab == null || hazardCount <= 0) return;
            var seats = new List<Vector3>();
            foreach (Seat seat in FindObjectsByType<Seat>(FindObjectsSortMode.None))
                if (seat.isActiveAndEnabled && seat.SitPoint != null) seats.Add(seat.SitPoint.position);
            var taken = new List<Vector3>();
            foreach (TripHazard h in FindObjectsByType<TripHazard>(FindObjectsSortMode.None)) taken.Add(h.transform.position);

            Vector3 center = s.edge - s.outward * (hazardRadius * 0.5f);
            int made = 0;
            for (int attempt = 0; attempt < 200 && made < hazardCount; attempt++)
            {
                Vector2 r = Random.insideUnitCircle * hazardRadius;
                var guess = new Vector3(center.x + r.x, s.floor.max.y, center.z + r.y);
                if (guess.x < s.floor.min.x + 0.7f || guess.x > s.floor.max.x - 0.7f || guess.z < s.floor.min.z + 0.7f || guess.z > s.floor.max.z - 0.7f) continue;
                if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                if (Mathf.Abs(p.y - s.floor.max.y) > 0.8f) continue;
                if (seats.Exists(q => Flat(q - p).magnitude < hazardSpacing * 0.8f)) continue;
                if (taken.Exists(q => Flat(q - p).magnitude < hazardSpacing)) continue;
                if (CaptainEvent.Blocked(p)) continue;

                SpawnHazard(p, Random.Range(0f, 360f));
                Vfx.Play(breakVfx, p + Vector3.up * 0.3f, vfxScale * 0.6f);
                taken.Add(p);
                made++;
            }
            if (made < hazardCount)
                Debug.LogWarning($"{name}: 파손 장판 자리를 {hazardCount}곳 중 {made}곳만 찾았다 — 부딪친 자리 근처가 좁거나 물건이 많다.", this);
        }

        private void SpawnHazard(Vector3 position, float yaw)
        {
            if (hazardPrefab == null) return;
            GameObject go = Instantiate(hazardPrefab, position, Quaternion.Euler(0f, yaw, 0f));
            go.name = $"TripHazard_Whale_{_hazards.Count + 1}";
            _hazards.Add(go.GetComponent<TripHazard>());
        }

        // 판자 조각을 데크 쪽 위로 튕긴다. 몇 개는 바다로 떨어진다.
        private void Burst(Vector3 at, Vector3 outward)
        {
            if (burstPieces == null || burstPieces.Length == 0) return;
            for (int i = 0; i < burstCount; i++)
            {
                GameObject src = burstPieces[i % burstPieces.Length];
                if (src == null) continue;
                Vector2 flat = Random.insideUnitCircle;
                GameObject piece = Instantiate(src, at + new Vector3(flat.x, 0f, flat.y) * 0.8f, Random.rotation);
                if (burstMaterial != null)
                    foreach (MeshRenderer mr in piece.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = burstMaterial;
                foreach (MeshFilter mf in piece.GetComponentsInChildren<MeshFilter>())
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().convex = true;
                Rigidbody body = piece.GetComponent<Rigidbody>();
                if (body == null) body = piece.AddComponent<Rigidbody>();
                Vector3 dir = -outward * Random.Range(0.2f, 0.9f) + new Vector3(flat.x, 0f, flat.y) * 0.6f + Vector3.up;
                body.linearVelocity = dir.normalized * burstSpeed * Random.Range(0.6f, 1.2f);
                body.angularVelocity = Random.insideUnitSphere * 10f;
                Destroy(piece, pieceLife);
            }
        }

        // --- 저장 ---

        private void Save(int day)
        {
            var parts = new List<string>();
            foreach (TripHazard h in _hazards)
            {
                if (h == null || h.IsRepaired) continue;
                Vector3 p = h.transform.position;
                parts.Add(string.Join(",", S(p.x), S(p.y), S(p.z), S(h.transform.eulerAngles.y)));
            }
            SaveFile.Set("whaleCrashDay", _doneDay);
            SaveFile.Set("whaleCrashHazards", string.Join(";", parts));
        }

        private static float Extent(Bounds b, Vector3 dir) => Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
        private static float F(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
        private static string S(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
