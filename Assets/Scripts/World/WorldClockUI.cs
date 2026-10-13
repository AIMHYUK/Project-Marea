using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.World
{
    /// <summary>
    /// 우측 상단 시각 표시. <see cref="WorldClock"/> 의 구독자다. (+9/8)
    ///
    /// UI/ 폴더에 넣지 않는다 — 거기는 1차부터 B 몫이고, 2차 분담 문서가
    /// "A의 UI는 각자 자기 시스템 폴더 안에 둔다"로 정해 뒀다.
    ///
    /// 시간대의 한글 이름도 여기 있다. 표시용 문자열은 시계가 알 일이 아니다.
    ///
    /// (+10/12) 상단 중앙 텍스트에서 골드바 아래 둥근 시계로 바꿨다. 가운데 아이콘은 낮·저녁엔 해, 밤·새벽엔 달.
    /// (+10/13) 바늘은 12시간에 한 바퀴(하루 두 바퀴) — 판 눈금이 12칸이라 하루 한 바퀴면 06:00에 9시를 가리켜
    /// 아래 글자와 어긋났다. 오전·오후는 해·달 아이콘과 글자가 가른다.
    /// 텍스트(label)는 시계 옆 보조 표시로 남겼다.
    /// </summary>
    public class WorldClockUI : MonoBehaviour
    {
        [Header("표시할 곳")]
        [Tooltip("시각을 찍을 곳. 자동으로 못 찾으니 반드시 넣어야 한다 — "
               + "비면 OnEnable에서 에러를 내고 시각이 표시되지 않는다. (+9/8)")]
        [SerializeField] private TextMeshProUGUI label;

        [Header("둥근 시계 (+10/12)")]
        [Tooltip("하루에 한 바퀴 도는 바늘. 스프라이트는 위를 가리키고 피벗이 회전축이어야 한다.")]
        [SerializeField] private RectTransform hand;
        [Tooltip("해·달이 바뀌어 들어갈 자리.")]
        [SerializeField] private Image slotIcon;
        [SerializeField] private Sprite sunSprite;
        [SerializeField] private Sprite moonSprite;

        [Header("시계 안쪽 색 (+10/12)")]
        [Tooltip("원판 안쪽에 까는 흰 원. 시간대 색으로 칠한다.")]
        [SerializeField] private Image faceFill;
        [Tooltip("눈금. 밝은 판에선 진하게, 어두운 판에선 밝게.")]
        [SerializeField] private Image ticks;
        [SerializeField] private Color dayFace = new Color(0.70f, 0.87f, 0.98f);
        [SerializeField] private Color eveningFace = new Color(0.99f, 0.76f, 0.52f);
        [SerializeField] private Color nightFace = new Color(0.12f, 0.15f, 0.30f);
        [SerializeField] private Color dawnFace = new Color(0.36f, 0.33f, 0.55f);
        [SerializeField] private Color ticksOnLight = new Color(0.36f, 0.21f, 0.10f);
        [SerializeField] private Color ticksOnDark = new Color(0.95f, 0.85f, 0.64f);
        [Tooltip("판 안의 구름들. 눈금처럼 판 밝기를 따라 색이 바뀐다.")]
        [SerializeField] private Image[] clouds;
        [SerializeField] private Color cloudsOnLight = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color cloudsOnDark = new Color(0.55f, 0.60f, 0.80f, 0.45f);
        [Tooltip("시간대가 바뀔 때 색이 넘어가는 시간(초).")]
        [SerializeField] private float fadeSeconds = 1.5f;

        [Header("읽어올 시계")]
        [Tooltip("비워두면 WorldClock.Instance 를 쓴다.")]
        [SerializeField] private WorldClock clock;

        [Header("확인용")]
        [Tooltip("시간대가 바뀔 때마다 Console에 찍는다. 한 바퀴에 네 번 뜨는지 세는 용도다.")]
        [SerializeField] private bool logSlotChange = true;

        private string _slotName = string.Empty;
        private Color _faceFrom, _faceTo, _ticksFrom, _ticksTo, _cloudsFrom, _cloudsTo;
        private float _fade = 1f;

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
            ApplyIcon(clock.Slot);
            ApplyColors(clock.Slot, instant: true);
        }

        private void OnDisable()
        {
            if (clock != null) clock.OnSlotChanged -= HandleSlotChanged;
        }

        private void Update()
        {
            if (clock == null || label == null) return;

            // 0시 · 12시에 0°로 위를 가리킨다. z가 줄어야 시계방향이다. (+10/13) 12시간에 한 바퀴.
            if (hand != null) hand.localRotation = Quaternion.Euler(0f, 0f, -(clock.Hour % 12f) / 12f * 360f);

            if (_fade < 1f)
            {
                _fade = fadeSeconds > 0f ? Mathf.Min(1f, _fade + Time.deltaTime / fadeSeconds) : 1f;
                if (faceFill != null) faceFill.color = Color.Lerp(_faceFrom, _faceTo, _fade);
                if (ticks != null) ticks.color = Color.Lerp(_ticksFrom, _ticksTo, _fade);
                SetCloudColor(Color.Lerp(_cloudsFrom, _cloudsTo, _fade));
            }

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
            ApplyIcon(slot);
            ApplyColors(slot, instant: false);

            if (logSlotChange) Debug.Log($"[WorldClock] 시간대 전환 → {_slotName}", this);
        }

        private void ApplyIcon(TimeSlot slot)
        {
            if (slotIcon == null) return;
            bool sun = slot == TimeSlot.Day || slot == TimeSlot.Evening;
            slotIcon.sprite = sun ? sunSprite : moonSprite;
        }

        private void ApplyColors(TimeSlot slot, bool instant)
        {
            _faceTo = slot switch
            {
                TimeSlot.Day => dayFace,
                TimeSlot.Evening => eveningFace,
                TimeSlot.Night => nightFace,
                _ => dawnFace,
            };
            // 밝기로 판단한다 — 색을 인스펙터에서 바꿔도 눈금이 따라간다.
            bool light = _faceTo.grayscale > 0.5f;
            _ticksTo = light ? ticksOnLight : ticksOnDark;
            _cloudsTo = light ? cloudsOnLight : cloudsOnDark;
            _faceFrom = faceFill != null ? faceFill.color : _faceTo;
            _ticksFrom = ticks != null ? ticks.color : _ticksTo;
            _cloudsFrom = clouds != null && clouds.Length > 0 && clouds[0] != null ? clouds[0].color : _cloudsTo;
            _fade = 0f;

            if (!instant) return;
            _fade = 1f;
            if (faceFill != null) faceFill.color = _faceTo;
            if (ticks != null) ticks.color = _ticksTo;
            SetCloudColor(_cloudsTo);
        }

        private void SetCloudColor(Color c)
        {
            if (clouds == null) return;
            foreach (Image cloud in clouds)
                if (cloud != null) cloud.color = c;
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
