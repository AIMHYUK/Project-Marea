using Marea.Data;
using UnityEngine;

namespace Marea.Economy
{
    /// <summary>
    /// 하루 정산 금액 계산. (+10/7) 기획 와이어프레임 「영업 정산」 — 총매출 - 직원 인건비 = 최종 수익.
    ///
    /// 매출(B의 DailySalesData)에 영업 시설 정산 배율(SETTLEMENT_MULTIPLIER)을 곱하고, 직원을 고용했으면
    /// 하루 인건비를 뺀다. 입금(BusinessManager)과 정산 창(SettlementUI)이 같은 값을 쓰게 여기 한 곳에서 계산한다.
    /// 인건비는 기획 표에 아직 없어 정해진 값 하나다 — 표가 생기면 FacilityData로 옮긴다.
    /// </summary>
    public static class Settlement
    {
        /// <summary>직원(서빙) 하루 인건비. 직원 운영 Lv1(AUTO_SERVE)부터 붙는다.</summary>
        public const int StaffDailyWage = 20;

        public readonly struct Result
        {
            public readonly int Revenue;     // 손님에게 받은 돈 합계
            public readonly int Bonus;       // 정산 배율로 더 받은 돈
            public readonly float Multiplier;
            public readonly int Wage;        // 직원 인건비 (양수)
            public int Final => Mathf.Max(0, Revenue + Bonus - Wage);

            public Result(int revenue, int bonus, float multiplier, int wage)
            {
                Revenue = revenue;
                Bonus = bonus;
                Multiplier = multiplier;
                Wage = wage;
            }
        }

        // (+10/9, 이슈 128) 특별 이벤트(선장) 성공 — 그날만 수익 배율을 더 곱한다. 일차로 묶어서 다음 날엔 저절로 풀린다.
        private static int _eventDay = -1;
        private static float _eventMultiplier = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _eventDay = -1;
            _eventMultiplier = 1f;
        }

        /// <summary>(+10/9) 오늘 정산에 이벤트 배율이 들어갔는가 — 정산 창이 보너스 이름을 바꾼다.</summary>
        public static bool EventBonusToday => Marea.Restaurant.BusinessManager.Instance != null
                                              && Marea.Restaurant.BusinessManager.Instance.Day == _eventDay && _eventMultiplier != 1f;

        /// <summary>(+10/9) 이 일차의 정산에만 배율을 더 곱한다. 예: 선장 이벤트 성공 → ×2.</summary>
        public static void SetEventMultiplier(int day, float multiplier)
        {
            _eventDay = day;
            _eventMultiplier = Mathf.Max(0f, multiplier);
        }

        public static Result Calculate(int revenue)
        {
            FacilityLevels levels = FacilityLevels.Instance;
            float m = levels != null ? levels.SettlementBonus : 1f;
            if (Marea.Restaurant.BusinessManager.Instance != null && Marea.Restaurant.BusinessManager.Instance.Day == _eventDay)
                m *= _eventMultiplier;
            int bonus = Mathf.RoundToInt(revenue * (m - 1f));
            bool hired = levels != null && levels.EffectValue(FacilityKind.Staff, FacilityEffectType.AutoServe) >= 1f;
            return new Result(revenue, bonus, m, hired ? StaffDailyWage : 0);
        }
    }
}
