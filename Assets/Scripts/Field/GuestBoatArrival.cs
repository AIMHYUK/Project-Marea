using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Player;
using Marea.Restaurant;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Field
{
    /// <summary>
    /// 영업 시작 연출 — 손님 배(범선)가 선착장에 들어오고 첫 손님이 내린다. (+10/7)
    ///
    /// 흐름: 준비 중엔 배가 없다 → [영업 시작] → 카메라가 배를 따라가며 배가 바다에서 선착장으로 들어온다
    /// → 닿으면 첫 손님을 내리고 잠깐 그 손님을 본다 → 카메라가 플레이어로 돌아온다.
    /// 배는 영업 내내 정박해 있고(손님은 선착장의 CustomerSpawnPoint에서 계속 나온다), 영업이 끝나면 떠난다.
    ///
    /// 손님 · 영업 코드(B)는 건드리지 않는다 — 영업 상태 이벤트를 듣고, 연출 동안만 공개된 PauseSpawning으로
    /// 스폰을 멈췄다가 배가 닿으면 SpawnNow로 첫 손님을 내린다. 카메라는 해금 연출과 같은 CameraFollow.FocusOn.
    /// 클릭이나 E로 건너뛸 수 있다.
    /// </summary>
    public class GuestBoatArrival : MonoBehaviour
    {
        [Header("배")]
        [Tooltip("움직일 배. 씬의 GuestBoat.")]
        [SerializeField] private Transform boat;
        [Tooltip("정박 자세(위치 · 방향). 배가 여기 와서 선다.")]
        [SerializeField] private Transform dockPoint;
        [Tooltip("정박 지점에서 이만큼 떨어진 바다에서 출발한다. 들어올 땐 이 방향의 반대로 뱃머리를 둔다. "
               + "x · z를 같이 줘서 대각선으로 들어오게 한다 — 부두를 정면으로 향하면 들이받는 것처럼 보인다.")]
        [SerializeField] private Vector3 approachOffset = new(-24.5f, 0f, -14.1f);   // 진행 방향 60°
        [SerializeField, Min(0.5f)] private float arriveSeconds = 6f;
        [Tooltip("도착 직전 이 비율 동안 뱃머리를 정박 방향으로 돌린다.")]
        [SerializeField, Range(0f, 1f)] private float turnPortion = 0.6f;
        [SerializeField, Min(0.5f)] private float departSeconds = 6f;

        [Header("정박 중 흔들림")]
        [SerializeField, Min(0f)] private float bobHeight = 0.08f;
        [SerializeField, Min(0f)] private float bobSpeed = 1.2f;

        [Header("카메라")]
        [Tooltip("배를 볼 때 배 원점에서 올려 볼 높이.")]
        [SerializeField] private float lookHeight = 2f;
        [Tooltip("첫 손님이 내린 뒤 그 손님을 보는 시간.")]
        [SerializeField, Min(0f)] private float watchCustomerSeconds = 2f;
        [Tooltip("영업 시작 버튼 클릭이 바로 건너뛰기로 읽히지 않게, 이 시간 동안은 건너뛰기를 안 받는다.")]
        [SerializeField, Min(0f)] private float skipGuardSeconds = 0.4f;

        private enum BoatState { Away, Arriving, Docked, Departing }

        private BoatState _state = BoatState.Away;
        private Coroutine _routine;
        private CameraFollow _cam;
        private PlayerController _player;
        private CustomerManager _customers;
        private readonly List<UiPanel> _hiddenPanels = new();
        private bool _inCutscene;
        private bool _skip;
        private float _skipAllowedAt;
        private bool _subscribed;

        private void Awake()
        {
            if (boat == null || dockPoint == null)
            {
                Debug.LogError($"{name}: GuestBoatArrival.boat / dockPoint가 비어 있다. 영업 시작 연출이 안 나온다.", this);
                enabled = false;
                return;
            }
            boat.gameObject.SetActive(false);   // 준비 중엔 배가 없다
        }

        private void Start()
        {
            _customers = FindAnyObjectByType<CustomerManager>();
            if (_customers == null)
                Debug.LogError($"{name}: 씬에 CustomerManager가 없다. 배는 오지만 손님을 못 내린다.", this);

            if (BusinessManager.Instance == null)
            {
                Debug.LogError($"{name}: 씬에 BusinessManager가 없다. 영업 시작을 알 수 없어 배가 안 온다.", this);
                return;
            }
            BusinessManager.Instance.OnStateChanged += HandleStateChanged;
            _subscribed = true;

            // 이미 영업 중이면(재시작 등) 연출 없이 정박해 둔다.
            if (BusinessManager.Instance.CurrentState == BusinessState.Open) PlaceDocked();
        }

        private void OnDestroy()
        {
            if (_subscribed && BusinessManager.Instance != null)
                BusinessManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (_state == BoatState.Docked && bobHeight > 0f)
                boat.position = dockPoint.position + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

            if (!_inCutscene || _skip || Time.time < _skipAllowedAt) return;
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool key = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            if (click || key) _skip = true;
        }

        private void HandleStateChanged(BusinessState state)
        {
            if (state == BusinessState.Open) Restart(ArriveRoutine());
            else if (state == BusinessState.Settlement && _state != BoatState.Away) Restart(DepartRoutine());
        }

        private void Restart(IEnumerator routine)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                EndCutscene();
            }
            _routine = StartCoroutine(routine);
        }

        // --- 도착 ---

        private IEnumerator ArriveRoutine()
        {
            BeginCutscene();
            if (_customers != null) _customers.PauseSpawning(true);   // 배가 닿기 전엔 아무도 안 나온다

            Vector3 from = dockPoint.position + approachOffset;
            Quaternion heading = Quaternion.LookRotation(-Flat(approachOffset), Vector3.up);
            boat.SetPositionAndRotation(from, heading);
            boat.gameObject.SetActive(true);
            _state = BoatState.Arriving;

            for (float t = 0f; t < arriveSeconds && !_skip; t += Time.deltaTime)
            {
                float k = t / arriveSeconds;
                PoseBoat(from, heading, EaseOut(k), k);
                Focus(boat.position);
                yield return null;
            }
            PlaceDocked();

            CustomerController first = SpawnFirstCustomer();
            if (first != null && !_skip)
            {
                for (float t = 0f; t < watchCustomerSeconds && !_skip && first != null; t += Time.deltaTime)
                {
                    Focus(first.transform.position);
                    yield return null;
                }
            }

            EndCutscene();
            _routine = null;
        }

        private void PoseBoat(Vector3 from, Quaternion heading, float travel, float k)
        {
            Vector3 pos = Vector3.Lerp(from, dockPoint.position, travel);
            float turnStart = 1f - turnPortion;
            float turn = turnPortion <= 0f ? (k >= 1f ? 1f : 0f) : Mathf.Clamp01((k - turnStart) / turnPortion);
            boat.SetPositionAndRotation(pos, Quaternion.Slerp(heading, dockPoint.rotation, Mathf.SmoothStep(0f, 1f, turn)));
        }

        private void PlaceDocked()
        {
            boat.SetPositionAndRotation(dockPoint.position, dockPoint.rotation);
            boat.gameObject.SetActive(true);
            _state = BoatState.Docked;
        }

        private CustomerController SpawnFirstCustomer()
        {
            if (_customers == null) return null;
            _customers.PauseSpawning(false);

            // SpawnNow는 손님 참조를 안 돌려준다 — 전후로 씬을 비교해 새로 생긴 손님을 찾는다.
            var before = new HashSet<CustomerController>(FindObjectsByType<CustomerController>(FindObjectsSortMode.None));
            if (!_customers.SpawnNow()) return null;
            foreach (CustomerController c in FindObjectsByType<CustomerController>(FindObjectsSortMode.None))
                if (!before.Contains(c)) return c;
            return null;
        }

        // --- 출항 ---

        private IEnumerator DepartRoutine()
        {
            _state = BoatState.Departing;
            Vector3 from = boat.position;
            Quaternion start = boat.rotation;
            Vector3 to = dockPoint.position + approachOffset;
            Quaternion away = Quaternion.LookRotation(Flat(approachOffset), Vector3.up);

            for (float t = 0f; t < departSeconds; t += Time.deltaTime)
            {
                float k = t / departSeconds;
                boat.SetPositionAndRotation(
                    Vector3.Lerp(from, to, k * k),   // 천천히 떠나 점점 빨라진다
                    Quaternion.Slerp(start, away, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.4f))));
                yield return null;
            }
            boat.gameObject.SetActive(false);
            _state = BoatState.Away;
            _routine = null;
        }

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

            // 건너뛰었거나 중간에 끊겨도 손님은 나와야 한다.
            if (_customers != null && BusinessManager.Instance != null
                && BusinessManager.Instance.CurrentState == BusinessState.Open)
                _customers.PauseSpawning(false);

            _cam = null;
            _player = null;
        }

        private void Focus(Vector3 point)
        {
            if (_cam != null) _cam.FocusOn(point + Vector3.up * lookHeight);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        // 3차 — 끝으로 갈수록 오래 미끄러지다 선다. 2차는 부두에 들이받는 느낌이었다.
        private static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
    }
}
