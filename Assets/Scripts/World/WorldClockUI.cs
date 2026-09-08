using TMPro;
using UnityEngine;

namespace Marea.World
{
    /// <summary>
    /// 우측 상단 시각 표시. <see cref="WorldClock"/> 의 구독자다. (+9/8)
    ///
    /// UI/ 폴더에 넣지 않는다 — 거기는 1차부터 B 몫이고, 2차 분담 문서가
    /// "A의 UI는 각자 자기 시스템 폴더 안에 둔다"로 정해 뒀다.
    ///
    /// 시간대의 한글 이름도 여기 있다. 표시용 문자열은 시계가 알 일이 아니다.
    /// </summary>
    public class WorldClockUI : MonoBehaviour
    {
        [Header("표시할 곳")]
        [SerializeField] private TextMeshProUGUI label;

        [Header("읽어올 시계")]
        [Tooltip("비워두면 WorldClock.Instance 를 쓴다.")]
        [SerializeField] private WorldClock clock;

        [Header("확인용")]
        [Tooltip("시간대가 바뀔 때마다 Console에 찍는다. 한 바퀴에 네 번 뜨는지 세는 용도다.")]
        [SerializeField] private bool logSlotChange = true;

        private string _slotName = string.Empty;

        private void OnEnable()
        {
            if (clock == null) clock = WorldClock.Instance;

            // 둘 중 하나라도 비면 화면에 아무것도 안 뜨는데, 그건 "아직 시간이
            // 안 갔다"와 구분이 안 된다. 조용히 넘어가지 않는다.
            if (clock == null)
            {
                Debug.LogError(
                    "[WorldClockUI] WorldClock을 못 찾았습니다. 인스펙터에 넣거나, " +
                    "씬에 WorldClock이 있는지 확인하세요. 시각이 표시되지 않습니다.", this);
                return;
            }

            if (label == null)
            {
                Debug.LogError(
                    "[WorldClockUI] label이 비어 있습니다. 인스펙터에 TextMeshProUGUI를 넣으세요.", this);
                return;
            }

            clock.OnSlotChanged += HandleSlotChanged;

            // 시계는 시작 이벤트를 쏘지 않는다. 초기 상태는 직접 읽어 맞춘다.
            _slotName = NameOf(clock.Slot);
        }

        private void OnDisable()
        {
            if (clock != null) clock.OnSlotChanged -= HandleSlotChanged;
        }

        private void Update()
        {
            if (clock == null || label == null) return;

            float hour = clock.Hour;
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);

            // 구분자에 가운뎃점(·)을 쓰면 안 된다. 한글 폰트 아틀라스에 U+00B7이
            // 없어서 TMP가 공백으로 바꾸고 경고를 매 프레임 찍는다. 없는 기호 목록은
            // Docs/개발_환경.md 「한글 폰트 아틀라스에 없는 글자가 있다」 참고. (9/8)
            label.text = $"{_slotName} {h:00}:{m:00}";
        }

        private void HandleSlotChanged(TimeSlot slot)
        {
            _slotName = NameOf(slot);

            if (logSlotChange) Debug.Log($"[WorldClock] 시간대 전환 → {_slotName}", this);
        }

        private static string NameOf(TimeSlot slot) => slot switch
        {
            TimeSlot.Day => "낮",
            TimeSlot.Evening => "저녁",
            TimeSlot.Night => "밤",
            TimeSlot.Dawn => "새벽",
            _ => slot.ToString(),
        };
    }
}
