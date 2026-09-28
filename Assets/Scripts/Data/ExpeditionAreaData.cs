using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 탐사 해역 하나의 정의 — 걸리는 시간과 보상 그룹. (+9/28, 이슈 73)
    ///
    /// 필드는 기획 「데이터 별 필드 정의」의 ExpeditionAreaData 순서를 따른다.
    /// UnlockConditionID는 아직 가리킬 조건 데이터가 정의되지 않아서 뺐다.
    /// 「참조 ID」는 이 프로젝트 관례대로 에셋 참조로 든다 (RecipeEntry.ingredient와 같다).
    ///
    /// 변하지 않는 값만 넣는다. "지금 몇 초 남았나"는 ExpeditionManager가 든다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Expedition Area", fileName = "Area_")]
    public class ExpeditionAreaData : ScriptableObject
    {
        [Tooltip("기획 ExpeditionAreaData.AreaID. 중복 불가.")]
        [SerializeField] private int id;

        [Tooltip("기획 ExpeditionAreaData.DurationSec. 파견부터 귀환까지 실제 초.")]
        [SerializeField, Min(0.1f)] private float durationSeconds = 20f;

        [Tooltip("기획 ExpeditionAreaData.RewardGroupID. 귀환 보상을 여기서 추첨한다.")]
        [SerializeField] private ExpeditionRewardData rewardGroup;

        // 표에 없다. 지역 선택 창에 띄울 이름이 필요해서 둔다 — 기획 8 「탐사 가능한 지역 목록을 표시한다」.
        [Tooltip("지역 선택 창에 보일 이름. 기획 표에는 없는 칸이다.")]
        [SerializeField] private string displayName;

        public int Id => id;
        public float DurationSeconds => durationSeconds;
        public ExpeditionRewardData RewardGroup => rewardGroup;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}
