using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 밭에서 자라는 작물 한 종류의 정의. (+9/28, 이슈 72)
    ///
    /// 재료(IngredientData)와 따로 둔다 — 설계 결정 4. 창고에 쌓이는 건 재료고,
    /// 성장 시간·씨앗값·수확량은 작물 쪽 정보다. B는 이걸 모른다.
    ///
    /// 필드는 기획 「데이터 별 필드 정의」의 CropData 순서를 따른다. 「참조 ID」(RewardItemID)는
    /// 이 프로젝트 관례대로 에셋 참조로 든다. 씨앗 가격은 표에는 없고 기획 PDF 9절에 있어서 따로 둔다.
    ///
    /// 변하지 않는 값만 넣는다. "이 밭은 지금 몇 초째다"는 FarmManager가 든다.
    /// 성장 모습은 stagePrefabs로 넣는다. 비어 있으면 밭의 기본 싹이 크기로만 자란다. (+9/30)
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Crop", fileName = "Crop_")]
    public class CropData : ScriptableObject
    {
        [Tooltip("기획 CropData.CropID. 중복 불가.")]
        [SerializeField] private int id;

        [Tooltip("기획 CropData.GrowTimeSec. 심고 나서 다 자랄 때까지 걸리는 실제 초.")]
        [SerializeField, Min(0.1f)] private float growSeconds = 10f;

        [Tooltip("기획 CropData.RewardItemID. 수확하면 창고에 들어갈 재료. 작물 이름도 여기서 가져온다.")]
        [SerializeField] private IngredientData harvest;

        [Tooltip("기획 CropData.HarvestAmount. 한 번 수확할 때 들어오는 개수.")]
        [SerializeField, Min(1)] private int harvestCount = 1;

        // 표에 없다. 기획 PDF 9절 「작물별 씨앗 가격」.
        [Tooltip("심을 때 빠지는 골드. 모자라면 못 심는다. 기획 표에는 없고 PDF 9절에 있다.")]
        [SerializeField, Min(0)] private int seedPrice;

        // (+9/30) 표에 없다. 아트 에셋(Art/Asset/Farm/Farm_prefab)을 단계 순서대로 넣는다.
        [Tooltip("성장 단계 모델. 씨앗 → … → 다 자람 순서. 마지막 칸이 수확 가능한 모습이다. 비우면 기본 싹이 자란다.")]
        [SerializeField] private GameObject[] stagePrefabs;

        public int Id => id;
        public float GrowSeconds => growSeconds;
        public IngredientData Harvest => harvest;
        public int HarvestCount => harvestCount;
        public int SeedPrice => seedPrice;
        public GameObject[] StagePrefabs => stagePrefabs;

        /// <summary>목록에 보일 이름. 재료 이름을 그대로 쓴다.</summary>
        public string DisplayName => harvest != null && !string.IsNullOrWhiteSpace(harvest.DisplayName)
            ? harvest.DisplayName
            : name;
    }
}
