using System;
using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Player;
using Marea.Restaurant;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace Marea.Field
{
    /// <summary>
    /// 손님 배 — 영업 중 손님을 태운 배가 계속 들어와 선착장에 손님을 내리고 떠난다. (+10/7, (+10/9) 이슈 128)
    ///
    /// (+10/9) 예전엔 영업 시작에 배 한 척이 들어와 첫 손님만 내리고 영업 내내 서 있었고, 나머지 손님은
    /// CustomerManager가 주기적으로 선착장에서 그냥 내보냈다. 기획 피드백 「손님 운송」 · 「배 외형 차별화」 ·
    /// 「손님 등장 빈도」로 바뀌었다:
    /// - 손님은 배에서만 내린다(CustomerManager.SpawnByBoats). 배마다 정원이 다르고(작은 2 · 중간 5 · 큰 10)
    ///   배 크기로 몇 명 왔는지 보인다. 빈자리가 정원보다 적으면 들어가는 배 중에서 고른다.
    /// - 다음 배까지의 간격과 배 크기 비율은 일차별 표(GuestWaveData, BusinessManager.Day)에서 정한다.
    /// - 선착장은 한 자리 — 앞 배가 떠나기 시작해야 다음 배가 들어온다.
    /// - (+10/9) 배는 내린 손님이 다 먹고 돌아와 다시 탈 때까지 정박해 기다렸다가 태우고 떠난다.
    ///   그래서 한 번에 한 무리만 식당에 있고, 간격은 앞 배가 떠난 뒤부터 센다.
    /// - 배가 닿을 때마다 뱃고동(arriveClip). 카메라 연출(배 따라가기 → 첫 손님 보기)은 cameraOnFirstBoat를 켜면 그날 첫 배만 — 기본 끔.
    ///
    /// 배는 종류별 프리팹을 매번 만들고 떠나면 지운다. 프리팹은 뱃머리가 +Z, 원점이 뱃머리 끝이라
    /// 경로가 곧 뱃머리 자리다(정박점 = 뱃머리가 서는 곳). 크기가 달라 부두에 걸리면 종류별 pathOffset으로 비킨다.
    /// 카메라는 해금 연출과 같은 CameraFollow.FocusOn.
    /// </summary>
    public class GuestBoatArrival : MonoBehaviour
    {
        [Serializable]
        private class BoatType
        {
            [Tooltip("인스펙터 · 로그에 보일 이름. 예: 작은 배")]
            public string label = "배";
            [Tooltip("배 프리팹. 뱃머리 +Z, 원점 = 뱃머리 끝. 모델 자식에 FloatBob이 있으면 숙임 값을 넣는다.")]
            public GameObject prefab;
            [Tooltip("태우는 손님 수(정원).")]
            [Min(1)] public int passengers = 2;
            [Tooltip("이 배만 경로에서 비켜 갈 거리(월드, m). 큰 배가 부두에 걸리면 바다 쪽으로. y는 흘수(물에 잠기는 깊이) 맞춤.")]
            public Vector3 pathOffset;
            [Tooltip("프리팹 +Z와 모델 뱃머리 사이 각도(도). 프리팹을 뱃머리 +Z로 만들었으면 0.")]
            public float bowYawOffset;
        }

        [Header("배 종류 (+10/9)")]
        [Tooltip("작은 · 중간 · 큰 순서. GuestWaveData의 boatWeights 순서와 같아야 한다.")]
        [SerializeField] private BoatType[] boatTypes;
        [Tooltip("일차별 배 간격 · 크기 비율.")]
        [SerializeField] private GuestWaveData waves;

        [Header("경로")]
        [Tooltip("입항 경로(첫 번째 스플라인). 씬 뷰에서 점을 옮겨 편집한다. 첫 점 = 바다 출발점, "
               + "마지막 점 = 정박 위치이고 마지막 점에서의 진행 방향이 정박 방향이다. (+10/9)")]
        [SerializeField] private SplineContainer path;
        [Tooltip("출항 경로(첫 번째 스플라인). 첫 점 = 정박 위치(입항 경로 끝과 같은 자리 · 같은 방향), "
               + "마지막 점 = 바다. 배는 제자리에서 돌지 않고 이 경로를 앞으로 따라 나간다. (+10/9)")]
        [SerializeField] private SplineContainer departPath;
        [Tooltip("출항 경로 첫 점이 입항 경로 끝에서 이 거리(m)보다 멀거나 방향이 10° 넘게 틀어지면 시작할 때 경고한다 — 배가 튄다.")]
        [SerializeField, Min(0f)] private float joinTolerance = 0.3f;
        [SerializeField, Min(0.5f)] private float arriveSeconds = 6f;
        [SerializeField, Min(0.5f)] private float departSeconds = 6f;
        [Tooltip("입항 마지막 이 거리(m)에서만 고르게 감속해 정박점에서 선다. 그 앞은 등속. 0이면 감속 없이 딱 선다. (+10/9)")]
        [SerializeField, Min(0f)] private float brakeDistance = 8f;
        [Tooltip("출항 처음 이 거리(m)에서만 고르게 가속한다. 그 뒤는 등속. (+10/9)")]
        [SerializeField, Min(0f)] private float launchDistance = 6f;

        [Header("손님 내리기 (+10/9)")]
        [Tooltip("손님 한 명씩 내리는 간격(초). 입구 한 자리에서 나와 겹치지 않게.")]
        [SerializeField, Min(0.05f)] private float unloadInterval = 0.5f;
        [Tooltip("(+10/9) 손님이 다 탄 뒤(마지막 손님이 돌아와 사라진 뒤) 출항까지 기다리는 초.")]
        [SerializeField, Min(0f)] private float dockHoldSeconds = 1.5f;
        [Tooltip("빈자리가 하나도 없을 때 다시 볼 때까지 초.")]
        [SerializeField, Min(0.1f)] private float noSeatRetrySeconds = 2f;

        // 출렁임 · 기울임은 배 모델 자식의 FloatBob이 맡는다 (+10/7).

        [Header("숙임 (배의 FloatBob에 넣는다)")]
        [Tooltip("감속 · 가속 1 m/s²당 뱃머리 숙임(도). 배를 만들 때 FloatBob 값을 이걸로 덮는다 — "
               + "배 종류마다 따로 맞추지 않게 이 프리팹이 값을 들고 다닌다. (+10/9)")]
        [SerializeField, Min(0f)] private float leanPerAccel = 0.5f;
        [Tooltip("숙임 최대 각도(도).")]
        [SerializeField, Min(0f)] private float maxLean = 4f;

        [Header("소리 (+10/9)")]
        [Tooltip("배가 선착장에 닿을 때마다 뱃고동. 화면 밖에서 와도 알게 화면 소리로 낸다. 영업 시작 버튼 뱃고동과 겹치지 않게 출발이 아니라 정박 때.")]
        [SerializeField] private AudioClip arriveClip;
        [SerializeField, Range(0f, 1f)] private float arriveVolume = 0.8f;

        [Header("카메라 (그날 첫 배만)")]
        [Tooltip("(+10/9) 끄면 연출 없이 배만 온다 — 기본은 끔(배가 계속 오니 소리로 알린다). 켜면 그날 첫 배를 카메라가 따라가고 첫 손님을 본다.")]
        [SerializeField] private bool cameraOnFirstBoat;
        [Tooltip("배를 볼 때 배 원점에서 올려 볼 높이.")]
        [SerializeField] private float lookHeight = 2f;
        [Tooltip("첫 손님이 내린 뒤 그 손님을 보는 시간.")]
        [SerializeField, Min(0f)] private float watchCustomerSeconds = 2f;
        [Tooltip("영업 시작 버튼 클릭이 바로 건너뛰기로 읽히지 않게, 이 시간 동안은 건너뛰기를 안 받는다.")]
        [SerializeField, Min(0f)] private float skipGuardSeconds = 0.4f;

        private Coroutine _loop;
        private CameraFollow _cam;
        private PlayerController _player;
        private CustomerManager _customers;
        private readonly List<UiPanel> _hiddenPanels = new();
        private readonly List<CustomerController> _passengers = new();
        private readonly List<BoatType> _fits = new();
        private readonly List<float> _fitWeights = new();
        private bool _newDay;
        private bool _inCutscene;
        private bool _skip;
        private float _skipAllowedAt;
        private bool _subscribed;

        private static bool IsOpen => BusinessManager.Instance != null && BusinessManager.Instance.CurrentState == BusinessState.Open;

        private void Awake()
        {
            if (!HasSpline(path) || !HasSpline(departPath))
            {
                Debug.LogError($"{name}: GuestBoatArrival path / departPath 스플라인에 점이 2개 미만이다. 손님 배가 안 온다.", this);
                enabled = false;
                return;
            }
            if (!HasBoatTypes())
            {
                Debug.LogError($"{name}: GuestBoatArrival.boatTypes가 비었거나 프리팹이 빠졌다. 손님 배가 안 온다.", this);
                enabled = false;
                return;
            }
            if (waves == null)
                Debug.LogError($"{name}: GuestBoatArrival.waves(GuestWaveData)가 비어 있다. 30초 간격 · 같은 비율로 온다.", this);
            WarnIfPathsDontJoin();
        }

        private void Start()
        {
            if (!enabled) return;

            _customers = FindAnyObjectByType<CustomerManager>();
            if (_customers == null)
                Debug.LogError($"{name}: 씬에 CustomerManager가 없다. 배는 오지만 손님을 못 내린다.", this);
            else
                _customers.SpawnByBoats = true;   // 손님은 배에서만 내린다

            if (BusinessManager.Instance == null)
            {
                Debug.LogError($"{name}: 씬에 BusinessManager가 없다. 영업 시작을 알 수 없어 배가 안 온다.", this);
                return;
            }
            BusinessManager.Instance.OnStateChanged += HandleStateChanged;
            _subscribed = true;

            if (IsOpen) _loop = StartCoroutine(BoatLoop(cutsceneFirst: false));   // 이미 영업 중(재시작 등)
        }

        private void OnDestroy()
        {
            if (_subscribed && BusinessManager.Instance != null)
                BusinessManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (!_inCutscene || _skip || Time.time < _skipAllowedAt) return;
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool key = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            if (click || key) _skip = true;
        }

        private void HandleStateChanged(BusinessState state)
        {
            // 영업이 끝나면 루프는 다음 배를 안 고르고 저절로 끝난다. 오는 중인 배는 손님 없이 닿았다가 떠난다.
            if (state != BusinessState.Open) return;
            if (_loop == null) _loop = StartCoroutine(BoatLoop(cutsceneFirst: cameraOnFirstBoat));
            else _newDay = true;   // 앞날 루프가 아직 안 끝났다(영업 종료 → 다음 날 → 시작이 한 프레임에) — 기다리던 간격을 끊고 새 날 첫 배부터
        }

        // --- 영업 중 배 루프 ---

        private IEnumerator BoatLoop(bool cutsceneFirst)
        {
            bool cutscene = cutsceneFirst;
            _newDay = false;
            while (IsOpen)
            {
                if (_newDay)
                {
                    _newDay = false;
                    cutscene = cameraOnFirstBoat;
                }

                int seats = _customers != null ? _customers.EmptySeatCount : 0;
                BoatType type = PickBoat(seats);
                if (type == null)
                {
                    yield return Wait(noSeatRetrySeconds);
                    continue;
                }

                yield return ArriveAndUnload(type, cutscene, seats);   // 출항을 시작하면 돌아온다
                if (cutscene) _newDay = false;   // 방금 배가 연출을 했으면 새 날 연출을 또 하지 않는다
                cutscene = false;

                yield return Wait(NextInterval());
            }
            _loop = null;
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && IsOpen && !_newDay; t += Time.deltaTime) yield return null;
        }

        /// <summary>오늘 단계의 가중치로 배를 고른다. 정원이 빈자리보다 많은 배는 빼고, 다 빠지면 가장 작은 배(빈자리만큼만 태움).</summary>
        private BoatType PickBoat(int seats)
        {
            if (seats <= 0) return null;

            GuestWaveData.Stage stage = default;
            bool hasStage = waves != null && BusinessManager.Instance != null
                            && waves.TryGetStage(BusinessManager.Instance.Day, out stage);

            _fits.Clear();
            _fitWeights.Clear();
            float total = 0f;
            BoatType smallest = null;
            for (int i = 0; i < boatTypes.Length; i++)
            {
                BoatType type = boatTypes[i];
                if (type == null || type.prefab == null) continue;
                if (smallest == null || type.passengers < smallest.passengers) smallest = type;
                if (type.passengers > seats) continue;

                float w = hasStage ? (stage.boatWeights != null && i < stage.boatWeights.Length ? stage.boatWeights[i] : 0f) : 1f;
                if (w <= 0f) continue;
                _fits.Add(type);
                _fitWeights.Add(w);
                total += w;
            }
            if (_fits.Count == 0) return smallest;

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < _fits.Count; i++)
            {
                roll -= _fitWeights[i];
                if (roll < 0f) return _fits[i];
            }
            return _fits[_fits.Count - 1];
        }

        private float NextInterval()
        {
            if (waves != null && BusinessManager.Instance != null
                && waves.TryGetStage(BusinessManager.Instance.Day, out GuestWaveData.Stage stage))
            {
                float a = Mathf.Max(0f, stage.intervalSeconds.x);
                float b = Mathf.Max(a, stage.intervalSeconds.y);
                return UnityEngine.Random.Range(a, b);
            }
            return 30f;
        }

        // --- 배 한 척 ---

        private IEnumerator ArriveAndUnload(BoatType type, bool cutscene, int seats)
        {
            Transform boat = SpawnBoat(type);
            if (cutscene) BeginCutscene();

            float arriveLength = path.CalculateLength();
            for (float t = 0f; t < arriveSeconds && !(cutscene && _skip); t += Time.deltaTime)
            {
                // (+10/9) 등속으로 오다가 끝 brakeDistance만 고르게 감속. 예전 EaseOut은 끝 40% 시간 동안
                // 거의 서 있다가 정박점으로 튀어 끊겨 보였다 — 감속 구간을 시간이 아니라 거리로 잡는다.
                float s = BrakeAtEnd(t, arriveSeconds, arriveLength, brakeDistance);
                Pose(boat, type, path, s / arriveLength);
                if (cutscene) Focus(boat.position);
                yield return null;
            }
            Pose(boat, type, path, 1f);
            SoundManager.Play(arriveClip, arriveVolume);   // (+10/9) 배마다 닿을 때 뱃고동

            // 손님은 정원과 빈자리 중 적은 만큼. 영업이 끝났으면 아무도 안 내린다.
            int count = Mathf.Min(type.passengers, seats);
            _passengers.Clear();
            CustomerController first = null;
            int unloaded = 0;
            float nextUnload = 0f;
            float watchUntil = -1f;
            float clock = 0f;
            while (IsOpen && (unloaded < count || (cutscene && _inCutscene && clock < watchUntil)))
            {
                if (unloaded < count && clock >= nextUnload)
                {
                    CustomerController c = _customers != null ? _customers.SpawnOne() : null;
                    if (c == null) count = unloaded;   // 그사이 자리가 찼다
                    else
                    {
                        unloaded++;
                        _passengers.Add(c);
                        nextUnload = clock + unloadInterval;
                        if (first == null)
                        {
                            first = c;
                            watchUntil = clock + watchCustomerSeconds;
                        }
                    }
                }

                if (cutscene && _inCutscene)
                {
                    if (_skip || first == null && unloaded >= count) EndCutscene();
                    else if (first != null) Focus(first.transform.position);
                }

                clock += Time.deltaTime;
                yield return null;
            }
            if (cutscene && _inCutscene) EndCutscene();

            // (+10/9) 내린 손님이 다 먹고 선착장 출구로 돌아와 사라질 때(= 배에 탐)까지 정박해 기다린다.
            // 음식을 잘못 받아 일찍 떠난 손님도 사라지면 탄 것으로 친다. 영업이 끝나면 BusinessManager가
            // 손님을 한꺼번에 지우니 그때 바로 떠난다.
            while (HasPassengersAshore()) yield return null;

            for (float t = 0f; t < dockHoldSeconds; t += Time.deltaTime) yield return null;

            StartCoroutine(Depart(boat, type));
        }

        /// <summary>이 배가 내린 손님 중 아직 안 탄(살아 있는) 손님이 있는가.</summary>
        private bool HasPassengersAshore()
        {
            foreach (CustomerController c in _passengers)
                if (c != null) return true;
            return false;
        }

        private Transform SpawnBoat(BoatType type)
        {
            GameObject go = Instantiate(type.prefab, transform);
            go.name = $"GuestBoat ({type.label}, {type.passengers}명)";

            FloatBob bob = go.GetComponentInChildren<FloatBob>(true);
            if (bob != null) bob.SetLean(leanPerAccel, maxLean);
            else Debug.LogWarning($"{name}: '{type.label}' 프리팹 아래에 FloatBob이 없다. 출렁임 · 숙임이 안 나온다.", type.prefab);

            Pose(go.transform, type, path, 0f);
            return go.transform;
        }

        private IEnumerator Depart(Transform boat, BoatType type)
        {
            // (+10/9) 출항 경로를 앞으로 따라 나간다 — 뱃머리는 늘 경로 방향. 처음 launchDistance만 고르게 가속
            // (입항 감속을 시간으로 뒤집은 것). 예전(+10/7)엔 제자리에서 180° 돈 뒤 입항 경로를 거꾸로 탔다.
            float departLength = departPath.CalculateLength();
            for (float t = 0f; t < departSeconds && boat != null; t += Time.deltaTime)
            {
                float s = departLength - BrakeAtEnd(departSeconds - t, departSeconds, departLength, launchDistance);
                Pose(boat, type, departPath, s / departLength);
                yield return null;
            }
            if (boat != null) Destroy(boat.gameObject);
        }

        /// <summary>경로 위 u(0~1, 길이 기준)에 배를 놓는다. 뱃머리는 경로 진행 방향, 종류별로 pathOffset만큼 비킨다.</summary>
        private static void Pose(Transform boat, BoatType type, SplineContainer c, float u)
        {
            Sample(c, u, out Vector3 pos, out Vector3 tangent);
            boat.SetPositionAndRotation(pos + type.pathOffset, HeadingFor(tangent, type.bowYawOffset));
        }

        private bool HasBoatTypes()
        {
            if (boatTypes == null || boatTypes.Length == 0) return false;
            foreach (BoatType type in boatTypes)
                if (type != null && type.prefab != null) return true;
            return false;
        }

        private static bool HasSpline(SplineContainer c) => c != null && c.Spline != null && c.Spline.Count >= 2;

        /// <summary>
        /// 시간 t(0~total)에 간 거리(0~length). 등속 v로 가다가 끝 brake(m)에서 일정한 감속도로 0까지 줄인다.
        /// 감속 구간은 같은 거리를 등속보다 두 배 시간 들여 가므로 total = (length + brake) / v.
        /// </summary>
        private static float BrakeAtEnd(float t, float total, float length, float brake)
        {
            brake = Mathf.Clamp(brake, 0f, length * 0.5f);
            float v = (length + brake) / total;
            float cruiseTime = (length - brake) / v;
            if (t <= cruiseTime || brake <= 0f) return Mathf.Min(v * t, length);
            float tau = Mathf.Min(t - cruiseTime, 2f * brake / v);
            float decel = v * v / (2f * brake);
            return (length - brake) + v * tau - 0.5f * decel * tau * tau;
        }

        /// <summary>경로 위 u(0 = 첫 점, 1 = 마지막 점, 길이 기준)의 월드 위치와 진행 방향.</summary>
        private static void Sample(SplineContainer c, float u, out Vector3 pos, out Vector3 tangent)
        {
            c.Evaluate(Mathf.Clamp01(u), out var p, out var tan, out _);
            pos = p;
            tangent = tan;
        }

        /// <summary>출항 경로가 정박 자세에서 이어지지 않으면 출항 첫 프레임에 배가 튄다 — 편집 실수를 바로 알린다.</summary>
        private void WarnIfPathsDontJoin()
        {
            Sample(path, 1f, out Vector3 dockPos, out Vector3 dockTan);
            Sample(departPath, 0f, out Vector3 startPos, out Vector3 startTan);
            float gap = Vector3.Distance(dockPos, startPos);
            float turn = Vector3.Angle(dockTan, startTan);
            if (gap > joinTolerance || turn > 10f)
                Debug.LogWarning($"{name}: 출항 경로 첫 점이 입항 경로 끝과 안 맞는다 (거리 {gap:F2}m · 방향 {turn:F0}°). "
                               + "출항 시작에 배가 튄다 — departPath 첫 점을 path 끝점에 맞춰라.", this);
        }

        /// <summary>
        /// 뱃머리가 이 방향을 보게 하는 배 루트 회전. (+10/9) 수평으로 눌러 펴지 않는다 — 경로가 오르내리면
        /// 뱃머리도 들리고 숙는다. 좌우 기울기는 위쪽을 월드 위로 고정해 안 생긴다(출렁임은 모델의 FloatBob).
        /// </summary>
        private static Quaternion HeadingFor(Vector3 bowDir, float bowYawOffset)
            => Quaternion.LookRotation(Dir(bowDir), Vector3.up) * Quaternion.Euler(0f, -bowYawOffset, 0f);

        // --- 연출 시작 · 끝 (FacilitySite 해금 연출과 같은 방식) ---

        private void BeginCutscene()
        {
            _inCutscene = true;
            _skip = false;
            _skipAllowedAt = Time.time + skipGuardSeconds;

            _cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (_cam == null)
                Debug.LogError($"{name}: MainCamera에 CameraFollow가 없다. 배는 오지만 카메라가 안 따라간다.", this);

            _player = FindAnyObjectByType<PlayerController>();
            if (_player != null) _player.BeginBusy();

            // 창이 화면을 가리면 연출이 안 보인다. 닫지 않고 가렸다가 되돌린다 (UiPanel.Suspend 주석).
            foreach (UiPanel panel in FindObjectsByType<UiPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (panel.IsVisible) { panel.Suspend(); _hiddenPanels.Add(panel); }
        }

        private void EndCutscene()
        {
            _inCutscene = false;
            if (_cam != null) _cam.ClearFocus();
            if (_player != null) _player.EndBusy();
            foreach (UiPanel panel in _hiddenPanels)
                if (panel != null) panel.Resume();
            _hiddenPanels.Clear();

            _cam = null;
            _player = null;
        }

        private void Focus(Vector3 point)
        {
            if (_cam != null) _cam.FocusOn(point + Vector3.up * lookHeight);
        }

        private static Vector3 Dir(Vector3 v)
            => v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;

        // 종류별로 비켜 간 경로를 씬 뷰에 보인다 — pathOffset을 맞출 때 부두에 걸리는지 눈으로 본다.
        private void OnDrawGizmosSelected()
        {
            if (!HasSpline(path) || boatTypes == null) return;
            Color[] colors = { Color.green, Color.yellow, Color.red, Color.cyan };
            for (int k = 0; k < boatTypes.Length; k++)
            {
                BoatType type = boatTypes[k];
                if (type == null || type.pathOffset == Vector3.zero) continue;
                Gizmos.color = colors[k % colors.Length];
                foreach (SplineContainer c in new[] { path, departPath })
                {
                    if (!HasSpline(c)) continue;
                    Sample(c, 0f, out Vector3 prev, out _);
                    for (int i = 1; i <= 40; i++)
                    {
                        Sample(c, i / 40f, out Vector3 p, out _);
                        Gizmos.DrawLine(prev + type.pathOffset, p + type.pathOffset);
                        prev = p;
                    }
                }
            }
        }
    }
}
