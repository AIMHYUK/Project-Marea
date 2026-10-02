using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    /// <summary>
    /// 조개구이 3단계 — 조리대에서 소스 뿌리기. 기획 MG_GUIDE_DRAG 자리. (+10/2, 이슈 85)
    ///
    /// 소스통을 눌러 집고, 누른 채로 움직이면 소스통이 조리대 위 일정 높이에서 따라온다.
    /// 집고 있는 동안 노즐에서 아래로 소스 줄기가 나온다 — 지금은 베지어 곡선을 LineRenderer로 그린 임시 표현이다.
    /// 줄기 끝(조리대에 닿는 점)이 조개 위에 머문 시간이 쌓여 coatSeconds를 넘으면 그 조개는 소스가 묻은 것이다.
    /// 점수 = 소스 묻은 조개 수 / 전체. 다 묻히거나 제한시간이 끝나면 끝.
    ///
    /// 입력은 Input System 마우스를 직접 읽는다 (PopupTouchGame과 같다). 소스통을 집었는지는 카메라 레이로 본다.
    /// 단계 진행 · 결과는 모른다 — 컨트롤러가 Begin으로 시작하고 IsFinished/Score를 읽는다.
    /// </summary>
    public class SauceDrizzleGame : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("소스를 뿌릴 조개 자리. 접시 위 조개.")]
        [SerializeField] private Transform[] targets;

        [Tooltip("조개 모습. 비우면 임시 모양(납작한 구).")]
        [SerializeField] private GameObject clamPrefab;

        [Tooltip("(+10/2 아트 적용) 자리별 조개 — i번 자리는 [i % 개수]. 칸이 비면 clamPrefab.")]
        [SerializeField] private GameObject[] targetPrefabs;
        [Tooltip("소스가 묻은 뒤 갈아 끼울 모습(전복 → 와사비 등). 칸이 비면 coatedColor로 칠한다.")]
        [SerializeField] private GameObject[] coatedPrefabs;

        [Tooltip("줄기 끝이 이 반경(m) 안이면 조개 위다.")]
        [SerializeField, Min(0.01f)] private float targetRadius = 0.12f;

        [Tooltip("조개 하나에 이만큼(초) 뿌리면 묻은 것으로 친다.")]
        [SerializeField, Min(0.05f)] private float coatSeconds = 0.5f;

        [Header("소스통")]
        [Tooltip("집을 소스통. 콜라이더가 있어야 한다.")]
        [SerializeField] private Transform bottle;

        [Tooltip("노즐 위치(소스통 자식). 비우면 소스통 위치.")]
        [SerializeField] private Transform nozzle;

        [Tooltip("조리대 윗면 높이 기준. 소스통은 이 위 bottleHeight에서 움직이고 줄기는 여기에 닿는다.")]
        [SerializeField] private Transform surface;

        [SerializeField, Min(0.05f)] private float bottleHeight = 0.35f;

        [Header("줄기 (LineRenderer · 베지어 임시 표현)")]
        [SerializeField] private LineRenderer stream;
        [Tooltip("줄기가 앞으로 휘는 정도(m). 소스통 진행 방향으로 휜다.")]
        [SerializeField] private float streamBend = 0.08f;
        [SerializeField, Range(4, 32)] private int streamSegments = 16;

        [Header("시간")]
        [Tooltip("제한시간(초). 기획 MG_GUIDE_DRAG = 8.")]
        [SerializeField, Min(1f)] private float timeLimit = 8f;

        [Tooltip("소스 묻은 조개 색(임시 모양일 때).")]
        [SerializeField] private Color coatedColor = new Color(0.85f, 0.35f, 0.1f);
        [SerializeField] private Color plainColor = new Color(0.96f, 0.93f, 0.86f);

        private sealed class Target
        {
            public Transform Anchor;
            public int Index;
            public GameObject Visual;
            public float Coat;
            public bool Done;
        }

        private readonly List<Target> _targets = new();
        private Collider _bottleCollider;
        private Vector3 _bottleHome;
        private Vector3 _lastBottlePos;
        private bool _holding;
        private float _timeLeft;
        private bool _running;

        public bool IsFinished { get; private set; }
        public bool IsPouring => _holding;
        public int CoatedCount => _targets.FindAll(t => t.Done).Count;
        public int TotalCount => _targets.Count;
        public float TimeLeft01 => timeLimit > 0f ? Mathf.Clamp01(_timeLeft / timeLimit) : 0f;
        public float Score => _targets.Count > 0 ? (float)CoatedCount / _targets.Count : 1f;

        /// <summary>소스 묻은 조개 수가 바뀔 때. (묻은 수, 전체)</summary>
        public event Action<int, int> OnProgress;

        private void Awake()
        {
            if (bottle != null)
            {
                _bottleCollider = bottle.GetComponentInChildren<Collider>();
                _bottleHome = bottle.position;
            }
        }

        public void Begin()
        {
            Cleanup();
            if (targets == null || targets.Length == 0 || bottle == null || surface == null)
            {
                Debug.LogError($"{name}: SauceDrizzleGame의 targets · bottle · surface 중 비어 있는 게 있다.", this);
                IsFinished = true;
                return;
            }
            if (_bottleCollider == null)
                Debug.LogError($"{name}: 소스통에 콜라이더가 없다. 집을 수 없다.", bottle);

            for (int i = 0; i < targets.Length; i++)
            {
                Transform anchor = targets[i];
                if (anchor == null) continue;
                var t = new Target { Anchor = anchor, Index = i };
                GameObject prefab = Pick(targetPrefabs, i, clamPrefab);
                t.Visual = prefab != null
                    ? Instantiate(prefab, anchor.position, anchor.rotation, transform)
                    : MakePlaceholder(anchor);
                StripColliders(t.Visual);
                _targets.Add(t);
            }

            bottle.position = _bottleHome;
            _lastBottlePos = _bottleHome;
            _holding = false;
            SetStream(false);
            _timeLeft = timeLimit;
            IsFinished = false;
            _running = true;
            OnProgress?.Invoke(CoatedCount, TotalCount);
        }

        private GameObject MakePlaceholder(Transform anchor)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Clam_Plate";
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            go.transform.localScale = new Vector3(0.24f, 0.06f, 0.2f);
            Destroy(go.GetComponent<Collider>());   // 소스통 레이를 가리지 않게
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = plainColor;
            return go;
        }

        private void Update()
        {
            if (!_running) return;
            _timeLeft -= Time.deltaTime;

            HandleInput();
            if (_holding) Pour();

            if (_timeLeft <= 0f || CoatedCount == TotalCount) Finish();
        }

        private void HandleInput()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame && _bottleCollider != null)
            {
                foreach (RaycastHit hit in Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide))
                    if (hit.collider == _bottleCollider) { _holding = true; break; }
            }
            if (!mouse.leftButton.isPressed) _holding = false;
            SetStream(_holding);

            if (!_holding) return;

            // 조리대 위 bottleHeight 높이의 수평면에 마우스 레이를 맞혀 소스통을 옮긴다.
            var plane = new Plane(Vector3.up, surface.position + Vector3.up * bottleHeight);
            if (plane.Raycast(ray, out float enter)) bottle.position = ray.GetPoint(enter);
        }

        /// <summary>노즐에서 조리대까지 줄기를 그리고, 줄기 끝 아래 조개에 소스를 쌓는다.</summary>
        private void Pour()
        {
            Vector3 from = nozzle != null ? nozzle.position : bottle.position;
            Vector3 move = bottle.position - _lastBottlePos;
            _lastBottlePos = bottle.position;

            // 소스는 움직이는 쪽 반대로 살짝 늦게 떨어진다 — 끝점을 진행 방향 뒤로, 중간점을 앞으로 휜다.
            Vector3 dir = new Vector3(move.x, 0f, move.z);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;
            Vector3 to = new Vector3(from.x, surface.position.y, from.z) - dir * (streamBend * 0.5f);
            Vector3 ctrl = Vector3.Lerp(from, to, 0.5f) + dir * streamBend;

            if (stream != null)
            {
                stream.positionCount = streamSegments + 1;
                for (int i = 0; i <= streamSegments; i++)
                {
                    float t = (float)i / streamSegments;
                    // 2차 베지어: (1-t)^2 P0 + 2(1-t)t P1 + t^2 P2
                    Vector3 p = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * ctrl + t * t * to;
                    stream.SetPosition(i, p);
                }
            }

            foreach (Target tg in _targets)
            {
                if (tg.Done) continue;
                Vector3 d = tg.Anchor.position - to;
                d.y = 0f;
                if (d.magnitude > targetRadius) continue;

                tg.Coat += Time.deltaTime;
                if (tg.Coat < coatSeconds) continue;

                tg.Done = true;
                GameObject coated = Pick(coatedPrefabs, tg.Index, null);
                if (coated != null)
                {
                    Destroy(tg.Visual);
                    tg.Visual = Instantiate(coated, tg.Anchor.position, tg.Anchor.rotation, transform);
                    StripColliders(tg.Visual);
                }
                else
                {
                    foreach (Renderer r in tg.Visual.GetComponentsInChildren<Renderer>()) r.material.color = coatedColor;
                }
                OnProgress?.Invoke(CoatedCount, TotalCount);
            }
        }

        private static GameObject Pick(GameObject[] perSlot, int index, GameObject fallback)
        {
            if (perSlot == null || perSlot.Length == 0) return fallback;
            GameObject p = perSlot[index % perSlot.Length];
            return p != null ? p : fallback;
        }

        // 접시 위 조개 콜라이더가 소스통 레이를 가리지 않게 뗀다 (판정은 거리로 한다).
        private static void StripColliders(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        private void SetStream(bool on)
        {
            if (stream != null && stream.enabled != on) stream.enabled = on;
        }

        private void Finish()
        {
            _running = false;
            _holding = false;
            SetStream(false);
            IsFinished = true;
        }

        private void OnDisable()
        {
            _running = false;
            _holding = false;
            SetStream(false);
            Cleanup();
            if (bottle != null) bottle.position = _bottleHome;
        }

        private void Cleanup()
        {
            foreach (Target t in _targets) if (t.Visual != null) Destroy(t.Visual);
            _targets.Clear();
        }
    }
}
