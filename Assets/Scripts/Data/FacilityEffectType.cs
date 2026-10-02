namespace Marea.Data
{
    /// <summary>
    /// 시설 업그레이드 효과 종류. 기획 「업그레이드_해금 데이터테이블」 Effect Type 정의 시트 그대로다. (+10/2)
    ///
    /// 값은 각 단계의 <b>누적 총합</b>이다 — 탐사 시간 Lv.1 0.1, Lv.3 0.2 는 "Lv.3이면 총 -20%".
    /// 그래서 효과를 읽는 쪽은 더하지 않고 "지금 레벨까지 중 가장 높은 단계의 값"을 쓴다
    /// (FacilityLevels.EffectValue).
    /// 순서를 바꾸지 말 것 — 에셋에 정수로 저장된다. 늘릴 땐 뒤에 붙인다.
    /// </summary>
    public enum FacilityEffectType
    {
        None,
        BusinessTimeAdd,       // BUSINESS_TIME_ADD 영업시간 증가(초)
        TableAdd,              // TABLE_ADD 테이블 추가(개)
        MenuSlotAdd,           // MENU_SLOT_ADD 오늘의 메뉴 선택 가능 수 증가
        SettlementMultiplier,  // SETTLEMENT_MULTIPLIER 최종 정산 골드 배율
        CookingStationUnlock,  // COOKING_STATION_UNLOCK 조리대 오픈 (대상: 조리대 ID)
        MenuUnlock,            // MENU_UNLOCK 메뉴 오픈 (대상: 메뉴 ID)
        CropGrowTimeReduce,    // CROP_GROW_TIME_REDUCE 작물 성장시간 감소 비율
        HarvestBonus,          // HARVEST_BONUS 수확량 증가 (확률 있음)
        FarmSlotAdd,           // FARM_SLOT_ADD 동시 재배 칸 증가
        CropGradeUnlock,       // CROP_GRADE_UNLOCK 작물 등급 해금 (대상: 등급)
        ExploreTimeReduce,     // EXPLORE_TIME_REDUCE 탐사시간 감소 비율
        ExploreRewardAdd,      // EXPLORE_REWARD_ADD 탐사 보상 증가
        RegionUnlock,          // REGION_UNLOCK 해역 해금 (대상: 해역 ID)
        LandLevelUnlock,       // LAND_LEVEL_UNLOCK 부지 확장 단계 구매 가능 (대상: 해금 ID)
        AutoServe,             // AUTO_SERVE 직원 자동 서빙
        StaffSpeedAdd,         // STAFF_SPEED_ADD 직원 이동·작업 속도 증가 비율
        StaffCarryAdd,         // STAFF_CARRY_ADD 직원 운반량 증가
    }
}
