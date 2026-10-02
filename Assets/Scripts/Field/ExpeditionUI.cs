using System.Collections.Generic;
using System.Text;
using Marea.Core;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 탐사정 지역 선택 창. (+9/28, 이슈 73)
    ///
    /// (+10/2, 이슈 92 — 와이어프레임 「탐사 지역 선택」) 줄 목록에서 해역 지도 + 오른쪽 상세 + [파견]으로
    /// 바뀌었다. 지도 위 해역 버튼은 areas 순서대로 nodePositions 자리에 놓인다 — 실제 지도 배치는
    /// 기획이 정하면 그 값만 바꾼다. 잠긴 해역은 해금 조건 데이터가 없어(ExpeditionAreaData 참고)
    /// 프리팹에 놓인 자리 표시뿐이다.
    ///
    /// 논블로킹이다 (업그레이드 목록과 같은 판단).
    /// </summary>
    public class ExpeditionUI : UiPanel
    {
        [Header("해역 지도")]
        [Tooltip("해역 버튼이 놓일 곳.")]
        [SerializeField] private RectTransform mapRoot;
        [Tooltip("해역 버튼 하나. Button과 자식 TextMeshProUGUI가 있어야 한다.")]
        [SerializeField] private Button nodePrefab;
        [Tooltip("areas 순서대로 놓을 자리(mapRoot 기준). 모자라면 아래로 이어 놓는다.")]
        [SerializeField] private Vector2[] nodePositions;
        [Tooltip("고른 / 안 고른 해역 바탕. 비우면 바탕을 안 바꾼다.")]
        [SerializeField] private Sprite nodeOnSprite;
        [SerializeField] private Sprite nodeOffSprite;

        [Header("선택한 해역")]
        [SerializeField] private TextMeshProUGUI detailName;
        [SerializeField] private TextMeshProUGUI detailTime;
        [SerializeField] private TextMeshProUGUI detailRewards;
        [SerializeField] private Button dispatchButton;

        private readonly List<Button> _nodes = new();
        private ExpeditionManager _manager;
        private ExpeditionAreaData _selected;

        protected override void Awake()
        {
            base.Awake();
            if (mapRoot == null || nodePrefab == null)
                Debug.LogError($"{name}: ExpeditionUI의 mapRoot·nodePrefab 중 비어 있는 게 있다.", this);
            if (dispatchButton == null)
                Debug.LogError($"{name}: ExpeditionUI.dispatchButton이 비어 있다. 파견할 수 없다.", this);
            else
                dispatchButton.onClick.AddListener(HandleDispatch);
        }

        public void Open(ExpeditionManager manager)
        {
            if (mapRoot == null || nodePrefab == null) return;

            _manager = manager;
            ExpeditionAreaData[] areas = _manager.Areas;
            _selected = areas != null && areas.Length > 0 ? areas[0] : null;

            Open();
            Rebuild();
        }

        protected override void OnClosed()
        {
            _manager = null;
            _selected = null;
        }

        private void Rebuild()
        {
            ExpeditionAreaData[] areas = _manager.Areas ?? System.Array.Empty<ExpeditionAreaData>();

            while (_nodes.Count < areas.Length) _nodes.Add(Instantiate(nodePrefab, mapRoot));
            for (int i = 0; i < _nodes.Count; i++) _nodes[i].gameObject.SetActive(i < areas.Length);

            for (int i = 0; i < areas.Length; i++)
            {
                ExpeditionAreaData area = areas[i];
                Button node = _nodes[i];

                var rect = (RectTransform)node.transform;
                rect.anchoredPosition = i < (nodePositions?.Length ?? 0)
                    ? nodePositions[i]
                    : new Vector2(0f, -110f * (i - (nodePositions?.Length ?? 0)));

                TextMeshProUGUI label = node.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = area.DisplayName;

                node.onClick.RemoveAllListeners();
                node.onClick.AddListener(() => Select(area));
            }

            RefreshDetail();
        }

        private void Select(ExpeditionAreaData area)
        {
            _selected = area;
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            ExpeditionAreaData[] areas = _manager != null ? _manager.Areas : null;
            for (int i = 0; i < _nodes.Count; i++)
            {
                bool on = areas != null && i < areas.Length && areas[i] == _selected;
                if (nodeOnSprite != null && nodeOffSprite != null && _nodes[i].targetGraphic is Image image)
                    image.sprite = on ? nodeOnSprite : nodeOffSprite;
            }

            if (detailName != null) detailName.text = _selected != null ? _selected.DisplayName : string.Empty;
            if (detailTime != null) detailTime.text = _selected != null ? $"탐사 시간 {TimeText.Format(_selected.DurationSeconds)}" : string.Empty;
            if (detailRewards != null) detailRewards.text = _selected != null ? RewardText(_selected) : string.Empty;

            if (dispatchButton != null)
                dispatchButton.interactable = _selected != null && _manager != null
                                           && _manager.Current == ExpeditionManager.State.Idle;
        }

        private void HandleDispatch()
        {
            if (_manager != null && _selected != null && _manager.TryDispatch(_selected)) Close();
        }

        /// <summary>
        /// 와이어프레임 「획득 가능한 자원 — 식재료 / 특수 자원」. 후보 이름과 수량 범위를 종류별로 나눈다.
        /// 한 번에 이 중 하나만 들어온다는 건 창에 안 적었다. 문구는 기획이 정하면 바꾼다.
        /// </summary>
        // 기호를 안 쓴다 — Pretendard-Bold SDF에 ·, × 가 없어 TMP가 조용히 지운다 (FacilityCell과 같은 이유).
        private static string RewardText(ExpeditionAreaData area)
        {
            if (area.RewardGroup == null) return string.Empty;

            var food = new StringBuilder();
            var special = new StringBuilder();
            foreach (ExpeditionRewardEntry e in area.RewardGroup.Entries)
            {
                if (e.item == null || e.weight <= 0f) continue;
                StringBuilder sb = e.item.ItemType == ItemType.SpecialResource ? special : food;
                if (sb.Length > 0) sb.Append(", ");
                int max = Mathf.Max(e.amountMin, e.amountMax);
                sb.Append(e.item.DisplayName).Append(' ').Append(e.amountMin);
                if (max != e.amountMin) sb.Append('~').Append(max);
            }

            return "획득 가능한 자원\n"
                 + $"식재료: {(food.Length > 0 ? food.ToString() : "없음")}\n"
                 + $"특수 자원: {(special.Length > 0 ? special.ToString() : "없음")}";
        }
    }
}
