using System;
using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    /// <summary>
    /// 조개구이 1단계 — 싱크대에서 조개 닦기. 기획 MG_SURFACE_DRAG. (+10/2, 이슈 85)
    ///
    /// 조개는 움직이지 않는다. 조개 위에서 드래그한 거리만 쌓이고, 쌓인 양이 문턱을 넘을 때마다
    /// 다음 단계 모습(더러움 1 → 깨끗함 4)으로 바뀐다 — 집어서 옮기는 드래그가 아니다.
    ///
    /// (+10/2) 입력은 마우스를 직접 읽는다: 누르고 있는 동안 매 프레임 커서 아래 조개를 레이로 찾아
    /// 마우스 이동량을 쌓는다. 예전엔 조개마다 IDragHandler를 붙였는데, 단계가 넘어갈 때 모습을 갈아 끼우면
    /// 드래그가 처음 누른 오브젝트에 묶여 있어 끊겼다(떼고 다시 눌러야 했다). 판정 콜라이더도 모습보다 넉넉하게.
    ///
    /// (+10/5) 솔(brush)이 있으면 커서를 따라 조개 위를 오가며 누를 때 내려가 닿는다.
    ///
    /// 단계 진행 · 결과는 모른다 — 컨트롤러가 Begin으로 시작하고 IsFinished/Score를 읽는다 (CatchGame과 같은 모양).
    /// </summary>
    public class ClamScrubGame : MonoBehaviour
    {
        /// <summary>조개 한 종류의 닦인 정도별 모습 (+10/2 아트 적용). 0번이 가장 더러운 것.</summary>
        [Serializable]
        public struct StageSet
        {
            public string label;
            public GameObject[] stages;
        }

        [Header("조개")]
        [Tooltip("조개를 놓을 자리. 개수만큼 조개가 놓인다.")]
        [SerializeField] private Transform[] slots;

        [Tooltip("자리별 조개 종류. i번 자리는 slotStages[i % 개수]를 쓴다 (홍합 · 가리비 · 전복). 비우면 stagePrefabs.")]
        [SerializeField] private StageSet[] slotStages;

        [Tooltip("(+10/6) 조개 모습에 곱하는 크기. 프리팹은 넓이 기준으로 맞춰 두고, 놓이는 자리(싱크대 · 그릴 · 접시) 크기는 단계마다 여기서.")]
        [SerializeField, Min(0.01f)] private float clamScale = 1f;

        [Tooltip("(+10/6) 닦는 동안 잠시 끌 것 — 개수대 안에 놓인 장식 접시처럼 조개 자리를 가리는 씬 오브젝트. 끝나면 원래대로 켠다.")]
        [SerializeField] private GameObject[] hideWhileScrubbing;
        private readonly List<GameObject> _hidden = new();

        [Tooltip("모든 자리가 같은 조개일 때. 0번이 가장 더러운 것, 마지막이 깨끗한 것. 둘 다 비우면 임시 모양(납작한 구 · 색).")]
        [SerializeField] private GameObject[] stagePrefabs;

        [Tooltip("임시 모양일 때 단계별 색 (더러움 → 깨끗함). stagePrefabs가 있으면 안 쓴다.")]
        [SerializeField] private Color[] placeholderColors =
        {
            new(0.35f, 0.25f, 0.15f), new(0.55f, 0.45f, 0.32f), new(0.78f, 0.70f, 0.58f), new(0.96f, 0.93f, 0.86f),
        };

        [Tooltip("임시 모양 크기(m).")]
        [SerializeField] private Vector3 placeholderSize = new(0.22f, 0.07f, 0.18f);

        [Header("닦기")]
        // (+10/6) 화면 px로 세다가 월드 거리(m)로 바꿨다 — px은 카메라를 당기거나 해상도가 높으면 같은 손짓이 더 길게 잡혀 너무 빨리 닦였다.
        [Tooltip("한 단계 넘어가는 데 필요한 문지른 거리(m) — 솔이 조개 위에서 실제로 움직인 거리. 조개 폭 약 25cm라 1.5면 6번쯤 왕복.")]
        [SerializeField, Min(0.05f)] private float scrubPerStage = 1.5f;

        [Tooltip("제한시간(초). 기획 MG_SURFACE_DRAG = 10.")]
        [SerializeField, Min(1f)] private float timeLimit = 10f;

        [Tooltip("판정 범위를 모습보다 이만큼 키운다. 조금 빗나가도 닦인다.")]
        [SerializeField, Min(1f)] private float hitPadding = 1.3f;

        [Header("연출 (+10/6, 이슈 117) — 기획 「재료 씻기」 VFX_04")]
        [Tooltip("문지르는 동안 솔 자리에서 튀는 물방울.")]
        [SerializeField] private VfxId scrubSplashVfx = VfxId.Splash;
        [SerializeField] private Color waterTint = new Color(0.8f, 0.92f, 1f, 1f);
        [SerializeField, Min(0.05f)] private float splashInterval = 0.25f;
        [Tooltip("한 단계 깨끗해질 때 크게 한 번.")]
        [SerializeField, Min(0.1f)] private float stageSplashScale = 1.4f;

        [Header("단계 바뀔 때 거품 (+10/6)")]
        [Tooltip("한 단계 깨끗해질 때 조개를 덮는 거품. 이게 커진 뒤(coverSwapDelay)에 모습을 바꿔서 바뀌는 순간을 가린다. None이면 바로 바꾼다.")]
        [SerializeField] private VfxId stageCoverVfx = VfxId.SmokePoofDense;
        [SerializeField, Min(0.01f)] private float stageCoverScale = 0.55f;
        [SerializeField] private Color stageCoverTint = new Color(1f, 1f, 1f, 0.95f);
        [Tooltip("거품을 터뜨리고 모습을 바꾸기까지(초). 그동안 옛 모습이 squashScale까지 오그라든다.")]
        [SerializeField, Min(0f)] private float coverSwapDelay = 0.1f;
        [Tooltip("(+10/6) 크기 팝 — 옛 모습이 오그라드는 크기(원래의 비율). 새 모습도 이 크기에서 튀어나온다. 1이면 팝 없음.")]
        [SerializeField, Range(0.1f, 1f)] private float squashScale = 0.6f;
        [Tooltip("새 모습이 튀어나와 원래 크기로 돌아오는 시간(초).")]
        [SerializeField, Min(0.01f)] private float popTime = 0.18f;
        [Tooltip("튀어나올 때 원래 크기를 넘었다 돌아오는 정도(1.1이면 10% 넘게).")]
        [SerializeField, Min(1f)] private float popOvershoot = 1.12f;

        private float _nextSplash;

        [Header("소리 (+10/6, 이슈 117) — 기획 sfx_wash_loop")]
        [Tooltip("조개를 문지르는 동안.")]
        [SerializeField] private AudioClip washLoop;
        [SerializeField, Range(0f, 1f)] private float washVolume = 0.6f;
        [Tooltip("문지르기를 멈추고 이만큼(초) 지나면 소리를 끊는다 — 마우스가 잠깐 멈출 때마다 끊기지 않게.")]
        [SerializeField, Min(0f)] private float washHold = 0.15f;

        private SoundLoop _wash;
        private float _lastScrub = float.NegativeInfinity;
        private Clam _scrubClam;          // (+10/6) 지난 프레임에 문지르던 조개와 그 위 지점 — 거리를 잴 기준
        private Vector3 _scrubPoint;

        [Header("솔 (+10/5)")]
        [Tooltip("조개를 닦는 솔. 기준점 = 솔털 끝 가운데. 커서를 따라 조개 위를 오가고, 누르면 내려가 닿는다. " +
                 "비우면 솔 없이 닦인다.")]
        [SerializeField] private Transform brush;
        [Tooltip("누르고 있을 때 솔 기준점이 조개 자리보다 이만큼 위(m) — 조개 윗면에 닿을 높이.")]
        [SerializeField] private float brushContact = 0.04f;
        [Tooltip("안 누를 때 그보다 이만큼 더 든다(m).")]
        [SerializeField, Min(0f)] private float brushLift = 0.08f;
        [Tooltip("커서를 따라가는 빠르기. 클수록 바짝 붙는다.")]
        [SerializeField, Min(1f)] private float brushFollow = 18f;
        [Tooltip("문지르는 방향으로 기우는 최대 각도. 솔털이 끌리는 느낌.")]
        [SerializeField, Range(0f, 45f)] private float brushTilt = 15f;

        private sealed class Clam
        {
            public Transform Slot;
            public GameObject[] Prefabs;   // 이 조개의 단계 모습. 비면 임시 모양
            public GameObject Visual;
            public Collider Hit;           // 판정용 — 모습을 갈아 끼워도 자리에 그대로 남는다
            public int Stage;
            public float Accum;
            public Coroutine Swap;         // (+10/6) 진행 중인 크기 팝 — 단계가 또 오르면 끊고 새로 한다
        }

        private readonly List<Clam> _clams = new();
        private float _timeLeft;
        private bool _running;

        private Quaternion _brushRest;   // 솔의 처음 회전 — 기울기는 이 위에 얹는다
        private Vector3 _brushVelocity;
        private bool _hasBrushRest;

        public bool IsFinished { get; private set; }
        private int StageCountOf(Clam c) => c.Prefabs != null && c.Prefabs.Length > 0 ? c.Prefabs.Length : placeholderColors.Length;
        public float TimeLeft01 => timeLimit > 0f ? Mathf.Clamp01(_timeLeft / timeLimit) : 0f;

        /// <summary>닦인 정도 평균 (0 = 전부 더러움, 1 = 전부 깨끗함).</summary>
        public float Score
        {
            get
            {
                if (_clams.Count == 0) return 1f;
                float sum = 0f;
                foreach (Clam c in _clams) sum += StageCountOf(c) <= 1 ? 1f : (float)c.Stage / (StageCountOf(c) - 1);
                return sum / _clams.Count;
            }
        }

        /// <summary>다 닦인 조개 수가 바뀔 때. (깨끗한 수, 전체)</summary>
        public event Action<int, int> OnProgress;

        public void Begin()
        {
            Cleanup();
            if (slots == null || slots.Length == 0)
            {
                Debug.LogError($"{name}: ClamScrubGame.slots가 비어 있다. 조개를 놓을 자리가 없다.", this);
                IsFinished = true;
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                Transform slot = slots[i];
                if (slot == null) continue;
                GameObject[] prefabs = slotStages != null && slotStages.Length > 0
                    ? slotStages[i % slotStages.Length].stages
                    : stagePrefabs;
                var clam = new Clam { Slot = slot, Prefabs = prefabs };
                _clams.Add(clam);
                ShowStage(clam);
                clam.Hit = MakeHitBox(clam);
            }

            // (+10/6) 켜져 있던 것만 끄고 기억한다 — 원래 꺼져 있던 걸 끝날 때 켜 버리지 않게.
            if (hideWhileScrubbing != null)
                foreach (GameObject go in hideWhileScrubbing)
                    if (go != null && go.activeSelf) { go.SetActive(false); _hidden.Add(go); }

            _timeLeft = timeLimit;
            IsFinished = false;
            _running = true;
            PlaceBrush();
            OnProgress?.Invoke(CleanCount, _clams.Count);
        }

        private int CleanCount
        {
            get
            {
                int n = 0;
                foreach (Clam c in _clams) if (c.Stage >= StageCountOf(c) - 1) n++;
                return n;
            }
        }

        private void Update()
        {
            if (!_running) return;
            _timeLeft -= Time.deltaTime;
            ReadMouse();
            MoveBrush();
            SoundManager.Hold(ref _wash, washLoop, Time.time - _lastScrub <= washHold, washVolume);   // (+10/6)
            if (_timeLeft <= 0f || CleanCount == _clams.Count) Finish();
        }

        /// <summary>조개들 가운데 위 든 높이에 솔을 둔다.</summary>
        private void PlaceBrush()
        {
            if (brush == null) return;
            if (!_hasBrushRest) { _brushRest = brush.rotation; _hasBrushRest = true; }
            brush.SetPositionAndRotation(SlotCenter() + Vector3.up * (brushContact + brushLift), _brushRest);
            _brushVelocity = Vector3.zero;
        }

        private Vector3 SlotCenter()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Transform s in slots) if (s != null) { sum += s.position; n++; }
            return n > 0 ? sum / n : transform.position;
        }

        /// <summary>
        /// (+10/5) 솔이 커서를 따라간다 — 누르면 조개 윗면 높이로 내려가 닿고, 떼면 든다.
        /// 움직이는 쪽으로 살짝 기울어 솔털이 끌리는 느낌을 준다. 판정과는 상관없다(판정은 커서 아래 조개).
        /// </summary>
        private void MoveBrush()
        {
            if (brush == null) return;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null) return;

            bool pressed = mouse.leftButton.isPressed;
            float slotY = SlotCenter().y;
            float y = slotY + brushContact + (pressed ? 0f : brushLift);
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Vector3 goal = brush.position;
            if (new Plane(Vector3.up, new Vector3(0f, slotY + brushContact, 0f)).Raycast(ray, out float t))
                goal = ray.GetPoint(t);
            goal.y = y;

            float k = 1f - Mathf.Exp(-brushFollow * Time.deltaTime);
            Vector3 next = Vector3.Lerp(brush.position, goal, k);
            Vector3 v = Time.deltaTime > 0f ? (next - brush.position) / Time.deltaTime : Vector3.zero;
            _brushVelocity = Vector3.Lerp(_brushVelocity, new Vector3(v.x, 0f, v.z), k);

            // 끌리는 쪽 반대로 솔 윗부분이 앞서간다 — 진행 방향 축으로 기운다. 초속 1m에서 최대.
            Vector3 axis = Vector3.Cross(Vector3.up, _brushVelocity);
            float angle = pressed ? Mathf.Clamp01(_brushVelocity.magnitude) * brushTilt : 0f;
            Quaternion tilt = axis.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(angle, axis.normalized) : Quaternion.identity;
            brush.SetPositionAndRotation(next, Quaternion.Slerp(brush.rotation, tilt * _brushRest, k));
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null || !mouse.leftButton.isPressed) { _scrubClam = null; return; }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide))
            {
                Clam clam = _clams.Find(c => c.Hit == hit.collider);
                if (clam == null) continue;
                // 같은 조개를 이어서 문지를 때만 지난 지점과의 수평 거리를 더한다(다른 조개로 넘어간 첫 프레임은 기준만 잡는다).
                if (clam == _scrubClam)
                {
                    Vector3 d = hit.point - _scrubPoint;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1e-8f) AddScrub(clam, d.magnitude);
                }
                _scrubClam = clam;
                _scrubPoint = hit.point;
                return;
            }
            _scrubClam = null;
        }

        private void AddScrub(Clam clam, float meters)
        {
            if (clam.Stage >= StageCountOf(clam) - 1) return;

            clam.Accum += meters;
            _lastScrub = Time.time;   // (+10/6) 문지르는 소리
            Vector3 at = brush != null ? brush.position : clam.Slot.position;
            if (Time.time >= _nextSplash)
            {
                _nextSplash = Time.time + splashInterval;
                Vfx.Play(scrubSplashVfx, at, 0.6f, waterTint);   // (+10/6)
            }
            if (clam.Accum < scrubPerStage) return;

            clam.Accum = 0f;
            clam.Stage++;
            Vfx.Play(scrubSplashVfx, clam.Slot.position, stageSplashScale, waterTint);   // (+10/6) 한 단계 깨끗해짐
            // (+10/6) 거품으로 덮고, 커진 뒤에 모습을 바꾼다. 점수 · 진행은 Stage로 바로 반영된다.
            if (stageCoverVfx != VfxId.None)
            {
                Vfx.Play(stageCoverVfx, clam.Slot.position + Vector3.up * 0.03f, stageCoverScale, stageCoverTint);
                if (clam.Swap != null) StopCoroutine(clam.Swap);
                clam.Swap = StartCoroutine(PopToStage(clam));
            }
            else ShowStage(clam);
            OnProgress?.Invoke(CleanCount, _clams.Count);
        }

        /// <summary>
        /// (+10/6) 크기 팝 — 옛 모습이 coverSwapDelay 동안 squashScale까지 오그라들고, 그 순간 갈아 끼운 새 모습이
        /// squashScale에서 popOvershoot까지 튀었다가 원래 크기로 돌아온다. 바뀌는 순간이 움직임과 거품에 묻힌다.
        /// 패널이 꺼지면(Cleanup) 코루틴도 같이 멈춘다.
        /// </summary>
        private IEnumerator PopToStage(Clam clam)
        {
            GameObject old = clam.Visual;
            Vector3 oldFull = old != null ? old.transform.localScale : Vector3.one;
            for (float t = 0f; t < coverSwapDelay; t += Time.deltaTime)
            {
                if (old != null) old.transform.localScale = oldFull * Mathf.Lerp(1f, squashScale, t / coverSwapDelay);
                yield return null;
            }
            if (!_clams.Contains(clam)) yield break;

            ShowStage(clam);
            Transform shown = clam.Visual != null ? clam.Visual.transform : null;
            if (shown == null) { clam.Swap = null; yield break; }
            Vector3 full = shown.localScale;
            for (float t = 0f; t < popTime; t += Time.deltaTime)
            {
                if (shown == null) yield break;
                shown.localScale = full * PopCurve(t / popTime);
                yield return null;
            }
            if (shown != null) shown.localScale = full;
            clam.Swap = null;
        }

        // squashScale → popOvershoot(앞 60%) → 1(뒤 40%). 부드럽게.
        private float PopCurve(float u)
        {
            if (u < 0.6f) return Mathf.Lerp(squashScale, popOvershoot, Mathf.SmoothStep(0f, 1f, u / 0.6f));
            return Mathf.Lerp(popOvershoot, 1f, Mathf.SmoothStep(0f, 1f, (u - 0.6f) / 0.4f));
        }

        /// <summary>지금 단계 모습으로 갈아 끼운다. 프리팹이 없으면 임시 모양.</summary>
        private void ShowStage(Clam clam)
        {
            if (clam.Visual != null) Destroy(clam.Visual);

            GameObject go;
            if (clam.Prefabs != null && clam.Stage < clam.Prefabs.Length && clam.Prefabs[clam.Stage] != null)
            {
                go = Instantiate(clam.Prefabs[clam.Stage], clam.Slot.position, clam.Slot.rotation, transform);
                go.transform.localScale *= clamScale;   // (+10/6)
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Clam_Stage{clam.Stage + 1}";
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(clam.Slot.position, clam.Slot.rotation);
                go.transform.localScale = placeholderSize;
                var r = go.GetComponent<Renderer>();
                if (r != null && clam.Stage < placeholderColors.Length) r.material.color = placeholderColors[clam.Stage];
            }

            // 모습의 콜라이더는 끈다 — 판정은 자리에 고정된 Hit 상자로 한다(갈아 끼워도 안 끊긴다).
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            clam.Visual = go;
        }

        /// <summary>첫 모습 크기보다 hitPadding만큼 큰 판정 상자를 자리에 둔다.</summary>
        private Collider MakeHitBox(Clam clam)
        {
            var box = new GameObject("ScrubHit").AddComponent<BoxCollider>();
            box.transform.SetParent(transform, false);
            box.transform.SetPositionAndRotation(clam.Slot.position, clam.Slot.rotation);
            box.isTrigger = true;

            Bounds b = new Bounds(clam.Slot.position, placeholderSize);
            bool has = false;
            if (clam.Visual != null)
                foreach (Renderer r in clam.Visual.GetComponentsInChildren<Renderer>())
                {
                    if (!has) { b = r.bounds; has = true; }
                    else b.Encapsulate(r.bounds);
                }
            box.center = box.transform.InverseTransformPoint(b.center);
            box.size = box.transform.InverseTransformVector(b.size * hitPadding);
            box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
            return box;
        }

        private void Finish()
        {
            _running = false;
            IsFinished = true;
            _wash.Stop();
            // (+10/6) 숨긴 접시는 여기서 안 켠다 — 끝나고도 stepTransitionDelay 동안 싱크대 화면이라 다시 보인다.
            // 2단계가 시작되며 이 패널이 꺼질 때(OnDisable) 켠다.
        }

        private void OnDisable()
        {
            _running = false;
            _wash.Stop();
            ShowHidden();
            Cleanup();
        }

        private void ShowHidden()
        {
            foreach (GameObject go in _hidden) if (go != null) go.SetActive(true);
            _hidden.Clear();
        }

        private void Cleanup()
        {
            foreach (Clam c in _clams)
            {
                if (c.Visual != null) Destroy(c.Visual);
                if (c.Hit != null) Destroy(c.Hit.gameObject);
            }
            _clams.Clear();
        }
    }
}
