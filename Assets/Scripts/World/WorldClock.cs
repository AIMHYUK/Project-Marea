using System;
using UnityEngine;

namespace Marea.World
{
    /// <summary>
    /// 하루의 시간대. 낮 → 저녁 → 밤 → 새벽 순으로 한 바퀴 돈다. (+9/8)
    ///
    /// 2차 계약 6의 원안은 Morning/Day/Evening/Night 였는데, 기획이 말하는 네 번째가
    /// 아침이 아니라 새벽이라 Dawn 으로 바꿨다. B가 읽는 값이므로 계약 변경이다 —
    /// Docs/2차_구현_분담.md 계약 6에 같이 반영해 뒀다.
    /// </summary>
    public enum TimeSlot { Day, Evening, Night, Dawn }

    /// <summary>
    /// 월드 시계. 하루를 굴리고 시간대가 바뀌는 순간에만 알린다.
    /// 2차 계약 6 — A가 주고 B가 <see cref="Slot"/> 만 읽는다.
    ///
    /// 이 클래스는 시간만 센다. 조명도 UI도 여기서 만지지 않는다 —
    /// 필요한 쪽이 <see cref="OnSlotChanged"/> 를 구독하거나 매 프레임
    /// <see cref="NormalizedTime"/> 을 읽어 간다. 아트가 조명을 구워 오면
    /// 구독자 하나를 새로 붙이면 되고 이 파일은 안 건드린다.
    ///
    /// DontDestroyOnLoad 는 일부러 안 붙였다. 프로젝트에 아직 SceneManager.LoadScene 이
    /// 한 군데도 없어서 지금은 하는 일이 없고, 나중에 씬 전환이 생기면 살아남아야 하는 건
    /// 이 숫자들이지 씬 참조를 든 조명 쪽이 아니다. 그때 여기에만 붙인다.
    /// </summary>
    public class WorldClock : MonoBehaviour
    {
        public static WorldClock Instance { get; private set; }

        [Header("하루 길이")]
        [Tooltip("현실 시간 몇 초가 게임 하루인가. 24분 = 1440초.")]
        [SerializeField] private float dayLengthSeconds = 1440f;

        [Tooltip("게임을 켤 때의 게임 내 시각(0~24). 기본 6 = 낮 시작.")]
        [SerializeField] private float startHour = 6f;

        [Header("시간대 경계 — 게임 내 시각(0~24), 오름차순이어야 한다")]
        [Tooltip("새벽 시작. 기본 4시.")]
        [SerializeField] private float dawnStartHour = 4f;

        [Tooltip("낮 시작. 기본 6시.")]
        [SerializeField] private float dayStartHour = 6f;

        [Tooltip("저녁 시작. 기본 16시.")]
        [SerializeField] private float eveningStartHour = 16f;

        [Tooltip("밤 시작. 기본 20시. 밤은 여기서 다음 새벽까지 — 자정을 넘어간다.")]
        [SerializeField] private float nightStartHour = 20f;

        /// <summary>지금 시간대. B는 이것만 읽는다.</summary>
        public TimeSlot Slot { get; private set; }

        /// <summary>하루 안에서의 위치. 0~1. 조명 보간은 이걸 쓴다.</summary>
        public float NormalizedTime { get; private set; }

        /// <summary>게임 내 시각. 0~24.</summary>
        public float Hour => NormalizedTime * 24f;

        /// <summary>
        /// 시간대가 바뀐 순간에만 발행된다. 하루에 정확히 네 번이다.
        ///
        /// 시작할 때는 쏘지 않는다 — 쏘면 한 바퀴에 다섯 번이 되어 세기가 어려워진다.
        /// 늦게 켜진 구독자는 <see cref="Slot"/> 을 직접 읽어 초기 상태를 맞춘다.
        /// </summary>
        public event Action<TimeSlot> OnSlotChanged;

        private void Awake()
        {
            // 시계가 둘이면 시간이 두 배로 흐르는데, 화면상으로는 그냥 좀 빠른 걸로
            // 보여서 원인을 못 찾는다. 조용히 넘어가지 않는다.
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"[WorldClock] 씬에 WorldClock이 둘 이상 있습니다. 먼저 있던 " +
                    $"'{Instance.name}' 을 두고 이 오브젝트('{name}')의 시계를 끕니다.", this);
                enabled = false;
                return;
            }

            Instance = this;

            NormalizedTime = Mathf.Repeat(startHour, 24f) / 24f;
            Slot = SlotOf(Hour);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            NormalizedTime = Mathf.Repeat(NormalizedTime + Time.deltaTime / dayLengthSeconds, 1f);

            TimeSlot next = SlotOf(Hour);
            if (next == Slot) return;

            Slot = next;
            OnSlotChanged?.Invoke(next);
        }

        /// <summary>
        /// 시각을 시간대로 바꾼다.
        ///
        /// 밤만 자정을 넘어가므로(20시 → 다음날 4시) 앞의 셋을 먼저 걸러내고
        /// 나머지를 전부 밤으로 본다. 경계값 넷이 오름차순이라는 전제가 깔려 있고,
        /// 그건 OnValidate 가 지킨다.
        /// </summary>
        private TimeSlot SlotOf(float hour)
        {
            if (hour >= dawnStartHour && hour < dayStartHour) return TimeSlot.Dawn;
            if (hour >= dayStartHour && hour < eveningStartHour) return TimeSlot.Day;
            if (hour >= eveningStartHour && hour < nightStartHour) return TimeSlot.Evening;
            return TimeSlot.Night;
        }

        private void OnValidate()
        {
            if (dayLengthSeconds < 1f) dayLengthSeconds = 1f;

            // 순서가 어긋나면 SlotOf 가 시간대 하나를 통째로 건너뛴다. 이벤트가
            // 한 바퀴에 세 번만 떠서 "왜 저녁이 안 오지"가 된다.
            if (!(dawnStartHour < dayStartHour &&
                  dayStartHour < eveningStartHour &&
                  eveningStartHour < nightStartHour &&
                  dawnStartHour >= 0f && nightStartHour < 24f))
            {
                Debug.LogError(
                    "[WorldClock] 시간대 경계는 0 <= 새벽 < 낮 < 저녁 < 밤 < 24 여야 합니다. " +
                    $"지금: 새벽 {dawnStartHour} / 낮 {dayStartHour} / 저녁 {eveningStartHour} / 밤 {nightStartHour}", this);
            }
        }
    }
}
