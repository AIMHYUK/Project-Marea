using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.UI
{
    public class BusinessUIController : MonoBehaviour
    {
        [Header("상단 타이머 UI")]
        [SerializeField] private GameObject timerRoot;
        [SerializeField] private TextMeshProUGUI txtTimer;

        [Header("영업 시작 UI (준비 단계)")]
        [SerializeField] private GameObject openPromptRoot;
        [SerializeField] private Button btnStartBusiness;

        [Header("일일 정산 팝업 UI")]
        [SerializeField] private GameObject settlementPanel;
        [SerializeField] private TextMeshProUGUI txtTotalServed;
        [SerializeField] private TextMeshProUGUI txtPerfectCount;
        [SerializeField] private TextMeshProUGUI txtGoodCount;
        [SerializeField] private TextMeshProUGUI txtTotalRevenue;
        [SerializeField] private Button btnConfirmSettlement;

        private bool _isPlayerInZone;

        private void Start()
        {
            if (BusinessManager.Instance != null)
            {
                BusinessManager.Instance.OnTimerUpdated += UpdateTimerText;
                BusinessManager.Instance.OnStateChanged += HandleStateChanged;
                BusinessManager.Instance.OnBusinessEnded += ShowSettlementPopup;
            }

            if (btnStartBusiness != null)
            {
                btnStartBusiness.onClick.AddListener(() => BusinessManager.Instance.StartBusiness());
            }

            if (btnConfirmSettlement != null)
            {
                btnConfirmSettlement.onClick.AddListener(OnClickConfirmSettlement);
            }

            InitUI();
        }

        private void OnDestroy()
        {
            if (BusinessManager.Instance != null)
            {
                BusinessManager.Instance.OnTimerUpdated -= UpdateTimerText;
                BusinessManager.Instance.OnStateChanged -= HandleStateChanged;
                BusinessManager.Instance.OnBusinessEnded -= ShowSettlementPopup;
            }
        }

        private void InitUI()
        {
            if (openPromptRoot != null) openPromptRoot.SetActive(true);
            if (timerRoot != null) timerRoot.SetActive(false);
            if (settlementPanel != null) settlementPanel.SetActive(false);
        }

        // 영역 진입 상태 갱신
        public void SetPlayerInZone(bool inZone)
        {
            _isPlayerInZone = inZone;
            UpdateOpenPromptVisibility();
        }

        // 준비 단계와 영역 내 위치 여부를 모두 만족할 때만 버튼 활성화
        private void UpdateOpenPromptVisibility()
        {
            bool isReady = BusinessManager.Instance != null && BusinessManager.Instance.CurrentState == BusinessState.Ready;
            if (openPromptRoot != null)
            {
                openPromptRoot.SetActive(isReady && _isPlayerInZone);
            }
        }
        private void HandleStateChanged(BusinessState state)
        {
            switch (state)
            {
                case BusinessState.Ready:
                    if (openPromptRoot != null) openPromptRoot.SetActive(true);
                    if (timerRoot != null) timerRoot.SetActive(false);
                    if (settlementPanel != null) settlementPanel.SetActive(false);
                    break;

                case BusinessState.Open:
                    if (openPromptRoot != null) openPromptRoot.SetActive(false);
                    if (timerRoot != null) timerRoot.SetActive(true);
                    if (settlementPanel != null) settlementPanel.SetActive(false);
                    break;

                case BusinessState.Settlement:
                    if (timerRoot != null) timerRoot.SetActive(false);
                    break;
            }
        }

        private void UpdateTimerText(float seconds)
        {
            if (txtTimer == null) return;

            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            txtTimer.text = $"{m:00}:{s:00}";
        }

        private void ShowSettlementPopup(DailySalesData sales)
        {
            if (settlementPanel == null) return;

            settlementPanel.SetActive(true);

            if (txtTotalServed != null) txtTotalServed.text = $"총 판매 수량: {sales.totalServedCount}개";
            if (txtPerfectCount != null) txtPerfectCount.text = $"Perfect 조리: {sales.perfectCount}회";
            if (txtGoodCount != null) txtGoodCount.text = $"Good 조리: {sales.goodCount}회";
            if (txtTotalRevenue != null) txtTotalRevenue.text = $"총 매출: {sales.totalRevenue:N0} Gold";
        }

        private void OnClickConfirmSettlement()
        {
            if (settlementPanel != null) settlementPanel.SetActive(false);
            BusinessManager.Instance.PrepareNextDay();
        }
    }
}
