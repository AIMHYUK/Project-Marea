using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Economy
{
    /// <summary>
    /// 부서진 시설을 클릭하면 뜨는 해금 창. 이름 · 해금 후 쓸 수 있는 기능 · 비용 · 해금 버튼. (+9/28, 이슈 71)
    ///
    /// 기획 7-2 「해금 대상 선택」이 근거다. U 목록에서도 해금할 수 있고(이미 있음) 이 창은
    /// 월드에서 들어오는 창구다. 해금 자체는 둘 다 FacilityUpgrade.TryUnlock 한 곳을 탄다.
    ///
    /// 논블로킹이다 — 업그레이드 목록과 같은 판단 (UIStack은 B 몫인데 아직 없다).
    /// </summary>
    public class FacilityUnlockUI : MonoBehaviour
    {
        [Tooltip("여닫을 대상. 이 컴포넌트가 붙은 오브젝트와 따로 둔다.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private Button unlockButton;
        [SerializeField] private TextMeshProUGUI unlockButtonLabel;
        [SerializeField] private Button closeButton;

        private FacilityKind _kind;
        private Wallet _wallet;

        private void Awake()
        {
            if (panel == null)
                Debug.LogError($"{name}: FacilityUnlockUI.panel이 비어 있다. 해금 창이 안 뜬다.", this);
            if (unlockButton == null)
                Debug.LogError($"{name}: FacilityUnlockUI.unlockButton이 비어 있다. 해금할 수 없다.", this);

            if (unlockButton != null) unlockButton.onClick.AddListener(HandleUnlockClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panel != null) panel.SetActive(false);
        }

        private void Start()
        {
            _wallet = Wallet.Instance;
            if (_wallet != null) _wallet.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDestroy()
        {
            if (_wallet != null) _wallet.OnGoldChanged -= HandleGoldChanged;
        }

        /// <summary>이 시설의 해금 창을 연다. 이미 열려 있으면 내용만 바꾼다.</summary>
        public void Open(FacilityKind kind)
        {
            if (panel == null) return;

            _kind = kind;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void HandleUnlockClicked()
        {
            // 실패하면 창을 두고 사유를 다시 그린다. 버튼이 이미 꺼져 있어서 드문 경로다.
            if (FacilityUpgrade.TryUnlock(_kind) == FacilityUpgrade.Result.Ok) Close();
            else Refresh();
        }

        // 열어둔 채 정산이 끝나 골드가 들어오면 버튼이 켜져야 한다.
        private void HandleGoldChanged(int gold)
        {
            if (panel != null && panel.activeSelf) Refresh();
        }

        private void Refresh()
        {
            FacilityLevels levels = FacilityLevels.Instance;
            FacilityData data = levels != null ? levels.DataOf(_kind) : null;
            FacilityUpgrade.Result state = FacilityUpgrade.CanUnlock(_kind);

            SetText(nameLabel, data == null ? _kind.ToString()
                : string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName);
            SetText(descriptionLabel, data != null ? data.UnlockDescription : string.Empty);
            SetText(costLabel, data != null ? $"{data.UnlockCost:N0} G" : "—");

            if (unlockButton != null) unlockButton.interactable = state == FacilityUpgrade.Result.Ok;

            SetText(unlockButtonLabel, state switch
            {
                FacilityUpgrade.Result.Ok                  => "해금",
                FacilityUpgrade.Result.NotEnoughGold       => "골드 부족",
                FacilityUpgrade.Result.MissingPrerequisite => "선행 필요",
                FacilityUpgrade.Result.AlreadyUnlocked     => "해금됨",
                _                                          => "설정 오류",
            });
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
