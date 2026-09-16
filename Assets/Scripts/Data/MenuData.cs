using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Marea.Data
{
    /// <summary>메뉴 등급. 기획 MenuData.MenuTier. (+9/16)</summary>
    public enum MenuTier
    {
        Basic,
        Advanced,
    }

    /// <summary>
    /// 어느 요리 미니게임을 쓰는가. 기획 MenuData.MiniGameID. (+9/16)
    ///
    /// Marea.Cooking.CookingType과 값이 겹치지만 따로 둔다 — 데이터 계층이 B의 UI 계층을
    /// using 하게 만들 수는 없다. 둘을 잇는 변환은 CookingMenuUI가 들고 있다.
    ///
    /// 기획 CookingMiniGameData.MiniGameType 기준: Skewer=Placement,
    /// FishGrill=FlipTiming, Stew=GaugeKeep.
    /// </summary>
    public enum MiniGameId
    {
        None,
        Skewer,
        Stew,
        FishGrill,
    }

    /// <summary>
    /// 레시피 한 줄. 무엇을 몇 개, 몇 번째로 쓰는가.
    /// 기획 RecipeData 테이블을 MenuData 안에 임베드한 것이다 (#47) — 별도 SO로 빼면
    /// Warehouse.Has/Consume(계약 2)의 입력이 바뀐다.
    /// </summary>
    [Serializable]
    public struct RecipeEntry
    {
        [Tooltip("기획 RecipeData.IngredientID.")]
        public IngredientData ingredient;

        // count에서 이름만 바꿨다 (+9/16). 씬·프리팹에 이미 count로 직렬화된 값이 있어서
        // FormerlySerializedAs가 없으면 Warehouse.startingStock과 SupplyCrate.contents가
        // 전부 0으로 리셋된다 — 증상이 "테스트 씬에서 재료가 안 들어온다"라 원인이 안 보인다.
        [Tooltip("기획 RecipeData.RequiredAmount. 요리 1회 차감량.")]
        [FormerlySerializedAs("count")]
        [Min(1)] public int requiredAmount;

        /// <summary>
        /// 기획 RecipeData.InputOrder. 작은 수부터 들어간다.
        ///
        /// 순서 판정을 하는 미니게임만 본다 — 지금은 꼬치뿐이고, 창고 차감은 순서를 안 본다.
        /// 전에는 SkewerMinigameController가 배열 순서에 암묵 의존했다. 인스펙터에서 행을
        /// 위아래로 옮기면 조리 순서가 조용히 바뀌던 자리다. (#47)
        /// </summary>
        [Tooltip("기획 RecipeData.InputOrder. 작은 수부터 투입한다. 순서 판정 미니게임만 본다.")]
        public int inputOrder;
    }

    /// <summary>
    /// 판매 메뉴 하나의 정의. A와 B가 같이 쓰는 유일한 공유 데이터다.
    /// 필드를 늘릴 땐 상대에게 말하고 늘린다.
    ///
    /// 필드 순서는 기획 「데이터 별 필드 정의_요리」 시트의 MenuData 순서를 따른다 (+9/16).
    /// UnlockConditionID는 넣지 않았다 — 기획 시트에 「나중에 진행」으로 적혀 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Menu", fileName = "Menu_")]
    public class MenuData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("기획 MenuData.MenuID. 변경되지 않는 고유 키.")]
        [SerializeField] private int id;

        [Tooltip("기획 MenuData.Name. 플레이어 표시 이름.")]
        [SerializeField] private string displayName;

        [Tooltip("기획 MenuData.MenuTier.")]
        [SerializeField] private MenuTier menuTier = MenuTier.Basic;

        [Header("판매")]
        [Tooltip("기획 MenuData.BasePrice. 요리 판정 배율을 여기에 곱한다.")]
        [SerializeField, Min(0)] private int basePrice;

        [Tooltip("기획 MenuData.IsActive. 꺼두면 손님 주문 후보에서 빠진다.")]
        [SerializeField] private bool isActive = true;

        [Header("조리")]
        // 지금 어느 미니게임을 돌릴지는 씬의 CookingMenuUI.categoryDataList가 정한다.
        // 이 필드는 기획 테이블을 옮겨둔 것이고, 둘이 어긋나면 StartCooking이 에러를 낸다.
        // 디스패치를 이 값 기준으로 옮기는 건 CookingMiniGameData 테이블을 할 때 같이 한다. (#47)
        [Tooltip("기획 MenuData.MiniGameID. 지금은 씬 카테고리와 대조만 한다.")]
        [SerializeField] private MiniGameId miniGameId = MiniGameId.None;

        [Tooltip("기획 RecipeData. 투입 순서는 inputOrder가 정한다 — 배열 순서가 아니다.")]
        [SerializeField] private RecipeEntry[] recipe;

        [Header("비주얼 에셋")]
        // 기획은 FoodPrefabKey·IconKey를 리소스 키(문자열)로 뒀지만 직접 참조로 둔다 (#47).
        [Tooltip("기획 MenuData.FoodPrefabKey. 조리대/플레이어 손에 생성될 3D 모델 프리팹.")]
        [SerializeField] private GameObject servingPrefab;

        [Tooltip("기획 MenuData.IconKey. 손님 머리 위 주문 아이콘.")]
        [SerializeField] private Sprite icon;

        public int Id => id;
        public string DisplayName => displayName;
        public MenuTier MenuTier => menuTier;
        public int BasePrice => basePrice;
        public bool IsActive => isActive;
        public MiniGameId MiniGameId => miniGameId;

        /// <summary>
        /// 레시피 줄 전부. 인스펙터에 적힌 순서 그대로다 — 투입 순서가 필요하면
        /// inputOrder로 정렬해서 쓴다 (SkewerMinigameController가 그렇게 한다).
        /// </summary>
        public IReadOnlyList<RecipeEntry> Recipe => recipe;

        public GameObject ServingPrefab => servingPrefab;
        public Sprite Icon => icon;
    }
}
