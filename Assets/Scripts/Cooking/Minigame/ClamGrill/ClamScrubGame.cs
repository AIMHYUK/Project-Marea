using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Marea.Cooking
{
    /// <summary>
    /// 조개구이 1단계 — 싱크대에서 조개 닦기. 기획 MG_SURFACE_DRAG. (+10/2, 이슈 85)
    ///
    /// 조개는 움직이지 않는다. 조개 위에서 드래그한 거리만 쌓이고, 쌓인 양이 문턱을 넘을 때마다
    /// 다음 단계 모습(더러움 1 → 깨끗함 4)으로 바뀐다. 그래서 조개마다 <see cref="ClamScrubTarget"/>
    /// (IDragHandler)이 붙어 이동량을 여기로 넘긴다 — 집어서 옮기는 드래그가 아니다.
    /// 드래그 이벤트는 PhysicsRaycaster가 켜져 있어야 온다 (BaseCookingMinigame이 단계 동안 켠다).
    ///
    /// 단계 진행 · 결과는 모른다 — 컨트롤러가 Begin으로 시작하고 IsFinished/Score를 읽는다 (CatchGame과 같은 모양).
    /// </summary>
    public class ClamScrubGame : MonoBehaviour
    {
        [Header("조개")]
        [Tooltip("조개를 놓을 자리. 개수만큼 조개가 놓인다.")]
        [SerializeField] private Transform[] slots;

        [Tooltip("닦인 정도별 모습. 0번이 가장 더러운 것, 마지막이 깨끗한 것. 비우면 임시 모양(납작한 구 · 색)으로 만든다.")]
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

        private sealed class Clam
        {
            public Transform Slot;
            public GameObject Visual;
            public int Stage;
            public float Accum;
        }

        private readonly List<Clam> _clams = new();
        private float _timeLeft;
        private bool _running;

        public bool IsFinished { get; private set; }
        public int StageCount => stagePrefabs != null && stagePrefabs.Length > 0 ? stagePrefabs.Length : placeholderColors.Length;
        public float TimeLeft01 => timeLimit > 0f ? Mathf.Clamp01(_timeLeft / timeLimit) : 0f;

        /// <summary>닦인 정도 평균 (0 = 전부 더러움, 1 = 전부 깨끗함).</summary>
        public float Score
        {
            get
            {
                if (_clams.Count == 0 || StageCount <= 1) return 1f;
                float sum = 0f;
                foreach (Clam c in _clams) sum += (float)c.Stage / (StageCount - 1);
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

            foreach (Transform slot in slots)
            {
                if (slot == null) continue;
                var clam = new Clam { Slot = slot };
                _clams.Add(clam);
                ShowStage(clam);
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
                foreach (Clam c in _clams) if (c.Stage >= StageCount - 1) n++;
                return n;
            }
        }

        private void Update()
        {
            if (!_running) return;
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f || CleanCount == _clams.Count) Finish();
        }

        /// <summary>ClamScrubTarget이 드래그 이동량(화면 px)을 넘긴다.</summary>
        internal void AddScrub(ClamScrubTarget target, float pixels)
        {
            if (!_running) return;
            Clam clam = _clams.Find(c => c.Visual != null && c.Visual == target.gameObject);
            if (clam == null || clam.Stage >= StageCount - 1) return;

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
            if (stagePrefabs != null && clam.Stage < stagePrefabs.Length && stagePrefabs[clam.Stage] != null)
            {
                go = Instantiate(stagePrefabs[clam.Stage], clam.Slot.position, clam.Slot.rotation, transform);
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

            if (go.GetComponentInChildren<Collider>() == null) go.AddComponent<SphereCollider>();
            var target = go.GetComponent<ClamScrubTarget>();
            if (target == null) target = go.AddComponent<ClamScrubTarget>();
            target.Game = this;
            clam.Visual = go;
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
            foreach (Clam c in _clams) if (c.Visual != null) Destroy(c.Visual);
            _clams.Clear();
        }
    }
}
