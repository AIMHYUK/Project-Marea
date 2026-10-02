using System.Collections;
using Marea.Core;
using Marea.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace Marea.Field
{
    /// <summary>
    /// 탐사정 하나의 파견 · 타이머 · 귀환 · 보상. (+9/28, 이슈 73)
    ///
    /// 선착장(ExpeditionDock)은 클릭을 받아 여기로 넘기기만 한다.
    /// 기획 8페이지의 첫 구현 그대로다 — 귀환한 탐사정을 클릭하면 보상이 자동으로 들어오고,
    /// 연출은 배가 월드에서 사라졌다가 다시 나타나는 것. 결과창은 없다.
    ///
    /// (+9/30) 떠날 때만 연출이 있다 — 배가 바라보는 방향으로 가속하며 나아가다 끝에 가라앉아 사라진다.
    /// 타이머는 파견한 순간부터 돈다. 연출보다 탐사가 먼저 끝나면 연출을 끊고 제자리로 돌려놓는다.
    /// 귀환은 여전히 제자리에 바로 나타난다.
    ///
    /// 저장·로드는 없다. 씬을 다시 켜면 대기 상태다 (보류).
    /// </summary>
    public class ExpeditionManager : MonoBehaviour
    {
        public enum State { Idle, Away, Returned }

        [Tooltip("갈 수 있는 해역. 지역 선택 창에 이 순서대로 뜬다.")]
        [FormerlySerializedAs("regions")]
        [SerializeField] private ExpeditionAreaData[] areas;

        [Tooltip("탐사 중에 사라질 배. 선착장 발판은 여기 넣지 않는다.")]
        [SerializeField] private GameObject boat;

        // (+9/28) 월드 텍스트(TMP)를 쓰다가 화면 라벨(WorldLabelUI)로 바꿨다 — junhyuk_main 에서
        // 아트 천막 아래 가려 안 보였다. 여기는 라벨을 띄울 월드 위치만 든다.
        [Tooltip("남은 시간 · 귀환 라벨을 띄울 위치. 비우면 이 오브젝트 위 2.5m.")]
        [SerializeField] private Transform labelAnchor;

        [Tooltip("귀환했을 때 켜는 표식(느낌표).")]
        [SerializeField] private GameObject returnedMarker;

        [Header("떠날 때 연출 (+9/30)")]
        [Tooltip("파견하고 배가 사라질 때까지 걸리는 초.")]
        [SerializeField, Min(0f)] private float departSeconds = 2.5f;

        [Tooltip("그동안 배가 바라보는 방향으로 나아가는 거리(m). 서서히 가속한다.")]
        [SerializeField, Min(0f)] private float departDistance = 8f;

        [Tooltip("끝의 몇 초 동안 가라앉는가. departSeconds보다 길면 처음부터 가라앉는다.")]
        [SerializeField, Min(0f)] private float sinkSeconds = 1f;

        [Tooltip("가라앉는 깊이(m). 배 높이보다 깊어야 물속으로 완전히 들어간다.")]
        [SerializeField, Min(0f)] private float sinkDepth = 3f;

        private ExpeditionUI _ui;
        private WorldLabelUI _labels;
        private ExpeditionAreaData _current;
        private float _returnAt;
        private Vector3 _boatHome;      // 배의 제자리(로컬). 연출이 끝나거나 끊기면 여기로 돌린다.
        private Coroutine _depart;

        public State Current { get; private set; } = State.Idle;
        public ExpeditionAreaData[] Areas => areas;

        private void Awake()
        {
            _ui = FindAnyObjectByType<ExpeditionUI>(FindObjectsInactive.Include);
            _labels = FindAnyObjectByType<WorldLabelUI>(FindObjectsInactive.Include);

            if (areas == null || areas.Length == 0)
                Debug.LogError($"{name}: ExpeditionManager.areas가 비어 있다. 보낼 곳이 없다.", this);
            if (boat == null)
                Debug.LogError($"{name}: ExpeditionManager.boat가 비어 있다. 파견해도 배가 안 사라진다.", this);
            if (_ui == null)
                Debug.LogError($"{name}: 씬에 ExpeditionUI가 없다. 탐사정을 눌러도 지역 창이 안 뜬다.", this);
            if (_labels == null)
                Debug.LogError($"{name}: 씬에 WorldLabelUI가 없다. 남은 시간과 귀환 표시가 안 뜬다.", this);

            if (boat != null) _boatHome = boat.transform.localPosition;
            Show();
        }

        private void Update()
        {
            if (Current == State.Away && Time.time >= _returnAt)
            {
                Current = State.Returned;
                Show();
            }

            // 라벨은 보이고 싶은 프레임마다 부른다. 대기 중엔 안 불러서 저절로 꺼진다.
            if (_labels == null) return;
            if (Current == State.Away)
                _labels.Set(this, LabelPosition, TimeText.Format(_returnAt - Time.time));
            else if (Current == State.Returned)
                _labels.Set(this, LabelPosition, "귀환");
        }

        private Vector3 LabelPosition => labelAnchor != null ? labelAnchor.position : transform.position + Vector3.up * 2.5f;

        /// <summary>탐사 중에는 클릭을 안 받는다 (기획 8 파견 3번).</summary>
        public bool CanInteract => Current != State.Away;

        /// <summary>플레이어가 선착장에 도착했다. 대기면 지역 창, 귀환이면 보상.</summary>
        public void Interact()
        {
            switch (Current)
            {
                case State.Idle:
                    if (_ui != null) _ui.Open(this);
                    break;
                case State.Returned:
                    Collect();
                    break;
            }
        }

        /// <summary>이 해역으로 보낸다. 대기가 아니면 false.</summary>
        public bool TryDispatch(ExpeditionAreaData area)
        {
            if (Current != State.Idle || area == null) return false;

            _current = area;
            _returnAt = Time.time + area.DurationSeconds;
            Current = State.Away;
            Show();
            return true;
        }

        private void Collect()
        {
            Warehouse warehouse = Warehouse.Instance;
            if (warehouse == null)
            {
                // 받기 전에 멈춘다. 대기로 돌려버리면 보상이 그냥 사라진다.
                Debug.LogError($"{name}: 씬에 Warehouse가 없다. 보상을 받을 곳이 없어 귀환 상태로 둔다.", this);
                return;
            }

            // 보상 그룹에서 가중치로 한 줄을 뽑는다. 몇 번 뽑는지는 기획에 없어서 한 번이다 (ExpeditionRewardData).
            ExpeditionRewardData group = _current.RewardGroup;
            if (group != null && group.TryPick(Random.value, out ExpeditionRewardEntry picked))
            {
                int max = Mathf.Max(picked.amountMin, picked.amountMax);
                warehouse.Add(picked.item, Random.Range(picked.amountMin, max + 1));
            }
            else
            {
                // 빈손으로 대기로 돌린다. 그룹이 비었다고 탐사정을 귀환 상태에 묶어두면 다시 못 보낸다.
                Debug.LogError($"{name}: '{_current.name}'의 보상 그룹이 비었거나 가중치 합이 0이다. 보상 없이 대기로 돌린다.", _current);
            }

            _current = null;
            Current = State.Idle;
            Show();
        }

        private void Show()
        {
            if (boat != null)
            {
                if (Current == State.Away)
                {
                    // 떠나는 중이면 그대로 둔다. 이미 꺼져 있으면(연출이 끝났으면) 다시 틀지 않는다.
                    if (_depart == null && boat.activeSelf) _depart = StartCoroutine(Depart());
                }
                else
                {
                    StopDepart();
                    boat.SetActive(true);
                }
            }
            if (returnedMarker != null) returnedMarker.SetActive(Current == State.Returned);
        }

        /// <summary>바라보는 방향으로 가속하며 나아가다 끝에 가라앉고 꺼진다. (+9/30)</summary>
        private IEnumerator Depart()
        {
            Transform t = boat.transform;
            Vector3 start = t.position;
            Vector3 dir = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            float sinkFrom = Mathf.Max(0f, departSeconds - sinkSeconds);

            for (float e = 0f; e < departSeconds; e += Time.deltaTime)
            {
                float k = e / departSeconds;
                float sink = sinkSeconds > 0f ? Mathf.Clamp01((e - sinkFrom) / sinkSeconds) : 0f;
                t.position = start + dir * (departDistance * k * k) + Vector3.down * (sinkDepth * sink * sink);
                yield return null;
            }

            boat.SetActive(false);
            t.localPosition = _boatHome;
            _depart = null;
        }

        private void StopDepart()
        {
            if (_depart != null)
            {
                StopCoroutine(_depart);
                _depart = null;
            }
            if (boat != null) boat.transform.localPosition = _boatHome;
        }
    }
}
