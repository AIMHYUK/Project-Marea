using System;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    /// <summary>
    /// 팝업 터치 미니게임 부품 — 표면 위에 무작위로 뜨는 오브젝트를 사라지기 전에 클릭한다. 기획 MG_POPUP_TOUCH. (+9/30)
    ///
    /// 스튜 3단계에선 국물 위 거품이다. 단계 진행 · 결과는 모른다 — 컨트롤러가 Begin으로 시작하고
    /// IsFinished/Score를 읽는다. 클릭은 카메라 레이를 직접 쏜다 (EventSystem 없이도 동작).
    ///
    /// (+10/2, 이슈 85) 고정 슬롯 모드 — slots를 채우면 무작위 자리 대신 그 자리에 하나씩 놓인다(조개구이 2단계).
    /// 처음엔 닫힌 모습(idlePrefab)으로 있다가 무작위 시점에 열린 모습(popupPrefab)으로 바뀌고, 그때부터 누를 수 있다.
    /// 빨리 누를수록 점수가 높고(1 에서 반응 창 끝의 minReactionScore까지), 반응 창이 지나면 탄 것(0점)이다.
    /// 점수 = 슬롯 점수의 평균. slots가 비어 있으면 예전 모드 그대로다.
    /// </summary>
    public class PopupTouchGame : MonoBehaviour
    {
        [Header("나타날 자리")]
        [Tooltip("표면 중심. 스튜는 국물(SM_Soup_Pot_Inside).")]
        [SerializeField] private Transform surface;

        [Tooltip("표면 중심에서 이 반경(m) 안에 뜬다.")]
        [SerializeField, Min(0.05f)] private float radius = 0.45f;

        [Tooltip("표면 위로 띄우는 높이(m).")]
        [SerializeField] private float heightOffset = 0.05f;

        [Header("팝업")]
        [Tooltip("띄울 모델. 비우면 흰 구(거품)를 쓴다.")]
        [SerializeField] private GameObject popupPrefab;

        [Tooltip("다 부풀었을 때 지름(m).")]
        [SerializeField, Min(0.02f)] private float size = 0.18f;

        [Tooltip("새로 뜨는 간격(초).")]
        [SerializeField, Min(0.05f)] private float spawnInterval = 0.6f;

        [Tooltip("떠 있는 시간 범위(초). 이 안에 못 누르면 놓친 것이다.")]
        [SerializeField] private Vector2 lifetimeRange = new Vector2(1.2f, 2.0f);

        [Tooltip("동시에 떠 있을 수 있는 최대 개수.")]
        [SerializeField, Min(1)] private int maxAlive = 4;

        [Header("연출 (+10/6, 이슈 117) — 기획 「정확한 타이밍·위치 입력」 VFX_08/12 · 「보통 판정」 VFX_08")]
        [Tooltip("맞힌 자리에 퍼지는 링. 모든 성공에.")]
        [SerializeField] private VfxId hitRingVfx = VfxId.Ping;
        [Tooltip("빨리 맞혀 점수가 perfectScore 이상이면 더하는 반짝임. 무작위 모드는 늘 더한다.")]
        [SerializeField] private VfxId perfectVfx = VfxId.HitSpark;
        [SerializeField, Range(0f, 1f)] private float perfectScore = 0.75f;
        [Tooltip("놓쳤을 때. 기획 「잘못된 입력·입력 놓침」은 파티클 없음(—)이라 기본은 비운다.")]
        [SerializeField] private VfxId missVfx = VfxId.None;
        [SerializeField, Min(0.1f)] private float vfxScale = 1f;

        [Header("시간")]
        [Tooltip("제한시간(초). 기획 CookingMiniGameData MG_POPUP_TOUCH = 10.")]
        [SerializeField, Min(1f)] private float timeLimit = 10f;

        [Header("고정 슬롯 모드 (+10/2) — 비우면 위 무작위 모드")]
        [Tooltip("하나씩 놓을 자리. 조개구이는 그릴 위 조개 자리.")]
        [SerializeField] private Transform[] slots;

        /// <summary>(+10/6) 고정 슬롯 자리. 컨트롤러가 그릴 위치를 잡을 때 읽는다(불꽃 · 증기).</summary>
        public IReadOnlyList<Transform> SlotAnchors => slots;

        [Tooltip("열리기 전 모습(닫힌 조개). 비우면 임시 모양.")]
        [SerializeField] private GameObject idlePrefab;

        [Tooltip("(+10/2 아트 적용) 자리별 모습 — i번 자리는 [i % 개수]. 칸이 비면 위 idlePrefab · popupPrefab, 그것도 비면 임시 모양.")]
        [SerializeField] private GameObject[] slotIdlePrefabs;
        [SerializeField] private GameObject[] slotOpenPrefabs;
        [Tooltip("탔을 때 갈아 끼울 모습. 비면 burntColor로 칠한다.")]
        [SerializeField] private GameObject[] slotBurntPrefabs;

        [Tooltip("열리는 시점 범위(초, Begin부터).")]
        [SerializeField] private Vector2 openTimeRange = new Vector2(3f, 9f);

        [Tooltip("열린 뒤 이 시간 안에 눌러야 한다(초). 지나면 탄다.")]
        [SerializeField, Min(0.1f)] private float reactionWindow = 2f;

        [Tooltip("반응 창 끝에 겨우 눌렀을 때 점수. 바로 누르면 1.")]
        [SerializeField, Range(0f, 1f)] private float minReactionScore = 0.3f;

        [Tooltip("임시 모양 색 (닫힘 / 열림 / 탐).")]
        [SerializeField] private Color idleColor = new Color(0.55f, 0.45f, 0.35f);
        [SerializeField] private Color openColor = new Color(1f, 0.85f, 0.6f);
        [SerializeField] private Color burntColor = new Color(0.15f, 0.12f, 0.1f);

        private enum SlotState { Waiting, Open, Done }

        private sealed class Slot
        {
            public Transform Anchor;
            public int Index;
            public GameObject Visual;
            public Collider Col;
            public SlotState State;
            public float OpenAt;
            public float OpenedAt;
            public float Score;
        }

        private readonly List<Slot> _slots = new();
        private float _elapsed;

        private bool SlotMode => slots != null && slots.Length > 0;

        private sealed class Popup
        {
            public Transform T;
            public Collider Col;
            public float Age;
            public float Life;
        }

        private readonly List<Popup> _alive = new();
        private float _spawnTimer;
        private float _timeLeft;
        private int _spawned;
        private int _popped;
        private bool _running;

        public bool IsFinished { get; private set; }
        public float Score => SlotMode ? SlotScore : _spawned > 0 ? (float)_popped / _spawned : 1f;

        private float SlotScore
        {
            get
            {
                if (_slots.Count == 0) return 0f;
                float sum = 0f;
                foreach (Slot sl in _slots) sum += sl.Score;
                return sum / _slots.Count;
            }
        }
        public int Popped => _popped;
        public int Spawned => _spawned;
        public float TimeLeft01 => timeLimit > 0f ? Mathf.Clamp01(_timeLeft / timeLimit) : 0f;

        /// <summary>터뜨린 수가 바뀔 때. (터뜨린 수, 나온 수)</summary>
        public event Action<int, int> OnProgress;

        public void Begin()
        {
            Cleanup();
            if (SlotMode)
            {
                BeginSlots();
                return;
            }
            if (surface == null)
            {
                Debug.LogError($"{name}: PopupTouchGame.surface가 비어 있다. 띄울 자리가 없다.", this);
                IsFinished = true;
                return;
            }

            _spawned = 0;
            _popped = 0;
            _timeLeft = timeLimit;
            _spawnTimer = 0.2f;
            IsFinished = false;
            _running = true;
            OnProgress?.Invoke(_popped, _spawned);
        }

        private void Update()
        {
            if (!_running) return;

            _timeLeft -= Time.deltaTime;
            if (SlotMode)
            {
                UpdateSlots();
                return;
            }

            // 시간이 끝나기 직전엔 새로 안 띄운다 — 누를 틈도 없이 놓친 걸로 세면 억울하다.
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f && _alive.Count < maxAlive && _timeLeft > lifetimeRange.x)
            {
                Spawn();
                _spawnTimer = spawnInterval;
            }

            HandleClick();

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Popup p = _alive[i];
                p.Age += Time.deltaTime;
                // 0.15초 동안 부풀고, 마지막 0.3초 동안 오그라든다.
                float grow = Mathf.Clamp01(p.Age / 0.15f);
                float fade = Mathf.Clamp01((p.Life - p.Age) / 0.3f);
                p.T.localScale = Vector3.one * (size * grow * Mathf.Lerp(0.4f, 1f, fade));

                if (p.Age >= p.Life)
                {
                    Vfx.Play(missVfx, p.T.position, vfxScale);   // (+10/6)
                    Destroy(p.T.gameObject);   // 놓쳤다
                    _alive.RemoveAt(i);
                    OnProgress?.Invoke(_popped, _spawned);
                }
            }

            if (_timeLeft <= 0f) Finish();
        }

        private void HandleClick()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null || !mouse.leftButton.wasPressedThisFrame) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
            // 냄비 콜라이더에 가려도 거품을 맞힌 게 있으면 그걸 쓴다.
            for (int h = 0; h < hits.Length; h++)
            {
                for (int i = 0; i < _alive.Count; i++)
                {
                    if (hits[h].collider != _alive[i].Col) continue;
                    // (+10/2, 이슈 84) 터지는 연출이 있으면 지우기 전에 튼다.
                    PopupBurst burst = _alive[i].T.GetComponentInChildren<PopupBurst>();
                    if (burst != null) burst.Burst();
                    PlayHit(_alive[i].T.position, 1f);   // (+10/6)
                    Destroy(_alive[i].T.gameObject);
                    _alive.RemoveAt(i);
                    _popped++;
                    OnProgress?.Invoke(_popped, _spawned);
                    return;
                }
            }
        }

        private void Spawn()
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 pos = surface.position + new Vector3(r.x, heightOffset, r.y);

            GameObject go;
            if (popupPrefab != null)
            {
                go = Instantiate(popupPrefab, pos, Quaternion.identity, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform, true);
                go.transform.position = pos;
            }
            go.transform.localScale = Vector3.zero;

            Collider col = go.GetComponentInChildren<Collider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;   // 플레이어·직원 물리와 엉키지 않게. 클릭 레이는 트리거도 맞힌다.

            _alive.Add(new Popup
            {
                T = go.transform,
                Col = col,
                Life = UnityEngine.Random.Range(lifetimeRange.x, Mathf.Max(lifetimeRange.x, lifetimeRange.y)),
            });
            _spawned++;
            OnProgress?.Invoke(_popped, _spawned);
        }

        // ── 고정 슬롯 모드 (+10/2) ─────────────────────────────────────

        private void BeginSlots()
        {
            _spawned = 0;
            _popped = 0;
            _elapsed = 0f;
            _timeLeft = timeLimit;
            // 열리고도 누를 틈은 있게 — 제한시간 끝에 열리면 누를 수가 없다.
            float latest = Mathf.Max(openTimeRange.x, timeLimit - reactionWindow);
            float hi = Mathf.Min(Mathf.Max(openTimeRange.x, openTimeRange.y), latest);
            for (int i = 0; i < slots.Length; i++)
            {
                Transform anchor = slots[i];
                if (anchor == null) continue;
                var sl = new Slot
                {
                    Anchor = anchor,
                    Index = i,
                    State = SlotState.Waiting,
                    OpenAt = UnityEngine.Random.Range(openTimeRange.x, hi),
                };
                ShowSlot(sl, Pick(slotIdlePrefabs, i, idlePrefab), idleColor, false);
                _slots.Add(sl);
                _spawned++;
            }
            IsFinished = false;
            _running = true;
            OnProgress?.Invoke(_popped, _spawned);
        }

        private void UpdateSlots()
        {
            _elapsed += Time.deltaTime;

            foreach (Slot sl in _slots)
            {
                if (sl.State == SlotState.Waiting && _elapsed >= sl.OpenAt)
                {
                    sl.State = SlotState.Open;
                    sl.OpenedAt = _elapsed;
                    ShowSlot(sl, Pick(slotOpenPrefabs, sl.Index, popupPrefab), openColor, true);
                }
                else if (sl.State == SlotState.Open && _elapsed - sl.OpenedAt > reactionWindow)
                {
                    sl.State = SlotState.Done;   // 탔다
                    sl.Score = 0f;
                    if (sl.Anchor != null) Vfx.Play(missVfx, sl.Anchor.position, vfxScale);   // (+10/6)
                    GameObject burnt = Pick(slotBurntPrefabs, sl.Index, null);
                    if (burnt != null) ShowSlot(sl, burnt, burntColor, false);
                    else Tint(sl.Visual, burntColor);
                    if (sl.Col != null) sl.Col.enabled = false;
                    OnProgress?.Invoke(_popped, _spawned);
                }
            }

            HandleSlotClick();

            bool allDone = _slots.TrueForAll(x => x.State == SlotState.Done);
            if (allDone || _timeLeft <= 0f)
            {
                _running = false;
                IsFinished = true;   // 모습은 남겨둔다 — 다음 단계로 넘어가며 패널이 꺼질 때 같이 사라진다
            }
        }

        private void HandleSlotClick()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null || !mouse.leftButton.wasPressedThisFrame) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                Slot sl = _slots.Find(x => x.State == SlotState.Open && x.Col == hit.collider);
                if (sl == null) continue;

                float t = Mathf.Clamp01((_elapsed - sl.OpenedAt) / reactionWindow);
                sl.Score = Mathf.Lerp(1f, minReactionScore, t);
                sl.State = SlotState.Done;
                PlayHit(sl.Anchor != null ? sl.Anchor.position : hit.point, sl.Score);   // (+10/6)
                if (sl.Col != null) sl.Col.enabled = false;
                _popped++;
                OnProgress?.Invoke(_popped, _spawned);
                return;
            }
        }

        /// <summary>슬롯 모습을 갈아 끼운다. 프리팹이 없으면 납작한 구에 색.</summary>
        private void ShowSlot(Slot sl, GameObject prefab, Color placeholderColor, bool clickable)
        {
            if (sl.Visual != null) Destroy(sl.Visual);

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, sl.Anchor.position, sl.Anchor.rotation, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(sl.Anchor.position, sl.Anchor.rotation);
                go.transform.localScale = clickable
                    ? new Vector3(size * 1.3f, size * 0.7f, size * 1.1f)
                    : new Vector3(size * 1.2f, size * 0.4f, size);
                Tint(go, placeholderColor);
            }

            Collider col = go.GetComponentInChildren<Collider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.enabled = clickable;

            sl.Visual = go;
            sl.Col = col;
        }

        private static GameObject Pick(GameObject[] perSlot, int index, GameObject fallback)
        {
            if (perSlot == null || perSlot.Length == 0) return fallback;
            GameObject p = perSlot[index % perSlot.Length];
            return p != null ? p : fallback;
        }

        private static void Tint(GameObject go, Color color)
        {
            if (go == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.material.color = color;
        }

        /// <summary>(+10/6) 성공 — 링은 늘, 반짝임은 점수가 perfectScore 이상일 때만(보통 판정은 링만).</summary>
        private void PlayHit(Vector3 at, float score)
        {
            Vfx.Play(hitRingVfx, at, vfxScale);
            if (score >= perfectScore) Vfx.Play(perfectVfx, at + Vector3.up * 0.05f, vfxScale);
        }

        private void Finish()
        {
            _running = false;
            IsFinished = true;
            Cleanup();
        }

        private void OnDisable() => Cleanup();

        private void Cleanup()
        {
            foreach (var p in _alive)
                if (p.T != null) Destroy(p.T.gameObject);
            _alive.Clear();

            foreach (Slot sl in _slots)
                if (sl.Visual != null) Destroy(sl.Visual);
            _slots.Clear();
        }
    }
}
