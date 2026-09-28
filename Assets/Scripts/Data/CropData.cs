using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 밭에서 자라는 작물 한 종류의 정의. (+9/28, 이슈 72)
    ///
    /// 재료(IngredientData)와 따로 둔다 — 설계 결정 4. 창고에 쌓이는 건 재료고,
    /// 성장 시간·씨앗값·수확량은 작물 쪽 정보다. B는 이걸 모른다.
    ///
    /// 변하지 않는 값만 넣는다. "이 밭은 지금 몇 초째다"는 FarmManager가 든다.
    /// 성장 단계 스프라이트는 아트가 나오면 넣는다 — 지금은 싹 크기로만 보인다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Crop", fileName = "Crop_")]
    public class CropData : ScriptableObject
    {
        [Tooltip("수확하면 창고에 들어갈 재료. 작물 이름도 여기서 가져온다.")]
        [SerializeField] private IngredientData harvest;

        [Tooltip("한 번 수확할 때 들어오는 개수.")]
        [SerializeField, Min(1)] private int harvestCount = 1;

        [Tooltip("심고 나서 다 자랄 때까지 걸리는 실제 초.")]
        [SerializeField, Min(0.1f)] private float growSeconds = 10f;

        [Tooltip("심을 때 빠지는 골드. 모자라면 못 심는다 (기획 9 씨앗 비용).")]
        [SerializeField, Min(0)] private int seedPrice;

        public IngredientData Harvest => harvest;
        public int HarvestCount => harvestCount;
        public float GrowSeconds => growSeconds;
        public int SeedPrice => seedPrice;

        /// <summary>목록에 보일 이름. 재료 이름을 그대로 쓴다.</summary>
        public string DisplayName => harvest != null && !string.IsNullOrWhiteSpace(harvest.DisplayName)
            ? harvest.DisplayName
            : name;
    }
}
