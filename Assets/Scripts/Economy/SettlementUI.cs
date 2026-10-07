using System.Collections;
using Marea.Core;
using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Economy
{
    /// <summary>
    /// 영업 정산 창. (+10/7) 기획 와이어프레임 「영업 정산」.
    ///
    /// 왼쪽 「이번 영업 결과」 — 총 판매량, Perfect · Good · Bad/Fail 수.
    /// 오른쪽 — 총매출 · 업그레이드 보너스 · 직원 인건비 → 최종 수익(숫자가 올라간다).
    /// [확인]을 누르면 다음 날 준비로 넘어간다. 돈은 이미 BusinessManager가 같은 Settlement.Calculate로 넣었다 —
    /// 여기서 또 넣으면 두 번 들어간다.
    /// </summary>
    public class SettlementUI : UiPanel
    {
        [Header("이번 영업 결과")]
        [SerializeField] private TextMeshProUGUI servedLabel;
        [SerializeField] private TextMeshProUGUI perfectLabel;
        [SerializeField] private TextMeshProUGUI goodLabel;
        [SerializeField] private TextMeshProUGUI badLabel;

        [Header("수익")]
        [SerializeField] private TextMeshProUGUI revenueLabel;
        [Tooltip("업그레이드 정산 배율 줄. 배율이 1이면 통째로 숨긴다.")]
        [SerializeField] private TextMeshProUGUI bonusLabel;
        [SerializeField] private TextMeshProUGUI wageLabel;
        [SerializeField] private TextMeshProUGUI finalLabel;

        [Header("확인")]
        [SerializeField] private Button confirmButton;

        [Header("연출")]
        [Tooltip("최종 수익 숫자가 0에서 올라가는 시간.")]
        [SerializeField, Min(0f)] private float countUpSeconds = 1.2f;
        [SerializeField] private AudioClip coinClip;
        [SerializeField, Range(0f, 1f)] private float coinVolume = 0.8f;

        private int _final;
        private Coroutine _count;

        protected override void Awake()
        {
            base.Awake();
            if (confirmButton == null)
                Debug.LogError($"{name}: SettlementUI.confirmButton이 비어 있다. 정산 뒤 다음 날로 못 넘어간다.", this);
            else
                confirmButton.onClick.AddListener(Confirm);
        }

        protected override void Start()
        {
            base.Start();
            if (BusinessManager.Instance == null)
            {
                Debug.LogError($"{name}: 씬에 BusinessManager가 없다. 정산 창이 안 뜬다.", this);
                return;
            }
            BusinessManager.Instance.OnBusinessEnded += Show;
        }

        private void OnDestroy()
        {
            if (BusinessManager.Instance != null) BusinessManager.Instance.OnBusinessEnded -= Show;
        }

        private void Show(DailySalesData sales)
        {
            Settlement.Result r = Settlement.Calculate(sales.totalRevenue);
            _final = r.Final;

            Set(servedLabel, $"총 판매량  {sales.totalServedCount}개");
            Set(perfectLabel, $"Perfect  {sales.perfectCount}개");
            Set(goodLabel, $"Good  {sales.goodCount}개");
            Set(badLabel, $"Bad / Fail  {sales.badCount}개");

            Set(revenueLabel, $"총매출  +{r.Revenue:N0} G");
            if (bonusLabel != null)
            {
                bonusLabel.gameObject.SetActive(r.Bonus > 0);
                bonusLabel.text = $"업그레이드 보너스 ×{r.Multiplier:0.##}  +{r.Bonus:N0} G";
            }
            Set(wageLabel, $"직원 인건비  -{r.Wage:N0} G");
            Set(finalLabel, "0 G");

            Open();
        }

        protected override void OnOpened()
        {
            if (_count != null) StopCoroutine(_count);
            _count = StartCoroutine(CountUp());
        }

        private IEnumerator CountUp()
        {
            for (float t = 0f; t < countUpSeconds; t += Time.unscaledDeltaTime)
            {
                Set(finalLabel, $"{Mathf.RoundToInt(_final * Mathf.SmoothStep(0f, 1f, t / countUpSeconds)):N0} G");
                yield return null;
            }
            Set(finalLabel, $"{_final:N0} G");
            SoundManager.Play(coinClip, coinVolume);
            _count = null;
        }

        protected override void OnClosed()
        {
            // ×로 닫아도 다음 날로 넘어가야 한다 — 안 그러면 정산 상태에 갇힌다.
            if (BusinessManager.Instance != null && BusinessManager.Instance.CurrentState == BusinessState.Settlement)
                BusinessManager.Instance.PrepareNextDay();
        }

        private void Confirm() => Close();

        private static void Set(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
