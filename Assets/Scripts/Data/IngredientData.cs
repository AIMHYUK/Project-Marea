using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 아이템 분류. 기획 ItemData.ItemType. (+9/16)
    /// 특수 자원은 시설 업그레이드 비용(FacilityUpgradeData.CostResourceID)으로 쓰인다.
    /// </summary>
    public enum ItemType
    {
        Ingredient,        // 일반 식재료 — 요리에 들어간다
        SpecialResource,   // 특수 자원 — 상위 성장에 쓴다
    }

    /// <summary>아이템 희귀도. 기획 ItemData.Grade. (+9/16)</summary>
    public enum ItemGrade
    {
        Common,
        Rare,
        Epic,
    }

    /// <summary>
    /// 아이템 획득 경로. 기획 ItemData.ObtainRoute. (+9/16)
    /// 기획 테이블 목록의 수급처를 그대로 옮긴 것이다 — 보급품 상자·농사·낚시·탐사·상점.
    /// </summary>
    public enum ObtainRoute
    {
        SupplyCrate,   // 보급품 상자 (SupplyRewardData)
        Farming,       // 농사 (CropData)
        Fishing,       // 낚시 (FishingRewardData)
        Expedition,    // 탐사 (ExpeditionRewardData)
        Shop,          // 상점 구매
    }

    /// <summary>
    /// 창고에 쌓이는 식재료 한 종류의 정의.
    /// 변하지 않는 값만 넣는다. 보유 수량 같은 건 Warehouse가 들고 있다.
    ///
    /// 필드 순서는 기획 「데이터 별 필드 정의_자원」 시트의 ItemData 순서를 따른다 (+9/16).
    /// 인스펙터를 기획 표와 나란히 놓고 볼 수 있어야 빠진 값이 눈에 띈다.
    ///
    /// 기획 테이블명은 ItemData지만 클래스명은 IngredientData로 둔다 — 계약 1(A·B 공동)이라
    /// 타입명을 바꾸면 B 쪽이 같이 깨진다. 필드만 맞춘다. (#47)
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Ingredient", fileName = "Ingredient_")]
    public class IngredientData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("기획 ItemData.ItemID. 중복 불가.")]
        [SerializeField] private int id;

        [Tooltip("기획 ItemData.Name. 인벤토리·UI 표시 이름.")]
        [SerializeField] private string displayName;

        [Header("분류")]
        [Tooltip("기획 ItemData.ItemType.")]
        [SerializeField] private ItemType itemType = ItemType.Ingredient;

        [Tooltip("기획 ItemData.Grade.")]
        [SerializeField] private ItemGrade grade = ItemGrade.Common;

        [Tooltip("기획 ItemData.MaxStack. 창고가 이 수를 넘겨 받지 않는다 (Warehouse.Add).")]
        [SerializeField, Min(1)] private int maxStack = 999;

        [Header("리소스")]
        // 기획은 IconKey·PrefabKey를 리소스 키(문자열)로 뒀지만 여기서는 직접 참조로 둔다 (#47).
        // 문자열 키는 Addressables/Resources 로딩 계층이 전제인데 프로젝트에 아직 없고,
        // 참조로 두면 인스펙터에서 빈 칸과 오타가 바로 보인다.
        [Tooltip("기획 ItemData.IconKey. 창고·메뉴 UI 아이콘.")]
        [SerializeField] private Sprite icon;

        [Tooltip("기획 ItemData.PrefabKey. 월드·미니게임에 생성할 프리팹.")]
        [SerializeField] private GameObject minigamePrefab;

        [Header("기획 메모")]
        [Tooltip("기획 ItemData.ObtainRoute. 지금은 표시·분류용이고 획득 로직이 읽지 않는다.")]
        [SerializeField] private ObtainRoute obtainRoute = ObtainRoute.SupplyCrate;

        [Tooltip("기획 ItemData.Description. 용도·획득 방식 설명.")]
        [SerializeField, TextArea(2, 4)] private string description;

        public int Id => id;
        public string DisplayName => displayName;
        public ItemType ItemType => itemType;
        public ItemGrade Grade => grade;
        public int MaxStack => maxStack;
        public Sprite Icon => icon;
        public GameObject MinigamePrefab => minigamePrefab;
        public ObtainRoute ObtainRoute => obtainRoute;
        public string Description => description;
    }
}
