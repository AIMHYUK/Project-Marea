using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 탐사 지역 하나의 정의 — 이름 · 걸리는 시간 · 보상. (+9/28, 이슈 73)
    ///
    /// 변하지 않는 값만 넣는다. "지금 몇 초 남았나"는 ExpeditionManager가 든다.
    /// 보상은 고정값이다. 확률·특수 자원은 기획이 수치를 정하면 붙인다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Expedition Region", fileName = "Region_")]
    public class ExpeditionRegionData : ScriptableObject
    {
        [Tooltip("지역 선택 창에 보일 이름.")]
        [SerializeField] private string displayName;

        [Tooltip("파견부터 귀환까지 실제 초.")]
        [SerializeField, Min(0.1f)] private float durationSeconds = 20f;

        [Tooltip("귀환한 탐사정을 클릭하면 창고에 들어갈 재료와 개수. "
               + "RecipeEntry를 빌려 쓴다 — requiredAmount가 여기선 보상 개수다.")]
        [SerializeField] private RecipeEntry[] rewards;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public float DurationSeconds => durationSeconds;
        public RecipeEntry[] Rewards => rewards ?? System.Array.Empty<RecipeEntry>();
    }
}
