using System.Text;
using Marea.Core;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 탐사 중 · 귀환 결과 창. (+10/2, 이슈 92 — 와이어프레임 「탐사 중 / 귀환」)
    ///
    /// A. 탐사 중: 해역 이름 · 남은 시간 · "탐사 완료 후 돌아옵니다". 재파견 버튼은 없다.
    /// B. 귀환: 식재료 +n / 특수 자원 +n 과 [확인]. 확인을 눌러야 창고에 들어간다 —
    ///    × 로 닫으면 안 받은 채 남고, 다시 열면 같은 결과가 보인다 (ExpeditionManager가 들고 있다).
    ///
    /// 창이 열린 채 귀환하면 그 자리에서 B로 바뀐다.
    ///
    /// (+10/12, 와이어프레임 「탐사정 결과」) B를 다시 짰다 — 왼쪽 지도에 다녀온 해역 강조 + 이름,
    /// 오른쪽에 「OO 탐사 완료!」 · 소요 시간 · 얻은 탐사물 · 짧은 이야기, 아래에 아이콘 줄과 [수령하기].
    /// 지도 그림과 해역 자리는 지역 선택 창(ExpeditionUI)의 것을 그대로 읽는다 — 자리를 바꾸면 둘 다 따라간다.
    /// </summary>
    public class ExpeditionStatusUI : UiPanel
    {
        // 크기를 바꾸는 창은 UiPanel.panel 이다. 장식 테두리는 그 창에 맞춰 늘어나게 앵커가 잡혀 있다.
        [Header("창 크기 (+10/12)")]
        [Tooltip("탐사 중 화면 크기. 와이어프레임 개편 전 크기 그대로다.")]
        [SerializeField] private Vector2 awaySize = new Vector2(660f, 440f);
        [Tooltip("귀환 결과 화면 크기. 지도 · 정보 · 아이콘 줄이 들어간다.")]
        [SerializeField] private Vector2 resultSize = new Vector2(940f, 570f);

        [Header("A. 탐사 중")]
        [SerializeField] private GameObject awayGroup;
        [SerializeField] private TextMeshProUGUI awayArea;
        [SerializeField] private TextMeshProUGUI awayRemaining;

        [Header("B. 귀환")]
        [SerializeField] private GameObject resultGroup;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button confirmButton;

        [Header("B. 귀환 — 지도 (+10/12)")]
        [Tooltip("지도 그림. 비우면 그림은 프리팹에 넣어둔 그대로 쓴다. 지역 선택 창 지도를 받아 갈아 끼운다.")]
        [SerializeField] private Image mapImage;
        [Tooltip("다녀온 해역 자리에 띄울 강조. mapImage의 자식이고 앵커가 가운데여야 한다.")]
        [SerializeField] private RectTransform areaHighlight;
        [Tooltip("강조 위 해역 이름.")]
        [SerializeField] private TextMeshProUGUI areaLabel;

        // (+10/9) 얻은 아이템 아이콘 칸. 템플릿(꺼둔 자식)을 복제한다 — 자식 Icon · Name · Count.
        // 아이콘이 없는 아이템은 Name 글자로 대신한다.
        [SerializeField] private Transform iconGrid;
        [SerializeField] private GameObject slotTemplate;

        private ExpeditionManager _manager;
        private ExpeditionUI _areaPicker;

        protected override void Awake()
        {
            base.Awake();
            if (awayGroup == null || resultGroup == null)
                Debug.LogError($"{name}: ExpeditionStatusUI의 awayGroup·resultGroup 중 비어 있는 게 있다.", this);
            if (confirmButton == null)
                Debug.LogError($"{name}: ExpeditionStatusUI.confirmButton이 비어 있다. 보상을 받을 수 없다.", this);
            else
                confirmButton.onClick.AddListener(HandleConfirm);
            if (iconGrid == null || slotTemplate == null)
                Debug.LogError($"{name}: ExpeditionStatusUI의 iconGrid·slotTemplate 중 비어 있는 게 있다. 결과 아이콘이 안 뜬다.", this);
            else
                slotTemplate.SetActive(false);
            if (mapImage == null || areaHighlight == null)
                Debug.LogError($"{name}: ExpeditionStatusUI의 mapImage·areaHighlight 중 비어 있는 게 있다. 지도에 해역이 안 보인다.", this);
            _areaPicker = FindAnyObjectByType<ExpeditionUI>(FindObjectsInactive.Include);
        }

        public void Open(ExpeditionManager manager)
        {
            if (_manager != null) _manager.OnStateChanged -= HandleStateChanged;
            _manager = manager;
            _manager.OnStateChanged += HandleStateChanged;

            Open();
            Refresh();
        }

        protected override void OnClosed()
        {
            if (_manager != null) _manager.OnStateChanged -= HandleStateChanged;
            _manager = null;
        }

        private void OnDestroy()
        {
            if (_manager != null) _manager.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (_manager == null || !IsOpen || _manager.Current != ExpeditionManager.State.Away) return;
            if (awayRemaining != null) awayRemaining.text = $"남은 시간 {TimeText.Format(_manager.RemainingSeconds)}";
        }

        private void HandleStateChanged(ExpeditionManager.State state)
        {
            // 대기로 돌아갔다 = 받았다. 열어둘 내용이 없다.
            if (state == ExpeditionManager.State.Idle) Close();
            else Refresh();
        }

        private void Refresh()
        {
            if (_manager == null) return;
            bool away = _manager.Current == ExpeditionManager.State.Away;
            bool returned = _manager.Current == ExpeditionManager.State.Returned;

            if (panel != null && (away || returned) && panel.transform is RectTransform frame)
                frame.sizeDelta = returned ? resultSize : awaySize;
            if (awayGroup != null) awayGroup.SetActive(away);
            if (resultGroup != null) resultGroup.SetActive(returned);

            if (away)
            {
                if (awayArea != null) awayArea.text = _manager.CurrentArea != null ? _manager.CurrentArea.DisplayName : string.Empty;
                if (awayRemaining != null) awayRemaining.text = $"남은 시간 {TimeText.Format(_manager.RemainingSeconds)}";
            }

            if (returned && resultText != null) resultText.text = ResultText(_manager);
            if (returned) RefreshIcons();
            if (returned) RefreshMap();
        }

        private void RefreshIcons()
        {
            if (iconGrid == null || slotTemplate == null) return;

            // 템플릿만 남기고 지운다. 결과는 한 번에 몇 개 안 돼서 풀링하지 않는다.
            for (int i = iconGrid.childCount - 1; i >= 0; i--)
            {
                GameObject child = iconGrid.GetChild(i).gameObject;
                if (child != slotTemplate) Destroy(child);
            }

            foreach (var (item, amount) in _manager.PendingReward)
            {
                if (item == null) continue;
                GameObject slot = Instantiate(slotTemplate, iconGrid);
                slot.SetActive(true);

                var icon = slot.transform.Find("Icon")?.GetComponent<Image>();
                var label = slot.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                var count = slot.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();

                bool hasIcon = item.Icon != null;
                if (icon != null) { icon.sprite = item.Icon; icon.enabled = hasIcon; }
                if (label != null) { label.text = item.DisplayName; label.enabled = !hasIcon; }
                if (count != null) count.text = $"x{amount}";
            }
        }

        private void RefreshMap()
        {
            ExpeditionAreaData area = _manager.CurrentArea;
            if (areaLabel != null) areaLabel.text = area != null ? area.DisplayName : string.Empty;
            if (mapImage == null || areaHighlight == null) return;

            if (_areaPicker != null && _areaPicker.MapSprite != null) mapImage.sprite = _areaPicker.MapSprite;

            // 해역 자리를 모르면 강조를 끈다 — 엉뚱한 곳(가운데)을 칠하는 것보다 낫다.
            int index = area != null && _manager.Areas != null ? System.Array.IndexOf(_manager.Areas, area) : -1;
            Vector2 point = Vector2.zero;
            bool known = _areaPicker != null && _areaPicker.TryGetMapPoint(index, out point);
            areaHighlight.gameObject.SetActive(known);
            if (!known) return;

            var map = (RectTransform)mapImage.transform;
            areaHighlight.anchoredPosition = Vector2.Scale(point, map.rect.size);

            // (+10/12) 가장자리 해역(깊은 바다)은 고리 아래 이름표가 지도 밖으로 나가 잘렸다.
            // 이름표까지 포함한 범위가 지도 안에 들어오게 그만큼 안쪽으로 민다.
            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(map, areaHighlight);
            Rect r = map.rect;
            Vector2 push = Vector2.zero;
            if (b.min.x < r.xMin) push.x = r.xMin - b.min.x; else if (b.max.x > r.xMax) push.x = r.xMax - b.max.x;
            if (b.min.y < r.yMin) push.y = r.yMin - b.min.y; else if (b.max.y > r.yMax) push.y = r.yMax - b.max.y;
            areaHighlight.anchoredPosition += push;
        }

        private void HandleConfirm()
        {
            // Collect가 대기로 돌리면 HandleStateChanged가 창을 닫는다.
            if (_manager != null) _manager.Collect();
        }

        // 기호를 안 쓴다 — Pretendard-Bold SDF에 · 가 없다.
        // (+10/12) 와이어프레임 순서: 「OO 탐사 완료!」 / 소요 시간 / 얻은 탐사물 / 짧은 이야기.
        private static string ResultText(ExpeditionManager manager)
        {
            string area = manager.CurrentArea != null ? manager.CurrentArea.DisplayName : "탐사";

            var items = new StringBuilder();
            foreach (var (item, amount) in manager.PendingReward)
            {
                if (item == null) continue;
                if (items.Length > 0) items.Append(", ");
                items.Append(item.DisplayName).Append(" x").Append(amount);
            }

            var sb = new StringBuilder();
            sb.Append("<size=115%><b>").Append(area).Append(" 탐사 완료!</b></size>\n\n");
            sb.Append("소요 시간 : ").Append(TimeText.Format(manager.TripSeconds)).Append('\n');
            sb.Append("얻은 탐사물 : ").Append(items.Length > 0 ? items.ToString() : "없음");

            // 이야기는 작고 옅게 — 결과(시간 · 탐사물)가 먼저 읽히게.
            if (!string.IsNullOrWhiteSpace(manager.PendingStory))
                sb.Append("\n\n<size=85%><color=#6E5236>").Append(manager.PendingStory.Trim()).Append("</color></size>");
            return sb.ToString();
        }
    }
}
