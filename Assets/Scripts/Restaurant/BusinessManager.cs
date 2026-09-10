using System;
using System.Collections;
using System.Collections.Generic;
using Marea.Cooking;
using Marea.Economy;
using UnityEngine;

namespace Marea.Restaurant
{
    public enum BusinessState
    {
        Ready,      // 영업 준비 단계
        Open,       // 영업 중 (타이머 감소 및 손님 입장)
        Settlement  // 정산 단계
    }

    [Serializable]
    public class DailySalesData
    {
        public int totalServedCount;
        public int perfectCount;
        public int goodCount;
        public int totalRevenue;

        public void Reset()
        {
            totalServedCount = 0;
            perfectCount = 0;
            goodCount = 0;
            totalRevenue = 0;
        }

        public void RecordSale(HitGrade grade, int price)
        {
            totalServedCount++;
            totalRevenue += price;

            if (grade == HitGrade.Perfect) perfectCount++;
            else perfectCount += 0; // Good 또는 기본 판정

            if (grade == HitGrade.Good) goodCount++;
        }
    }

    public class BusinessManager : MonoBehaviour
    {
        public static BusinessManager Instance { get; private set; }

        [Header("영업 설정")]
        [SerializeField] private float businessDuration = 180f; // 기본 3분

        [Header("참조 컴포넌트")]
        [SerializeField] private CustomerManager customerManager;
        [SerializeField] private SkewerMinigameUI skewerMinigameUI;
        [SerializeField] private CookingMenuUI cookingMenuUI;

        public BusinessState CurrentState { get; private set; } = BusinessState.Ready;
        public float RemainingTime { get; private set; }
        public DailySalesData TodaySales { get; private set; } = new();

        public event Action<float> OnTimerUpdated;
        public event Action<BusinessState> OnStateChanged;
        public event Action<DailySalesData> OnBusinessEnded;

        private Coroutine _timerCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            RemainingTime = businessDuration;
        }

        private void Start()
        {
            // 준비 단계에서는 손님 스폰 중지 상태 유지
            if (customerManager != null)
            {
                customerManager.PauseSpawning(true);
            }
        }

        // 플레이어 상호작용 또는 UI 버튼을 통해 영업 개시
        public void StartBusiness()
        {
            if (CurrentState != BusinessState.Ready) return;

            CurrentState = BusinessState.Open;
            RemainingTime = businessDuration;
            TodaySales.Reset();

            if (customerManager != null)
            {
                customerManager.PauseSpawning(false);
            }

            OnStateChanged?.Invoke(CurrentState);

            if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
            _timerCoroutine = StartCoroutine(BusinessTimerRoutine());

            Debug.Log("[BusinessManager] 영업을 시작합니다! (영업 시간: 180초)");
        }

        private IEnumerator BusinessTimerRoutine()
        {
            while (RemainingTime > 0f)
            {
                RemainingTime -= Time.deltaTime;
                OnTimerUpdated?.Invoke(Mathf.Max(0f, RemainingTime));
                yield return null;
            }

            RemainingTime = 0f;
            EndBusiness();
        }

        // 영업 종료 처리
        public void EndBusiness()
        {
            if (CurrentState != BusinessState.Open) return;

            CurrentState = BusinessState.Settlement;
            if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);

            // 신규 손님 스폰 정지 및 매장 내 손님 일괄 강제 정리
            if (customerManager != null)
            {
                customerManager.PauseSpawning(true);
                customerManager.ClearAllCustomers();
            }

            // 플레이어가 손에 들고 있는 요리 강제 회수/폐기
            PlayerServingController playerServing = FindFirstObjectByType<PlayerServingController>(FindObjectsInactive.Include);
            if (playerServing != null)
            {
                playerServing.ClearHeldFood();
            }

            // 열려 있는 요리 UI 및 미니게임 강제 닫기
            if (cookingMenuUI != null) cookingMenuUI.Close();

            // 오늘 번 돈을 지갑에 넣는다. 여기가 정산이 확정되는 유일한 지점이라
            // 계약 5의 "음식 하나 팔 때마다가 아니라 정산 확정 시 한 번"에 해당한다. (+9/10)
            //
            // ⚠️ A가 먼저 붙였다 (이슈 32). B가 정산에 Wallet.Add를 따로 넣으면
            //    같은 매출이 두 번 들어온다 — 붙이기 전에 이 줄을 먼저 볼 것.
            //
            // OnBusinessEnded보다 앞에 둔다. 정산 팝업이 뜬 뒤에 넣으면
            // 팝업에 적힌 매출과 그 순간 화면의 보유 골드가 한 프레임 어긋난다.
            if (Wallet.Instance != null)
            {
                Wallet.Instance.Add(TodaySales.totalRevenue);

                // 이 씬에는 골드를 보여주는 UI가 없다(UpgradeUI는 다른 씬). 잔액을 안 찍으면
                // 입금이 됐는지 확인할 방법이 아예 없어서 여기서 한 줄 남긴다. (+9/10)
                Debug.Log($"[BusinessManager] 지갑 입금: +{TodaySales.totalRevenue}G → 잔액 {Wallet.Instance.Gold}G");
            }
            else
            {
                // 조용히 넘어가면 증상이 "하루 종일 팔았는데 골드가 그대로"다.
                Debug.LogError("[BusinessManager] 씬에 Wallet이 없다. "
                             + $"오늘 매출 {TodaySales.totalRevenue}G가 지갑에 들어가지 못했다.", this);
            }

            OnStateChanged?.Invoke(CurrentState);
            OnBusinessEnded?.Invoke(TodaySales);

            Debug.Log($"[BusinessManager] 영업 종료! 총 서빙: {TodaySales.totalServedCount}, 총 매출: {TodaySales.totalRevenue}G");
        }

        // 손님 서빙 성공 시 매출 기록
        public void RecordServedDish(HitGrade grade, int price)
        {
            if (CurrentState != BusinessState.Open) return;

            TodaySales.RecordSale(grade, price);
            Debug.Log($"[BusinessManager] 서빙 기록 완료: +{price}G (판정: {grade})");
        }

        // 정산 확인 후 다음 날 준비 단계로 전환
        public void PrepareNextDay()
        {
            CurrentState = BusinessState.Ready;
            RemainingTime = businessDuration;
            OnTimerUpdated?.Invoke(RemainingTime);
            OnStateChanged?.Invoke(CurrentState);

            Debug.Log("[BusinessManager] 다음 날 영업 준비 상태로 전환되었습니다.");
        }
    }
}
