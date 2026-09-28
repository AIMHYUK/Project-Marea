using System.Collections.Generic;
using System.Text;
using Marea.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 탐사정 지역 선택 창. 지역마다 이름 · 탐사 시간 · 주요 보상 · 파견. (+9/28, 이슈 73)
    ///
    /// 지도 UI는 기획서에 "추후 구현"으로 적혀 있어서 목록으로 둔다.
    /// 논블로킹이다 (업그레이드 목록과 같은 판단).
    /// </summary>
    public class ExpeditionUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [Tooltip("줄이 쌓일 곳. VerticalLayoutGroup이 붙어 있어야 한다.")]
        [SerializeField] private Transform rowParent;
        [SerializeField] private ChoiceRow rowPrefab;
        [SerializeField] private Button closeButton;

        private readonly List<ChoiceRow> _rows = new();
        private ExpeditionManager _manager;

        private void Awake()
        {
            if (panel == null || rowParent == null || rowPrefab == null)
                Debug.LogError($"{name}: ExpeditionUI의 panel·rowParent·rowPrefab 중 비어 있는 게 있다.", this);

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panel != null) panel.SetActive(false);
        }

        public void Open(ExpeditionManager manager)
        {
            if (panel == null || rowParent == null || rowPrefab == null) return;

            _manager = manager;
            panel.SetActive(true);
            Rebuild();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            _manager = null;
        }

        private void Rebuild()
        {
            ExpeditionRegionData[] regions = _manager.Regions ?? System.Array.Empty<ExpeditionRegionData>();

            while (_rows.Count < regions.Length) _rows.Add(Instantiate(rowPrefab, rowParent));
            for (int i = 0; i < _rows.Count; i++) _rows[i].gameObject.SetActive(i < regions.Length);

            for (int i = 0; i < regions.Length; i++)
            {
                ExpeditionRegionData region = regions[i];
                _rows[i].Set(
                    region.DisplayName,
                    $"{region.DurationSeconds:0}초   {RewardText(region)}",
                    "파견",
                    _manager.Current == ExpeditionManager.State.Idle,
                    () => HandleDispatch(region));
            }
        }

        private void HandleDispatch(ExpeditionRegionData region)
        {
            if (_manager != null && _manager.TryDispatch(region)) Close();
        }

        // 기호를 안 쓴다 — Pretendard-Bold SDF에 ·, × 가 없어 TMP가 조용히 지운다 (FacilityCell과 같은 이유).
        private static string RewardText(ExpeditionRegionData region)
        {
            var sb = new StringBuilder();
            foreach (RecipeEntry reward in region.Rewards)
            {
                if (reward.ingredient == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(reward.ingredient.DisplayName).Append(' ').Append(reward.requiredAmount);
            }
            return sb.ToString();
        }
    }
}
