using System;
using System.Collections.Generic;
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
        [Tooltip("한 단계 넘어가는 데 필요한 드래그 거리(화면 px). 한 번 문지르기가 대략 100~150px — 2~3번이면 넘어간다.")]
        [SerializeField, Min(10f)] private float dragPerStage = 300f;

        [Tooltip("제한시간(초). 기획 MG_SURFACE_DRAG = 10.")]
        [SerializeField, Min(1f)] private float timeLimit = 10f;

        [Tooltip("판정 범위를 모습보다 이만큼 키운다. 조금 빗나가도 닦인다.")]
        [SerializeField, Min(1f)] private float hitPadding = 1.3f;

        private sealed class Clam
        {
            public Transform Slot;
            public GameObject[] Prefabs;   // 이 조개의 단계 모습. 비면 임시 모양
            public GameObject Visual;
            public Collider Hit;           // 판정용 — 모습을 갈아 끼워도 자리에 그대로 남는다
            public int Stage;
            public float Accum;
        }

        private readonly List<Clam> _clams = new();
        private float _timeLeft;
        private bool _running;

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

            _timeLeft = timeLimit;
            IsFinished = false;
            _running = true;
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
            if (_timeLeft <= 0f || CleanCount == _clams.Count) Finish();
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null || !mouse.leftButton.isPressed) return;

            float pixels = mouse.delta.ReadValue().magnitude;
            if (pixels <= 0f) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide))
            {
                Clam clam = _clams.Find(c => c.Hit == hit.collider);
                if (clam == null) continue;
                AddScrub(clam, pixels);
                return;
            }
        }

        private void AddScrub(Clam clam, float pixels)
        {
            if (clam.Stage >= StageCountOf(clam) - 1) return;

            clam.Accum += pixels;
            if (clam.Accum < dragPerStage) return;

            clam.Accum = 0f;
            clam.Stage++;
            ShowStage(clam);
            OnProgress?.Invoke(CleanCount, _clams.Count);
        }

        /// <summary>지금 단계 모습으로 갈아 끼운다. 프리팹이 없으면 임시 모양.</summary>
        private void ShowStage(Clam clam)
        {
            if (clam.Visual != null) Destroy(clam.Visual);

            GameObject go;
            if (clam.Prefabs != null && clam.Stage < clam.Prefabs.Length && clam.Prefabs[clam.Stage] != null)
            {
                go = Instantiate(clam.Prefabs[clam.Stage], clam.Slot.position, clam.Slot.rotation, transform);
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
        }

        private void OnDisable()
        {
            _running = false;
            Cleanup();
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
