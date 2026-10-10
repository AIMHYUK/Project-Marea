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
    /// </summary>
    public class ExpeditionStatusUI : UiPanel
    {
        [Header("A. 탐사 중")]
        [SerializeField] private GameObject awayGroup;
        [SerializeField] private TextMeshProUGUI awayArea;
        [SerializeField] private TextMeshProUGUI awayRemaining;

        [Header("B. 귀환")]
        [SerializeField] private GameObject resultGroup;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button confirmButton;

        // (+10/9) 얻은 아이템 아이콘 칸. 템플릿(꺼둔 자식)을 복제한다 — 자식 Icon · Name · Count.
        // 아이콘이 없는 아이템은 Name 글자로 대신한다.
        [SerializeField] private Transform iconGrid;
        [SerializeField] private GameObject slotTemplate;

        private ExpeditionManager _manager;

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

            if (awayGroup != null) awayGroup.SetActive(away);
            if (resultGroup != null) resultGroup.SetActive(returned);

            if (away)
            {
                if (awayArea != null) awayArea.text = _manager.CurrentArea != null ? _manager.CurrentArea.DisplayName : string.Empty;
                if (awayRemaining != null) awayRemaining.text = $"남은 시간 {TimeText.Format(_manager.RemainingSeconds)}";
            }

            if (returned && resultText != null) resultText.text = ResultText(_manager);
            if (returned) RefreshIcons();
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

        private void HandleConfirm()
        {
            // Collect가 대기로 돌리면 HandleStateChanged가 창을 닫는다.
            if (_manager != null) _manager.Collect();
        }

        // 기호를 안 쓴다 — Pretendard-Bold SDF에 · 가 없다.
        private static string ResultText(ExpeditionManager manager)
        {
            var food = new StringBuilder();
            var special = new StringBuilder();
            foreach (var (item, amount) in manager.PendingReward)
            {
                if (item == null) continue;
                StringBuilder sb = item.ItemType == ItemType.SpecialResource ? special : food;
                sb.Append(item.DisplayName).Append(" +").Append(amount).Append('\n');
            }

            if (food.Length == 0 && special.Length == 0) return "빈손으로 돌아왔습니다";
            // (+10/9) 분류 이름은 작고 옅게 — 양피지 위에서 항목이 먼저 읽히게.
            const string head = "<size=80%><color=#8A6A45>";
            const string tail = "</color></size>";
            return $"{head}식재료{tail}\n{(food.Length > 0 ? food.ToString() : "없음\n")}\n{head}특수 자원{tail}\n{(special.Length > 0 ? special.ToString() : "없음\n")}".TrimEnd();
        }
    }
}
